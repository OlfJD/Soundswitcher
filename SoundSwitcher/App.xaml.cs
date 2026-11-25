using System;
using System.Threading;
using System.Windows;
using SoundSwitcher.Core;
using SoundSwitcher.Views;

namespace SoundSwitcher;

public partial class App : System.Windows.Application
{
    private SingleInstance? _instance;
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
        // Errors go to %APPDATA%\SoundSwitcher\errors.log (capped); no dialogs, so nothing can stack up
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            ErrorLog.Write("Unhandled exception", args.ExceptionObject);

        DispatcherUnhandledException += (s, args) =>
        {
            ErrorLog.Write("Unhandled UI exception", args.Exception);
            args.Handled = true;
        };

        try
        {
            _instance = new SingleInstance();
            if (!_instance.IsFirstInstance)
            {
                // Started again by the user: bring up the running copy. Started at sign-in: just quit.
                if (!e.Args.Contains(SettingsManager.AutostartArgument, StringComparer.OrdinalIgnoreCase))
                {
                    _instance.RequestShow();
                }
                _instance.Dispose();
                _instance = null;
                Shutdown();
                return;
            }
        }
        catch (Exception ex)
        {
            // Without the claim, run anyway rather than not at all
            ErrorLog.Write("Single-instance check failed", ex.Message);
        }

        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        InitDummyHwndHost();

        try
        {
            _audioController = new AudioController();
            _settingsManager = new SettingsManager();
            ThemeManager.Apply(_settingsManager.Settings.Theme);

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

            // Another launch of the exe opens this copy's flyout
            _instance?.ListenForShowRequests(() => Dispatcher.InvokeAsync(() =>
            {
                if (_flyoutWindow.IsVisible)
                {
                    _flyoutWindow.Activate();
                }
                else
                {
                    _flyoutWindow.ShowAtTaskbar();
                }
            }));
        }
        catch (Exception ex)
        {
            ErrorLog.Write("Starting SoundSwitcher failed", ex);
            System.Windows.MessageBox.Show($"Failed to initialize SoundSwitcher: {ex.Message}", "SoundSwitcher Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayManager?.Dispose();
        _audioController?.Dispose();
        try { _instance?.Dispose(); } catch { }
        _instance = null;
        base.OnExit(e);
    }
}
