using System;
using Artemis.Plugins.LayerBrushes.AmbilightSmoothed.PropertyGroups;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HPPH;
using ReactiveUI;
using ScreenCapture.NET;

namespace Artemis.Plugins.LayerBrushes.AmbilightSmoothed.Screens;

public sealed class DisplayPreview : ReactiveObject, IDisposable
{
    #region Properties & Fields

    private bool _isDisposed;

    private readonly ICaptureZone? _captureZone;
    private readonly ICaptureZone? _processedCaptureZone;

    private readonly int _blackBarThreshold;
    private readonly bool _blackBarDetectionTop;
    private readonly bool _blackBarDetectionBottom;
    private readonly bool _blackBarDetectionLeft;
    private readonly bool _blackBarDetectionRight;
    
    private readonly int _brightness;
    private readonly int _contrast;
    private readonly int _exposure;
    private readonly int _saturation;
    private readonly bool _autoExposure;
    private byte[]? _colorLut;
    private int _lastBuiltBrightness;
    private int _lastBuiltContrast;
    private int _lastBuiltExposure;
    private int _lastBuiltSaturation;

    public Display Display { get; }

    public WriteableBitmap Preview { get; }

    private WriteableBitmap? _processedPreview;
    public WriteableBitmap? ProcessedPreview
    {
        get => _processedPreview;
        set => this.RaiseAndSetIfChanged(ref _processedPreview, value);
    }

    #endregion

    #region Constructors

