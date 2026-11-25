using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.Win32;

namespace SoundSwitcher.Core;

public class AppSettings
{
    public string? FavoriteDeviceA_Id { get; set; }
    public string? FavoriteDeviceB_Id { get; set; }
    public bool LaunchOnStartup { get; set; } = false;
    public bool DirectToggleOnTrayClick { get; set; } = false;
    public bool PlayAudioCueOnSwitch { get; set; } = false;
    public string Theme { get; set; } = ThemeManager.Dark;
}

/// <summary>Where the settings in memory came from.</summary>
public enum SettingsSource
{
    /// <summary>settings.json was read normally.</summary>
    File,
    /// <summary>settings.json was missing or unreadable; settings.json.bak (one save behind) was used.</summary>
    Backup,
    /// <summary>Nothing readable: built-in defaults.</summary>
    Defaults
}

/// <summary>The Windows "start at sign-in" entry. Behind an interface so tests never touch the real registry.</summary>
public interface IStartupEntry
{
    string? Read();
    void Write(string command);
    void Delete();
}

public sealed class RegistryStartupEntry : IStartupEntry
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SoundSwitcher";

    public string? Read()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        return key?.GetValue(ValueName)?.ToString();
    }

    public void Write(string command)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
        key?.SetValue(ValueName, command);
    }

    public void Delete()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
        if (key?.GetValue(ValueName) != null)
        {
            key.DeleteValue(ValueName, false);
        }
    }
}

public class SettingsManager
{
    /// <summary>Passed by the autostart entry, so a copy started at sign-in stays quiet if one already runs.</summary>
    public const string AutostartArgument = "--autostart";

    // Waits between save attempts when the file is briefly locked (virus scanner, OneDrive, backup tools)
    private static readonly int[] RetryDelaysMs = { 50, 100, 200, 400 };

    private readonly string _folder;
    private readonly string _filePath;
    private readonly string _backupPath;
    private readonly string _tempPath;
    private readonly string _unreadableCopyPath;
    private readonly IStartupEntry _startup;
    private readonly string _exePath;

    // The autostart entry is only synced from settings that are the user's real choice:
    // a normally read settings.json, or anything the user has changed since.
    private bool _startupChoiceIsTrusted;

    public AppSettings Settings { get; private set; } = new();
    public SettingsSource Source { get; private set; } = SettingsSource.Defaults;

