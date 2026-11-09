using System;
using System.Threading;
using System.Windows;
using SoundSwitcher.Core;
using SoundSwitcher.Views;

namespace SoundSwitcher;

public partial class App : System.Windows.Application
{
    private static Mutex? _mutex;
    private static bool _ownsMutex;
    private AudioController? _audioController;
    private SettingsManager? _settingsManager;
    private TrayManager? _trayManager;
    private FlyoutWindow? _flyoutWindow;
    private Window? _dummyHwndHost;

    private void InitDummyHwndHost()
    {
        _dummyHwndHost = new Window
        {
            Width = 1,
            Height = 1,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            Left = -10000,
            Top = -10000,
            ShowActivated = false
        };
        _dummyHwndHost.Show();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            try
            {
                string crashPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                System.IO.File.AppendAllText(crashPath, $"[{DateTime.Now}] UNHANDLED: {args.ExceptionObject}\n");
            }
            catch { }
        };

        DispatcherUnhandledException += (s, args) =>
        {
            try
            {
                string crashPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                System.IO.File.AppendAllText(crashPath, $"[{DateTime.Now}] DISPATCHER: {args.Exception}\n");
            }
            catch { }
            args.Handled = true;
        };

        const string mutexName = "SoundSwitcher_Unique_App_Mutex";
        try
        {
            _mutex = new Mutex(true, mutexName, out bool isNewInstance);
            _ownsMutex = isNewInstance;
            if (!_ownsMutex)
            {
                Shutdown();
                return;
            }
        }
        catch (AbandonedMutexException)
        {
            _ownsMutex = true;
        }
        catch
        {
            _ownsMutex = false;
        }

        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        InitDummyHwndHost();

        try
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string iconPath = System.IO.Path.Combine(appDir, "App.ico");
            if (!System.IO.File.Exists(iconPath))
            {
                IconGenerator.GenerateAppIcon(iconPath);
            }
        }
        catch { }

        try
        {
            _audioController = new AudioController();
            _settingsManager = new SettingsManager();
            _settingsManager.SyncStartupRegistration();

            _flyoutWindow = new FlyoutWindow(_audioController, _settingsManager);
            MainWindow = _flyoutWindow;

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

            MemoryOptimizer.Initialize();
            _flyoutWindow.Show();
            _flyoutWindow.Hide();
            Dispatcher.InvokeAsync(MemoryOptimizer.TrimWorkingSet, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
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
        try
        {
            if (_ownsMutex && _mutex != null)
            {
                _mutex.ReleaseMutex();
            }
        }
        catch { }
        try { _mutex?.Dispose(); } catch { }
        _mutex = null;
        base.OnExit(e);
    }
}
