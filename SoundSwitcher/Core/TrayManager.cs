using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SoundSwitcher.Core;

namespace SoundSwitcher.Core;

public class TrayManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly AudioController _audioController;
    private readonly SettingsManager _settingsManager;
    private readonly Action _onOpenFlyout;
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

        _notifyIcon.MouseClick += NotifyIcon_MouseClick;
        _audioController.DevicesChanged += UpdateState;
        _audioController.DefaultDeviceChanged += _ => UpdateState();

        UpdateState();
    }

    private void NotifyIcon_MouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (_settingsManager.Settings.DirectToggleOnTrayClick)
            {
                _audioController.ToggleBetween(_settingsManager.Settings.FavoriteDeviceA_Id, _settingsManager.Settings.FavoriteDeviceB_Id);
            }
            else
            {
                _onOpenFlyout();
            }
        }
        else if (e.Button == MouseButtons.Right)
        {
            ShowContextMenu();
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

    private void ShowContextMenu()
    {
        var contextMenu = new ContextMenuStrip
        {
            ShowImageMargin = true,
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(241, 245, 249),
            BackColor = Color.FromArgb(18, 20, 27)
        };
        contextMenu.Renderer = new ModernMenuRenderer();

        var openItem = new ToolStripMenuItem("Open SoundSwitcher", null, (s, e) => _onOpenFlyout())
        {
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 255, 255)
        };
        contextMenu.Items.Add(openItem);

        var quickToggle = new ToolStripMenuItem("Toggle Output Device", null, (s, e) => 
            _audioController.ToggleBetween(_settingsManager.Settings.FavoriteDeviceA_Id, _settingsManager.Settings.FavoriteDeviceB_Id))
        {
            ForeColor = Color.FromArgb(241, 245, 249)
        };
        contextMenu.Items.Add(quickToggle);

        contextMenu.Items.Add(new ToolStripSeparator());

        var devices = _audioController.GetPlaybackDevices();
        foreach (var dev in devices)
        {
            var devItem = new ToolStripMenuItem(dev.Name, null, (s, e) =>
            {
                _audioController.SetDefaultPlaybackDevice(dev.Id);
            })
            {
                Checked = dev.IsDefault,
                ForeColor = dev.IsDefault ? Color.FromArgb(167, 139, 250) : Color.FromArgb(226, 232, 240)
            };
            contextMenu.Items.Add(devItem);
        }

        contextMenu.Items.Add(new ToolStripSeparator());

        var startupItem = new ToolStripMenuItem("Start with Windows", null, (s, e) =>
        {
            _settingsManager.Settings.LaunchOnStartup = !_settingsManager.Settings.LaunchOnStartup;
            _settingsManager.Save();
        })
        {
            Checked = _settingsManager.Settings.LaunchOnStartup,
            ForeColor = Color.FromArgb(226, 232, 240)
        };
        contextMenu.Items.Add(startupItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Exit", null, (s, e) =>
        {
            System.Windows.Application.Current.Shutdown();
        })
        {
            ForeColor = Color.FromArgb(248, 113, 113) // Pastel Red
        };
        contextMenu.Items.Add(exitItem);

        // Show menu at cursor
        TaskbarHelper.GetCursorPos(out var pt);
        contextMenu.Show(pt.X, pt.Y);
    }

    private static Icon LoadOrGenerateTrayIcon()
    {
        try
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates = {
                System.IO.Path.Combine(appDir, "App.ico"),
                System.IO.Path.Combine(appDir, "app.ico"),
                "App.ico",
                "app.ico"
            };

            foreach (var path in candidates)
            {
                if (System.IO.File.Exists(path))
                {
                    return new Icon(path);
                }
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

internal class ModernMenuRenderer : ToolStripProfessionalRenderer
{
    public ModernMenuRenderer() : base(new ModernColorTable()) { }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item.Selected)
        {
            var rc = new Rectangle(3, 1, e.Item.Width - 6, e.Item.Height - 2);
            using var b = new SolidBrush(Color.FromArgb(42, 34, 60)); // Pastel lavender dark hover
            using var borderPen = new Pen(Color.FromArgb(100, 167, 139, 250), 1);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = CreateRoundedRectangle(rc, 4);
            e.Graphics.FillPath(b, path);
            e.Graphics.DrawPath(borderPen, path);
        }
        else
        {
            base.OnRenderMenuItemBackground(e);
        }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Selected 
            ? Color.White 
            : (e.Item.ForeColor != Color.Empty && e.Item.ForeColor != SystemColors.ControlText 
                ? e.Item.ForeColor 
                : Color.FromArgb(241, 245, 249));
        e.TextFont = e.Item.Font;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rc = new Rectangle(e.ImageRectangle.X + 2, e.ImageRectangle.Y + 2, e.ImageRectangle.Width - 4, e.ImageRectangle.Height - 4);
        
        // Draw modern circular checkmark
        using var brush = new SolidBrush(Color.FromArgb(52, 211, 153)); // Pastel Mint
        e.Graphics.FillEllipse(brush, rc);

        using var pen = new Pen(Color.FromArgb(18, 20, 27), 2);
        pen.StartCap = LineCap.Round;
        pen.EndCap = LineCap.Round;
        
        float cx = rc.X + rc.Width / 2f;
        float cy = rc.Y + rc.Height / 2f;
        
        e.Graphics.DrawLines(pen, new PointF[]
        {
            new PointF(cx - 3.5f, cy),
            new PointF(cx - 1f, cy + 2.5f),
            new PointF(cx + 3.5f, cy - 2.5f)
        });
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using var pen = new Pen(Color.FromArgb(37, 45, 62), 1);
        e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(Color.FromArgb(18, 20, 27));
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    private static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int diameter = radius * 2;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal class ModernColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => Color.FromArgb(18, 20, 27);
    public override Color MenuBorder => Color.FromArgb(37, 45, 62);
    public override Color MenuItemBorder => Color.Transparent;
    public override Color MenuItemSelected => Color.FromArgb(42, 34, 60);
    public override Color MenuStripGradientBegin => Color.FromArgb(18, 20, 27);
    public override Color MenuStripGradientEnd => Color.FromArgb(18, 20, 27);
    public override Color MenuItemSelectedGradientBegin => Color.FromArgb(42, 34, 60);
    public override Color MenuItemSelectedGradientEnd => Color.FromArgb(42, 34, 60);
    public override Color MenuItemPressedGradientBegin => Color.FromArgb(35, 28, 50);
    public override Color MenuItemPressedGradientEnd => Color.FromArgb(35, 28, 50);
    public override Color ImageMarginGradientBegin => Color.FromArgb(18, 20, 27);
    public override Color ImageMarginGradientMiddle => Color.FromArgb(18, 20, 27);
    public override Color ImageMarginGradientEnd => Color.FromArgb(18, 20, 27);
    public override Color CheckBackground => Color.FromArgb(28, 32, 44);
    public override Color CheckSelectedBackground => Color.FromArgb(42, 34, 60);
    public override Color CheckPressedBackground => Color.FromArgb(35, 28, 50);
    public override Color SeparatorDark => Color.FromArgb(37, 45, 62);
    public override Color SeparatorLight => Color.Transparent;
}
