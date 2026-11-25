using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SoundSwitcher.Views;

namespace SoundSwitcher.Core;

public class TrayManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly AudioController _audioController;
    private readonly SettingsManager _settingsManager;
    private readonly Action _onOpenFlyout;
    private TrayMenuWindow? _menu;
    private bool _isDisposed;

    public TrayManager(AudioController audioController, SettingsManager settingsManager, Action onOpenFlyout)
    {
        _audioController = audioController;
        _settingsManager = settingsManager;
        _onOpenFlyout = onOpenFlyout;

        _notifyIcon = new NotifyIcon
        {
            Text = "SoundSwitcher",
            Visible = true,
            Icon = LoadOrGenerateTrayIcon()
        };

        // Right-click shows a themed WPF menu (just "Exit") instead of a WinForms ContextMenuStrip,
        // so it matches the flyout in both themes.
        _notifyIcon.MouseClick += NotifyIcon_MouseClick;
        // Raised on the UI thread; also covers default-device changes
        _audioController.DevicesChanged += UpdateState;

        UpdateState();
    }

    private void NotifyIcon_MouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (_settingsManager.Settings.DirectToggleOnTrayClick)
            {
                bool switched = _audioController.ToggleBetween(_settingsManager.Settings.FavoriteDeviceA_Id, _settingsManager.Settings.FavoriteDeviceB_Id);
                if (!switched)
                {
                    // The flyout is hidden in this mode, so say it where the user clicked
                    _notifyIcon.ShowBalloonTip(4000, "SoundSwitcher",
                        "Couldn't switch the output device. Details are in %APPDATA%\\SoundSwitcher\\errors.log.",
                        ToolTipIcon.Warning);
                }
            }
            else
            {
                _onOpenFlyout();
            }
        }
        else if (e.Button == MouseButtons.Right)
        {
            _menu ??= new TrayMenuWindow();
            _menu.ShowAtCursor();
        }
    }

    public void UpdateState()
    {
        try
        {
            var defaultDev = _audioController.GetDefaultPlaybackDevice();
            string devName = defaultDev?.Name ?? "No active device";
            
            // NotifyIcon text limit is 63 chars
            string tooltip = $"SoundSwitcher\n{devName}";
            if (tooltip.Length > 63)
            {
                tooltip = tooltip.Substring(0, 60) + "...";
            }
            _notifyIcon.Text = tooltip;
        }
        catch { }
    }

    private static Icon LoadOrGenerateTrayIcon()
    {
        // The icon is embedded in the exe (Assets/app.ico, made by generate_icon.ps1). Ask for the
        // tray's real size so Windows picks the matching frame instead of scaling the 32 px one.
        try
        {
            var res = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/SoundSwitcher;component/Assets/app.ico", UriKind.Absolute));
            if (res != null)
            {
                using var stream = res.Stream;
                return new Icon(stream, SystemInformation.SmallIconSize);
            }
        }
        catch { }

        return CreateModernAudioIcon();
    }

    private static Icon CreateModernAudioIcon()
    {
        int size = 32;
        using var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        // Modern accent rounded pill background
        using var brush = new SolidBrush(Color.FromArgb(245, 167, 139, 250)); // Pastel Lavender
        using var bgPath = CreateRoundedRectangleF(new RectangleF(1, 1, 30, 30), 8);
        g.FillPath(brush, bgPath);

        // Draw Lucide Volume-2 icon
        var iconRect = new RectangleF(5, 5, 22, 22);
        IconGenerator.DrawLucideVolume2(g, iconRect, Color.White);

        IntPtr hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    private static GraphicsPath CreateRoundedRectangleF(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float diameter = radius * 2;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
