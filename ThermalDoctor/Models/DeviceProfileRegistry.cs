using System.Collections.Generic;
using System.Windows;

namespace ThermalDoctor.Models;

public static class DeviceProfileRegistry
{
    private static readonly Dictionary<string, SurfaceDeviceProfile> _profiles = new();

    static DeviceProfileRegistry()
    {
        RegisterDefaults();
    }

    public static SurfaceDeviceProfile GetProfile(string modelName)
    {
        // Try exact match first
        if (_profiles.TryGetValue(modelName, out var profile))
            return profile;

        // Try partial match, preferring the longest (most specific) key
        SurfaceDeviceProfile? bestMatch = null;
        int bestMatchLength = 0;
        foreach (var kvp in _profiles)
        {
            if (modelName.Contains(kvp.Key, System.StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.Contains(modelName, System.StringComparison.OrdinalIgnoreCase))
            {
                if (kvp.Key.Length > bestMatchLength)
                {
                    bestMatch = kvp.Value;
                    bestMatchLength = kvp.Key.Length;
                }
            }
        }

        return bestMatch ?? _profiles["Generic Surface"];
    }

    public static IReadOnlyDictionary<string, SurfaceDeviceProfile> AllProfiles => _profiles;

    private static void RegisterDefaults()
    {
        // ───────────────────────────────────────────────────────────
        //  Generic Surface (tablet form factor) — 3D isometric
        // ───────────────────────────────────────────────────────────
        _profiles["Generic Surface"] = new SurfaceDeviceProfile
        {
            ModelName = "Generic Surface",
            DisplayName = "Surface Device",
            FormFactor = DeviceFormFactor.Tablet,
            DeviceWidth = 460,
            DeviceHeight = 360,
            OutlinePathData = "M 60,60 L 400,30 L 430,290 L 90,320 Z",
            ScreenPathData = "M 75,72 L 392,44 L 420,282 L 103,310 Z",
            FrontEdgePathData = "M 90,320 L 430,290 L 434,302 L 94,332 Z",
            RightEdgePathData = "M 400,30 L 404,42 L 434,302 L 430,290 Z",
            Components = new()
            {
                new() { ComponentName = "CPU", Position = new Point(240, 110), Radius = 24, Label = "CPU", ThermalLimitC = 90, TjMaxC = 100 },
                new() { ComponentName = "GPU", Position = new Point(244, 155), Radius = 19, Label = "GPU", ThermalLimitC = 85, TjMaxC = 95 },
                new() { ComponentName = "SSD", Position = new Point(165, 210), Radius = 16, Label = "SSD", ThermalLimitC = 70, TjMaxC = 85 },
                new() { ComponentName = "Battery", Position = new Point(300, 230), Radius = 22, Label = "BAT", ThermalLimitC = 45, TjMaxC = 60 },
                new() { ComponentName = "Skin (Front)", Position = new Point(160, 120), Radius = 14, Label = "SKIN-F", ThermalLimitC = 42, TjMaxC = 50 },
                new() { ComponentName = "Skin (Back)", Position = new Point(330, 100), Radius = 14, Label = "SKIN-B", ThermalLimitC = 42, TjMaxC = 50 },
            }
        };

        // ───────────────────────────────────────────────────────────
        //  Surface Pro (generic: 7, 8, 9, 10, 11) — 3D isometric
        // ───────────────────────────────────────────────────────────
        _profiles["Surface Pro"] = new SurfaceDeviceProfile
        {
            ModelName = "Surface Pro",
            DisplayName = "Surface Pro",
            FormFactor = DeviceFormFactor.Tablet,
            DeviceWidth = 480,
            DeviceHeight = 380,
            OutlinePathData = "M 55,65 L 420,30 L 452,305 L 87,340 Z",
            ScreenPathData = "M 70,77 L 412,44 L 442,296 L 100,329 Z",
            FrontEdgePathData = "M 87,340 L 452,305 L 456,317 L 91,352 Z",
            RightEdgePathData = "M 420,30 L 424,42 L 456,317 L 452,305 Z",
            Components = new()
            {
                new() { ComponentName = "CPU", Position = new Point(245, 105), Radius = 25, Label = "CPU", ThermalLimitC = 90, TjMaxC = 100 },
                new() { ComponentName = "GPU", Position = new Point(249, 150), Radius = 20, Label = "GPU", ThermalLimitC = 85, TjMaxC = 95 },
                new() { ComponentName = "SSD", Position = new Point(165, 210), Radius = 17, Label = "SSD", ThermalLimitC = 70, TjMaxC = 85 },
                new() { ComponentName = "Battery", Position = new Point(310, 240), Radius = 24, Label = "BAT", ThermalLimitC = 45, TjMaxC = 60 },
                new() { ComponentName = "Skin (Front)", Position = new Point(170, 120), Radius = 14, Label = "SKIN-F", ThermalLimitC = 42, TjMaxC = 50 },
                new() { ComponentName = "Skin (Back)", Position = new Point(345, 95), Radius = 14, Label = "SKIN-B", ThermalLimitC = 42, TjMaxC = 50 },
                new() { ComponentName = "Charger IC", Position = new Point(155, 165), Radius = 13, Label = "CHG", ThermalLimitC = 85, TjMaxC = 105 },
                new() { ComponentName = "WiFi", Position = new Point(355, 75), Radius = 13, Label = "WiFi", ThermalLimitC = 80, TjMaxC = 105 },
            }
        };

        // ───────────────────────────────────────────────────────────
        //  Surface Pro 11 (Snapdragon X) — 3D isometric tablet
        //  ARM variant with Qualcomm ACPI + SAM thermal zones
        // ───────────────────────────────────────────────────────────
        _profiles["Surface Pro, 11th Edition"] = new SurfaceDeviceProfile
        {
            ModelName = "Surface Pro, 11th Edition",
            DisplayName = "Surface Pro 11",
            FormFactor = DeviceFormFactor.Tablet,
            DeviceWidth = 480,
            DeviceHeight = 380,
            OutlinePathData = "M 55,65 L 420,30 L 452,305 L 87,340 Z",
            ScreenPathData = "M 70,77 L 412,44 L 442,296 L 100,329 Z",
            FrontEdgePathData = "M 87,340 L 452,305 L 456,317 L 91,352 Z",
            RightEdgePathData = "M 420,30 L 424,42 L 456,317 L 452,305 Z",
            Components = new()
            {
                new() { ComponentName = "CPU", Position = new Point(245, 105), Radius = 25, Label = "CPU", ThermalLimitC = 80, TjMaxC = 105 },
                new() { ComponentName = "GPU", Position = new Point(249, 155), Radius = 18, Label = "GPU", ThermalLimitC = 80, TjMaxC = 105 },
                new() { ComponentName = "SoC", Position = new Point(310, 115), Radius = 16, Label = "SoC", ThermalLimitC = 80, TjMaxC = 105 },
                new() { ComponentName = "NPU", Position = new Point(180, 120), Radius = 15, Label = "NPU", ThermalLimitC = 80, TjMaxC = 100 },
                new() { ComponentName = "SSD", Position = new Point(165, 210), Radius = 17, Label = "SSD", ThermalLimitC = 70, TjMaxC = 85 },
                new() { ComponentName = "Battery", Position = new Point(310, 240), Radius = 24, Label = "BAT", ThermalLimitC = 45, TjMaxC = 60 },
                new() { ComponentName = "WiFi", Position = new Point(355, 75), Radius = 13, Label = "WiFi", ThermalLimitC = 80, TjMaxC = 105 },
                new() { ComponentName = "Charger IC", Position = new Point(155, 170), Radius = 13, Label = "CHG", ThermalLimitC = 85, TjMaxC = 105 },
                new() { ComponentName = "Display", Position = new Point(245, 60), Radius = 16, Label = "LCD", ThermalLimitC = 50, TjMaxC = 65 },
                new() { ComponentName = "Skin (Left)", Position = new Point(115, 170), Radius = 12, Label = "SKN-L", ThermalLimitC = 42, TjMaxC = 50 },
                new() { ComponentName = "Skin (Right)", Position = new Point(390, 165), Radius = 12, Label = "SKN-R", ThermalLimitC = 42, TjMaxC = 50 },
                new() { ComponentName = "Skin (Back)", Position = new Point(245, 270), Radius = 12, Label = "SKN-B", ThermalLimitC = 42, TjMaxC = 50 },
                new() { ComponentName = "Skin (Bottom)", Position = new Point(245, 310), Radius = 12, Label = "SKN-D", ThermalLimitC = 42, TjMaxC = 50 },
            }
        };

        // ───────────────────────────────────────────────────────────
        //  Surface Laptop 7 (Snapdragon X) — 3D isometric clamshell
        //  Component names match WmiThermalProvider's QCOM/SAM mapping
        // ───────────────────────────────────────────────────────────
        _profiles["Surface Laptop, 7th Edition"] = new SurfaceDeviceProfile
        {
            ModelName = "Surface Laptop, 7th Edition",
            DisplayName = "Surface Laptop 7",
            FormFactor = DeviceFormFactor.Laptop,
            DeviceWidth = 520,
            DeviceHeight = 420,
            // Lid (top half)
            OutlinePathData = "M 50,25 L 450,10 L 465,195 L 65,210 Z",
            ScreenPathData = "M 65,37 L 440,23 L 454,188 L 79,202 Z",
            // Keyboard deck (bottom half)  built into front/right edges
            FrontEdgePathData = "M 35,220 L 475,205 L 480,390 L 40,405 Z",
            RightEdgePathData = "M 450,10 L 458,16 L 488,396 L 480,390 L 475,205 L 465,195 Z",
            Components = new()
            {
                // Qualcomm Snapdragon X sensors (mapped by WmiThermalProvider)
                new() { ComponentName = "CPU", Position = new Point(260, 290), Radius = 26, Label = "CPU", ThermalLimitC = 80, TjMaxC = 105 },
                new() { ComponentName = "GPU", Position = new Point(345, 280), Radius = 18, Label = "GPU", ThermalLimitC = 80, TjMaxC = 105 },
                new() { ComponentName = "SoC", Position = new Point(260, 330), Radius = 16, Label = "SoC", ThermalLimitC = 80, TjMaxC = 105 },
                new() { ComponentName = "NPU", Position = new Point(175, 310), Radius = 15, Label = "NPU", ThermalLimitC = 80, TjMaxC = 100 },
                new() { ComponentName = "SSD", Position = new Point(365, 340), Radius = 17, Label = "SSD", ThermalLimitC = 70, TjMaxC = 85 },
                new() { ComponentName = "Battery", Position = new Point(150, 355), Radius = 24, Label = "BAT", ThermalLimitC = 45, TjMaxC = 60 },
                new() { ComponentName = "WiFi", Position = new Point(400, 240), Radius = 13, Label = "WiFi", ThermalLimitC = 80, TjMaxC = 105 },
                new() { ComponentName = "Charger IC", Position = new Point(115, 280), Radius = 13, Label = "CHG", ThermalLimitC = 85, TjMaxC = 105 },
                // Surface SAM thermal zone sensors
                new() { ComponentName = "Display", Position = new Point(260, 115), Radius = 20, Label = "LCD", ThermalLimitC = 50, TjMaxC = 65 },
                new() { ComponentName = "Skin (Left)", Position = new Point(100, 310), Radius = 14, Label = "SKN-L", ThermalLimitC = 42, TjMaxC = 50 },
                new() { ComponentName = "Skin (Right)", Position = new Point(420, 310), Radius = 14, Label = "SKN-R", ThermalLimitC = 42, TjMaxC = 50 },
                new() { ComponentName = "Skin (Back)", Position = new Point(260, 240), Radius = 14, Label = "SKN-B", ThermalLimitC = 42, TjMaxC = 50 },
                new() { ComponentName = "Skin (Bottom)", Position = new Point(260, 380), Radius = 14, Label = "SKN-D", ThermalLimitC = 42, TjMaxC = 50 },
            }
        };

        // ───────────────────────────────────────────────────────────
        //  Surface Laptop (generic: 3, 4, 5, 6) — 3D isometric
        // ───────────────────────────────────────────────────────────
        _profiles["Surface Laptop"] = new SurfaceDeviceProfile
        {
            ModelName = "Surface Laptop",
            DisplayName = "Surface Laptop",
            FormFactor = DeviceFormFactor.Laptop,
            DeviceWidth = 520,
            DeviceHeight = 420,
            OutlinePathData = "M 50,25 L 450,10 L 465,195 L 65,210 Z",
            ScreenPathData = "M 65,37 L 440,23 L 454,188 L 79,202 Z",
            FrontEdgePathData = "M 35,220 L 475,205 L 480,390 L 40,405 Z",
            RightEdgePathData = "M 450,10 L 458,16 L 488,396 L 480,390 L 475,205 L 465,195 Z",
            Components = new()
            {
                new() { ComponentName = "CPU", Position = new Point(260, 285), Radius = 26, Label = "CPU", ThermalLimitC = 90, TjMaxC = 100 },
                new() { ComponentName = "GPU", Position = new Point(340, 275), Radius = 18, Label = "GPU", ThermalLimitC = 85, TjMaxC = 95 },
                new() { ComponentName = "SSD", Position = new Point(170, 340), Radius = 16, Label = "SSD", ThermalLimitC = 70, TjMaxC = 85 },
                new() { ComponentName = "Battery", Position = new Point(350, 345), Radius = 24, Label = "BAT", ThermalLimitC = 45, TjMaxC = 60 },
                new() { ComponentName = "Display", Position = new Point(260, 115), Radius = 18, Label = "LCD", ThermalLimitC = 50, TjMaxC = 65 },
                new() { ComponentName = "Skin (Front)", Position = new Point(260, 370), Radius = 14, Label = "SKIN-F", ThermalLimitC = 42, TjMaxC = 50 },
                new() { ComponentName = "Skin (Back)", Position = new Point(260, 240), Radius = 14, Label = "SKIN-B", ThermalLimitC = 42, TjMaxC = 50 },
                new() { ComponentName = "Charger IC", Position = new Point(110, 290), Radius = 13, Label = "CHG", ThermalLimitC = 85, TjMaxC = 105 },
                new() { ComponentName = "WiFi", Position = new Point(400, 240), Radius = 13, Label = "WiFi", ThermalLimitC = 80, TjMaxC = 105 },
            }
        };

        // ───────────────────────────────────────────────────────────
        //  Surface Go — 3D isometric tablet
        // ───────────────────────────────────────────────────────────
        _profiles["Surface Go"] = new SurfaceDeviceProfile
        {
            ModelName = "Surface Go",
            DisplayName = "Surface Go",
            FormFactor = DeviceFormFactor.Tablet,
            DeviceWidth = 420,
            DeviceHeight = 320,
            OutlinePathData = "M 65,55 L 360,30 L 385,255 L 90,280 Z",
            ScreenPathData = "M 78,65 L 352,42 L 376,248 L 102,271 Z",
            FrontEdgePathData = "M 90,280 L 385,255 L 389,267 L 94,292 Z",
            RightEdgePathData = "M 360,30 L 364,42 L 389,267 L 385,255 Z",
            Components = new()
            {
                new() { ComponentName = "CPU", Position = new Point(220, 105), Radius = 22, Label = "CPU", ThermalLimitC = 90, TjMaxC = 100 },
                new() { ComponentName = "SSD", Position = new Point(155, 185), Radius = 15, Label = "SSD", ThermalLimitC = 70, TjMaxC = 85 },
                new() { ComponentName = "Battery", Position = new Point(270, 195), Radius = 20, Label = "BAT", ThermalLimitC = 45, TjMaxC = 60 },
                new() { ComponentName = "Skin (Front)", Position = new Point(220, 165), Radius = 13, Label = "SKIN", ThermalLimitC = 42, TjMaxC = 50 },
                new() { ComponentName = "WiFi", Position = new Point(310, 80), Radius = 12, Label = "WiFi", ThermalLimitC = 80, TjMaxC = 105 },
            }
        };

        // ───────────────────────────────────────────────────────────
        //  Surface Book — 3D isometric detachable
        // ───────────────────────────────────────────────────────────
        _profiles["Surface Book"] = new SurfaceDeviceProfile
        {
            ModelName = "Surface Book",
            DisplayName = "Surface Book",
            FormFactor = DeviceFormFactor.Laptop,
            DeviceWidth = 520,
            DeviceHeight = 440,
            // Clipboard/lid
            OutlinePathData = "M 50,20 L 450,5 L 465,190 L 65,205 Z",
            ScreenPathData = "M 65,32 L 440,18 L 454,183 L 79,197 Z",
            // Base with dGPU
            FrontEdgePathData = "M 35,220 L 475,205 L 480,400 L 40,415 Z",
            RightEdgePathData = "M 450,5 L 458,11 L 488,406 L 480,400 L 475,205 L 465,190 Z",
            Components = new()
            {
                new() { ComponentName = "CPU", Position = new Point(260, 110), Radius = 24, Label = "CPU", ThermalLimitC = 90, TjMaxC = 100 },
                new() { ComponentName = "GPU", Position = new Point(260, 300), Radius = 24, Label = "dGPU", ThermalLimitC = 85, TjMaxC = 95 },
                new() { ComponentName = "SSD", Position = new Point(175, 320), Radius = 15, Label = "SSD", ThermalLimitC = 70, TjMaxC = 85 },
                new() { ComponentName = "Battery", Position = new Point(260, 350), Radius = 22, Label = "BAT", ThermalLimitC = 45, TjMaxC = 60 },
                new() { ComponentName = "Charger IC", Position = new Point(350, 120), Radius = 13, Label = "CHG", ThermalLimitC = 85, TjMaxC = 105 },
                new() { ComponentName = "Skin (Front)", Position = new Point(260, 380), Radius = 14, Label = "SKIN-F", ThermalLimitC = 42, TjMaxC = 50 },
                new() { ComponentName = "WiFi", Position = new Point(400, 240), Radius = 13, Label = "WiFi", ThermalLimitC = 80, TjMaxC = 105 },
            }
        };

        // ───────────────────────────────────────────────────────────
        //  Surface Studio — 3D isometric AIO desktop
        // ───────────────────────────────────────────────────────────
        _profiles["Surface Studio"] = new SurfaceDeviceProfile
        {
            ModelName = "Surface Studio",
            DisplayName = "Surface Studio",
            FormFactor = DeviceFormFactor.Studio,
            DeviceWidth = 460,
            DeviceHeight = 420,
            // Large tilted display
            OutlinePathData = "M 40,15 L 420,5 L 435,260 L 55,270 Z",
            ScreenPathData = "M 55,27 L 410,18 L 424,253 L 69,262 Z",
            // Stand + base
            FrontEdgePathData = "M 190,280 L 270,278 L 275,340 L 185,342 Z",
            RightEdgePathData = "M 160,350 L 310,346 L 315,380 L 155,384 Z",
            Components = new()
            {
                new() { ComponentName = "CPU", Position = new Point(230, 140), Radius = 26, Label = "CPU", ThermalLimitC = 90, TjMaxC = 100 },
                new() { ComponentName = "GPU", Position = new Point(310, 170), Radius = 24, Label = "GPU", ThermalLimitC = 85, TjMaxC = 95 },
                new() { ComponentName = "SSD", Position = new Point(145, 180), Radius = 16, Label = "SSD", ThermalLimitC = 70, TjMaxC = 85 },
                new() { ComponentName = "Display", Position = new Point(230, 70), Radius = 18, Label = "LCD", ThermalLimitC = 50, TjMaxC = 65 },
                new() { ComponentName = "RAM", Position = new Point(320, 120), Radius = 14, Label = "RAM", ThermalLimitC = 85, TjMaxC = 95 },
                new() { ComponentName = "PSU", Position = new Point(230, 370), Radius = 16, Label = "PSU", ThermalLimitC = 80, TjMaxC = 100 },
                new() { ComponentName = "Skin (Front)", Position = new Point(145, 100), Radius = 14, Label = "SKIN", ThermalLimitC = 42, TjMaxC = 50 },
                new() { ComponentName = "WiFi", Position = new Point(360, 50), Radius = 12, Label = "WiFi", ThermalLimitC = 80, TjMaxC = 105 },
            }
        };
    }
}
