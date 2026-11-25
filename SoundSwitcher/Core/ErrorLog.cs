using System;
using System.IO;
using System.Text;

namespace SoundSwitcher.Core;

/// <summary>
/// Small capped log for problems the user would otherwise never see (failed saves, refused device
/// switches, unhandled exceptions). Lives in %APPDATA%\SoundSwitcher\errors.log; once it grows past
/// <see cref="MaxBytes"/> only the newer half is kept. Logging never throws.
/// </summary>
public static class ErrorLog
{
    public const int MaxBytes = 64 * 1024;
    private const string TrimMarker = "[older entries removed]";
    private static readonly object Gate = new();

    public static string LogPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SoundSwitcher", "errors.log");

    public static void Write(string what, object? detail = null)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                string entry = detail == null
                    ? $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {what}{Environment.NewLine}"
                    : $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {what}: {detail}{Environment.NewLine}";
                File.AppendAllText(LogPath, entry, Encoding.UTF8);
                TrimIfNeeded();
            }
        }
        catch
        {
            // Nowhere left to report a logging failure
        }
    }

    private static void TrimIfNeeded()
    {
        var info = new FileInfo(LogPath);
        if (!info.Exists || info.Length <= MaxBytes) return;

        string text = File.ReadAllText(LogPath, Encoding.UTF8);
        int keepFrom = Math.Max(0, text.Length - MaxBytes / 2);

        // Start at a whole entry
        int lineStart = text.IndexOf('\n', keepFrom);
        string kept = lineStart >= 0 ? text[(lineStart + 1)..] : "";
        File.WriteAllText(LogPath, TrimMarker + Environment.NewLine + kept, Encoding.UTF8);
    }
}
