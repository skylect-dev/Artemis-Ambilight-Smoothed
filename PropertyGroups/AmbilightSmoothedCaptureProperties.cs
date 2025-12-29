using Artemis.Core;
using ScreenCapture.NET;

namespace Artemis.Plugins.LayerBrushes.AmbilightSmoothed.PropertyGroups
{
    public class AmbilightSmoothedCaptureProperties : LayerPropertyGroup
    {
        public IntLayerProperty GraphicsCardVendorId { get; set; }
        public IntLayerProperty GraphicsCardDeviceId { get; set; }
        public LayerProperty<string> DisplayName { get; set; }

        public IntLayerProperty X { get; set; }
        public IntLayerProperty Y { get; set; }
        public IntLayerProperty Width { get; set; }
        public IntLayerProperty Height { get; set; }
        public BoolLayerProperty CaptureFullScreen { get; set; }

        public BoolLayerProperty FlipHorizontal { get; set; }
        public BoolLayerProperty FlipVertical { get; set; }
        public IntLayerProperty DownscaleLevel { get; set; }

        public BoolLayerProperty BlackBarDetectionTop { get; set; }
        public BoolLayerProperty BlackBarDetectionBottom { get; set; }
        public BoolLayerProperty BlackBarDetectionLeft { get; set; }
        public BoolLayerProperty BlackBarDetectionRight { get; set; }
        public IntLayerProperty BlackBarDetectionThreshold { get; set; }

        // HDR compensation (for washed out HDR captures)
        public BoolLayerProperty Hdr { get; set; }
        public IntLayerProperty HdrBlackPoint { get; set; }
        public IntLayerProperty HdrWhitePoint { get; set; }
        public IntLayerProperty HdrSaturation { get; set; }

        // Smoothing properties
        public IntLayerProperty SmoothingLevel { get; set; }
        public IntLayerProperty FrameSkip { get; set; }

        protected override void PopulateDefaults()
        {
            DownscaleLevel.DefaultValue = 6;

            // HDR compensation defaults
            Hdr.DefaultValue = false;
            HdrBlackPoint.DefaultValue = 20;   // Raise dark values to reduce washout
            HdrWhitePoint.DefaultValue = 200;  // Aggressive highlight compression for HDR
            HdrSaturation.DefaultValue = 110;  // Moderate boost for washed colors

            SmoothingLevel.DefaultValue = 6;  // 0-10, higher = smoother but more lag
            FrameSkip.DefaultValue = 0;  // 0 = process every frame
        }

        protected override void EnableProperties()
        {
        }

        protected override void DisableProperties()
        {
        }

        public void ApplyDisplay(Display display, bool includeRegion)
        {
            GraphicsCardVendorId.BaseValue = display.GraphicsCard.VendorId;
            GraphicsCardDeviceId.BaseValue = display.GraphicsCard.DeviceId;
            DisplayName.BaseValue = display.DeviceName;

            if (includeRegion)
            {
                X.BaseValue = 0;
                Y.BaseValue = 0;
                Width.BaseValue = display.Width;
                Height.BaseValue = display.Height;
                CaptureFullScreen.BaseValue = true;
            }

            // Always true of course if includeRegion is true
            CaptureFullScreen.BaseValue = X == 0 & Y == 0 && Width == display.Width && Height == display.Height;
        }
    }
}