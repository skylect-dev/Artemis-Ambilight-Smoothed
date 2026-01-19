using System;
using Artemis.Core;
using Artemis.Core.Services;
using Artemis.Plugins.LayerBrushes.AmbilightSmoothed.ScreenCapture;
using Microsoft.Win32;
using ScreenCapture.NET;
using Serilog;

namespace Artemis.Plugins.LayerBrushes.AmbilightSmoothed
{
    public class AmbilightSmoothedBootstrapper : PluginBootstrapper
    {
        private ILogger? _logger;
        private IPluginManagementService? _managementService;
        private PluginFeatureInfo? _brushProvider;

        #region Properties & Fields

        internal static IScreenCaptureService? ScreenCaptureService { get; private set; }

        #endregion

        #region Methods

        public override void OnPluginEnabled(Plugin plugin)
        {
            _logger = plugin.Resolve<ILogger>();
            _managementService = plugin.Resolve<IPluginManagementService>();
            _brushProvider = plugin.GetFeatureInfo<AmbilightSmoothedLayerBrushProvider>();

            // Detect Wayland: if running under Wayland, prefer an explicit policy because
            // libX11-based capture will usually not work and PipeWire-backed capture is
            // not available in the current dependencies (ScreenCapture.NET lacks a PipeWire backend).
            bool isWayland = false;
            try
            {
                string? xdg = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
                string? wayland = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
                isWayland = (xdg != null && xdg.Equals("wayland", StringComparison.OrdinalIgnoreCase)) || !string.IsNullOrEmpty(wayland);
            }
            catch { /* ignore env read errors */ }

            if (OperatingSystem.IsLinux() && isWayland)
            {
                _logger?.Information("Wayland session detected. PipeWire/xdg-desktop-portal backend is required for screen capture. Using no-op fallback until Wayland support is available.");
                ScreenCaptureService ??= new AmbilightSmoothedScreenCaptureService(new NoOpScreenCaptureService());
            }
            else
            {
                try
                {
                    IScreenCaptureService screenCaptureService = OperatingSystem.IsWindows() ? new DX11ScreenCaptureService() : new X11ScreenCaptureService();
                    ScreenCaptureService ??= new AmbilightSmoothedScreenCaptureService(screenCaptureService);
                }
                catch (Exception ex)
                {
                    _logger?.Error(ex, "Failed to initialize screen capture service; using no-op fallback.");
                    ScreenCaptureService ??= new AmbilightSmoothedScreenCaptureService(new NoOpScreenCaptureService());
                }
            }

            if (OperatingSystem.IsWindows())
            {
                SystemEvents.DisplaySettingsChanged += SystemEventsOnDisplaySettingsChanged;
            }
        }

        public override void OnPluginDisabled(Plugin plugin)
        {
            ScreenCaptureService?.Dispose();
            ScreenCaptureService = null;
            if (OperatingSystem.IsWindows())
            {
                SystemEvents.DisplaySettingsChanged -= SystemEventsOnDisplaySettingsChanged;
            }
        }

        private void SystemEventsOnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            if (_brushProvider?.Instance == null || !_brushProvider.Instance.IsEnabled)
                _logger?.Debug("Display settings changed, but ambilight feature is disabled");
            else
            {
                _logger?.Debug("Display settings changed, restarting ambilight feature");

                _managementService?.DisablePluginFeature(_brushProvider.Instance, false);
                ScreenCaptureService?.Dispose();
                try
                {
                    IScreenCaptureService screenCaptureService = OperatingSystem.IsWindows() ? new DX11ScreenCaptureService() : new X11ScreenCaptureService();
                    ScreenCaptureService = new AmbilightSmoothedScreenCaptureService(screenCaptureService);
                }
                catch (Exception ex)
                {
                    _logger?.Error(ex, "Failed to reinitialize screen capture service after display change; switching to no-op fallback.");
                    ScreenCaptureService = new AmbilightSmoothedScreenCaptureService(new NoOpScreenCaptureService());
                }
                _managementService?.EnablePluginFeature(_brushProvider.Instance, false);
            }
        }

        #endregion
    }
}