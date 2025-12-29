# Ambilight Smoothed

An enhanced Artemis RGB plugin based on the official Ambilight layer brush, adding smooth color transitions and HDR compensation for screen capture.

## Features

- **Smooth Color Transitions** - Adjustable smoothing (0-10) eliminates LED color jitter
- **Frame Skip** - Configurable frame skip to reduce CPU usage
- **HDR Compensation** - Black point, white point, and saturation adjustments to fix washed-out HDR captures
- **Real-time Preview** - All adjustments reflect immediately in the preview window

## Installation

1. Download the latest `AmbilightSmoothed-HDR.zip` from releases
2. In Artemis, go to Settings → Plugins → Import Plugin
3. Select the downloaded ZIP file

## Configuration

### Smoothing Settings
- **Smoothing Level** (0-10): Higher values create smoother color transitions. 0 = no smoothing, 10 = maximum smoothing
- **Frame Skip** (0-10): Skip frames to reduce CPU usage. 0 = process every frame

### HDR Settings
- **HDR Toggle**: Enable HDR compensation for washed-out HDR screen captures
- **Black Point** (0-255): Raises dark values to reduce washout (default: 30)
- **White Point** (0-255): Compresses bright values (default: 235)
- **Saturation** (0-200%): Boosts color intensity (default: 120%)

## Building from Source

```powershell
cd f:\ArtemisPlugins\AmbilightSmoothed
dotnet build -c Release
```

The compiled plugin will be in `bin\Release\net10.0-windows\`

## Credits

Based on the official [Artemis.Plugins.LayerBrushes.Ambilight](https://github.com/Artemis-RGB/Artemis.Plugins) by the Artemis RGB team.

## License

Licensed under the same terms as the original Artemis plugins (see LICENSE file).
