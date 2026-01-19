# Ambilight Smoothed

An enhanced Artemis RGB plugin based on the official Ambilight layer brush, adding smooth color transitions and flexible color adjustment controls for perfect screen capture reproduction.

## Features

- **Smooth Color Transitions** - Adjustable smoothing (0-10) eliminates LED color jitter and flickering
- **Frame Skip** - Configurable frame skip (0-10) to reduce CPU usage
- **Color Adjustments** - Brightness, contrast, exposure, and saturation controls for any capture scenario
- **Auto-Exposure** - Intelligent exposure adjustment based on image analysis
- **Black Bar Detection** - Automatic letterbox removal with configurable threshold
- **Real-time Preview** - All adjustments reflect immediately in the preview window
- **Zero-Cost Neutral** - No performance impact when all adjustments are at default values

## Installation

1. Download `AmbilightSmoothed.zip`
2. In Artemis, go to Settings → Plugins → Import Plugin
3. Select the downloaded ZIP file

--OR--

1. Install Ambilight Smoothed from the Artemis workshop
2. Install the Ambilight Smoothed - Profile from the Artemis Workshop

## Configuration

### Capture Settings
- **Source Region**: Define screen capture area with X/Y offset and width/height
- **Downscale Level** (0-10): Higher values improve performance (default: 6)
- **Flip Horizontal/Vertical**: Mirror the captured image
- **Black Bar Detection**: Automatically crop letterbox bars (top/bottom/left/right)
- **Detection Threshold** (0-50): How dark pixels must be to be considered black bars

### Color Adjustments
- **Brightness** (-100 to +100): Simple offset adjustment. 0 = unchanged (default)
- **Contrast** (50-200%): Expand or compress tonal range. 100 = unchanged (default)
- **Exposure** (25-400%): Tone curve adjustment with highlight rolloff. 100 = unchanged (default)
  - Use 25-99% to dim over-bright captures
  - Use 101-400% to brighten under-exposed captures
- **Saturation** (0-200%): Color intensity. 0 = grayscale, 100 = unchanged (default), 200 = vivid
- **Auto-Exposure**: Automatically adjusts exposure based on image brightness (checkbox)
  - Analyzes 95th/99.5th percentile to avoid clipped highlights
  - Exposure slider acts as maximum when enabled

### Smoothing Settings
- **Smoothing Level** (0-10): Higher values create smoother color transitions
  - 0 = no smoothing (instant, may be jittery)
  - 6 = default (good balance)
  - 10 = maximum smoothing (very smooth, slower response)
- **Frame Skip** (0-10): Skip frames to reduce CPU usage
  - 0 = process every frame (default)
  - Higher values reduce CPU load but update less frequently

## Credits

Based on the official [Artemis.Plugins.LayerBrushes.Ambilight](https://github.com/Artemis-RGB/Artemis.Plugins) by the Artemis RGB team.

## License

Licensed under the same terms as the original Artemis plugins (see LICENSE file).
