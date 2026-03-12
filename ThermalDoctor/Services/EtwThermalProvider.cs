using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using ThermalDoctor.Models;

namespace ThermalDoctor.Services;

public class EtwThermalProvider : IDisposable
{
    private TraceEventSession? _session;
    private Task? _processingTask;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    // Known thermal-related ETW provider GUIDs
    private static readonly Guid KernelAcpiProvider = new("C514638F-7723-485B-BCFC-96565D735D4A");
    private static readonly Guid ThermalControllerProvider = new("D5F56DC1-4153-4673-83C5-809EA2C16993");
    private static readonly Guid KernelProcessorPowerProvider = new("0F67E49F-FE51-4E9F-B490-6F2948CC6027");

    public event Action<ThermalReading>? ThermalReadingReceived;
    public event Action<bool>? ThrottlingStateChanged;
    public event Action<string>? DiagnosticMessage;

    public bool IsThrottled { get; private set; }

    public bool IsRunning => _session != null && _processingTask != null && !_processingTask.IsCompleted;

    public bool RequiresElevation()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return !principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    public void Start()
    {
        if (IsRunning) return;

        if (RequiresElevation())
        {
            DiagnosticMessage?.Invoke("ETW requires administrator privileges. Falling back to WMI polling.");
            return;
        }

        _cts = new CancellationTokenSource();
        var sessionName = $"ThermalDoctor_{Environment.ProcessId}";

        try
        {
            _session = new TraceEventSession(sessionName);

            // Enable thermal-related providers
            _session.EnableProvider(KernelAcpiProvider, TraceEventLevel.Informational);

            try
            {
                _session.EnableProvider(ThermalControllerProvider, TraceEventLevel.Informational);
            }
            catch
            {
                DiagnosticMessage?.Invoke("ThermalController ETW provider not available on this system.");
            }

            // Enable processor power provider for throttling detection
            try
            {
                _session.EnableProvider(KernelProcessorPowerProvider, TraceEventLevel.Informational);
            }
            catch
            {
                DiagnosticMessage?.Invoke("Kernel-Processor-Power ETW provider not available.");
            }

            // Enable Surface-specific providers if available
            EnableSurfaceProviders();

            _session.Source.Dynamic.All += OnTraceEvent;

            _processingTask = Task.Run(() =>
            {
                try
                {
                    _session.Source.Process();
                }
                catch (OperationCanceledException)
                {
                    // Normal shutdown
                }
                catch (Exception ex)
                {
                    DiagnosticMessage?.Invoke($"ETW processing error: {ex.Message}");
                }
            }, _cts.Token);

            DiagnosticMessage?.Invoke("ETW thermal trace session started.");
        }
        catch (Exception ex)
        {
            DiagnosticMessage?.Invoke($"Failed to start ETW session: {ex.Message}");
            _session?.Dispose();
            _session = null;
        }
    }

    private void EnableSurfaceProviders()
    {
        // Surface Aggregator Module (SAM) – GUID may vary by firmware version
        var surfaceSamGuids = new[]
        {
            "6D7EB41C-B0B7-4377-B5F7-E0C7D8F1E8A6", // Surface SAM general
            "1B562BB8-50A2-4D4E-A755-76A3B7C7B233", // Surface Thermal
        };

        foreach (var guidStr in surfaceSamGuids)
        {
            try
            {
                if (Guid.TryParse(guidStr, out var guid))
                {
                    _session!.EnableProvider(guid, TraceEventLevel.Informational);
                }
            }
            catch
            {
                // Provider not available; silently skip
            }
        }
    }

    private void OnTraceEvent(TraceEvent data)
    {
        var eventName = data.EventName ?? string.Empty;
        var providerName = data.ProviderName ?? string.Empty;

        // Check for throttling/power capping events
        if (IsThrottlingEvent(eventName, providerName, data))
            return;

        // Filter for thermal-related events
        if (!IsThermalEvent(eventName, providerName))
            return;

        try
        {
            var reading = ParseThermalEvent(data);
            if (reading != null)
            {
                ThermalReadingReceived?.Invoke(reading);
            }
        }
        catch (Exception ex)
        {
            DiagnosticMessage?.Invoke($"Error parsing ETW event: {ex.Message}");
        }
    }

