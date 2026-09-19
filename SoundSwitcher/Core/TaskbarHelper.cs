using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;

namespace SoundSwitcher.Core;

public static class TaskbarHelper
{
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    public static System.Windows.Point GetIdealFlyoutPosition(double windowWidth, double windowHeight)
    {
        // Default to work area of current screen under cursor
        GetCursorPos(out POINT cursor);
        var screen = Screen.FromPoint(new System.Drawing.Point(cursor.X, cursor.Y));
        var workArea = screen.WorkingArea;
        var bounds = screen.Bounds;

        // Convert screen bounds to WPF device-independent units
        double dpiScaleX = 1.0;
        double dpiScaleY = 1.0;

        try
        {
            using var g = Graphics.FromHwnd(IntPtr.Zero);
            dpiScaleX = g.DpiX / 96.0;
            dpiScaleY = g.DpiY / 96.0;
        }
        catch { }

        double workLeft = workArea.Left / dpiScaleX;
        double workTop = workArea.Top / dpiScaleY;
        double workRight = workArea.Right / dpiScaleX;
        double workBottom = workArea.Bottom / dpiScaleY;

        double targetX;
        double targetY;

        // Determine taskbar position (Bottom, Top, Left, Right)
        // With Grid Margin="6" in FlyoutWindow, +2 X and +3 Y places the visible border ~4px from screen edge and ~3px above taskbar
        if (workArea.Bottom < bounds.Bottom)
        {
            // Taskbar at bottom
            targetX = workRight - windowWidth + 2;
            targetY = workBottom - windowHeight + 3;
        }
        else if (workArea.Top > bounds.Top)
        {
            // Taskbar at top
            targetX = workRight - windowWidth + 2;
            targetY = workTop - 3;
        }
        else if (workArea.Right < bounds.Right)
        {
            // Taskbar at right
            targetX = workRight - windowWidth - 2;
            targetY = workBottom - windowHeight + 3;
        }
        else
        {
            // Taskbar at left or auto-hide
            targetX = workRight - windowWidth + 2;
            targetY = workBottom - windowHeight + 3;
        }

        // Clamp to screen bounds allowing edge hugging
        targetX = Math.Max(workLeft - 4, Math.Min(targetX, workRight - windowWidth + 4));
        targetY = Math.Max(workTop - 4, Math.Min(targetY, workBottom - windowHeight + 4));

        return new System.Windows.Point(targetX, targetY);
    }
}
