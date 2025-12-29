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

        // HDR compensation state (applied on downscaled capture)
        private byte[]? _hdrAdjustedPixels;
        private byte[]? _hdrLut;
        private int _hdrLastBlackPoint;
        private int _hdrLastWhitePoint;
        private int _hdrLastSaturation;

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
            bool hdrEnabled = properties.Hdr.CurrentValue;
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
                if (properties.BlackBarDetectionTop || properties.BlackBarDetectionBottom || properties.BlackBarDetectionLeft || properties.BlackBarDetectionRight)
                    image = image.RemoveBlackBars(properties.BlackBarDetectionThreshold,
                                                  properties.BlackBarDetectionTop, properties.BlackBarDetectionBottom,
                                                  properties.BlackBarDetectionLeft, properties.BlackBarDetectionRight);

                // Handle frame skipping
                _frameCounter++;
                bool shouldProcess = _frameCounter > frameSkip;
                if (shouldProcess)
                    _frameCounter = 0;

                fixed (byte* img = image)
                {
                    byte* sourcePixels = img;

                    // Apply HDR compensation (levels + saturation) into a reusable buffer.
                    // We only recompute when shouldProcess=true, so FrameSkip reduces CPU cost.
                    if (hdrEnabled)
                    {
                        if (shouldProcess)
                        {
                            EnsureHdrLut(hdrBlackPoint, hdrWhitePoint, hdrSaturation);
                            EnsureHdrBuffer(image.Width, image.Height, image.RawStride);
                            fixed (byte* hdrPtr = _hdrAdjustedPixels)
                            {
                                ApplyHdrCompensation(img, hdrPtr, image.Width, image.Height, image.RawStride, _hdrLut!, hdrSaturation);
                                sourcePixels = hdrPtr;
                            }
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
                    if (usesSmoothing && shouldProcess)
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

        private void EnsureHdrLut(int blackPoint, int whitePoint, int saturation)
        {
            blackPoint = Math.Clamp(blackPoint, 0, 255);
            whitePoint = Math.Clamp(whitePoint, 0, 255);
            if (whitePoint <= blackPoint)
                whitePoint = Math.Min(255, blackPoint + 1);

            if (_hdrLut != null &&
                _hdrLastBlackPoint == blackPoint &&
                _hdrLastWhitePoint == whitePoint &&
                _hdrLastSaturation == saturation)
                return;

            _hdrLut = new byte[256];
            for (int i = 0; i < 256; i++)
            {
                int v;
                if (i <= blackPoint)
                    v = 0;
                else if (i >= whitePoint)
                    v = 255;
                else
                    v = (i - blackPoint) * 255 / (whitePoint - blackPoint);

                _hdrLut[i] = (byte)v;
            }

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
            fixed (byte* smoothed = _smoothedPixels)
            {
                int pixelCount = width * height * 4; // 4 bytes per BGRA pixel
                for (int i = 0; i < pixelCount; i++)
                {
                    smoothed[i] = (byte)(currentPixels[i] * factor + smoothed[i] * (1f - factor));
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
            _hdrAdjustedPixels = null;
            _hdrLut = null;
        }

        #endregion
    }
}