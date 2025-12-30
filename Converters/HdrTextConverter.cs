using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Artemis.Plugins.LayerBrushes.AmbilightSmoothed.Converters;

public class HdrTextConverter : IValueConverter
{
    public static readonly HdrTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isHdr)
        {
            return isHdr ? "HDR" : "SDR";
        }

        return "SDR";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