    public DisplayPreview(Display display, bool highQuality = false)
    {
        Display = display;

        var scs = AmbilightSmoothedBootstrapper.ScreenCaptureService;
        if (scs != null)
        {
            _captureZone = scs.GetScreenCapture(display).RegisterCaptureZone(0, 0, display.Width, display.Height, highQuality ? 0 : 2);
            Preview = new WriteableBitmap(new PixelSize(_captureZone.Width, _captureZone.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        }
        else
        {
            _captureZone = null;
            Preview = new WriteableBitmap(new PixelSize(1, 1), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        }
    }

    public DisplayPreview(Display display, AmbilightSmoothedCaptureProperties properties)
    {
        Display = display;
        _blackBarDetectionBottom = properties.BlackBarDetectionBottom;
        _blackBarDetectionTop = properties.BlackBarDetectionTop;
        _blackBarDetectionLeft = properties.BlackBarDetectionLeft;
        _blackBarDetectionRight = properties.BlackBarDetectionRight;
        
        _brightness = properties.Brightness;
        _contrast = properties.Contrast;
        _exposure = properties.Exposure;
        _saturation = properties.Saturation;
        _autoExposure = properties.AutoExposure;

        var scs = AmbilightSmoothedBootstrapper.ScreenCaptureService;
        if (scs != null)
        {
            _captureZone = scs.GetScreenCapture(display).RegisterCaptureZone(0, 0, display.Width, display.Height);
            Preview = new WriteableBitmap(new PixelSize(_captureZone.Width, _captureZone.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        }
        else
        {
            _captureZone = null;
            Preview = new WriteableBitmap(new PixelSize(1, 1), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        }

        if (((properties.X + properties.Width) <= display.Width) && ((properties.Y + properties.Height) <= display.Height))
        {
            _processedCaptureZone = AmbilightSmoothedBootstrapper.ScreenCaptureService.GetScreenCapture(display)
                                                         .RegisterCaptureZone(properties.X, properties.Y, properties.Width, properties.Height, properties.DownscaleLevel);

            _blackBarThreshold = properties.BlackBarDetectionThreshold;
            ProcessedPreview = new WriteableBitmap(new PixelSize(_processedCaptureZone.Width, _processedCaptureZone.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        }
    }

    #endregion

    #region Methods

    public void Update()
    {
        if (_isDisposed) return;

        try
        {
            // DarthAffe 11.09.2023: Accessing the low-level images is a source of potential errors in the future since we assume the pixel-format. Currently both used providers are BGRA, but if there are ever issues with shifted colors, this is the place to start investigating.

            if (_captureZone != null)
            {
                using (_captureZone.Lock())
                    WritePixels(Preview, _captureZone.GetRefImage<ColorBGRA>());
            }

            if (_processedCaptureZone == null)
                return;

            using (_processedCaptureZone.Lock())
            {
                if (_processedCaptureZone.RawBuffer.Length == 0)
                    return;

                RefImage<ColorBGRA> processedImage = _processedCaptureZone.GetRefImage<ColorBGRA>();
                if (_blackBarDetectionTop || _blackBarDetectionBottom || _blackBarDetectionLeft || _blackBarDetectionRight)
                {
                    RefImage<ColorBGRA> croppedImage = processedImage.RemoveBlackBars(_blackBarThreshold, _blackBarDetectionTop, _blackBarDetectionBottom, _blackBarDetectionLeft, _blackBarDetectionRight);

                    // Validate cropped dimensions - if invalid, skip update but keep trying
                    if (croppedImage.Width <= 0 || croppedImage.Height <= 0)
                        return;

                    if ((ProcessedPreview == null) || (Math.Abs(ProcessedPreview.Size.Width - croppedImage.Width) > 0.001) || (Math.Abs(ProcessedPreview.Size.Height - croppedImage.Height) > 0.001))
                        ProcessedPreview = new WriteableBitmap(new PixelSize(croppedImage.Width, croppedImage.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);

                    bool hasColorAdjustments = _brightness != 0 || _contrast != 100 || _exposure != 100 || _saturation != 100 || _autoExposure;
                    if (hasColorAdjustments)
                        WritePixelsWithColorAdjustments(ProcessedPreview, croppedImage);
                    else
                        WritePixels(ProcessedPreview, croppedImage);
                }
                else
                {
                    // When black bars are disabled, recreate ProcessedPreview with original dimensions if needed
                    if ((ProcessedPreview == null) || (Math.Abs(ProcessedPreview.Size.Width - processedImage.Width) > 0.001) || (Math.Abs(ProcessedPreview.Size.Height - processedImage.Height) > 0.001))
                        ProcessedPreview = new WriteableBitmap(new PixelSize(processedImage.Width, processedImage.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);

                    bool hasColorAdjustments = _brightness != 0 || _contrast != 100 || _exposure != 100 || _saturation != 100 || _autoExposure;
                    if (hasColorAdjustments)
                        WritePixelsWithColorAdjustments(ProcessedPreview, processedImage);
                    else
                        WritePixels(ProcessedPreview, processedImage);
                }
            }
        }
        catch (Exception)
        {
            // Silently catch exceptions to prevent crashes - preview will freeze but continue trying on next update
        }
    }

    private void EnsureColorLut(int brightness, int contrast, int exposurePercent, int saturation)
    {
        brightness = Math.Clamp(brightness, -100, 100);
        contrast = Math.Clamp(contrast, 50, 200);
        exposurePercent = Math.Clamp(exposurePercent, 25, 400);

        if (_colorLut != null &&
            _lastBuiltBrightness == brightness &&
            _lastBuiltContrast == contrast &&
            _lastBuiltExposure == exposurePercent &&
            _lastBuiltSaturation == saturation)
            return;

        _colorLut = new byte[256];
        
        // Apply adjustments in order: brightness → contrast → exposure (with filmic curve)
        float brightnessOffset = brightness / 255f;
        float contrastFactor = contrast / 100f;
        float exposure = exposurePercent / 100f;
        const float a = 2.51f, b = 0.03f, c = 2.43f, d = 0.59f, e = 0.14f;

        for (int i = 0; i < 256; i++)
        {
            float x = i / 255f;
            x += brightnessOffset;
            x = Math.Clamp(x, 0f, 1f);
            x = (x - 0.5f) * contrastFactor + 0.5f;
            x = Math.Clamp(x, 0f, 1f);
            x *= exposure;
            float numerator = x * (a * x + b);
            float denominator = x * (c * x + d) + e;
            float y = denominator != 0f ? numerator / denominator : 0f;
            y = Math.Clamp(y, 0f, 1f);

            _colorLut[i] = (byte)Math.Clamp((int)(y * 255f + 0.5f), 0, 255);
        }

        _lastBuiltBrightness = brightness;
        _lastBuiltContrast = contrast;
        _lastBuiltExposure = exposurePercent;
        _lastBuiltSaturation = saturation;
    }

    private unsafe void WritePixelsWithColorAdjustments(WriteableBitmap preview, RefImage<ColorBGRA> image)
    {
        using ILockedFramebuffer framebuffer = preview.Lock();
        
        fixed (byte* src = image)
        {
            int effectiveExposure = _exposure;
            if (_autoExposure)
            {
                // Auto-exposure computes optimal value; use exposure slider as max clamp
                int autoExposureValue = ComputeAutoExposurePercent(src, image.Width, image.Height, image.RawStride);
                effectiveExposure = Math.Clamp(autoExposureValue, 25, _exposure);
            }

            EnsureColorLut(_brightness, _contrast, effectiveExposure, _saturation);
            if (_colorLut == null)
                return;

            fixed (byte* lutPtr = _colorLut)
            {
            byte* dst = (byte*)framebuffer.Address;
            int sat = Math.Clamp(_saturation, 0, 200);
            int rowBytes = image.Width * 4;
            
            for (int y = 0; y < image.Height; y++)
            {
                byte* srcRow = src + (y * image.RawStride);
                byte* dstRow = dst + (y * framebuffer.RowBytes);
                
                for (int x = 0; x < rowBytes; x += 4)
                {
                    byte b = lutPtr[srcRow[x + 0]];
                    byte g = lutPtr[srcRow[x + 1]];
                    byte r = lutPtr[srcRow[x + 2]];
                    byte a = srcRow[x + 3];

                    if (sat != 100)
                    {
                        int l = (r * 77 + g * 150 + b * 29) >> 8;
                        int rr = l + ((r - l) * sat) / 100;
                        int gg = l + ((g - l) * sat) / 100;
                        int bb = l + ((b - l) * sat) / 100;
                        r = (byte)Math.Clamp(rr, 0, 255);
                        g = (byte)Math.Clamp(gg, 0, 255);
                        b = (byte)Math.Clamp(bb, 0, 255);
                    }

                    dstRow[x + 0] = b;
                    dstRow[x + 1] = g;
                    dstRow[x + 2] = r;
                    dstRow[x + 3] = a;
                }
            }
            }
        }
    }

    private static unsafe int ComputeAutoExposurePercent(byte* src, int width, int height, int stride)
    {
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

        float desired = 0.96f;

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

    private static unsafe void WritePixels(WriteableBitmap preview, RefImage<ColorBGRA> image)
    {
        using ILockedFramebuffer framebuffer = preview.Lock();
        image.CopyTo(new Span<ColorBGRA>((void*)framebuffer.Address, framebuffer.Size.Width * framebuffer.Size.Height));
    }

    public void Dispose()
    {
        var scs = AmbilightSmoothedBootstrapper.ScreenCaptureService;
        if (_captureZone != null && scs != null)
            scs.GetScreenCapture(Display).UnregisterCaptureZone(_captureZone);
        _isDisposed = true;
    }

    #endregion
}