using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using SoundSwitcher.Core;

namespace SoundSwitcher.Views;

/// <summary>
/// Right-click tray menu. A themed WPF window instead of a WinForms ContextMenuStrip, so it uses the
/// same colours, outlines, font and scale as the flyout. Hides itself when it loses focus.
/// </summary>
public partial class TrayMenuWindow : Window
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;

    public TrayMenuWindow()
    {
        InitializeComponent();
        Deactivated += (s, e) => Hide();
        PreviewKeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Hide();
                e.Handled = true;
            }
        };
        // Reused for every right-click (Alt+F4 only hides it), same as the flyout
        Closing += (s, e) =>
        {
            e.Cancel = true;
            Hide();
        };
    }

    /// <summary>Opens the menu with its bottom-right corner at the cursor, kept inside the monitor's work area.</summary>
    public void ShowAtCursor()
    {
        TaskbarHelper.GetCursorPos(out var cursor);

        Opacity = 0;
        if (!IsVisible) Show();
        var hwnd = new WindowInteropHelper(this).Handle;

        // Move onto the cursor's monitor first so WPF picks up that monitor's DPI, then place precisely
        // in physical pixels (PerMonitorV2: Left/Top in DIPs don't map 1:1 across monitors).
        SetWindowPos(hwnd, HWND_TOPMOST, cursor.X, cursor.Y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
        UpdateLayout();
        Place(hwnd, cursor);
        UpdateLayout();
        Place(hwnd, cursor);

        Opacity = 1;
        SetForegroundWindow(hwnd);
        Activate();
        ExitButton.Focus();
    }

    private void Place(IntPtr hwnd, TaskbarHelper.POINT cursor)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        int w = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX);
        int h = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY);
        // The window has a transparent margin for the shadow; let the visible panel touch the cursor
        int mx = (int)Math.Round(Root.Margin.Right * dpi.DpiScaleX);
        int my = (int)Math.Round(Root.Margin.Bottom * dpi.DpiScaleY);

        var work = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(cursor.X, cursor.Y)).WorkingArea;
        int x = Math.Clamp(cursor.X - w + mx, work.Left - mx, Math.Max(work.Left - mx, work.Right - w + mx));
        int y = cursor.Y - h + my;
        if (y < work.Top - my) y = cursor.Y - my; // not enough room above (taskbar at the top): open below
        y = Math.Clamp(y, work.Top - my, Math.Max(work.Top - my, work.Bottom - h + my));

        SetWindowPos(hwnd, HWND_TOPMOST, x, y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        System.Windows.Application.Current.Shutdown();
    }
}
