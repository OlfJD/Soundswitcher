using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;
using SoundSwitcher.Core;

namespace SoundSwitcher.Views;

public partial class FlyoutWindow : Window
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private readonly AudioController _audioController;
    private readonly SettingsManager _settingsManager;
    private List<AudioDevice> _devices = new();
    private AudioDevice? _deviceA;
    private AudioDevice? _deviceB;
    private bool _isUpdatingUi;

    public FlyoutWindow(AudioController audioController, SettingsManager settingsManager)
    {
        InitializeComponent();
        _audioController = audioController;
        _settingsManager = settingsManager;

        Loaded += FlyoutWindow_Loaded;
        Deactivated += FlyoutWindow_Deactivated;

        _audioController.DevicesChanged += () => Dispatcher.Invoke(RefreshUI);
        _audioController.DefaultDeviceChanged += _ => Dispatcher.Invoke(RefreshUI);
    }

    private void FlyoutWindow_Loaded(object sender, RoutedEventArgs e)
    {
        WindowBlurHelper.ApplyModernWindowStyles(this);
        EnsureTopmost();

        StartupCheckBox.IsChecked = _settingsManager.Settings.LaunchOnStartup;
        DirectTrayToggleCheckBox.IsChecked = _settingsManager.Settings.DirectToggleOnTrayClick;
        RefreshUI();
    }

    private void FlyoutWindow_Deactivated(object? sender, EventArgs e)
    {
        // Keep window open when user clicks outside, but re-assert Topmost status
        EnsureTopmost();
    }

    public void EnsureTopmost()
    {
        Topmost = true;
        try
        {
            var helper = new WindowInteropHelper(this);
            if (helper.Handle != IntPtr.Zero)
            {
                SetWindowPos(helper.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
        }
        catch { }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch { }
        }
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            Hide();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    public void ToggleFlyout()
    {
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            ShowAtTaskbar();
        }
    }

    public void ShowAtTaskbar()
    {
        RefreshUI();
        UpdatePosition();

        Show();
        EnsureTopmost();
        Activate();
        Focus();

        if (Resources["FadeInStory"] is Storyboard sb)
        {
            sb.Begin(this);
        }
    }

    public void UpdatePosition()
    {
        UpdateLayout();
        double w = ActualWidth > 0 ? ActualWidth : Width;
        double h = ActualHeight > 0 ? ActualHeight : 60;

        var pos = TaskbarHelper.GetIdealFlyoutPosition(w, h);
        Left = pos.X;
        Top = pos.Y;
    }

    public void RefreshUI()
    {
        _isUpdatingUi = true;
        try
        {
            _devices = _audioController.GetPlaybackDevices();
            var currentDefault = _audioController.GetDefaultPlaybackDevice();

            if (_devices.Count > 0)
            {
                string? favA = _settingsManager.Settings.FavoriteDeviceA_Id;
                string? favB = _settingsManager.Settings.FavoriteDeviceB_Id;

                _deviceA = _devices.FirstOrDefault(d => d.Id == favA) ?? _devices[0];
                
                if (_devices.Count >= 2)
                {
                    _deviceB = _devices.FirstOrDefault(d => d.Id == favB) 
                               ?? _devices.FirstOrDefault(d => d.Id != _deviceA.Id) 
                               ?? _devices[1];
                }
                else
                {
                    _deviceB = _deviceA;
                }

                // If favorites were not set, save defaults
                if (string.IsNullOrEmpty(_settingsManager.Settings.FavoriteDeviceA_Id))
                {
                    _settingsManager.Settings.FavoriteDeviceA_Id = _deviceA.Id;
                }
                if (string.IsNullOrEmpty(_settingsManager.Settings.FavoriteDeviceB_Id))
                {
                    _settingsManager.Settings.FavoriteDeviceB_Id = _deviceB.Id;
                }

                // Update ComboBox items
                DeviceAComboBox.ItemsSource = null;
                DeviceAComboBox.ItemsSource = _devices;
                DeviceAComboBox.SelectedItem = _deviceA;

                DeviceBComboBox.ItemsSource = null;
                DeviceBComboBox.ItemsSource = _devices;
                DeviceBComboBox.SelectedItem = _deviceB;

                // Update Toggle State: Checked = Device B, Unchecked = Device A
                bool isDeviceBActive = currentDefault != null && currentDefault.Id == _deviceB.Id;
                MainToggleSwitch.IsChecked = isDeviceBActive;

                // Update tooltips & subtitles
                string devAName = _deviceA.Name;
                string devBName = _deviceB.Name;
                MainToggleSwitch.ToolTip = $"Switch Audio Device:\n• 🔴 Red (Left): {devAName}\n• 🟢 Green (Right): {devBName}";
            }
            else
            {
                ActiveDeviceSubtitle.Text = "• No audio devices detected";
                MainToggleSwitch.IsEnabled = false;
            }

            if (currentDefault != null)
            {
                ActiveDeviceSubtitle.Text = $"• {currentDefault.Name}";
                VolumeSlider.Value = currentDefault.Volume;
                VolumeText.Text = $"{(int)currentDefault.Volume}%";
                
                if (currentDefault.IsMuted)
                {
                    MuteVectorPath.Data = Geometry.Parse("M11 4.702a.705.705 0 0 0-1.203-.498L6.413 7.587A1.4 1.4 0 0 1 5.416 8H3a1 1 0 0 0-1 1v6a1 1 0 0 0 1 1h2.416a1.4 1.4 0 0 1 .997.413l3.383 3.384A.705.705 0 0 0 11 19.298z M22 9l-6 6 M16 9l6 6");
                    MuteVectorPath.Stroke = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    MuteVectorPath.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    MuteBtn.Opacity = 0.75;
                }
                else
                {
                    MuteVectorPath.Data = Geometry.Parse("M11 4.702a.705.705 0 0 0-1.203-.498L6.413 7.587A1.4 1.4 0 0 1 5.416 8H3a1 1 0 0 0-1 1v6a1 1 0 0 0 1 1h2.416a1.4 1.4 0 0 1 .997.413l3.383 3.384A.705.705 0 0 0 11 19.298z M16 9a5 5 0 0 1 0 6 M19.364 18.364a9 9 0 0 0 0-12.728");
                    MuteVectorPath.Stroke = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                    MuteVectorPath.Fill = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                    MuteBtn.Opacity = 1.0;
                }
            }
            else
            {
                ActiveDeviceSubtitle.Text = "• No Active Device";
            }
        }
        finally
        {
            _isUpdatingUi = false;
        }
    }

    private void MainToggleSwitch_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi) return;

        bool isChecked = MainToggleSwitch.IsChecked == true;
        if (isChecked && _deviceB != null)
        {
            _audioController.SetDefaultPlaybackDevice(_deviceB.Id);
        }
        else if (!isChecked && _deviceA != null)
        {
            _audioController.SetDefaultPlaybackDevice(_deviceA.Id);
        }
        else
        {
            _audioController.ToggleBetween(_deviceA?.Id, _deviceB?.Id);
        }
    }

    private void DeviceAComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingUi) return;

        if (DeviceAComboBox.SelectedItem is AudioDevice selectedA)
        {
            _deviceA = selectedA;
            _settingsManager.Settings.FavoriteDeviceA_Id = selectedA.Id;
            _settingsManager.Save();

            RefreshUI();
        }
    }

    private void DeviceBComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingUi) return;

        if (DeviceBComboBox.SelectedItem is AudioDevice selectedB)
        {
            _deviceB = selectedB;
            _settingsManager.Settings.FavoriteDeviceB_Id = selectedB.Id;
            _settingsManager.Save();

            RefreshUI();
        }
    }

    private void SettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        bool isOpening = SettingsPanel.Visibility != Visibility.Visible;
        SettingsPanel.Visibility = isOpening ? Visibility.Visible : Visibility.Collapsed;
        SettingsVectorPath.Stroke = isOpening 
            ? new SolidColorBrush(Color.FromRgb(167, 139, 250)) // #A78BFA Pastel Lavender
            : new SolidColorBrush(Color.FromRgb(156, 163, 175));

        // Re-anchor window position so expanding or collapsing keeps bottom docked above taskbar
        UpdatePosition();
        Dispatcher.InvokeAsync(() =>
        {
            UpdatePosition();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingUi) return;

        VolumeText.Text = $"{(int)e.NewValue}%";
        var defaultDev = _audioController.GetDefaultPlaybackDevice();
        if (defaultDev != null)
        {
            _audioController.SetDeviceVolume(defaultDev.Id, (float)e.NewValue);
        }
    }

    private void MuteBtn_Click(object sender, RoutedEventArgs e)
    {
        var defaultDev = _audioController.GetDefaultPlaybackDevice();
        if (defaultDev != null)
        {
            bool newMute = !defaultDev.IsMuted;
            _audioController.SetDeviceMute(defaultDev.Id, newMute);
            RefreshUI();
        }
    }

    private void StartupCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi) return;
        _settingsManager.Settings.LaunchOnStartup = StartupCheckBox.IsChecked == true;
        _settingsManager.Save();
    }

    private void DirectTrayToggleCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi) return;
        _settingsManager.Settings.DirectToggleOnTrayClick = DirectTrayToggleCheckBox.IsChecked == true;
        _settingsManager.Save();
    }

    private void OpenWindowsSoundSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true });
        }
        catch { }
    }

    private void QuitBtn_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}
