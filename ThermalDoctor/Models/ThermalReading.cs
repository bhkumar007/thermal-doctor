using System;

namespace ThermalDoctor.Models;

public class ThermalReading
{
    public string ComponentName { get; set; } = string.Empty;
    public string ZoneId { get; set; } = string.Empty;
    public double TemperatureCelsius { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public ThermalStatus Status => TemperatureCelsius switch
    {
        < 45 => ThermalStatus.Normal,
        < 70 => ThermalStatus.Warm,
        < 85 => ThermalStatus.Warning,
        _ => ThermalStatus.Critical
    };

    public string Trend { get; set; } = "→";
    public bool IsThrottled { get; set; }
    /// <summary>Throttle percentage: 0 = full speed, 100 = fully throttled.</summary>
    public double ThrottlePercentage { get; set; }
    /// <summary>Thermal throttle onset temperature (°C).</summary>
    public double ThermalLimitC { get; set; }
    /// <summary>Max junction / critical temperature (°C).</summary>
    public double TjMaxC { get; set; }
}

public enum ThermalStatus
{
    Normal,
    Warm,
    Warning,
    Critical
}
