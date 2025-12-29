using System;
using System.Linq;
using Artemis.Core.LayerBrushes;
using Artemis.Plugins.LayerBrushes.AmbilightSmoothed.PropertyGroups;
using Artemis.Plugins.LayerBrushes.AmbilightSmoothed.Screens;
using Artemis.Plugins.LayerBrushes.AmbilightSmoothed.Services;
using Artemis.UI.Shared.LayerBrushes;
using HPPH;
using ScreenCapture.NET;
using SkiaSharp;

namespace Artemis.Plugins.LayerBrushes.AmbilightSmoothed
{
    public class AmbilightSmoothedLayerBrush : LayerBrush<AmbilightSmoothedPropertyGroup>
    {
        #region Properties & Fields

        private IScreenCaptureService? _screenCaptureService => AmbilightSmoothedBootstrapper.ScreenCaptureService;
        public bool PropertiesOpen { get; set; }
        
        public bool IsUsingHdrCapture => _hdrCapture != null;

        private Display? _display;
        private ICaptureZone? _captureZone;
        private bool _creatingCaptureZone;
        
        // HDR capture (when display supports HDR)
        private HdrScreenCapture? _hdrCapture;
        private byte[]? _hdrCaptureBuffer;

        // Smoothing state
        private byte[]? _smoothedPixels;
        private int _frameCounter;
        private int _lastWidth;
        private int _lastHeight;

        // HDR compensation state (applied on downscaled capture)
        private byte[]? _hdrAdjustedPixels;
        private byte[]? _hdrLut;
        private int _hdrLastBlackPoint;
        private int _hdrLastWhitePoint;
        private int _hdrLastSaturation;
        private int _hdrLastExposure;
        private bool _hdrLastAutoEnabled;

        #endregion

        #region Methods

        public override void Update(double deltaTime)
        {
            _captureZone?.RequestUpdate();
        }
        
        private unsafe void RenderWithHdrCapture(SKCanvas canvas, SKRect bounds, SKPaint paint)
        {
            if (_hdrCapture == null || _hdrCaptureBuffer == null)
                return;
                
            // Capture frame from HDR capture
            if (!_hdrCapture.CaptureFrame(_hdrCaptureBuffer))
                return;
                
            AmbilightSmoothedCaptureProperties properties = Properties.Capture;
            int smoothingLevel = properties.SmoothingLevel.CurrentValue;
            int frameSkip = properties.FrameSkip.CurrentValue;
            bool hdrEnabled = properties.Hdr.CurrentValue;
            bool hdrAuto = properties.HdrAuto.CurrentValue;
            int hdrExposure = properties.HdrExposure.CurrentValue;
            int hdrBlackPoint = properties.HdrBlackPoint.CurrentValue;
            int hdrWhitePoint = properties.HdrWhitePoint.CurrentValue;
            int hdrSaturation = properties.HdrSaturation.CurrentValue;
            
            float smoothingFactor = smoothingLevel == 0 ? 1.0f : (float)Math.Pow((10 - smoothingLevel) / 10.0, 2.0);
            
            int width = _hdrCapture.Width;
            int height = _hdrCapture.Height;
            int stride = width * 4;
            
            // Handle frame skipping
            _frameCounter++;
            bool shouldProcess = _frameCounter > frameSkip;
            if (shouldProcess)
                _frameCounter = 0;
                
            bool hdrSettingsChanged = hdrEnabled && 
                (_hdrLastBlackPoint != hdrBlackPoint || 
                 _hdrLastWhitePoint != hdrWhitePoint || 
                 _hdrLastSaturation != hdrSaturation ||
                 _hdrLastExposure != hdrExposure ||
                 _hdrLastAutoEnabled != hdrAuto);
            if (hdrSettingsChanged)
                shouldProcess = true;
                
            fixed (byte* bufferPtr = _hdrCaptureBuffer)
            {
                byte* sourcePixels = bufferPtr;
                
                // Apply HDR compensation if enabled
                if (hdrEnabled && shouldProcess)
                {
                    if (_hdrAdjustedPixels == null || _hdrAdjustedPixels.Length != _hdrCaptureBuffer.Length)
                        _hdrAdjustedPixels = new byte[_hdrCaptureBuffer.Length];
                        
                    fixed (byte* hdrAdjusted = _hdrAdjustedPixels)
                    {
                        if (hdrAuto)
                        {
                            int autoExposure = ComputeAutoExposurePercent(sourcePixels, width, height, stride, hdrBlackPoint, hdrWhitePoint);
                            EnsureHdrLut(autoExposure, hdrBlackPoint, hdrWhitePoint, hdrSaturation);
                        }
                        else
                        {
                            EnsureHdrLut(hdrExposure, hdrBlackPoint, hdrWhitePoint, hdrSaturation);
                        }
                        
                        if (_hdrLut != null)
                        {
                            ApplyHdrCompensation(sourcePixels, hdrAdjusted, width, height, stride, _hdrLut, hdrSaturation);
                            sourcePixels = hdrAdjusted;
                        }
                    }
                }
                else if (hdrEnabled && _hdrAdjustedPixels != null)
                {
                    fixed (byte* hdrAdjusted = _hdrAdjustedPixels)
                    {
                        sourcePixels = hdrAdjusted;
                    }
                }
                
                // Apply smoothing
                if (smoothingLevel > 0)
                {
                    if (shouldProcess)
                        ApplySmoothing(sourcePixels, width, height, stride, smoothingFactor);
                        
                    if (_smoothedPixels != null)
                    {
                        fixed (byte* smoothed = _smoothedPixels)
                        {
                            sourcePixels = smoothed;
                        }
                    }
                }
                
                // Draw to canvas
                using SKBitmap bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                IntPtr pixelsAddr = bitmap.GetPixels();
                Buffer.MemoryCopy(sourcePixels, (void*)pixelsAddr, width * height * 4, width * height * 4);
                canvas.DrawBitmap(bitmap, bounds, paint);
            }
        }

