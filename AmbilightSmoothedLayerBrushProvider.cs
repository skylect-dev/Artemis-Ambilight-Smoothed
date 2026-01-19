using Artemis.Core.LayerBrushes;

namespace Artemis.Plugins.LayerBrushes.AmbilightSmoothed
{
    public class AmbilightSmoothedLayerBrushProvider : LayerBrushProvider
    {
        #region Methods

        public override void Enable()
        {
            RegisterLayerBrushDescriptor<AmbilightSmoothedLayerBrush>("Ambilight Smoothed", "A brush that shows the current display-image with color smoothing.", "MonitorMultiple");
        }

        public override void Disable()
        { }

        #endregion
    }
}
