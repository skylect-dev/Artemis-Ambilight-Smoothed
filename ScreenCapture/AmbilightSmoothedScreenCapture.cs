using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ScreenCapture.NET;

namespace Artemis.Plugins.LayerBrushes.AmbilightSmoothed.ScreenCapture
{
    public sealed class AmbilightSmoothedScreenCapture : IScreenCapture
    {
        #region Properties & Fields

        private readonly IScreenCapture _screenCapture;

        private int _zoneCount = 0;
        private double _currentFps = 0;
        private int _targetFps = 60; // Configurable 20-60

        private Task? _updateTask;
        private CancellationTokenSource? _cancellationTokenSource;
        private CancellationToken _cancellationToken = CancellationToken.None;

        public Display Display => _screenCapture.Display;
        public double CurrentFps => _currentFps;
        
        public int TargetFps
        {
            get => _targetFps;
            set => _targetFps = Math.Clamp(value, 20, 60);
        }

        #endregion

        #region Events

        public event EventHandler<ScreenCaptureUpdatedEventArgs>? Updated;

        #endregion

        #region Constructors

        public AmbilightSmoothedScreenCapture(IScreenCapture screenCapture)
        {
            this._screenCapture = screenCapture;

            if (screenCapture is DX11ScreenCapture dx11ScreenCapture)
                dx11ScreenCapture.Timeout = 250;
        }

        #endregion

        #region Methods

        private void UpdateLoop()
        {
            int consecutiveFailures = 0;
            long lastFpsUpdateTime = Stopwatch.GetTimestamp();
            long ticksPerSecond = Stopwatch.Frequency;
            int successCount = 0;
            
            while (true)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                
                // Measure only the capture time, not including previous sleep
                long captureStartTime = Stopwatch.GetTimestamp();
                bool success = _screenCapture.CaptureScreen();
                long captureEndTime = Stopwatch.GetTimestamp();
                Updated?.Invoke(this, new ScreenCaptureUpdatedEventArgs(success));
                
                if (success)
                {
                    consecutiveFailures = 0;
                    successCount++;
                    
                    // Update FPS display every 500ms for more responsive feedback
                    long timeSinceLastFpsUpdate = captureEndTime - lastFpsUpdateTime;
                    if (timeSinceLastFpsUpdate > ticksPerSecond / 2) // 500ms
                    {
                        double avgFps = successCount / (timeSinceLastFpsUpdate / (double)ticksPerSecond);
                        _currentFps = Math.Min(avgFps, 999.9); // Cap display at 999.9
                        lastFpsUpdateTime = captureEndTime;
                        successCount = 0;
                    }
                    
                    // Calculate precise sleep time to maintain target FPS
                    // Only consider the capture time itself, not previous sleeps
                    int maxTargetFps = _targetFps;
                    double targetFrameTimeMs = 1000.0 / maxTargetFps;
                    
                    long captureTicks = captureEndTime - captureStartTime;
                    double captureMs = (captureTicks * 1000.0) / ticksPerSecond;
                    
                    // Sleep for remaining time to hit target frame time (minimum 1ms)
                    double sleepTimeMs = Math.Max(1.0, targetFrameTimeMs - captureMs);
                    Thread.Sleep((int)Math.Round(sleepTimeMs));
                }
                else
                {
                    consecutiveFailures++;
                    // Simple backoff on failure - wait 16ms before retry
                    // User controls responsiveness via Target FPS slider
                    Thread.Sleep(16);
                }
            }
        }

        ICaptureZone IScreenCapture.RegisterCaptureZone(int x, int y, int width, int height, int downscaleLevel)
        {
            lock (_screenCapture)
            {
                ICaptureZone captureZone = _screenCapture.RegisterCaptureZone(x, y, width, height, downscaleLevel);
                _zoneCount++;

                if (_updateTask == null)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    _cancellationToken = _cancellationTokenSource.Token;
                    _updateTask = Task.Factory.StartNew(UpdateLoop, _cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                }

                return captureZone;
            }
        }

        public bool UnregisterCaptureZone(ICaptureZone captureZone)
        {
            lock (_screenCapture)
            {
                bool result = _screenCapture.UnregisterCaptureZone(captureZone);
                if (result)
                    _zoneCount--;

                if ((_zoneCount == 0) && (_updateTask != null))
                {
                    _cancellationTokenSource?.Cancel();
                    _updateTask = null;
                }

                return result;
            }
        }

        public void UpdateCaptureZone(ICaptureZone captureZone, int? x = null, int? y = null, int? width = null, int? height = null, int? downscaleLevel = null)
        {
            lock (_screenCapture)
                _screenCapture.UpdateCaptureZone(captureZone, x, y, width, height, downscaleLevel);
        }

        public bool CaptureScreen() => false;

        public void Restart() => _screenCapture.Restart();

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _updateTask = null;

            _screenCapture.Dispose();
        }

        #endregion
    }
}