        public override unsafe void Render(SKCanvas canvas, SKRect bounds, SKPaint paint)
        {
            // Handle HDR capture path
            if (_hdrCapture != null)
            {
                RenderWithHdrCapture(canvas, bounds, paint);
                return;
            }
            
            if (_captureZone == null) return;

            AmbilightSmoothedCaptureProperties properties = Properties.Capture;
            int smoothingLevel = properties.SmoothingLevel.CurrentValue;
            int frameSkip = properties.FrameSkip.CurrentValue;
            bool hdrEnabled = properties.Hdr.CurrentValue;
            bool hdrAuto = properties.HdrAuto.CurrentValue;
            int hdrExposure = properties.HdrExposure.CurrentValue;
            int hdrBlackPoint = properties.HdrBlackPoint.CurrentValue;
            int hdrWhitePoint = properties.HdrWhitePoint.CurrentValue;
            int hdrSaturation = properties.HdrSaturation.CurrentValue;
            
            // Convert smoothing level (0-10) to factor (1.0-0.0) with quadratic curve for better distribution
            // 0 = 1.0 (no smoothing), 5 = 0.25 (moderate), 10 = 0.0 (max smoothing)
            float smoothingFactor = smoothingLevel == 0 ? 1.0f : (float)Math.Pow((10 - smoothingLevel) / 10.0, 2.0);

            using (_captureZone.Lock())
            {
                // DarthAffe 11.09.2023: Accessing the low-level images is a source of potential errors in the future since we assume the pixel-format. Currently both used providers are BGRA, but if there are ever issues with shifted colors, this is the place to start investigating.
                RefImage<ColorBGRA> image = _captureZone.GetRefImage<ColorBGRA>();
                bool hasBlackBarDetection = properties.BlackBarDetectionTop || properties.BlackBarDetectionBottom || 
                                            properties.BlackBarDetectionLeft || properties.BlackBarDetectionRight;
                
                if (hasBlackBarDetection)
                    image = image.RemoveBlackBars(properties.BlackBarDetectionThreshold,
                                                  properties.BlackBarDetectionTop, properties.BlackBarDetectionBottom,
                                                  properties.BlackBarDetectionLeft, properties.BlackBarDetectionRight);

                // Handle frame skipping
                _frameCounter++;
                bool shouldProcess = _frameCounter > frameSkip;
                if (shouldProcess)
                    _frameCounter = 0;

                // Force reprocessing if HDR settings changed (for live preview updates)
                bool hdrSettingsChanged = hdrEnabled && 
                    (_hdrLastBlackPoint != hdrBlackPoint || 
                     _hdrLastWhitePoint != hdrWhitePoint || 
                     _hdrLastSaturation != hdrSaturation ||
                     _hdrLastExposure != hdrExposure ||
                     _hdrLastAutoEnabled != hdrAuto);
                if (hdrSettingsChanged)
                    shouldProcess = true;

                fixed (byte* img = image)
                {
                    byte* sourcePixels = img;

                    // Apply HDR compensation (levels + saturation) into a reusable buffer.
                    // We only recompute when shouldProcess=true, so FrameSkip reduces CPU cost.
                    // But we MUST recompute if dimensions changed (e.g., black bar detection).
                    bool dimensionsChanged = (image.Width != _lastWidth || image.Height != _lastHeight);
                    
                    if (hdrEnabled)
                    {
                        if (shouldProcess || dimensionsChanged)
                        {
                            int effectiveExposure = hdrExposure;
                            if (hdrAuto)
                            {
                                int autoExposure = ComputeAutoExposurePercent(img, image.Width, image.Height, image.RawStride, hdrBlackPoint, hdrWhitePoint);
                                float bias = hdrExposure / 100f;
                                effectiveExposure = (int)MathF.Round(autoExposure * bias);
                                effectiveExposure = Math.Clamp(effectiveExposure, 1, 400);
                            }

                            EnsureHdrLut(effectiveExposure, hdrBlackPoint, hdrWhitePoint, hdrSaturation);
                            EnsureHdrBuffer(image.Width, image.Height, image.RawStride);
                            fixed (byte* hdrPtr = _hdrAdjustedPixels)
                            {
                                ApplyHdrCompensation(img, hdrPtr, image.Width, image.Height, image.RawStride, _hdrLut!, hdrSaturation);
                                sourcePixels = hdrPtr;
                            }

                            _hdrLastAutoEnabled = hdrAuto;
                        }
                        else if (_hdrAdjustedPixels != null)
                        {
                            fixed (byte* hdrPtr = _hdrAdjustedPixels)
                            {
                                sourcePixels = hdrPtr;
                            }
                        }
                    }

                    // Apply smoothing if enabled (factor < 1.0) and should process this frame
                    bool usesSmoothing = smoothingFactor < 1.0f;
                    if (usesSmoothing && (shouldProcess || dimensionsChanged))
                    {
                        ApplySmoothing(sourcePixels, image.Width, image.Height, image.RawStride, smoothingFactor);
                    }

                    // Draw the image - use smoothed buffer if smoothing is enabled, otherwise raw
                    if (usesSmoothing && _smoothedPixels != null)
                    {
                        fixed (byte* smoothedPtr = _smoothedPixels)
                        {
                            using SKImage skImage = SKImage.FromPixels(
                                new SKImageInfo(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Opaque), 
                                (nint)smoothedPtr, 
                                image.RawStride);
                            canvas.DrawImage(skImage, bounds, paint);
                        }
                    }
                    else
                    {
                        using SKImage skImage = SKImage.FromPixels(
                            new SKImageInfo(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Opaque), 
                            (nint)sourcePixels, 
                            image.RawStride);
                        canvas.DrawImage(skImage, bounds, paint);
                    }
                }
            }
        }

