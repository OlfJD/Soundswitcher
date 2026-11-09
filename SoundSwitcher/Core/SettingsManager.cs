using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace SoundSwitcher.Core;

public class AppSettings
{
    public string? FavoriteDeviceA_Id { get; set; }
    public string? FavoriteDeviceB_Id { get; set; }
    public bool LaunchOnStartup { get; set; } = false;
    public bool DirectToggleOnTrayClick { get; set; } = false;
    public bool PlayAudioCueOnSwitch { get; set; } = false;
}

public class SettingsManager
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SoundSwitcher"
    );
    private static readonly string SettingsFilePath = Path.Combine(SettingsFolder, "settings.json");
    private const string StartupRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "SoundSwitcher";

    public AppSettings Settings { get; private set; } = new();

    public SettingsManager()
    {
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    Settings = loaded;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
        }

        // Self-heal and synchronize registry with current executable location
        SyncStartupRegistration(Settings.LaunchOnStartup);
    }

    public void SyncStartupRegistration()
    {
        SyncStartupRegistration(Settings.LaunchOnStartup);
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);
            string json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
            SyncStartupRegistration(Settings.LaunchOnStartup);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
        }
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

    public static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, false);
            var val = key?.GetValue(AppName)?.ToString();
            return !string.IsNullOrWhiteSpace(val);
        }
        catch
        {
            return false;
        }
    }

    public static void SyncStartupRegistration(bool shouldEnable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
            if (key == null) return;

            string currentExe = GetExecutablePath();
            string expectedVal = $"\"{currentExe}\"";

            if (shouldEnable)
            {
                var currentVal = key.GetValue(AppName)?.ToString();
                if (!string.Equals(currentVal?.Trim('\"'), currentExe, StringComparison.OrdinalIgnoreCase))
                {
                    key.SetValue(AppName, expectedVal);
                }
            }
            else
            {
                if (key.GetValue(AppName) != null)
                {
                    key.DeleteValue(AppName, false);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error configuring startup: {ex.Message}");
        }
    }

    public static void SetStartup(bool enable)
    {
        SyncStartupRegistration(enable);
    }
}
