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

        // Color adjustments (always available)
        public IntLayerProperty Brightness { get; set; }
        public IntLayerProperty Contrast { get; set; }
        public IntLayerProperty Exposure { get; set; }
        public IntLayerProperty Saturation { get; set; }
        public BoolLayerProperty AutoExposure { get; set; }

        // Smoothing properties
        public IntLayerProperty SmoothingLevel { get; set; }
        public IntLayerProperty FrameSkip { get; set; }

        protected override void PopulateDefaults()
        {
            DownscaleLevel.DefaultValue = 6;

            // Color adjustment defaults (neutral = no processing cost)
            Brightness.DefaultValue = 0;      // -100 to +100
            Contrast.DefaultValue = 100;      // 50 to 200%
            Exposure.DefaultValue = 100;      // 25 to 400%
            Saturation.DefaultValue = 100;    // 0 to 200%
            AutoExposure.DefaultValue = false;

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