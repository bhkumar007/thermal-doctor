using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ThermalDoctor.Helpers;

/// <summary>
/// Maps a temperature value (°C) to a gradient color:
/// Blue (≤30°C) → Green (50°C) → Yellow (70°C) → Red (≥90°C)
/// </summary>
public class HeatColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double temp)
            return new SolidColorBrush(Colors.Gray);

        var color = GetHeatColor(temp);
        return new SolidColorBrush(color);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();

    public static Color GetHeatColor(double temperatureCelsius)
    {
        // Clamp to range
        var t = Math.Clamp(temperatureCelsius, 20, 100);

        // Normalize to 0..1 over the 20-100°C range
        var normalized = (t - 20.0) / 80.0;

        // Multi-stop gradient: Blue → Cyan → Green → Yellow → Orange → Red
        return normalized switch
        {
            <= 0.15 => Lerp(Color.FromRgb(0, 100, 255), Color.FromRgb(0, 200, 255), normalized / 0.15),
            <= 0.35 => Lerp(Color.FromRgb(0, 200, 255), Color.FromRgb(0, 220, 80), (normalized - 0.15) / 0.20),
            <= 0.55 => Lerp(Color.FromRgb(0, 220, 80), Color.FromRgb(255, 230, 0), (normalized - 0.35) / 0.20),
            <= 0.75 => Lerp(Color.FromRgb(255, 230, 0), Color.FromRgb(255, 140, 0), (normalized - 0.55) / 0.20),
            _ => Lerp(Color.FromRgb(255, 140, 0), Color.FromRgb(220, 20, 20), (normalized - 0.75) / 0.25),
        };
    }

    private static Color Lerp(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }
}

/// <summary>
/// Converts temperature to a heat color with 50% opacity for overlay circles.
/// </summary>
public class HeatColorWithOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double temp)
            return new SolidColorBrush(Colors.Transparent);

        var color = HeatColorConverter.GetHeatColor(temp);
        var opacity = parameter is string s && double.TryParse(s, out var o) ? o : 0.6;
        color.A = (byte)(255 * opacity);
        return new SolidColorBrush(color);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
