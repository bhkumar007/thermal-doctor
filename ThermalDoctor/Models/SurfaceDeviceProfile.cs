using System.Collections.Generic;
using System.Windows;

namespace ThermalDoctor.Models;

public enum DeviceFormFactor
{
    Tablet,
    Laptop,
    Studio
}

public class SurfaceDeviceProfile
{
    public string ModelName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DeviceFormFactor FormFactor { get; set; } = DeviceFormFactor.Tablet;
    public double DeviceWidth { get; set; }
    public double DeviceHeight { get; set; }
    public string OutlinePathData { get; set; } = string.Empty;
    public string ScreenPathData { get; set; } = string.Empty;
    public string FrontEdgePathData { get; set; } = string.Empty;
    public string RightEdgePathData { get; set; } = string.Empty;
    public List<ComponentLocation> Components { get; set; } = new();
}

public class ComponentLocation
{
    public string ComponentName { get; set; } = string.Empty;
    public string ZoneId { get; set; } = string.Empty;
    public Point Position { get; set; }
    public double Radius { get; set; } = 20;
    public string Label { get; set; } = string.Empty;
    /// <summary>Temperature at which thermal throttling begins (°C).</summary>
    public double ThermalLimitC { get; set; }
    /// <summary>Maximum junction / critical temperature (°C). Shutdown threshold.</summary>
    public double TjMaxC { get; set; }
}
