using System;
using System.Collections.Generic;
using System.Linq;
using ScreenCapture.NET;

namespace Artemis.Plugins.LayerBrushes.AmbilightSmoothed.ScreenCapture
{
    // Minimal no-op implementations to avoid crashes when native capture is unavailable
    public sealed class NoOpScreenCaptureService : IScreenCaptureService
    {
        public IEnumerable<GraphicsCard> GetGraphicsCards() => Enumerable.Empty<GraphicsCard>();

        public IEnumerable<Display> GetDisplays(GraphicsCard graphicsCard) => Enumerable.Empty<Display>();

        public IScreenCapture GetScreenCapture(Display display) => new NoOpScreenCapture(display);

        public void Dispose() { }
    }

    internal sealed class NoOpScreenCapture : IScreenCapture
    {
        public NoOpScreenCapture(Display display)
        {
            Display = display;
            TargetFps = 60;
        }

        public Display Display { get; }

        public double CurrentFps => 0.0;

        public int TargetFps { get; set; }

        public event EventHandler<ScreenCaptureUpdatedEventArgs>? Updated;

        public ICaptureZone? RegisterCaptureZone(int x, int y, int width, int height, int downscaleLevel = 0)
            => null;

        public bool UnregisterCaptureZone(ICaptureZone captureZone) => true;

        public void UpdateCaptureZone(ICaptureZone captureZone, int? x = null, int? y = null, int? width = null, int? height = null, int? downscaleLevel = null) { }

        public bool CaptureScreen() => false;

        public void Restart() { }

        public void Dispose() { }
    }

    // Note: we intentionally return null for capture zones so callers must handle absence of capture.
}
