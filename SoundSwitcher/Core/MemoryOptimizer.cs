using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace SoundSwitcher.Core;

public static class MemoryOptimizer
{
    [DllImport("kernel32.dll", EntryPoint = "SetProcessWorkingSetSize", ExactSpelling = true, SetLastError = true)]
    private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

    private static DispatcherTimer? _idleTimer;

    public static void Initialize()
    {
        _idleTimer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
        {
            Interval = TimeSpan.FromMinutes(2)
        };
        _idleTimer.Tick += (s, e) => TrimWorkingSet();
        _idleTimer.Start();
    }

    public static void TrimWorkingSet()
    {
        try
        {
            GC.Collect(1, GCCollectionMode.Optimized, false, false);
            using var process = Process.GetCurrentProcess();
            SetProcessWorkingSetSize(process.Handle, (IntPtr)(-1), (IntPtr)(-1));
        }
        catch { }
    }
}
