using System;
using System.Collections.Generic;
using System.Management;

namespace ThermalDoctor.Services;

public class SurfaceModelDetector
{
    public string DetectModel()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Model FROM Win32_ComputerSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                var model = obj["Model"]?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(model))
                    return model;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WMI model detection failed: {ex.Message}");
        }

        return "Unknown";
    }

    public string DetectManufacturer()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Manufacturer FROM Win32_ComputerSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                return obj["Manufacturer"]?.ToString() ?? "Unknown";
            }
        }
        catch
        {
            // Ignore
        }
        return "Unknown";
    }

    public bool IsSurfaceDevice()
    {
        var manufacturer = DetectManufacturer();
        var model = DetectModel();
        return manufacturer.Contains("Microsoft", StringComparison.OrdinalIgnoreCase)
               && model.Contains("Surface", StringComparison.OrdinalIgnoreCase);
    }

    public Dictionary<string, string> GetSystemInfo()
    {
        var info = new Dictionary<string, string>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Model, Manufacturer, SystemFamily, SystemSKUNumber FROM Win32_ComputerSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                info["Model"] = obj["Model"]?.ToString() ?? "Unknown";
                info["Manufacturer"] = obj["Manufacturer"]?.ToString() ?? "Unknown";
                info["SystemFamily"] = obj["SystemFamily"]?.ToString() ?? "Unknown";
                info["SKU"] = obj["SystemSKUNumber"]?.ToString() ?? "Unknown";
            }
        }
        catch (Exception ex)
        {
            info["Error"] = ex.Message;
        }
        return info;
    }
}
