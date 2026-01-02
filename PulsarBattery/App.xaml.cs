using Microsoft.UI.Xaml;
using PulsarBattery.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;

namespace PulsarBattery
{
    public partial class App : Application
    {
        private Window? _window;
        private readonly BatteryMonitor _monitor = new();
        private TrayIconService? _trayIcon;

        public App()
        {
            InitializeComponent();
            AppDomain.CurrentDomain.ProcessExit += (_, _) => NotificationHelper.Unregister();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            NotificationHelper.Init();

            _monitor.Start();

            _window = new MainWindow();
            _window.Closed += (_, _) =>
            {
                try
                {
                    if (_window is MainWindow mw)
                    {
                        mw.Stop();
                    }
                }
                catch
                {
                    // ignore
                }

                _monitor.Dispose();
                _trayIcon?.Dispose();
            };

            _window.Activate();

            // Create tray icon only after WinUI window is activated.
            _trayIcon = new TrayIconService();
            _trayIcon.Initialize(_window);

            // Some WinUI scenarios require an explicit creation call.
            try
            {
                _trayIcon.ForceCreate();
            }
            catch
            {
                // ignore
            }
        }
    }
}