using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ThermalDoctor.Models;

namespace ThermalDoctor.Services;

public class ThermalDataAggregator : IDisposable
{
    private readonly WmiThermalProvider _wmiProvider;
    private readonly EtwThermalProvider _etwProvider;
    private readonly SurfaceModelDetector _modelDetector;
    private Timer? _pollingTimer;
    private bool _disposed;
    private readonly Dictionary<string, Queue<double>> _temperatureHistory = new();
    private const int MaxHistorySize = 60; // 60 samples for trend

    public ObservableCollection<ThermalReading> CurrentReadings { get; } = new();
    public Dictionary<string, List<double>> TemperatureHistory { get; } = new();
    public string DetectedModel { get; private set; } = "Detecting...";
    public bool IsSurface { get; private set; }
    public bool IsThrottled { get; private set; }

    /// <summary>Thermal limits lookup: ComponentName → (ThermalLimitC, TjMaxC). Set by caller.</summary>
    public Dictionary<string, (double ThermalLimitC, double TjMaxC)> ThermalLimits { get; } = new(StringComparer.OrdinalIgnoreCase);

    public event Action? ReadingsUpdated;
    public event Action<string>? DiagnosticMessage;

    public int PollingIntervalMs { get; set; } = 2000;

    public ThermalDataAggregator()
    {
        _wmiProvider = new WmiThermalProvider();
        _etwProvider = new EtwThermalProvider();
        _modelDetector = new SurfaceModelDetector();

        _etwProvider.ThermalReadingReceived += OnEtwReadingReceived;
        _etwProvider.ThrottlingStateChanged += OnThrottlingStateChanged;
        _etwProvider.DiagnosticMessage += msg => DiagnosticMessage?.Invoke(msg);
    }

    public void Initialize()
    {
        DetectedModel = _modelDetector.DetectModel();
        IsSurface = _modelDetector.IsSurfaceDevice();

        DiagnosticMessage?.Invoke($"Detected device: {DetectedModel} (Surface: {IsSurface})");

        // Try starting ETW (requires admin)
        _etwProvider.Start();

        // Always start WMI polling as primary/fallback
        _pollingTimer = new Timer(PollWmi, null, 0, PollingIntervalMs);
    }

    public List<string> GetAvailableEtwProviders()
    {
        return _etwProvider.EnumerateAvailableProviders();
    }

    private void PollWmi(object? state)
    {
        try
        {
            var readings = _wmiProvider.QueryThermalZones();

            // Calculate thermal throttle % for each component using its thermal limits
            bool anyThrottled = false;
            foreach (var r in readings)
            {
                if (ThermalLimits.TryGetValue(r.ComponentName, out var limits) && limits.TjMaxC > 0)
                {
                    r.ThermalLimitC = limits.ThermalLimitC;
                    r.TjMaxC = limits.TjMaxC;
                    // Thermal throttle: 0% below ThermalLimit, ramps to 100% at TjMax
                    if (r.TemperatureCelsius >= limits.ThermalLimitC)
                    {
                        var range = limits.TjMaxC - limits.ThermalLimitC;
                        var pct = range > 0
                            ? (r.TemperatureCelsius - limits.ThermalLimitC) / range * 100.0
                            : 100.0;
                        r.ThrottlePercentage = Math.Clamp(Math.Round(pct, 1), 0, 100);
                        r.IsThrottled = true;
                        anyThrottled = true;
                    }
                    else
                    {
                        r.ThrottlePercentage = 0;
                        r.IsThrottled = false;
                    }
                }
            }

            if (anyThrottled != IsThrottled)
            {
                IsThrottled = anyThrottled;
                OnThrottlingStateChanged(anyThrottled);
            }

            MergeReadings(readings);
        }
        catch (Exception ex)
        {
            DiagnosticMessage?.Invoke($"WMI poll error: {ex.Message}");
        }
    }

    private void OnEtwReadingReceived(ThermalReading reading)
    {
        MergeReadings(new List<ThermalReading> { reading });
    }

    private void OnThrottlingStateChanged(bool throttled)
    {
        IsThrottled = throttled;
        // Mark CPU/SoC readings as throttled when system reports thermal throttling
        Application.Current?.Dispatcher.Invoke(() =>
        {
            foreach (var reading in CurrentReadings)
            {
                var name = reading.ComponentName.ToUpperInvariant();
                if (name.Contains("CPU") || name.Contains("SOC") || name.Contains("GPU"))
                    reading.IsThrottled = throttled;
            }
            ReadingsUpdated?.Invoke();
        });
    }

    private void MergeReadings(List<ThermalReading> newReadings)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            foreach (var reading in newReadings)
            {
                // Calculate trend
                reading.Trend = CalculateTrend(reading.ComponentName, reading.TemperatureCelsius);

                // Update history
                UpdateHistory(reading.ComponentName, reading.TemperatureCelsius);

                // Update or add to current readings
                var existing = CurrentReadings.FirstOrDefault(
                    r => r.ComponentName == reading.ComponentName);
                if (existing != null)
                {
                    var index = CurrentReadings.IndexOf(existing);
                    CurrentReadings[index] = reading;
                }
                else
                {
                    CurrentReadings.Add(reading);
                }
            }

            ReadingsUpdated?.Invoke();
        });
    }

    private string CalculateTrend(string componentName, double currentTemp)
    {
        if (!_temperatureHistory.TryGetValue(componentName, out var history) || history.Count < 3)
            return "→";

        var recentAvg = history.Skip(Math.Max(0, history.Count - 3)).Average();
        var diff = currentTemp - recentAvg;

        return diff switch
        {
            > 1.0 => "↑",
            < -1.0 => "↓",
            _ => "→"
        };
    }

    private void UpdateHistory(string componentName, double temperature)
    {
        if (!_temperatureHistory.ContainsKey(componentName))
            _temperatureHistory[componentName] = new Queue<double>();

        var queue = _temperatureHistory[componentName];
        queue.Enqueue(temperature);
        while (queue.Count > MaxHistorySize)
            queue.Dequeue();

        // Mirror to the public dictionary for charting
        TemperatureHistory[componentName] = queue.ToList();
    }

    public void SetPollingInterval(int milliseconds)
    {
        PollingIntervalMs = Math.Max(500, milliseconds);
        _pollingTimer?.Change(0, PollingIntervalMs);
    }

    public void ExportToCsv(string filePath)
    {
        var lines = new List<string> { "Timestamp,Component,ZoneId,TemperatureC,Status,Trend" };
        foreach (var reading in CurrentReadings)
        {
            lines.Add($"{reading.Timestamp:O},{reading.ComponentName},{reading.ZoneId},{reading.TemperatureCelsius},{reading.Status},{reading.Trend}");
        }
        System.IO.File.WriteAllLines(filePath, lines);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _pollingTimer?.Dispose();
        _etwProvider.Dispose();
        _wmiProvider.Dispose();
        GC.SuppressFinalize(this);
    }
}