    private static bool IsThermalEvent(string eventName, string providerName)
    {
        var combined = $"{providerName}:{eventName}".ToUpperInvariant();
        return combined.Contains("THERM") ||
               combined.Contains("TEMPERATURE") ||
               combined.Contains("COOLING") ||
               combined.Contains("ACPI") && combined.Contains("TZ");
    }

    private bool IsThrottlingEvent(string eventName, string providerName, TraceEvent data)
    {
        var combined = $"{providerName}:{eventName}".ToUpperInvariant();

        // Must be from a power/thermal provider
        bool isPowerProvider = combined.Contains("POWER") || combined.Contains("THERMAL") || combined.Contains("PROCESSOR");
        if (!isPowerProvider)
            return false;

        // Must contain a throttling-related keyword
        bool hasThrottleKeyword = combined.Contains("THROTTL") || combined.Contains("CONSTRAINT") ||
                                  combined.Contains("PERFSTATE") || combined.Contains("IDLESTATE") ||
                                  combined.Contains("PARKED") || combined.Contains("FREQUENCY") ||
                                  combined.Contains("LIMIT") || combined.Contains("CAP");
        if (!hasThrottleKeyword)
            return false;

        // Detect thermal throttling from Kernel-Processor-Power events
        bool throttled = false;
        foreach (var fieldName in data.PayloadNames)
        {
            var upper = fieldName.ToUpperInvariant();
            if (upper.Contains("THROTTLE") || upper.Contains("CONSTRAINT") || upper.Contains("LIMIT"))
            {
                var value = data.PayloadByName(fieldName);
                if (value is int i)
                    throttled = i > 0;
                else if (value is uint u)
                    throttled = u > 0;
                else if (value is bool b)
                    throttled = b;
            }
        }

        if (IsThrottled != throttled)
        {
            IsThrottled = throttled;
            ThrottlingStateChanged?.Invoke(throttled);
            DiagnosticMessage?.Invoke($"Thermal throttling {(throttled ? "DETECTED" : "cleared")}");
        }

        return true;
    }

    private ThermalReading? ParseThermalEvent(TraceEvent data)
    {
        double? temperature = null;
        string zoneName = data.EventName ?? "Unknown";

        // Try to extract temperature from known payload field names
        foreach (var fieldName in data.PayloadNames)
        {
            var upper = fieldName.ToUpperInvariant();
            if (upper.Contains("TEMP") || upper.Contains("READING") || upper.Contains("VALUE"))
            {
                var value = data.PayloadByName(fieldName);
                if (value is double d)
                    temperature = d;
                else if (value is int i)
                    temperature = i;
                else if (value is uint u)
                    temperature = u;
                else if (value is long l)
                    temperature = l;
            }

            if (upper.Contains("ZONE") || upper.Contains("INSTANCE") || upper.Contains("NAME"))
            {
                var value = data.PayloadByName(fieldName);
                if (value is string s && !string.IsNullOrWhiteSpace(s))
                    zoneName = s;
            }
        }

        if (temperature == null)
            return null;

        // Check if temperature is in tenths-of-Kelvin (WMI convention) or Celsius
        var tempC = temperature.Value;
        if (tempC > 2000) // Likely tenths of Kelvin
            tempC = (tempC - 2732.0) / 10.0;
        else if (tempC > 200) // Likely Kelvin
            tempC -= 273.15;

        return new ThermalReading
        {
            ComponentName = zoneName,
            ZoneId = $"ETW:{data.ProviderName}:{zoneName}",
            TemperatureCelsius = Math.Round(tempC, 1),
            Timestamp = data.TimeStamp
        };
    }

    public List<string> EnumerateAvailableProviders()
    {
        var providers = new List<string>();
        try
        {
            // Use published providers list
            foreach (var p in TraceEventProviders.GetPublishedProviders())
            {
                var name = TraceEventProviders.GetProviderName(p);
                if (name != null &&
                    (name.Contains("thermal", StringComparison.OrdinalIgnoreCase) ||
                     name.Contains("acpi", StringComparison.OrdinalIgnoreCase) ||
                     name.Contains("surface", StringComparison.OrdinalIgnoreCase) ||
                     name.Contains("sam", StringComparison.OrdinalIgnoreCase)))
                {
                    providers.Add($"{name} ({p})");
                }
            }
        }
        catch (Exception ex)
        {
            providers.Add($"Error enumerating providers: {ex.Message}");
        }
        return providers;
    }

    public void Stop()
    {
        _cts?.Cancel();
        _session?.Stop();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        _session?.Dispose();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
