using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PulsarBattery.Services;

public sealed class BatteryMonitor : IDisposable
{
    private readonly PulsarBatteryReader _reader = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(10);
    private (DateTimeOffset timestamp, PulsarBatteryReader.BatteryStatus status)? _lastStatus;
    private readonly int _thresholdUnlocked;
    private readonly int _thresholdLocked;
    private int? _lastNotifiedBattery;

    public BatteryMonitor()
    {
        _thresholdUnlocked = GetEnvInt("BATTERY_LEVEL_ALERT_THRESHOLD", 5);
        _thresholdLocked = GetEnvInt("BATTERY_LEVEL_ALERT_THRESHOLD_LOCKED", 30);
    }

    public void Start()
    {
        Task.Run(() => RunAsync(_cts.Token));
    }

    public void Dispose()
    {
        _cts.Cancel();
    }

    private async Task RunAsync(CancellationToken token)
    {
        var lastUnlocked = DateTimeOffset.MinValue;
        var lastCheck = DateTimeOffset.MinValue;

        while (!token.IsCancellationRequested)
        {
            var isLocked = IsWorkstationLocked();
            await Task.Delay(TimeSpan.FromSeconds(2), token);
            isLocked = isLocked && IsWorkstationLocked();

            if (isLocked)
            {
                // Shortly after locking, keep checking (so notifications still work when you lock the PC).
                if (DateTimeOffset.UtcNow - lastUnlocked < TimeSpan.FromSeconds(10))
                {
                    await CheckBatteryAsync(_thresholdLocked, token);
                }
            }
            else
            {
                lastUnlocked = DateTimeOffset.UtcNow;

                var interval = TimeSpan.FromMinutes(Math.Max(0.1, PulsarBattery.ViewModels.MainViewModel.GlobalPollIntervalMinutes));

                if (DateTimeOffset.UtcNow - lastCheck >= interval)
                {
                    lastCheck = DateTimeOffset.UtcNow;
                    await CheckBatteryAsync(_thresholdUnlocked, token);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(5), token);
        }
    }

    private async Task CheckBatteryAsync(int threshold, CancellationToken token)
    {
        var status = ReadBatteryStatus();
        if (status is null)
        {
            Debug.WriteLine("Battery status not available");
            return;
        }

        var batteryStatus = status;
        var (battery, charging, model) = batteryStatus;

        if (_lastNotifiedBattery is null)
        {
            _lastNotifiedBattery = battery;
        }
        else if (_lastNotifiedBattery.Value != battery)
        {
            var previous = _lastNotifiedBattery.Value;
            _lastNotifiedBattery = battery;
            NotificationHelper.NotifyBatteryLevelChanged(previous, battery, charging, model);
        }

        if (!charging)
        {
            Debug.WriteLine($"Battery: NotCharging {battery}% threshold {threshold}");
            if (battery < threshold)
            {
                Debug.WriteLine("Warning: Battery level is below threshold and not charging!");

                // Keep the existing behavior for low-battery alerts.
                NotificationHelper.NotifyBatteryLevelChanged(battery, battery, charging, model);
                TryBeep();
            }
        }
        else
        {
            Debug.WriteLine("Battery state is charging or above the threshold");
        }

        await Task.CompletedTask;
    }

    private static void TryBeep()
    {
        try
        {
            Console.Beep(200, 200);
            Console.Beep(200, 200);
            Console.Beep(200, 200);
        }
        catch
        {
        }
    }

    private PulsarBatteryReader.BatteryStatus? ReadBatteryStatus()
    {
        var status = _reader.ReadBatteryStatus();
        if (status is not null)
        {
            _lastStatus = (DateTimeOffset.UtcNow, status);
            return status;
        }

        if (_lastStatus is null)
        {
            return null;
        }

        var (timestamp, cached) = _lastStatus.Value;
        if (DateTimeOffset.UtcNow - timestamp <= _cacheTtl)
        {
            Debug.WriteLine("Using cached battery status");
            return cached;
        }

        return null;
    }

    private static int GetEnvInt(string name, int fallback)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (int.TryParse(raw, out var value))
        {
            return value;
        }

        return fallback;
    }

    private static bool IsWorkstationLocked()
    {
        return GetForegroundWindow() == IntPtr.Zero;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}