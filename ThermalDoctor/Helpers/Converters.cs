using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ThermalDoctor.Models;

namespace ThermalDoctor.Helpers;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ThermalStatus status)
            return new SolidColorBrush(Colors.Gray);

        return status switch
        {
            ThermalStatus.Normal => new SolidColorBrush(Color.FromRgb(46, 204, 113)),
            ThermalStatus.Warm => new SolidColorBrush(Color.FromRgb(241, 196, 15)),
            ThermalStatus.Warning => new SolidColorBrush(Color.FromRgb(230, 126, 34)),
            ThermalStatus.Critical => new SolidColorBrush(Color.FromRgb(231, 76, 60)),
            _ => new SolidColorBrush(Colors.Gray)
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class TrendToSymbolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "↑" => "▲",
            "↓" => "▼",
            _ => "■"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class TrendToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "↑" => new SolidColorBrush(Color.FromRgb(231, 76, 60)),
            "↓" => new SolidColorBrush(Color.FromRgb(46, 204, 113)),
            _ => new SolidColorBrush(Color.FromRgb(149, 165, 166))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
