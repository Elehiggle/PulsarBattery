using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PulsarBattery.Models;
using PulsarBattery.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace PulsarBattery.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly PulsarBatteryReader _reader = new();
    private readonly DispatcherTimer _timer;
    private readonly HistoryStore _historyStore = new();
    private readonly DispatcherTimer _historySaveTimer;
    private readonly SemaphoreSlim _historyIoGate = new(1, 1);
    private DateTimeOffset _lastLogged = DateTimeOffset.MinValue;
    private bool _historyLoaded;
    private int _batteryPercentage;
    private bool _isCharging;
    private string _modelName = "-";
    private DateTimeOffset? _lastUpdated;
    private double _pollIntervalMinutes = 1.0;
    private double _logIntervalMinutes = 5.0;
    private string _statusText = "Ready";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<BatteryReading> History { get; } = new();

    public int BatteryPercentage
    {
        get => _batteryPercentage;
        private set => SetProperty(ref _batteryPercentage, value);
    }

    public bool IsCharging
    {
        get => _isCharging;
        private set => SetProperty(ref _isCharging, value);
    }

    public string ModelName
    {
        get => _modelName;
        private set => SetProperty(ref _modelName, value);
    }

    public string ChargingStateText => IsCharging ? "Charging" : "Not charging";

    public string LastUpdatedText => _lastUpdated is null
        ? "No data yet"
        : $"Last updated: {_lastUpdated.Value.ToString("T", CultureInfo.CurrentCulture)}";

    public static double GlobalPollIntervalMinutes { get; private set; } = 5.0;

    public double PollIntervalMinutes
    {
        get => _pollIntervalMinutes;
        set
        {
            if (SetProperty(ref _pollIntervalMinutes, value))
            {
                GlobalPollIntervalMinutes = value;
                UpdateTimerInterval();
            }
        }
    }

    public double LogIntervalMinutes
    {
        get => _logIntervalMinutes;
        set => SetProperty(ref _logIntervalMinutes, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public MainViewModel()
    {
        GlobalPollIntervalMinutes = _pollIntervalMinutes;
        _timer = new DispatcherTimer();
        UpdateTimerInterval();
        _timer.Tick += async (_, _) => await PollAsync();

        _historySaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(15),
        };
        _historySaveTimer.Tick += async (_, _) => await SaveHistoryAsync();
    }

    public void Start()
    {
        _ = LoadHistoryOnceAsync();
        _ = PollAsync();
        _timer.Start();
        _historySaveTimer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        _historySaveTimer.Stop();
        _ = SaveHistoryAsync();
    }

    private async Task LoadHistoryOnceAsync()
    {
        if (_historyLoaded)
        {
            return;
        }

        _historyLoaded = true;

        try
        {
            var items = await _historyStore.LoadAsync().ConfigureAwait(false);
            if (items.Count == 0)
            {
                return;
            }

            _ = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
            {
                foreach (var item in items)
                {
                    History.Add(item);
                }
            });
        }
        catch
        {
            // best-effort
        }
    }

    private async Task SaveHistoryAsync()
    {
        if (!_historyLoaded)
        {
            return;
        }

        if (!await _historyIoGate.WaitAsync(0).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            // Snapshot to avoid collection changes while writing.
            BatteryReading[] snapshot;
            snapshot = new BatteryReading[History.Count];
            for (var i = 0; i < History.Count; i++)
            {
                snapshot[i] = History[i];
            }

            await _historyStore.SaveAsync(snapshot).ConfigureAwait(false);
        }
        catch
        {
            // best-effort
        }
        finally
        {
            _historyIoGate.Release();
        }
    }

    private async Task PollAsync()
    {
        StatusText = "Reading battery status...";
        var status = await Task.Run(() => _reader.ReadBatteryStatus());
        if (status is null)
        {
            StatusText = "No device found";
            return;
        }

        BatteryPercentage = status.Percentage;
        IsCharging = status.IsCharging;
        ModelName = status.Model;
        _lastUpdated = DateTimeOffset.Now;
        OnPropertyChanged(nameof(ChargingStateText));
        OnPropertyChanged(nameof(LastUpdatedText));
        StatusText = "Updated";

        if (ShouldLog())
        {
            _lastLogged = DateTimeOffset.Now;
            History.Insert(0, new BatteryReading(_lastLogged, status.Percentage, status.IsCharging, status.Model));
            const int maxEntries = 500;
            while (History.Count > maxEntries)
            {
                History.RemoveAt(History.Count - 1);
            }
        }
    }

    private bool ShouldLog()
    {
        if (_lastLogged == DateTimeOffset.MinValue)
        {
            return true;
        }

        var diff = DateTimeOffset.Now - _lastLogged;
        return diff >= TimeSpan.FromMinutes(LogIntervalMinutes);
    }

    private void UpdateTimerInterval()
    {
        var minutes = Math.Max(0.1, PollIntervalMinutes);
        _timer.Interval = TimeSpan.FromMinutes(minutes);
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}