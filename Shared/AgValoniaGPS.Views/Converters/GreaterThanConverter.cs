using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AgValoniaGPS.Views.Converters;

/// <summary>
/// Converts a numeric value to bool based on whether it's greater than the parameter.
/// Use for visibility bindings to show/hide elements based on count.
/// Example: IsVisible="{Binding Count, Converter={StaticResource GreaterThanConverter}, ConverterParameter=3}"
/// Returns true if Count > 3
/// </summary>
public class GreaterThanConverter : IValueConverter
{
    public static readonly GreaterThanConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        double numValue;
        double threshold;

        // Parse the value
        if (value is int intVal)
            numValue = intVal;
        else if (value is double dblVal)
            numValue = dblVal;
        else if (value is float fltVal)
            numValue = fltVal;
        else if (value is long lngVal)
            numValue = lngVal;
        else if (!double.TryParse(value.ToString(), out numValue))
            return false;

        // Parse the threshold from parameter
        if (parameter is int intParam)
            threshold = intParam;
        else if (parameter is double dblParam)
            threshold = dblParam;
        else if (!double.TryParse(parameter.ToString(), out threshold))
            return false;

        return numValue > threshold;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
