using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using ThermalDoctor.Models;

namespace ThermalDoctor.Services;

public class WmiThermalProvider : IDisposable
{
    private bool _disposed;

    // Qualcomm Snapdragon ACPI device IDs found on ARM-based Surface devices
    private static readonly Dictionary<string, string> QcomAcpiMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "QCOM0C58", "CPU" },
        { "QCOM0C59", "CPU" },
        { "QCOM0C5A", "CPU" },
        { "QCOM0D01", "SoC" },
        { "QCOM0C91", "NPU" },
        { "QCOM0CBF", "GPU" },
        { "QCOM0CF2", "CPU" },
        { "QCOM0CF3", "CPU" },
        { "QCOM0CF7", "GPU" },
        { "QCOM0CF8", "NPU" },
        { "QCOM0CF9", "Modem" },
        { "QCOM0CFC", "WiFi" },
        { "QCOM0C5E", "Charger IC" },
        { "QCOM0C5F", "Charger IC" },
    };

    // Surface SAM thermal zone sensor sub-indices (MSHW0188\N)
    // Mapping based on observed Surface Laptop 7 data
    private static readonly Dictionary<string, string> SurfaceSamZoneMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "1", "Skin (Left)" },
        { "2", "SSD" },
        { "3", "Battery" },
        { "4", "Charger IC" },
        { "5", "Skin (Bottom)" },
        { "9", "Display" },
        { "A", "Skin (Right)" },
        { "B", "Skin (Back)" },
        { "C", "WiFi" },
    };

    public List<ThermalReading> QueryThermalZones()
    {
        var readings = new List<ThermalReading>();

        // Primary: MSAcpi_ThermalZoneTemperature (requires admin on some systems)
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WMI",
                "SELECT * FROM MSAcpi_ThermalZoneTemperature");

            foreach (ManagementObject obj in searcher.Get())
            {
                var instanceName = obj["InstanceName"]?.ToString() ?? "Unknown";
                var currentTemp = Convert.ToDouble(obj["CurrentTemperature"]);
                // WMI returns temperature in tenths of Kelvin
                var tempCelsius = (currentTemp - 2732.0) / 10.0;

                // Skip invalid/inactive sensors
                if (tempCelsius < -40)
                    continue;

                var componentName = MapZoneToComponent(instanceName);
                if (componentName == null)
                    continue; // Unknown or non-sensor zone

                readings.Add(new ThermalReading
                {
                    ComponentName = componentName,
                    ZoneId = instanceName,
                    TemperatureCelsius = Math.Round(tempCelsius, 1),
                    Timestamp = DateTime.Now
                });
            }
        }
        catch (ManagementException ex)
        {
            System.Diagnostics.Debug.WriteLine($"MSAcpi_ThermalZoneTemperature query failed: {ex.Message}");
        }

        // Deduplicate: keep highest reading per component
        readings = readings
            .GroupBy(r => r.ComponentName)
            .Select(g => g.OrderByDescending(r => r.TemperatureCelsius).First())
            .ToList();

        // Fallback: Win32_TemperatureProbe (sometimes available)
        if (readings.Count == 0)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_TemperatureProbe");

                foreach (ManagementObject obj in searcher.Get())
                {
                    var name = obj["Name"]?.ToString() ?? "Sensor";
                    var currentReading = obj["CurrentReading"];
                    if (currentReading != null)
                    {
                        readings.Add(new ThermalReading
                        {
                            ComponentName = name,
                            ZoneId = obj["DeviceID"]?.ToString() ?? "Unknown",
                            TemperatureCelsius = Convert.ToDouble(currentReading),
                            Timestamp = DateTime.Now
                        });
                    }
                }
            }
            catch (ManagementException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Win32_TemperatureProbe query failed: {ex.Message}");
            }
        }

        return readings;
    }

    private static string? MapZoneToComponent(string instanceName)
    {
        var upper = instanceName.ToUpperInvariant();

        // 1. Qualcomm ACPI device IDs: ACPI\QCOM0C58\0_0
        var qcomMatch = Regex.Match(instanceName, @"QCOM[0-9A-Fa-f]{4}", RegexOptions.IgnoreCase);
        if (qcomMatch.Success && QcomAcpiMap.TryGetValue(qcomMatch.Value, out var qcomName))
            return qcomName;

        // 2. Surface SAM thermal zones: ACPI\MSHW0188\N_0
        var samMatch = Regex.Match(instanceName, @"MSHW0188\\([0-9A-Fa-f]+)_", RegexOptions.IgnoreCase);
        if (samMatch.Success && SurfaceSamZoneMap.TryGetValue(samMatch.Groups[1].Value, out var samName))
            return samName;

        // 3. Surface thermal policy driver (not a sensor — skip it)
        if (upper.Contains("MSHW0187"))
            return null;

        // 4. Generic keyword matching for Intel/AMD systems
        if (upper.Contains("CPU") || upper.Contains("TCPU") || upper.Contains("PROC"))
            return "CPU";
        if (upper.Contains("GPU") || upper.Contains("TGPU") || upper.Contains("GFX"))
            return "GPU";
        if (upper.Contains("SSD") || upper.Contains("DISK") || upper.Contains("NVME") || upper.Contains("STOR"))
            return "SSD";
        if (upper.Contains("BATT") || upper.Contains("BAT0") || upper.Contains("BAT1"))
            return "Battery";
        if (upper.Contains("CHAR") || upper.Contains("CHG"))
            return "Charger IC";
        if (upper.Contains("WIFI") || upper.Contains("WLAN"))
            return "WiFi";
        if (upper.Contains("DISPLAY") || upper.Contains("LCD") || upper.Contains("PANEL"))
            return "Display";
        if (upper.Contains("RAM") || upper.Contains("MEM") || upper.Contains("DIMM"))
            return "RAM";
        if (upper.Contains("PSU") || upper.Contains("POWER_SUPPLY"))
            return "PSU";
        if (upper.Contains("SKIN") || upper.Contains("TSKIN") || upper.Contains("CHAS"))
        {
            if (upper.Contains("BACK") || upper.Contains("REAR"))
                return "Skin (Back)";
            return "Skin (Front)";
        }

        // 5. Thermal zone index fallback (Intel: TZ00, TZ01, ...)
        var tzMatch = Regex.Match(upper, @"(?:TZ|THM|ZONE)\.?0*(\d+)");
        if (tzMatch.Success)
        {
            return int.Parse(tzMatch.Groups[1].Value) switch
            {
                0 => "CPU",
                1 => "GPU",
                2 => "SSD",
                3 => "Battery",
                4 => "Skin (Front)",
                5 => "Skin (Back)",
                6 => "Charger IC",
                7 => "WiFi",
                _ => null  // Unknown zone index — skip
            };
        }

        return null;  // Unknown zone — skip it
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
