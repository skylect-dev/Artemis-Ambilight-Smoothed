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

    private readonly ICaptureZone _captureZone;
    private readonly ICaptureZone? _processedCaptureZone;

    private readonly int _blackBarThreshold;
    private readonly bool _blackBarDetectionTop;
    private readonly bool _blackBarDetectionBottom;
    private readonly bool _blackBarDetectionLeft;
    private readonly bool _blackBarDetectionRight;
    
    private readonly bool _hdr;
    private readonly int _hdrExposure;
    private readonly int _hdrBlackPoint;
    private readonly int _hdrWhitePoint;
    private readonly int _hdrSaturation;
    private byte[]? _hdrLut;

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

        _captureZone = AmbilightSmoothedBootstrapper.ScreenCaptureService!.GetScreenCapture(display).RegisterCaptureZone(0, 0, display.Width, display.Height, highQuality ? 0 : 2);
        Preview = new WriteableBitmap(new PixelSize(_captureZone.Width, _captureZone.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
    }

    public DisplayPreview(Display display, AmbilightSmoothedCaptureProperties properties)
    {
        Display = display;
        _blackBarDetectionBottom = properties.BlackBarDetectionBottom;
        _blackBarDetectionTop = properties.BlackBarDetectionTop;
        _blackBarDetectionLeft = properties.BlackBarDetectionLeft;
        _blackBarDetectionRight = properties.BlackBarDetectionRight;
        
        _hdr = properties.Hdr;
        _hdrExposure = properties.HdrExposure;
        _hdrBlackPoint = properties.HdrBlackPoint;
        _hdrWhitePoint = properties.HdrWhitePoint;
        _hdrSaturation = properties.HdrSaturation;
        
        if (_hdr)
            BuildHdrLut(_hdrExposure, _hdrBlackPoint, _hdrWhitePoint);

        _captureZone = AmbilightSmoothedBootstrapper.ScreenCaptureService!.GetScreenCapture(display).RegisterCaptureZone(0, 0, display.Width, display.Height);
        Preview = new WriteableBitmap(new PixelSize(_captureZone.Width, _captureZone.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);

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

            using (_captureZone.Lock())
                WritePixels(Preview, _captureZone.GetRefImage<ColorBGRA>());

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

                    if (_hdr)
                        WritePixelsWithHdr(ProcessedPreview, croppedImage);
                    else
                        WritePixels(ProcessedPreview, croppedImage);
                }
                else
                {
                    // When black bars are disabled, recreate ProcessedPreview with original dimensions if needed
                    if ((ProcessedPreview == null) || (Math.Abs(ProcessedPreview.Size.Width - processedImage.Width) > 0.001) || (Math.Abs(ProcessedPreview.Size.Height - processedImage.Height) > 0.001))
                        ProcessedPreview = new WriteableBitmap(new PixelSize(processedImage.Width, processedImage.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);

                    if (_hdr)
                        WritePixelsWithHdr(ProcessedPreview, processedImage);
                    else
                        WritePixels(ProcessedPreview, processedImage);
                }
            }
        }
        catch (Exception ex)
        {
            // Log exception but don't crash - previews will freeze but continue trying on next update
            System.Diagnostics.Debug.WriteLine($"DisplayPreview.Update exception: {ex.Message}");
        }
    }

    private void BuildHdrLut(int exposurePercent, int blackPoint, int whitePoint)
    {
        blackPoint = Math.Clamp(blackPoint, 0, 255);
        whitePoint = Math.Clamp(whitePoint, 0, 255);
        if (whitePoint <= blackPoint)
            whitePoint = Math.Min(255, blackPoint + 1);

        exposurePercent = Math.Clamp(exposurePercent, 1, 400);

        _hdrLut = new byte[256];
        // Filmic/ACES-like curve with exposure, then clamp to black/white as output bounds
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
    }

    private unsafe void WritePixelsWithHdr(WriteableBitmap preview, RefImage<ColorBGRA> image)
    {
        if (_hdrLut == null) return;

        using ILockedFramebuffer framebuffer = preview.Lock();
        
        fixed (byte* src = image)
        fixed (byte* lutPtr = _hdrLut)
        {
            byte* dst = (byte*)framebuffer.Address;
            int sat = Math.Clamp(_hdrSaturation, 0, 400);
            int rowBytes = image.Width * 4; // Actual pixel data per row
            
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

    private static unsafe void WritePixels(WriteableBitmap preview, RefImage<ColorBGRA> image)
    {
        using ILockedFramebuffer framebuffer = preview.Lock();
        image.CopyTo(new Span<ColorBGRA>((void*)framebuffer.Address, framebuffer.Size.Width * framebuffer.Size.Height));
    }

    public void Dispose()
    {
        AmbilightSmoothedBootstrapper.ScreenCaptureService!.GetScreenCapture(Display).UnregisterCaptureZone(_captureZone);
        _isDisposed = true;
    }

    #endregion
}