        private void EnsureHdrBuffer(int width, int height, int stride)
        {
            int bufferSize = height * stride;
            if (_hdrAdjustedPixels == null || _hdrAdjustedPixels.Length != bufferSize)
                _hdrAdjustedPixels = new byte[bufferSize];
        }

        private void EnsureHdrLut(int exposurePercent, int blackPoint, int whitePoint, int saturation)
        {
            blackPoint = Math.Clamp(blackPoint, 0, 255);
            whitePoint = Math.Clamp(whitePoint, 0, 255);
            if (whitePoint <= blackPoint)
                whitePoint = Math.Min(255, blackPoint + 1);

            exposurePercent = Math.Clamp(exposurePercent, 1, 400);

            if (_hdrLut != null &&
                _hdrLastExposure == exposurePercent &&
                _hdrLastBlackPoint == blackPoint &&
                _hdrLastWhitePoint == whitePoint &&
                _hdrLastSaturation == saturation)
                return;

            _hdrLut = new byte[256];
            // Filmic/ACES-like curve with exposure; black/white clamp output (floor/ceiling)
            float exposure = exposurePercent / 100f;
            float minOut = blackPoint / 255f;
            float maxOut = whitePoint / 255f;
            const float a = 2.51f, b = 0.03f, c = 2.43f, d = 0.59f, e = 0.14f;

            for (int i = 0; i < 256; i++)
            {
                float x = (i / 255f) * exposure;
                float numerator = x * (a * x + b);
                float denominator = x * (c * x + d) + e;
                float y = denominator != 0f ? numerator / denominator : 0f;
                y = Math.Clamp(y, 0f, 1f);
                y = Math.Clamp(y, minOut, maxOut);

                _hdrLut[i] = (byte)Math.Clamp((int)(y * 255f + 0.5f), 0, 255);
            }

            _hdrLastExposure = exposurePercent;
            _hdrLastBlackPoint = blackPoint;
            _hdrLastWhitePoint = whitePoint;
            _hdrLastSaturation = saturation;
        }

