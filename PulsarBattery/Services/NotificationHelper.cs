using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using System.Diagnostics;

namespace PulsarBattery.Services;

internal static class NotificationHelper
{
    private static bool _initialized;
    private static bool _registered;

    public static void Init()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        try
        {
            var manager = AppNotificationManager.Default;

            // Per quickstart: always hook before Register() so handling stays in this process.
            manager.NotificationInvoked += (_, args) =>
            {
                Debug.WriteLine($"Notification invoked: {args.Argument}");
            };

            manager.Register();
            _registered = true;
        }
        catch (Exception ex)
        {
            _registered = false;
            Debug.WriteLine($"Notification init/register failed: {ex}");
        }
    }

    public static void Unregister()
    {
        if (!_registered)
        {
            return;
        }

        try
        {
            AppNotificationManager.Default.Unregister();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Notification unregister failed: {ex}");
        }
        finally
        {
            _registered = false;
        }
    }

    public static void NotifyBatteryLevelChanged(int previousPercentage, int currentPercentage, bool isCharging, string? model)
    {
        if (!_initialized)
        {
            Init();
        }

        if (!_registered)
        {
            return;
        }

        try
        {
            // Determine notification scenario
            var isLowBattery = currentPercentage < 20 && !isCharging;
            var isCriticalBattery = currentPercentage < 10 && !isCharging;
            
            // Build title based on battery state
            var title = isCriticalBattery ? "Critical Battery Level" :
                       isLowBattery ? "Low Battery" :
                       isCharging ? "Charging" :
                       "Battery Update";

            // Build device info line
            var deviceLine = string.IsNullOrWhiteSpace(model) ? 
                $"Battery: {currentPercentage}%" : 
                $"{model}: {currentPercentage}%";

            // Build status line with charging state
            var statusLine = isCharging ? 
                "Currently charging" : 
                $"Not charging - {GetBatteryChangeText(previousPercentage, currentPercentage)}";

            var notification = new AppNotificationBuilder()
                .AddText(title)
                .AddText(deviceLine)
                .AddText(statusLine)
                .BuildNotification();

            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Battery notification failed: {ex}");
        }
    }

    private static string GetBatteryChangeText(int previousPercentage, int currentPercentage)
    {
        var change = currentPercentage - previousPercentage;
        
        if (change > 0)
        {
            return $"+{change}% since last update";
        }
        else if (change < 0)
        {
            return $"{change}% since last update";
        }
        else
        {
            return "No change";
        }
    }
}