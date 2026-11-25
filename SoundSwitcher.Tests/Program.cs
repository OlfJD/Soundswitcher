using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using SoundSwitcher.Core;

// Tests for the robustness work in 1.2.1. Everything runs in a temp folder with a fake startup entry,
// so the real settings, registry and audio devices are never changed.
//   dotnet run --project SoundSwitcher.Tests                 unit tests
//   dotnet run --project SoundSwitcher.Tests -- --app <exe>  also starts the real exe twice (second launch)

var root = Path.Combine(Path.GetTempPath(), "SoundSwitcherTests-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(root);
ErrorLog.LogPath = Path.Combine(root, "errors.log");

var t = new TestRunner();
const string Exe = @"C:\Apps\SoundSwitcher.exe";

string NewFolder(string name)
{
    var dir = Path.Combine(root, name);
    Directory.CreateDirectory(dir);
    return dir;
}

string Json(AppSettings s) => System.Text.Json.JsonSerializer.Serialize(s);

// ---------- Settings: saving ----------

t.Run("Save then load returns the same settings", () =>
{
    var dir = NewFolder("roundtrip");
    var sm = new SettingsManager(dir, new FakeStartup(), Exe);
    T.Equal(SettingsSource.Defaults, sm.Source);
    sm.Settings.Theme = ThemeManager.Stormtrooper;
    sm.Settings.FavoriteDeviceA_Id = "dev-a";
    T.True(sm.Save(), "Save returned false");

    var again = new SettingsManager(dir, new FakeStartup(), Exe);
    T.Equal(SettingsSource.File, again.Source);
    T.Equal(ThemeManager.Stormtrooper, again.Settings.Theme);
    T.Equal("dev-a", again.Settings.FavoriteDeviceA_Id);
    T.False(File.Exists(Path.Combine(dir, "settings.json.tmp")), "temp file left behind");
});

t.Run("Each save keeps the previous file as settings.json.bak", () =>
{
    var dir = NewFolder("backup");
    var sm = new SettingsManager(dir, new FakeStartup(), Exe);
    sm.Settings.Theme = ThemeManager.Dark;
    sm.Save();
    sm.Settings.Theme = ThemeManager.Stormtrooper;
    sm.Save();

    T.Contains("Stormtrooper", File.ReadAllText(Path.Combine(dir, "settings.json")));
    T.Contains("Dark", File.ReadAllText(Path.Combine(dir, "settings.json.bak")));
});

t.Run("A briefly locked settings file is retried and saved", () =>
{
    var dir = NewFolder("locked-briefly");
    var sm = new SettingsManager(dir, new FakeStartup(), Exe);
    sm.Save();

    var path = Path.Combine(dir, "settings.json");
    var lockStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
    _ = Task.Delay(150).ContinueWith(_ => lockStream.Dispose());

    sm.Settings.Theme = ThemeManager.Stormtrooper;
    T.True(sm.Save(), "Save gave up although the lock was released after 150 ms");
    T.Contains("Stormtrooper", File.ReadAllText(path));
});

t.Run("A file that stays locked fails quietly, keeps the change in memory and logs it", () =>
{
    var dir = NewFolder("locked");
    var sm = new SettingsManager(dir, new FakeStartup(), Exe);
    sm.Save();

    var path = Path.Combine(dir, "settings.json");
    using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
    {
        sm.Settings.Theme = ThemeManager.Stormtrooper;
        T.False(sm.Save(), "Save reported success on a locked file");
    }
    T.Equal(ThemeManager.Stormtrooper, sm.Settings.Theme);
    T.Contains("Saving settings failed", File.ReadAllText(ErrorLog.LogPath));
    T.False(File.Exists(path + ".tmp"), "temp file left behind");
});

// ---------- Settings: loading and the startup entry ----------

t.Run("Broken settings.json falls back to the backup, but not for the startup entry", () =>
{
    // The user had autostart off (now in .bak), switched it on (saved), then the PC crashed while
    // saving something else and left settings.json broken.
    var dir = NewFolder("broken-with-backup");
    var startup = new FakeStartup();
    var sm = new SettingsManager(dir, startup, Exe);
    sm.Settings.Theme = ThemeManager.Stormtrooper;
    sm.Settings.FavoriteDeviceA_Id = "dev-a";
    sm.Settings.LaunchOnStartup = false;
    sm.Save();
    sm.Settings.LaunchOnStartup = true;
    sm.Save();
    T.Equal(sm.StartupCommand, startup.Value);

    File.WriteAllText(Path.Combine(dir, "settings.json"), "{\"FavoriteDeviceA_Id\": \"dev");
    startup.Changes = 0;

    var loaded = new SettingsManager(dir, startup, Exe);
    T.Equal(SettingsSource.Backup, loaded.Source);
    T.Equal("dev-a", loaded.Settings.FavoriteDeviceA_Id);
    T.Equal(ThemeManager.Stormtrooper, loaded.Settings.Theme);
    T.True(loaded.Settings.LaunchOnStartup, "LaunchOnStartup should show the real Windows entry, not the stale backup");
    T.Equal(0, startup.Changes);
    T.Equal(sm.StartupCommand, startup.Value);
    T.True(File.Exists(Path.Combine(dir, "settings.unreadable.json")), "broken file was not kept aside");
});

t.Run("Saving after a backup restore replaces the broken file and keeps the good backup", () =>
{
    var dir = NewFolder("restore-then-save");
    var sm = new SettingsManager(dir, new FakeStartup(), Exe);
    sm.Settings.Theme = ThemeManager.Stormtrooper;
    sm.Save();
    sm.Save();
    File.WriteAllText(Path.Combine(dir, "settings.json"), "");

    var loaded = new SettingsManager(dir, new FakeStartup(), Exe);
    T.Equal(SettingsSource.Backup, loaded.Source);
    loaded.Settings.FavoriteDeviceB_Id = "dev-b";
    T.True(loaded.Save(), "Save failed");

    var after = new SettingsManager(dir, new FakeStartup(), Exe);
    T.Equal(SettingsSource.File, after.Source);
    T.Equal("dev-b", after.Settings.FavoriteDeviceB_Id);
    T.Contains("Stormtrooper", File.ReadAllText(Path.Combine(dir, "settings.json.bak")));
});

t.Run("Unreadable settings and no backup: defaults, startup entry left alone", () =>
{
    var dir = NewFolder("broken-no-backup");
    File.WriteAllText(Path.Combine(dir, "settings.json"), "not json");
    var startup = new FakeStartup { Value = "\"C:\\Apps\\SoundSwitcher.exe\"" };

    var sm = new SettingsManager(dir, startup, Exe);
    T.Equal(SettingsSource.Defaults, sm.Source);
    T.True(sm.Settings.LaunchOnStartup, "should reflect the existing Windows entry");
    T.Equal(0, startup.Changes);
});

t.Run("First run (no file) does not remove an existing startup entry", () =>
{
    var dir = NewFolder("first-run");
    var startup = new FakeStartup { Value = "\"C:\\Apps\\SoundSwitcher.exe\"" };
    var sm = new SettingsManager(dir, startup, Exe);
    T.Equal(0, startup.Changes);
    T.True(sm.Settings.LaunchOnStartup, "should reflect the existing Windows entry");
});

t.Run("A normally read file still repairs the startup entry (moved exe, old format)", () =>
{
    var dir = NewFolder("self-heal");
    File.WriteAllText(Path.Combine(dir, "settings.json"), Json(new AppSettings { LaunchOnStartup = true }));
    var startup = new FakeStartup { Value = "\"C:\\Old\\SoundSwitcher.exe\"" };

    var sm = new SettingsManager(dir, startup, Exe);
    T.Equal("\"C:\\Apps\\SoundSwitcher.exe\" --autostart", startup.Value);
    T.Equal(sm.StartupCommand, startup.Value);
});

t.Run("Switching autostart off removes the entry", () =>
{
    var dir = NewFolder("autostart-off");
    var startup = new FakeStartup();
    var sm = new SettingsManager(dir, startup, Exe);
    sm.Settings.LaunchOnStartup = true;
    sm.Save();
    T.Equal(sm.StartupCommand, startup.Value);
    sm.Settings.LaunchOnStartup = false;
    sm.Save();
    T.Equal(null, startup.Value);
});

t.Run("Odd values from a hand-edited file are cleaned up", () =>
{
    var dir = NewFolder("sanitize");
    File.WriteAllText(Path.Combine(dir, "settings.json"),
        "{\"Theme\": \"Neon\", \"FavoriteDeviceA_Id\": \"   \", \"FavoriteDeviceB_Id\": null}");
    var sm = new SettingsManager(dir, new FakeStartup(), Exe);
    T.Equal(ThemeManager.Dark, sm.Settings.Theme);
    T.Equal(null, sm.Settings.FavoriteDeviceA_Id);

    File.WriteAllText(Path.Combine(dir, "settings.json"), "{\"Theme\": null}");
    sm = new SettingsManager(dir, new FakeStartup(), Exe);
    T.Equal(ThemeManager.Dark, sm.Settings.Theme);
});

// ---------- Error log ----------

t.Run("errors.log is capped and keeps the newest entries", () =>
{
    var original = ErrorLog.LogPath;
    ErrorLog.LogPath = Path.Combine(NewFolder("log"), "errors.log");
    try
    {
        for (int i = 0; i < 1500; i++)
        {
            ErrorLog.Write($"entry {i:D4}", new string('x', 80));
        }
        long size = new FileInfo(ErrorLog.LogPath).Length;
        string text = File.ReadAllText(ErrorLog.LogPath);
        T.True(size <= ErrorLog.MaxBytes, $"log is {size} bytes");
        T.Contains("entry 1499", text);
        T.False(text.Contains("entry 0000"), "oldest entry should be gone");
        T.True(text.StartsWith("[older entries removed]"), "marker missing");
    }
    finally
    {
        ErrorLog.LogPath = original;
    }
});

// ---------- Single instance ----------

t.Run("Second copy is refused and wakes the first copy", () =>
{
    string id = Guid.NewGuid().ToString("N");
    string mutex = $@"Local\SST_{id}_m", show = $@"Local\SST_{id}_e";

    using var first = new SingleInstance(mutex, show);
    T.True(first.IsFirstInstance, "first copy did not get the claim");
    using var woken = new ManualResetEventSlim();
    first.ListenForShowRequests(() => woken.Set());

    // Mutex ownership is per thread, so the second copy runs on its own thread like a separate process
    bool secondWasFirst = true;
    OnOwnThread(() =>
    {
        using var second = new SingleInstance(mutex, show);
        secondWasFirst = second.IsFirstInstance;
        if (!second.IsFirstInstance) second.RequestShow();
    });

    T.False(secondWasFirst, "second copy also got the claim");
    T.True(woken.Wait(2000), "first copy was not asked to show itself");
});

t.Run("A copy that died without releasing the claim does not block the next start", () =>
{
    string id = Guid.NewGuid().ToString("N");
    string mutex = $@"Local\SST_{id}_m", show = $@"Local\SST_{id}_e";

    SingleInstance? crashed = null;
    OnOwnThread(() => crashed = new SingleInstance(mutex, show)); // thread ends without Dispose

    using var next = new SingleInstance(mutex, show);
    T.True(next.IsFirstInstance, "abandoned claim blocked the next start");
    GC.KeepAlive(crashed);
});

// ---------- Device switching ----------

t.Run("Switching to a device that doesn't exist reports failure and logs why", () =>
{
    using var audio = new AudioController();
    bool ok = audio.SetDefaultPlaybackDevice("{0.0.0.00000000}.{00000000-0000-0000-0000-000000000000}");
    T.False(ok, "switch to a non-existent device reported success");
    T.Contains("output device", File.ReadAllText(ErrorLog.LogPath));
});

// ---------- Real exe (optional) ----------

int appIndex = Array.IndexOf(args, "--app");
if (appIndex >= 0 && appIndex + 1 < args.Length)
{
    string appExe = Path.GetFullPath(args[appIndex + 1]);
    t.Run("Real exe: second launch opens the running copy's flyout; an autostart launch stays quiet", () =>
        AppLaunchTest.Run(appExe));
}

try { Directory.Delete(root, recursive: true); } catch { }
return t.Summary();

static void OnOwnThread(Action action)
{
    Exception? error = null;
    var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
    thread.Start();
    thread.Join();
    if (error != null) throw error;
}

sealed class FakeStartup : IStartupEntry
{
    public string? Value { get; set; }
    public int Changes { get; set; }
    public string? Read() => Value;
    public void Write(string command) { Value = command; Changes++; }
    public void Delete() { Value = null; Changes++; }
}

static class AppLaunchTest
{
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int max);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private static bool FlyoutVisible(int pid)
    {
        bool found = false;
        EnumWindows((h, _) =>
        {
            GetWindowThreadProcessId(h, out uint owner);
            if (owner != pid || !IsWindowVisible(h)) return true;
            var sb = new StringBuilder(64);
            GetWindowText(h, sb, sb.Capacity);
            if (sb.ToString() == "SoundSwitcher") { found = true; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    private static bool WaitFor(Func<bool> condition, int ms)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms)
        {
            if (condition()) return true;
            Thread.Sleep(100);
        }
        return condition();
    }

    public static void Run(string exe)
    {
        T.True(Process.GetProcessesByName("SoundSwitcher").Length == 0, "a SoundSwitcher copy is already running; close it first");
        using var first = Process.Start(exe)!;
        try
        {
            Thread.Sleep(2500);
            T.False(first.HasExited, "first copy exited");
            T.False(FlyoutVisible(first.Id), "flyout should start hidden in the tray");

            using (var quiet = Process.Start(exe, SettingsManager.AutostartArgument)!)
            {
                T.True(quiet.WaitForExit(5000), "autostart copy did not exit");
            }
            Thread.Sleep(500);
            T.False(FlyoutVisible(first.Id), "an autostart launch should not open the flyout");

            using (var second = Process.Start(exe)!)
            {
                T.True(second.WaitForExit(5000), "second copy did not exit");
            }
            T.True(WaitFor(() => FlyoutVisible(first.Id), 3000), "second launch did not open the running copy's flyout");
            T.False(first.HasExited, "first copy exited");
        }
        finally
        {
            try { first.Kill(); first.WaitForExit(3000); } catch { }
        }
    }
}

static class T
{
    public static void True(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static void False(bool condition, string message) { if (condition) throw new Exception(message); }
    public static void Equal<TV>(TV expected, TV actual)
    {
        if (!EqualityComparer<TV>.Default.Equals(expected, actual))
            throw new Exception($"expected <{expected}> but got <{actual}>");
    }
    public static void Contains(string expected, string actual)
    {
        if (!actual.Contains(expected)) throw new Exception($"expected text containing <{expected}>");
    }
}

sealed class TestRunner
{
    private int _passed, _failed;

    public void Run(string name, Action test)
    {
        try
        {
            test();
            _passed++;
            Console.WriteLine($"  PASS  {name}");
        }
        catch (Exception ex)
        {
            _failed++;
            Console.WriteLine($"  FAIL  {name}\n        {ex.Message}");
        }
    }

    public int Summary()
    {
        Console.WriteLine($"\n{_passed} passed, {_failed} failed");
        return _failed == 0 ? 0 : 1;
    }
}
