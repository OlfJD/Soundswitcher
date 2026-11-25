using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace SoundSwitcher.Core;

/// <summary>
/// One copy per sign-in session. The first copy owns a named mutex and listens on a named event;
/// a later copy sets the event (so the first one opens its flyout) and then exits.
/// Both names use the Local\ (this session only) namespace.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    // Same object the unprefixed name used before 1.2.1 (unprefixed names live in the session namespace),
    // so a new copy still detects an older one that is running.
    public const string DefaultMutexName = @"Local\SoundSwitcher_Unique_App_Mutex";
    public const string DefaultShowEventName = @"Local\SoundSwitcher_ShowFlyout";

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);
    private const int ASFW_ANY = -1;

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showEvent;
    private RegisteredWaitHandle? _registeredWait;
    private bool _disposed;

    public bool IsFirstInstance { get; }

    public SingleInstance(string mutexName = DefaultMutexName, string showEventName = DefaultShowEventName)
    {
        _mutex = new Mutex(initiallyOwned: true, mutexName, out bool createdNew);
        bool owns = createdNew;
        if (!owns)
        {
            try
            {
                // Free (its owner closed without releasing it) or still held by a running copy?
                owns = _mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                owns = true;
            }
        }
        IsFirstInstance = owns;
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, showEventName);
    }

    /// <summary>First copy: run <paramref name="onShowRequested"/> (on a pool thread) whenever another copy starts.</summary>
    public void ListenForShowRequests(Action onShowRequested)
    {
        if (!IsFirstInstance || _registeredWait != null) return;
        _registeredWait = ThreadPool.RegisterWaitForSingleObject(
            _showEvent, (_, _) => onShowRequested(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    /// <summary>Later copy: ask the running copy to show itself.</summary>
    public void RequestShow()
    {
        // This copy was just started by the user, so it may hand the foreground to the running one
        AllowSetForegroundWindow(ASFW_ANY);
        _showEvent.Set();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _registeredWait?.Unregister(null);
        if (IsFirstInstance)
        {
            try { _mutex.ReleaseMutex(); } catch { }
        }
        _mutex.Dispose();
        _showEvent.Dispose();
    }
}