        private static unsafe void ApplyHdrCompensation(byte* src, byte* dst, int width, int height, int stride, byte[] lut, int saturation)
        {
            // saturation: percent (100 = unchanged)
            int sat = Math.Clamp(saturation, 0, 400);

            fixed (byte* lutPtr = lut)
            {
                for (int y = 0; y < height; y++)
                {
                    byte* srcRow = src + (y * stride);
                    byte* dstRow = dst + (y * stride);
                    for (int x = 0; x < width; x++)
                    {
                        int i = x * 4;
                        byte b = lutPtr[srcRow[i + 0]];
                        byte g = lutPtr[srcRow[i + 1]];
                        byte r = lutPtr[srcRow[i + 2]];
                        byte a = srcRow[i + 3];

                        if (sat != 100)
                        {
                            // Fast luma approximation (Rec.601-ish)
                            int l = (r * 77 + g * 150 + b * 29) >> 8;
                            int rr = l + ((r - l) * sat) / 100;
                            int gg = l + ((g - l) * sat) / 100;
                            int bb = l + ((b - l) * sat) / 100;
                            r = (byte)Math.Clamp(rr, 0, 255);
                            g = (byte)Math.Clamp(gg, 0, 255);
                            b = (byte)Math.Clamp(bb, 0, 255);
                        }

                        dstRow[i + 0] = b;
                        dstRow[i + 1] = g;
                        dstRow[i + 2] = r;
                        dstRow[i + 3] = a;
                    }
                }
            }
        }

