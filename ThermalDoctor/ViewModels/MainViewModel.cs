using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using ThermalDoctor.Helpers;
using ThermalDoctor.Models;
using ThermalDoctor.Services;

namespace ThermalDoctor.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ThermalDataAggregator _aggregator;
    private SurfaceDeviceProfile _deviceProfile;
    private bool _disposed;

    [ObservableProperty] private string _deviceModelName = "Detecting...";
    [ObservableProperty] private string _statusMessage = "Initializing...";
    [ObservableProperty] private bool _isAdmin;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _deviceOutlinePath = string.Empty;
    [ObservableProperty] private string _deviceScreenPath = string.Empty;
    [ObservableProperty] private string _frontEdgePathData = string.Empty;
    [ObservableProperty] private string _rightEdgePathData = string.Empty;
    [ObservableProperty] private double _deviceWidth = 300;
    [ObservableProperty] private double _deviceHeight = 200;
    [ObservableProperty] private int _pollingIntervalSeconds = 2;
    [ObservableProperty] private double _maxTemperature;
    [ObservableProperty] private double _avgTemperature;
    [ObservableProperty] private string _lastUpdated = "Never";
    [ObservableProperty] private bool _isDarkTheme = true;
    [ObservableProperty] private bool _useFahrenheit;
    [ObservableProperty] private DeviceFormFactor _currentFormFactor = DeviceFormFactor.Tablet;

    public ObservableCollection<ThermalReadingViewModel> Readings { get; } = new();
    public ObservableCollection<string> DiagnosticLog { get; } = new();
    public ObservableCollection<string> AvailableProfiles { get; } = new();

    // Chart data
    private readonly Dictionary<string, ObservableCollection<ObservablePoint>> _chartPoints = new();
    private readonly Dictionary<string, ObservableCollection<ObservablePoint>> _throttleChartPoints = new();
    private int _chartSampleIndex;
    private const int MaxChartPoints = 120;

    public ObservableCollection<ISeries> ChartSeries { get; } = new();
    public ObservableCollection<ISeries> ThrottleChartSeries { get; } = new();

    [ObservableProperty] private SolidColorPaint _legendTextPaint = new(new SKColor(136, 153, 170)) { };

    public Axis[] ChartXAxes { get; } = new[]
    {
        new Axis
        {
            Name = "Time",
            NamePaint = new SolidColorPaint(SKColors.Gray),
            LabelsPaint = new SolidColorPaint(SKColors.Gray),
            TextSize = 10,
            SeparatorsPaint = new SolidColorPaint(new SKColor(60, 60, 90)) { StrokeThickness = 0.5f },
            Labeler = value => "",
            MinLimit = 0
        }
    };

    public Axis[] ChartYAxes { get; } = new[]
    {
        new Axis
        {
            Name = "°C",
            NamePaint = new SolidColorPaint(SKColors.Gray),
            LabelsPaint = new SolidColorPaint(SKColors.Gray),
            TextSize = 10,
            SeparatorsPaint = new SolidColorPaint(new SKColor(60, 60, 90)) { StrokeThickness = 0.5f },
            MinLimit = 20
        }
    };

    public Axis[] ThrottleChartXAxes { get; } = new[]
    {
        new Axis
        {
            Name = "Time",
            NamePaint = new SolidColorPaint(SKColors.Gray),
            LabelsPaint = new SolidColorPaint(SKColors.Gray),
            TextSize = 10,
            SeparatorsPaint = new SolidColorPaint(new SKColor(60, 60, 90)) { StrokeThickness = 0.5f },
            Labeler = value => "",
            MinLimit = 0
        }
    };

    public Axis[] ThrottleChartYAxes { get; } = new[]
    {
        new Axis
        {
            Name = "Throttle %",
            NamePaint = new SolidColorPaint(SKColors.Gray),
            LabelsPaint = new SolidColorPaint(SKColors.Gray),
            TextSize = 10,
            SeparatorsPaint = new SolidColorPaint(new SKColor(60, 60, 90)) { StrokeThickness = 0.5f },
            MinLimit = 0,
            MaxLimit = 100
        }
    };

    [ObservableProperty] private string _selectedProfileName = string.Empty;

    public MainViewModel()
    {
        _aggregator = new ThermalDataAggregator();
        _deviceProfile = DeviceProfileRegistry.GetProfile("Generic Surface");
        IsAdmin = AdminElevationHelper.IsRunningAsAdmin();

        _aggregator.ReadingsUpdated += OnReadingsUpdated;
        _aggregator.DiagnosticMessage += OnDiagnosticMessage;

        // Populate available profiles
        foreach (var profile in DeviceProfileRegistry.AllProfiles)
        {
            AvailableProfiles.Add(profile.Key);
        }
    }

    public void Initialize()
    {
        StatusMessage = "Starting thermal monitoring...";
        _aggregator.Initialize();
        DeviceModelName = _aggregator.DetectedModel;

        // Select the best matching profile
        _deviceProfile = DeviceProfileRegistry.GetProfile(DeviceModelName);
        SelectedProfileName = _deviceProfile.ModelName;
        ApplyDeviceProfile();

        IsRunning = true;
        StatusMessage = IsAdmin
            ? "Running with ETW + WMI (Administrator)"
            : "Running with WMI polling (run as Admin for ETW)";
    }

    partial void OnSelectedProfileNameChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _deviceProfile = DeviceProfileRegistry.GetProfile(value);
            ApplyDeviceProfile();
        }
    }

    partial void OnPollingIntervalSecondsChanged(int value)
    {
        _aggregator.SetPollingInterval(value * 1000);
    }

    private void ApplyDeviceProfile()
    {
        DeviceOutlinePath = _deviceProfile.OutlinePathData;
        DeviceScreenPath = _deviceProfile.ScreenPathData;
        FrontEdgePathData = _deviceProfile.FrontEdgePathData;
        RightEdgePathData = _deviceProfile.RightEdgePathData;
        DeviceWidth = _deviceProfile.DeviceWidth;
        DeviceHeight = _deviceProfile.DeviceHeight;
        CurrentFormFactor = _deviceProfile.FormFactor;

        // Push thermal limits to the aggregator so it can calculate throttle %
        _aggregator.ThermalLimits.Clear();
        foreach (var comp in _deviceProfile.Components)
        {
            if (comp.ThermalLimitC > 0 && comp.TjMaxC > 0)
                _aggregator.ThermalLimits[comp.ComponentName] = (comp.ThermalLimitC, comp.TjMaxC);
        }

        // Build set of component names in the new profile
        var profileComponentNames = new HashSet<string>(
            _deviceProfile.Components.Select(c => c.ComponentName),
            StringComparer.OrdinalIgnoreCase);

        // Remove readings that aren't in the new profile and have no live sensor data
        for (int i = Readings.Count - 1; i >= 0; i--)
        {
            if (!profileComponentNames.Contains(Readings[i].ComponentName))
                Readings.RemoveAt(i);
        }

        // Pre-populate readings from profile component layout
        foreach (var comp in _deviceProfile.Components)
        {
            var existing = Readings.FirstOrDefault(r => r.ComponentName == comp.ComponentName);
            if (existing == null)
            {
                Readings.Add(new ThermalReadingViewModel
                {
                    ComponentName = comp.ComponentName,
                    ZoneId = comp.ZoneId,
                    Position = comp.Position,
                    Radius = comp.Radius,
                    Label = comp.Label,
                    TemperatureCelsius = 0,
                    UseFahrenheit = UseFahrenheit,
                    ThermalLimitC = comp.ThermalLimitC,
                    TjMaxC = comp.TjMaxC
                });
            }
            else
            {
                existing.Position = comp.Position;
                existing.Radius = comp.Radius;
                existing.Label = comp.Label;
                existing.ThermalLimitC = comp.ThermalLimitC;
                existing.TjMaxC = comp.TjMaxC;
            }
        }
    }

    private void OnReadingsUpdated()
    {
        var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Build set of profile component names to filter against
        var profileComponents = new HashSet<string>(
            _deviceProfile.Components.Select(c => c.ComponentName),
            StringComparer.OrdinalIgnoreCase);

        foreach (var reading in _aggregator.CurrentReadings)
        {
            // Match by component name
            var vm = Readings.FirstOrDefault(r =>
                string.Equals(r.ComponentName, reading.ComponentName, StringComparison.OrdinalIgnoreCase)
                && !matched.Contains(r.ComponentName));

            if (vm != null)
            {
                vm.UpdateFrom(reading);
                matched.Add(vm.ComponentName);
            }
            // Only add sensors that are in the current profile — ignore unmapped zones
        }

        // Update summary stats
        var temps = Readings.Where(r => r.TemperatureCelsius > 0).ToList();
        if (temps.Count > 0)
        {
            MaxTemperature = Math.Round(temps.Max(r => r.TemperatureCelsius), 1);
            AvgTemperature = Math.Round(temps.Average(r => r.TemperatureCelsius), 1);
        }
        OnPropertyChanged(nameof(MaxTemperatureDisplay));
        OnPropertyChanged(nameof(AvgTemperatureDisplay));
        LastUpdated = DateTime.Now.ToString("HH:mm:ss");

        // Update chart
        UpdateChart();
        UpdateThrottleChart();
    }

    private static readonly SKColor[] SeriesColors =
    {
        new(231, 76, 60),    // red — CPU
        new(46, 204, 113),   // green — GPU
        new(52, 152, 219),   // blue — SoC
        new(241, 196, 15),   // yellow — NPU
        new(155, 89, 182),   // purple — SSD
        new(230, 126, 34),   // orange — Battery
        new(26, 188, 156),   // teal — WiFi
        new(236, 240, 241),  // light — Charger
        new(22, 160, 133),   // dark teal — Display
        new(192, 57, 43),    // dark red
        new(41, 128, 185),   // dark blue
        new(142, 68, 173),   // dark purple
        new(211, 84, 0),     // dark orange
        new(44, 62, 80),     // dark navy
    };

    private void UpdateChart()
    {
        _chartSampleIndex++;

        foreach (var reading in Readings)
        {
            if (reading.TemperatureCelsius <= 0) continue;

            var temp = UseFahrenheit
                ? reading.TemperatureCelsius * 9.0 / 5.0 + 32
                : reading.TemperatureCelsius;

            if (!_chartPoints.TryGetValue(reading.ComponentName, out var points))
            {
                points = new ObservableCollection<ObservablePoint>();
                _chartPoints[reading.ComponentName] = points;

                var colorIndex = ChartSeries.Count % SeriesColors.Length;
                var series = new LineSeries<ObservablePoint>
                {
                    Values = points,
                    Name = reading.ComponentName,
                    GeometrySize = 0,
                    LineSmoothness = 0.3,
                    Stroke = new SolidColorPaint(SeriesColors[colorIndex]) { StrokeThickness = 2 },
                    Fill = null,
                    ScalesYAt = 0,
                    ScalesXAt = 0,
                };
                ChartSeries.Add(series);
            }

            points.Add(new ObservablePoint(_chartSampleIndex, Math.Round(temp, 1)));

            // Trim old points
            while (points.Count > MaxChartPoints)
                points.RemoveAt(0);
        }

        // Slide X axis window
        if (_chartSampleIndex > MaxChartPoints)
        {
            ChartXAxes[0].MinLimit = _chartSampleIndex - MaxChartPoints;
            ChartXAxes[0].MaxLimit = _chartSampleIndex;
        }
        else
        {
            ChartXAxes[0].MaxLimit = Math.Max(MaxChartPoints, _chartSampleIndex);
        }

        // Update Y axis label for unit
        ChartYAxes[0].Name = UseFahrenheit ? "°F" : "°C";
    }

    private void UpdateThrottleChart()
    {
        foreach (var reading in Readings)
        {
            // Only chart components that have thermal limits defined
            if (reading.ThermalLimitC <= 0) continue;

            // Skip if no throttle data yet and not already tracking
            if (reading.ThrottlePercentage <= 0 && !_throttleChartPoints.ContainsKey(reading.ComponentName))
                continue;

            if (!_throttleChartPoints.TryGetValue(reading.ComponentName, out var points))
            {
                points = new ObservableCollection<ObservablePoint>();
                _throttleChartPoints[reading.ComponentName] = points;

                var colorIndex = ThrottleChartSeries.Count % SeriesColors.Length;
                var series = new LineSeries<ObservablePoint>
                {
                    Values = points,
                    Name = reading.ComponentName,
                    GeometrySize = 0,
                    LineSmoothness = 0.3,
                    Stroke = new SolidColorPaint(SeriesColors[colorIndex]) { StrokeThickness = 2 },
                    Fill = null,
                    ScalesYAt = 0,
                    ScalesXAt = 0,
                };
                ThrottleChartSeries.Add(series);
            }

            points.Add(new ObservablePoint(_chartSampleIndex, Math.Round(reading.ThrottlePercentage, 1)));

            while (points.Count > MaxChartPoints)
                points.RemoveAt(0);
        }

        // Slide X axis window
        if (_chartSampleIndex > MaxChartPoints)
        {
            ThrottleChartXAxes[0].MinLimit = _chartSampleIndex - MaxChartPoints;
            ThrottleChartXAxes[0].MaxLimit = _chartSampleIndex;
        }
        else
        {
            ThrottleChartXAxes[0].MaxLimit = Math.Max(MaxChartPoints, _chartSampleIndex);
        }
    }

    private void OnDiagnosticMessage(string message)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            DiagnosticLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
            if (DiagnosticLog.Count > 200)
                DiagnosticLog.RemoveAt(DiagnosticLog.Count - 1);
        });
    }

    [RelayCommand]
    private void RunAsAdmin()
    {
        AdminElevationHelper.RestartAsAdmin();
    }

    [RelayCommand]
    private void ExportCsv()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            DefaultExt = ".csv",
            FileName = $"ThermalData_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                _aggregator.ExportToCsv(dialog.FileName);
                StatusMessage = $"Exported to {dialog.FileName}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export failed: {ex.Message}";
            }
        }
    }

    public string ThemeButtonText => IsDarkTheme ? "☀️ Light" : "🌙 Dark";

    public string TempUnitButtonText => UseFahrenheit ? "°C" : "°F";

    partial void OnIsDarkThemeChanged(bool value)
    {
        OnPropertyChanged(nameof(ThemeButtonText));
    }

    partial void OnUseFahrenheitChanged(bool value)
    {
        OnPropertyChanged(nameof(TempUnitButtonText));
        // Push unit change to all readings
        foreach (var r in Readings)
            r.UseFahrenheit = value;
        // Update summary stats display
        OnPropertyChanged(nameof(MaxTemperature));
        OnPropertyChanged(nameof(AvgTemperature));
        // Convert existing chart points in-place instead of clearing
        foreach (var points in _chartPoints.Values)
        {
            foreach (var pt in points)
            {
                if (pt.Y is double y)
                {
                    pt.Y = value
                        ? Math.Round(y * 9.0 / 5.0 + 32, 1)   // C → F
                        : Math.Round((y - 32) * 5.0 / 9.0, 1); // F → C
                }
            }
        }
        // Update Y axis label
        ChartYAxes[0].Name = value ? "°F" : "°C";
    }

    public double MaxTemperatureDisplay => UseFahrenheit ? MaxTemperature * 9.0 / 5.0 + 32 : MaxTemperature;
    public double AvgTemperatureDisplay => UseFahrenheit ? AvgTemperature * 9.0 / 5.0 + 32 : AvgTemperature;
    public string TempUnit => UseFahrenheit ? "°F" : "°C";

    [RelayCommand]
    private void ToggleTempUnit()
    {
        UseFahrenheit = !UseFahrenheit;
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        var res = Application.Current.Resources;
        if (IsDarkTheme)
        {
            SetBrushColor(res, "ThemeBgBrush", "#12122A");
            SetBrushColor(res, "ThemeSurfaceBrush", "#1A1A2E");
            SetBrushColor(res, "ThemeControlBrush", "#2D2D54");
            SetBrushColor(res, "ThemeControlHoverBrush", "#3D3D64");
            SetBrushColor(res, "ThemeControlPressedBrush", "#4D4D74");
            SetBrushColor(res, "ThemeBorderBrush", "#444466");
            SetBrushColor(res, "ThemeBorderLightBrush", "#333355");
            SetBrushColor(res, "ThemeFgBrush", "#E0E0F0");
            SetBrushColor(res, "ThemeFgDimBrush", "#8899AA");
            SetBrushColor(res, "ThemeAccentBrush", "#5577CC");
            SetBrushColor(res, "ThemeHighlightBrush", "#3A3A6A");
        }
        else
        {
            SetBrushColor(res, "ThemeBgBrush", "#F0F0F5");
            SetBrushColor(res, "ThemeSurfaceBrush", "#FFFFFF");
            SetBrushColor(res, "ThemeControlBrush", "#E0E0EC");
            SetBrushColor(res, "ThemeControlHoverBrush", "#D0D0E0");
            SetBrushColor(res, "ThemeControlPressedBrush", "#C0C0D4");
            SetBrushColor(res, "ThemeBorderBrush", "#BBBBCC");
            SetBrushColor(res, "ThemeBorderLightBrush", "#DDDDEE");
            SetBrushColor(res, "ThemeFgBrush", "#1A1A2E");
            SetBrushColor(res, "ThemeFgDimBrush", "#556677");
            SetBrushColor(res, "ThemeAccentBrush", "#3366BB");
            SetBrushColor(res, "ThemeHighlightBrush", "#D0D0E8");
        }

        // Update chart axis colors for theme
        var labelColor = IsDarkTheme ? new SKColor(136, 153, 170) : new SKColor(85, 102, 119);
        var gridColor = IsDarkTheme ? new SKColor(60, 60, 90) : new SKColor(200, 200, 220);
        LegendTextPaint = new SolidColorPaint(labelColor);
        foreach (var axis in ChartXAxes.Concat(ChartYAxes).Concat(ThrottleChartXAxes).Concat(ThrottleChartYAxes))
        {
            axis.LabelsPaint = new SolidColorPaint(labelColor);
            axis.NamePaint = new SolidColorPaint(labelColor);
            axis.SeparatorsPaint = new SolidColorPaint(gridColor) { StrokeThickness = 0.5f };
        }
    }

    private static void SetBrushColor(ResourceDictionary res, string key, string hex)
    {
        if (res[key] is SolidColorBrush brush && !brush.IsFrozen)
            brush.Color = (Color)ColorConverter.ConvertFromString(hex);
        else
            res[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _aggregator.Dispose();
        GC.SuppressFinalize(this);
    }
}
