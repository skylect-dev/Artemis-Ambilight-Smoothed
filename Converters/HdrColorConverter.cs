using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Artemis.Plugins.LayerBrushes.AmbilightSmoothed.Converters;

public class HdrColorConverter : IValueConverter
{
    public static readonly HdrColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isHdr)
        {
            // Gold for HDR, Gray for SDR
            return isHdr ? Color.FromArgb(230, 255, 215, 0) : Color.FromArgb(200, 100, 100, 100);
        }

        return Color.FromArgb(200, 100, 100, 100);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