        private static unsafe int ComputeAutoExposurePercent(byte* src, int width, int height, int stride, int blackPoint, int whitePoint)
        {
            // Goal: choose an exposure so a high percentile (bright-but-not-clipped) maps near the top
            // of the output range using the existing filmic curve.

            if (width <= 0 || height <= 0)
                return 100;

            Span<int> hist = stackalloc int[256];

            int xStep = Math.Max(1, width / 80);
            int yStep = Math.Max(1, height / 60);
            int samples = 0;

            for (int y = 0; y < height; y += yStep)
            {
                byte* row = src + (y * stride);
                for (int x = 0; x < width; x += xStep)
                {
                    int i = x * 4;
                    byte b = row[i + 0];
                    byte g = row[i + 1];
                    byte r = row[i + 2];
                    int l = (r * 77 + g * 150 + b * 29) >> 8;
                    hist[l]++;
                    samples++;
                }
            }

            if (samples <= 0)
                return 100;

            int p95 = GetPercentileFromHistogram(hist, samples, 0.95f);
            int p995 = GetPercentileFromHistogram(hist, samples, 0.995f);
            int refLuma = p995 >= 254 ? p95 : p995;
            if (refLuma <= 0)
                refLuma = 1;

            float maxOut = Math.Clamp(whitePoint, 0, 255) / 255f;
            // Aim just below the ceiling so highlights roll off rather than clip.
            float desired = MathF.Min(0.96f, MathF.Max(0.10f, maxOut - 0.01f));

            // Binary search exposure in [0.25 .. 4.0] (matches LUT clamp up to 400%).
            float lo = 0.25f;
            float hi = 4.0f;
            for (int iter = 0; iter < 10; iter++)
            {
                float mid = (lo + hi) * 0.5f;
                float y = EvaluateFilmic(refLuma, mid, blackPoint, whitePoint);
                if (y < desired)
                    lo = mid;
                else
                    hi = mid;
            }

            int result = (int)MathF.Round(((lo + hi) * 0.5f) * 100f);
            return Math.Clamp(result, 1, 400);
        }

        private static float EvaluateFilmic(int inputByte, float exposure, int blackPoint, int whitePoint)
        {
            float x = (Math.Clamp(inputByte, 0, 255) / 255f) * exposure;
            const float a = 2.51f, b = 0.03f, c = 2.43f, d = 0.59f, e = 0.14f;
            float numerator = x * (a * x + b);
            float denominator = x * (c * x + d) + e;
            float y = denominator != 0f ? numerator / denominator : 0f;
            y = Math.Clamp(y, 0f, 1f);

            float minOut = Math.Clamp(blackPoint, 0, 255) / 255f;
            float maxOut = Math.Clamp(whitePoint, 0, 255) / 255f;
            if (maxOut <= minOut)
                maxOut = MathF.Min(1f, minOut + (1f / 255f));
            y = Math.Clamp(y, minOut, maxOut);
            return y;
        }

        private static int GetPercentileFromHistogram(Span<int> hist, int total, float percentile)
        {
            int target = (int)MathF.Ceiling(total * Math.Clamp(percentile, 0f, 1f));
            int cumulative = 0;
            for (int i = 0; i < 256; i++)
            {
                cumulative += hist[i];
                if (cumulative >= target)
                    return i;
            }
            return 255;
        }

        private unsafe void ApplySmoothing(byte* currentPixels, int width, int height, int stride, float factor)
        {
            int bufferSize = height * stride;

            // Reset buffer if dimensions changed
            if (width != _lastWidth || height != _lastHeight || _smoothedPixels == null)
            {
                _smoothedPixels = new byte[bufferSize];
                _lastWidth = width;
                _lastHeight = height;
                
                // Initialize with current frame
                fixed (byte* smoothed = _smoothedPixels)
                {
                    Buffer.MemoryCopy(currentPixels, smoothed, bufferSize, bufferSize);
                }
                return;
            }

            // Apply exponential moving average: output = current * factor + previous * (1 - factor)
            // IMPORTANT: Must iterate row-by-row using stride, not linearly, because stride includes padding
            fixed (byte* smoothed = _smoothedPixels)
            {
                int rowBytes = width * 4; // 4 bytes per BGRA pixel
                for (int y = 0; y < height; y++)
                {
                    byte* currentRow = currentPixels + (y * stride);
                    byte* smoothedRow = smoothed + (y * stride);
                    
                    for (int x = 0; x < rowBytes; x++)
                    {
                        smoothedRow[x] = (byte)(currentRow[x] * factor + smoothedRow[x] * (1f - factor));
                    }
                }
            }
        }

