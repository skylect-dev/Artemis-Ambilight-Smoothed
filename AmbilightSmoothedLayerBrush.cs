using System;
using System.Linq;
using Artemis.Core.LayerBrushes;
using Artemis.Plugins.LayerBrushes.AmbilightSmoothed.PropertyGroups;
using Artemis.Plugins.LayerBrushes.AmbilightSmoothed.Screens;
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

        private Display? _display;
        private ICaptureZone? _captureZone;
        private bool _creatingCaptureZone;

        // Smoothing state
        private byte[]? _smoothedPixels;
        private int _frameCounter;
        private int _lastWidth;
        private int _lastHeight;

        // Color adjustment state (applied on downscaled capture)
        private byte[]? _colorAdjustedPixels;
        private byte[]? _colorLut;
        private int _lastBrightness;
        private int _lastContrast;
        private int _lastExposure;
        private int _lastSaturation;
        private bool _lastAutoExposure;

        #endregion

        #region Methods

        public override void Update(double deltaTime)
        {
            _captureZone?.RequestUpdate();
        }

        public override unsafe void Render(SKCanvas canvas, SKRect bounds, SKPaint paint)
        {
            if (_captureZone == null) return;

            AmbilightSmoothedCaptureProperties properties = Properties.Capture;
            int smoothingLevel = properties.SmoothingLevel.CurrentValue;
            int frameSkip = properties.FrameSkip.CurrentValue;
            int brightness = properties.Brightness.CurrentValue;
            int contrast = properties.Contrast.CurrentValue;
            int exposure = properties.Exposure.CurrentValue;
            int saturation = properties.Saturation.CurrentValue;
            bool autoExposure = properties.AutoExposure.CurrentValue;
            
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

                // Check if any color adjustments are non-neutral
                bool hasColorAdjustments = brightness != 0 || contrast != 100 || exposure != 100 || saturation != 100 || autoExposure;

                // Force reprocessing if color settings changed (for live preview updates)
                bool colorSettingsChanged = hasColorAdjustments && 
                    (_lastBrightness != brightness || 
                     _lastContrast != contrast || 
                     _lastExposure != exposure ||
                     _lastSaturation != saturation ||
                     _lastAutoExposure != autoExposure);
                if (colorSettingsChanged)
                    shouldProcess = true;

                fixed (byte* img = image)
                {
                    byte* sourcePixels = img;

                    // Apply color adjustments (brightness, contrast, exposure, saturation) into a reusable buffer.
                    // We only recompute when shouldProcess=true, so FrameSkip reduces CPU cost.
                    // But we MUST recompute if dimensions changed (e.g., black bar detection).
                    // Skip entirely if all settings are neutral (zero cost optimization).
                    bool dimensionsChanged = (image.Width != _lastWidth || image.Height != _lastHeight);
                    
                    if (hasColorAdjustments)
                    {
                        if (shouldProcess || dimensionsChanged)
                        {
                            int effectiveExposure = exposure;
                            if (autoExposure)
                            {
                                // Auto-exposure computes optimal value; use exposure slider as max clamp
                                int autoExposureValue = ComputeAutoExposurePercent(img, image.Width, image.Height, image.RawStride);
                                effectiveExposure = Math.Clamp(autoExposureValue, 25, exposure);
                            }

                            EnsureColorLut(brightness, contrast, effectiveExposure, saturation);
                            EnsureColorBuffer(image.Width, image.Height, image.RawStride);
                            fixed (byte* colorPtr = _colorAdjustedPixels)
                            {
                                ApplyColorAdjustments(img, colorPtr, image.Width, image.Height, image.RawStride, _colorLut!, saturation);
                                sourcePixels = colorPtr;
                            }

                            _lastAutoExposure = autoExposure;
                        }
                        else if (_colorAdjustedPixels != null)
                        {
                            fixed (byte* colorPtr = _colorAdjustedPixels)
                            {
                                sourcePixels = colorPtr;
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

        private void EnsureColorBuffer(int width, int height, int stride)
        {
            int bufferSize = height * stride;
            if (_colorAdjustedPixels == null || _colorAdjustedPixels.Length != bufferSize)
                _colorAdjustedPixels = new byte[bufferSize];
        }

        private void EnsureColorLut(int brightness, int contrast, int exposurePercent, int saturation)
        {
            brightness = Math.Clamp(brightness, -100, 100);
            contrast = Math.Clamp(contrast, 50, 200);
            exposurePercent = Math.Clamp(exposurePercent, 25, 400);

            if (_colorLut != null &&
                _lastBrightness == brightness &&
                _lastContrast == contrast &&
                _lastExposure == exposurePercent &&
                _lastSaturation == saturation)
                return;

            _colorLut = new byte[256];
            
            // Apply adjustments in order: brightness → contrast → exposure (with filmic curve)
            float brightnessOffset = brightness / 255f;  // -100 to +100 → -0.39 to +0.39
            float contrastFactor = contrast / 100f;      // 50 to 200% → 0.5 to 2.0
            float exposure = exposurePercent / 100f;      // 25 to 400% → 0.25 to 4.0
            
            // Filmic/ACES-like curve constants
            const float a = 2.51f, b = 0.03f, c = 2.43f, d = 0.59f, e = 0.14f;

            for (int i = 0; i < 256; i++)
            {
                float x = i / 255f;
                
                // 1. Apply brightness (simple offset)
                x += brightnessOffset;
                x = Math.Clamp(x, 0f, 1f);
                
                // 2. Apply contrast (expand/compress around midpoint)
                x = (x - 0.5f) * contrastFactor + 0.5f;
                x = Math.Clamp(x, 0f, 1f);
                
                // 3. Apply exposure with filmic curve (prevents harsh clipping)
                x *= exposure;
                float numerator = x * (a * x + b);
                float denominator = x * (c * x + d) + e;
                float y = denominator != 0f ? numerator / denominator : 0f;
                y = Math.Clamp(y, 0f, 1f);

                _colorLut[i] = (byte)Math.Clamp((int)(y * 255f + 0.5f), 0, 255);
            }

            _lastBrightness = brightness;
            _lastContrast = contrast;
            _lastExposure = exposurePercent;
            _lastSaturation = saturation;
        }

        private static unsafe void ApplyColorAdjustments(byte* src, byte* dst, int width, int height, int stride, byte[] lut, int saturation)
        {
            // saturation: percent (100 = unchanged, 0 = grayscale, 200 = vivid)
            int sat = Math.Clamp(saturation, 0, 200);

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

        private static unsafe int ComputeAutoExposurePercent(byte* src, int width, int height, int stride)
        {
            // Goal: choose an exposure so a high percentile (bright-but-not-clipped) maps near the top
            // of the output range using the filmic curve, without needing black/white point clamping.

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

            // Aim for highlights just below max output (0.96) so the filmic curve rolls off smoothly
            float desired = 0.96f;

            // Binary search exposure in [0.25 .. 4.0] (matches LUT clamp 25-400%)
            float lo = 0.25f;
            float hi = 4.0f;
            for (int iter = 0; iter < 10; iter++)
            {
                float mid = (lo + hi) * 0.5f;
                float y = EvaluateFilmic(refLuma, mid);
                if (y < desired)
                    lo = mid;
                else
                    hi = mid;
            }

            int result = (int)MathF.Round(((lo + hi) * 0.5f) * 100f);
            return Math.Clamp(result, 25, 400);
        }

        private static float EvaluateFilmic(int inputByte, float exposure)
        {
            float x = (Math.Clamp(inputByte, 0, 255) / 255f) * exposure;
            const float a = 2.51f, b = 0.03f, c = 2.43f, d = 0.59f, e = 0.14f;
            float numerator = x * (a * x + b);
            float denominator = x * (c * x + d) + e;
            float y = denominator != 0f ? numerator / denominator : 0f;
            return Math.Clamp(y, 0f, 1f);
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
            }
            finally
            {
                _creatingCaptureZone = false;
            }
        }

        private void RemoveCaptureZone()
        {
            if ((_display != null) && (_captureZone != null) && (_screenCaptureService != null))
                _screenCaptureService.GetScreenCapture(_display.Value).UnregisterCaptureZone(_captureZone);
            _captureZone = null;
            _display = null;
        }

        public override void DisableLayerBrush()
        {
            RemoveCaptureZone();
            _smoothedPixels = null;
            _colorAdjustedPixels = null;
            _colorLut = null;
        }

        #endregion
    }
}