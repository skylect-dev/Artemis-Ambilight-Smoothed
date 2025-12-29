using Artemis.Core;

namespace Artemis.Plugins.LayerBrushes.AmbilightSmoothed.PropertyGroups
{
    public class AmbilightSmoothedPropertyGroup : LayerPropertyGroup
    {
        #region Properties & Fields

        public AmbilightSmoothedCaptureProperties Capture { get; set; }

        #endregion

        #region Methods

        protected override void PopulateDefaults()
        {
        }

        protected override void EnableProperties()
        {
            Capture.IsHidden = true;
        }

        protected override void DisableProperties()
        {
        }

        #endregion
    }
}