        public override void EnableLayerBrush()
        {
            ConfigurationDialog = new LayerBrushConfigurationDialog<CapturePropertiesViewModel>(1300, 650);
            RecreateCaptureZone();
        }

        public void RecreateCaptureZone()
        {
            if (PropertiesOpen || _creatingCaptureZone || _screenCaptureService == null)
                return;

            try
            {
                _creatingCaptureZone = true;
                RemoveCaptureZone();
                AmbilightSmoothedCaptureProperties props = Properties.Capture;
                bool defaulting = props.GraphicsCardDeviceId == 0 || props.GraphicsCardVendorId == 0 || props.DisplayName.CurrentValue == null;

                GraphicsCard? graphicsCard = _screenCaptureService.GetGraphicsCards()
                    .Where(gg => defaulting || (gg.VendorId == props.GraphicsCardVendorId) && (gg.DeviceId == props.GraphicsCardDeviceId))
                    .Cast<GraphicsCard?>()
                    .FirstOrDefault();
                if (graphicsCard == null)
                    return;

                _display = _screenCaptureService.GetDisplays(graphicsCard.Value)
                    .Where(d => defaulting || d.DeviceName.Equals(props.DisplayName.CurrentValue, StringComparison.OrdinalIgnoreCase))
                    .Cast<Display?>()
                    .FirstOrDefault();
                if (_display == null)
                    return;

                // If we're defaulting or always capturing full screen, apply the display to the properties
                if (defaulting || props.CaptureFullScreen.CurrentValue)
                    props.ApplyDisplay(_display.Value, true);

                // Stick to a valid region within the display
                int width = Math.Min(_display.Value.Width, props.Width);
                int height = Math.Min(_display.Value.Height, props.Height);
                int x = Math.Min(_display.Value.Width - width, props.X);
                int y = Math.Min(_display.Value.Height - height, props.Y);
                _captureZone = _screenCaptureService.GetScreenCapture(_display.Value).RegisterCaptureZone(x, y, width, height, props.DownscaleLevel);
                _captureZone.AutoUpdate = false; //TODO DarthAffe 09.04.2021: config?
                
                // Try to use HDR capture if display supports it
                TryInitializeHdrCapture();
            }
            finally
            {
                _creatingCaptureZone = false;
            }
        }
        
        private void TryInitializeHdrCapture()
        {
            if (_display == null)
                return;
                
            try
            {
                HdrScreenCapture hdrCapture = new HdrScreenCapture(_display.Value);
                if (hdrCapture.Initialize() && hdrCapture.IsHdr)
                {
                    // Successfully initialized HDR capture
                    _hdrCapture = hdrCapture;
                    _hdrCaptureBuffer = new byte[hdrCapture.Width * hdrCapture.Height * 4];
                    
                    // Remove the ScreenCapture.NET capture zone since we're using HDR capture
                    RemoveCaptureZone();
                    _display = _display.Value; // Keep display reference
                }
                else
                {
                    // Not HDR or failed to initialize, dispose and fall back to ScreenCapture.NET
                    hdrCapture.Dispose();
                }
            }
            catch
            {
                // Failed to initialize HDR capture, fall back to ScreenCapture.NET
                _hdrCapture?.Dispose();
                _hdrCapture = null;
            }
        }

        private void RemoveCaptureZone()
        {
            if ((_display != null) && (_captureZone != null) && (_screenCaptureService != null))
                _screenCaptureService.GetScreenCapture(_display.Value).UnregisterCaptureZone(_captureZone);
            _captureZone = null;
            
            // Don't null _display if we're using HDR capture
            if (_hdrCapture == null)
                _display = null;
        }

        public override void DisableLayerBrush()
        {
            RemoveCaptureZone();
            _hdrCapture?.Dispose();
            _hdrCapture = null;
            _hdrCaptureBuffer = null;
            _display = null;
            _smoothedPixels = null;
            _hdrAdjustedPixels = null;
            _hdrLut = null;
        }

        #endregion
    }
}