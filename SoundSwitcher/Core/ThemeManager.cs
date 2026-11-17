using System;
using System.Windows;

namespace SoundSwitcher.Core;

/// <summary>
/// Swaps the colour dictionary (Themes/*.xaml) merged into the application resources.
/// Everything that paints a themed colour uses DynamicResource, so a swap restyles open windows immediately.
/// </summary>
public static class ThemeManager
{
    public const string Dark = "Dark";
    public const string Stormtrooper = "Stormtrooper";

    public static string Normalize(string? name) =>
        string.Equals(name, Stormtrooper, StringComparison.OrdinalIgnoreCase) ? Stormtrooper : Dark;

    public static void Apply(string? name)
    {
        var app = System.Windows.Application.Current;
        if (app == null) return;

        var theme = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/SoundSwitcher;component/Themes/{Normalize(name)}.xaml", UriKind.Absolute)
        };

        var merged = app.Resources.MergedDictionaries;
        if (merged.Count > 0)
        {
            merged[0] = theme;
        }
        else
        {
            merged.Add(theme);
        }
    }
}
