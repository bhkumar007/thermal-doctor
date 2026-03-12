using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ThermalDoctor.Helpers;
using ThermalDoctor.Models;

namespace ThermalDoctor.ViewModels;

public partial class ThermalReadingViewModel : ObservableObject
{
    [ObservableProperty] private string _componentName = string.Empty;
    [ObservableProperty] private string _zoneId = string.Empty;
    [ObservableProperty] private double _temperatureCelsius;
    [ObservableProperty] private ThermalStatus _status;
    [ObservableProperty] private string _trend = "→";
    [ObservableProperty] private DateTime _timestamp;
    [ObservableProperty] private Point _position;
    [ObservableProperty] private double _radius = 20;
    [ObservableProperty] private string _label = string.Empty;
    [ObservableProperty] private bool _isThrottled;
    [ObservableProperty] private double _throttlePercentage;
    [ObservableProperty] private bool _useFahrenheit;
    [ObservableProperty] private double _thermalLimitC;
    [ObservableProperty] private double _tjMaxC;

    public SolidColorBrush HeatColor => new(HeatColorConverter.GetHeatColor(TemperatureCelsius));

    public SolidColorBrush HeatColorTranslucent
    {
        get
        {
            var c = HeatColorConverter.GetHeatColor(TemperatureCelsius);
            c.A = 160;
            return new SolidColorBrush(c);
        }
    }

    public double DisplayTemperature => UseFahrenheit
        ? Math.Round(TemperatureCelsius * 9.0 / 5.0 + 32, 1)
        : TemperatureCelsius;

    public string TemperatureDisplay => UseFahrenheit
        ? $"{DisplayTemperature:F1}°F"
        : $"{TemperatureCelsius:F1}°C";

    partial void OnUseFahrenheitChanged(bool value)
    {
        OnPropertyChanged(nameof(TemperatureDisplay));
        OnPropertyChanged(nameof(DisplayTemperature));
        OnPropertyChanged(nameof(ThermalLimitDisplay));
        OnPropertyChanged(nameof(TjMaxDisplay));
    }

    public string StatusDisplay => Status.ToString();

    public SolidColorBrush StatusColor => Status switch
    {
        ThermalStatus.Normal => new SolidColorBrush(Color.FromRgb(46, 204, 113)),
        ThermalStatus.Warm => new SolidColorBrush(Color.FromRgb(241, 196, 15)),
        ThermalStatus.Warning => new SolidColorBrush(Color.FromRgb(230, 126, 34)),
        ThermalStatus.Critical => new SolidColorBrush(Color.FromRgb(231, 76, 60)),
        _ => new SolidColorBrush(Colors.Gray)
    };

    public string TrendSymbol => Trend switch
    {
        "↑" => "▲",
        "↓" => "▼",
        _ => "—"
    };

    public SolidColorBrush TrendColor => Trend switch
    {
        "↑" => new SolidColorBrush(Color.FromRgb(231, 76, 60)),
        "↓" => new SolidColorBrush(Color.FromRgb(46, 204, 113)),
        _ => new SolidColorBrush(Color.FromRgb(149, 165, 166))
    };

    public string ThrottleDisplay => IsThrottled ? $"{ThrottlePercentage:F0}% Throttled" : "Normal";

    public string ThermalLimitDisplay => ThermalLimitC > 0
        ? UseFahrenheit ? $"{ThermalLimitC * 9.0 / 5.0 + 32:F0}°F" : $"{ThermalLimitC:F0}°C"
        : "—";
    public string TjMaxDisplay => TjMaxC > 0
        ? UseFahrenheit ? $"{TjMaxC * 9.0 / 5.0 + 32:F0}°F" : $"{TjMaxC:F0}°C"
        : "—";

    public SolidColorBrush ThrottleBrush => IsThrottled
        ? new SolidColorBrush(Color.FromRgb(231, 76, 60))
        : new SolidColorBrush(Color.FromRgb(46, 204, 113));

    public void UpdateFrom(ThermalReading reading)
    {
        ComponentName = reading.ComponentName;
        ZoneId = reading.ZoneId;
        TemperatureCelsius = reading.TemperatureCelsius;
        Status = reading.Status;
        Trend = reading.Trend;
        Timestamp = reading.Timestamp;
        IsThrottled = reading.IsThrottled;
        ThrottlePercentage = reading.ThrottlePercentage;
        if (reading.ThermalLimitC > 0) ThermalLimitC = reading.ThermalLimitC;
        if (reading.TjMaxC > 0) TjMaxC = reading.TjMaxC;

        OnPropertyChanged(nameof(HeatColor));
        OnPropertyChanged(nameof(HeatColorTranslucent));
        OnPropertyChanged(nameof(TemperatureDisplay));
        OnPropertyChanged(nameof(StatusDisplay));
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(TrendSymbol));
        OnPropertyChanged(nameof(TrendColor));
        OnPropertyChanged(nameof(ThrottleDisplay));
        OnPropertyChanged(nameof(ThrottleBrush));
        OnPropertyChanged(nameof(ThermalLimitDisplay));
        OnPropertyChanged(nameof(TjMaxDisplay));
    }
}
