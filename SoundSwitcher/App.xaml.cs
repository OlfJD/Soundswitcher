using System;
using System.Threading;
using System.Windows;
using SoundSwitcher.Core;
using SoundSwitcher.Views;

namespace SoundSwitcher;

public partial class App : System.Windows.Application
{
    private static Mutex? _mutex;
    private AudioController? _audioController;
    private SettingsManager? _settingsManager;
    private TrayManager? _trayManager;
    private FlyoutWindow? _flyoutWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        const string mutexName = "SoundSwitcher_Unique_App_Mutex";
        _mutex = new Mutex(true, mutexName, out bool isNewInstance);

        if (!isNewInstance)
        {
            // Another instance is already running
            Shutdown();
            return;
        }

        base.OnStartup(e);

        try
        {
            if (!System.IO.File.Exists("App.ico") && !System.IO.File.Exists("app.ico"))
            {
                IconGenerator.GenerateAppIcon("App.ico");
            }
        }
        catch { }

        try
        {
            _audioController = new AudioController();
            _settingsManager = new SettingsManager();

            _flyoutWindow = new FlyoutWindow(_audioController, _settingsManager);

            _trayManager = new TrayManager(
                _audioController,
                _settingsManager,
                onOpenFlyout: () =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        _flyoutWindow.ToggleFlyout();
                    });
                }
            );
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to initialize SoundSwitcher: {ex.Message}", "SoundSwitcher Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayManager?.Dispose();
        _audioController?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