    public SettingsManager()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SoundSwitcher"),
               new RegistryStartupEntry(),
               GetExecutablePath())
    {
    }

    public SettingsManager(string folder, IStartupEntry startup, string exePath)
    {
        _folder = folder;
        _filePath = Path.Combine(folder, "settings.json");
        _backupPath = _filePath + ".bak";
        _tempPath = _filePath + ".tmp";
        _unreadableCopyPath = Path.Combine(folder, "settings.unreadable.json");
        _startup = startup;
        _exePath = exePath;
        Load();
    }

    public string StartupCommand => $"\"{_exePath}\" {AutostartArgument}";

    public void Load()
    {
        _startupChoiceIsTrusted = false;

        if (TryRead(_filePath, out var fromFile, out bool fileExisted))
        {
            Settings = fromFile!;
            Source = SettingsSource.File;
            _startupChoiceIsTrusted = true;
        }
        else
        {
            if (fileExisted)
            {
                ErrorLog.Write("settings.json could not be read; a copy was kept as settings.unreadable.json");
                try { File.Copy(_filePath, _unreadableCopyPath, overwrite: true); } catch { }
            }

            if (TryRead(_backupPath, out var fromBackup, out _))
            {
                Settings = fromBackup!;
                Source = SettingsSource.Backup;
                ErrorLog.Write("Settings restored from settings.json.bak");
            }
            else
            {
                Settings = new AppSettings();
                Source = SettingsSource.Defaults;
            }

            // The backup is one save behind and defaults are a guess: show what Windows actually has
            // instead of acting on either.
            Settings.LaunchOnStartup = IsStartupEnabled();
        }

        Sanitize(Settings);

        // Self-heal the autostart entry (e.g. after the exe was moved), but only from a real choice
        SyncStartupRegistration();
    }

    /// <summary>
    /// Saves atomically (temp file, then swapped in, keeping settings.json.bak), retrying briefly while
    /// the file is locked. Returns false if it could not be saved; the settings stay in memory and the
    /// failure is logged. Call only after a user change: it also applies the autostart choice.
    /// </summary>
    public bool Save()
    {
        Sanitize(Settings);
        _startupChoiceIsTrusted = true;
        SyncStartupRegistration();

        string json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });

        for (int attempt = 0; ; attempt++)
        {
            try
            {
                Directory.CreateDirectory(_folder);
                using (var fs = new FileStream(_tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                    fs.Write(bytes, 0, bytes.Length);
                    fs.Flush(flushToDisk: true);
                }

                if (File.Exists(_filePath) && Source == SettingsSource.File)
                {
                    File.Replace(_tempPath, _filePath, _backupPath, ignoreMetadataErrors: true);
                }
                else
                {
                    // An unreadable settings.json (already copied aside) must not become the backup
                    File.Move(_tempPath, _filePath, overwrite: true);
                }

                Source = SettingsSource.File;
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt >= RetryDelaysMs.Length)
                {
                    ErrorLog.Write("Saving settings failed (file locked or not writable)", ex.Message);
                    TryDelete(_tempPath);
                    return false;
                }
                Thread.Sleep(RetryDelaysMs[attempt]);
            }
            catch (Exception ex)
            {
                ErrorLog.Write("Saving settings failed", ex);
                TryDelete(_tempPath);
                return false;
            }
        }
    }

    public bool IsStartupEnabled()
    {
        try
        {
            return !string.IsNullOrWhiteSpace(_startup.Read());
        }
        catch
        {
            return false;
        }
    }

    public void SyncStartupRegistration()
    {
        if (!_startupChoiceIsTrusted) return;

        try
        {
            if (Settings.LaunchOnStartup)
            {
                if (!string.Equals(_startup.Read(), StartupCommand, StringComparison.OrdinalIgnoreCase))
                {
                    _startup.Write(StartupCommand);
                }
            }
            else if (_startup.Read() != null)
            {
                _startup.Delete();
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Write("Updating the Windows startup entry failed", ex.Message);
        }
    }

    /// <summary>Clamps whatever a hand-edited or old settings file contains to values the app understands.</summary>
    public static void Sanitize(AppSettings s)
    {
        s.Theme = ThemeManager.Normalize(s.Theme);
        s.FavoriteDeviceA_Id = string.IsNullOrWhiteSpace(s.FavoriteDeviceA_Id) ? null : s.FavoriteDeviceA_Id.Trim();
        s.FavoriteDeviceB_Id = string.IsNullOrWhiteSpace(s.FavoriteDeviceB_Id) ? null : s.FavoriteDeviceB_Id.Trim();
    }

    private static bool TryRead(string path, out AppSettings? settings, out bool existed)
    {
        settings = null;
        existed = File.Exists(path);
        if (!existed) return false;

        for (int attempt = 0; ; attempt++)
        {
            try
            {
                settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));
                return settings != null;
            }
            catch (JsonException)
            {
                return false;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt >= RetryDelaysMs.Length) return false;
                Thread.Sleep(RetryDelaysMs[attempt]);
            }
            catch
            {
                return false;
            }
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { }
    }

    public static string GetExecutablePath()
    {
        string? procPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(procPath) && Path.GetExtension(procPath).Equals(".exe", StringComparison.OrdinalIgnoreCase) && !Path.GetFileName(procPath).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            return procPath;
        }

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string directExe = Path.Combine(baseDir, "SoundSwitcher.exe");
        if (File.Exists(directExe)) return directExe;

        DirectoryInfo? parent = Directory.GetParent(baseDir);
        while (parent != null)
        {
            string candidate = Path.Combine(parent.FullName, "SoundSwitcher.exe");
            if (File.Exists(candidate)) return candidate;
            parent = parent.Parent;
        }

        return directExe;
    }
}
