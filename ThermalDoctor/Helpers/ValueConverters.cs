using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ThermalDoctor.Helpers;

/// <summary>
/// Converts a radius value to a negative margin for centering circles on their position point.
/// </summary>
public class NegativeMarginConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double radius)
        {
            var offset = -radius * 1.2; // Account for the glow area
            return new Thickness(offset, offset, 0, 0);
        }
        return new Thickness(0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Multiplies a double by the ConverterParameter factor.
/// </summary>
public class DoubleMultiplierConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d && parameter is string s && double.TryParse(s, CultureInfo.InvariantCulture, out var factor))
            return d * factor;
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Inverts a boolean for visibility (true → Collapsed, false → Visible).
/// </summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
