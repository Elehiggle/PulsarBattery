using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PulsarBattery.Services;

namespace PulsarBattery.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    private void SendTestNotification_Click(object sender, RoutedEventArgs e)
    {
        NotificationHelper.Init();
        
        // Get actual battery data from the ViewModel
        if (DataContext is ViewModels.MainViewModel viewModel)
        {
            // Simulate a battery level drop of 5%
            var currentLevel = viewModel.BatteryPercentage;
            var previousLevel = currentLevel + 5;
            
            NotificationHelper.NotifyBatteryLevelChanged(
                previousLevel, 
                currentLevel, 
                viewModel.IsCharging, 
                viewModel.ModelName);
        }
        else
        {
            // Fallback to test data if ViewModel is not available
            NotificationHelper.NotifyBatteryLevelChanged(50, 45, isCharging: false, model: "Test Device");
        }
    }

    private void RefreshNotificationStatus_Click(object sender, RoutedEventArgs e)
    {
        NotificationHelper.Init();
    }
}