# SoundSwitcher - Developer Log & Technical Specification

## Project Overview
SoundSwitcher is a high-performance, ultra-lightweight Windows desktop audio switcher and floating pill widget built with C# and WPF on .NET 10. It is engineered to sit discreetly in the Windows system tray with a near-zero memory footprint (~6–10 MB RAM), providing instant one-click switching between primary audio devices (e.g. Headphones & Speakers), master volume control, and real-time CoreAudio hardware synchronization.

---

## Architecture & Subsystems

1. **Windows CoreAudio & COM Interop Subsystem (`Core/`)**:
   - `AudioNative.cs`: Direct COM interface definitions for Windows CoreAudio APIs (`IMMDeviceEnumerator`, `IMMDevice`, `IAudioEndpointVolume`, `IAudioEndpointVolumeCallback`, `IPolicyConfig`, `IMMNotificationClient`).
     - Utilizes `[PreserveSig] int` on `IMMNotificationClient` methods to avoid COM marshaling exceptions when handling Windows audio topology changes.
     - Implements `IPolicyConfig` for fast default playback device swapping (`SetDefaultEndpoint`) across Multimedia, Communications, and Console roles.
   - `AudioController.cs`: High-level wrapper managing audio device enumeration, active device detection, master volume retrieval/setting, hardware mute toggling, and asynchronous notification dispatching.

2. **Low-RAM Working Set Engine (`Core/MemoryOptimizer.cs`)**:
   - Manages memory optimization without freezing the WPF STA UI thread:
     - Triggers non-blocking Gen-1 garbage collection (`GC.Collect(1, GCCollectionMode.Optimized, false, false)`).
     - Calls native Win32 `SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1)` via `kernel32.dll` to purge unused page tables and return working set RAM to the OS.
     - Features an automatic 2-minute idle timer that silently trims memory after periods of user inactivity.
     - Immediately trims working set when the flyout widget is closed or minimized (`IsVisibleChanged`).

3. **Settings & Persistence Layer (`Core/SettingsManager.cs`)**:
   - Manages JSON serialization/deserialization to `%APPDATA%\SoundSwitcher\settings.json`.
   - **Self-Healing Windows Autostart Subsystem**:
     - Resolves the true executable path via `GetExecutablePath()` across single-file publish bundles and standard binaries.
     - Automatically verifies and refreshes `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\SoundSwitcher` on startup and configuration saves, seamlessly healing registry paths if the folder is relocated (e.g., moved to `Documents\Code\Soundswitcher\`).
     - The Run entry is `"<exe>" --autostart` and is only written or removed from settings that are the user's real choice (a normally read `settings.json`, or a change made since). A backup or defaults never touch it.
   - Saves are atomic (`settings.json.tmp` swapped in with `File.Replace`, previous file kept as `settings.json.bak`) and retried for ~750 ms if the file is locked. An unreadable `settings.json` is kept as `settings.unreadable.json` and the backup is used.
   - Errors go to `%APPDATA%\SoundSwitcher\errors.log` (`Core/ErrorLog.cs`, capped at 64 KB, newer half kept), never to dialogs.

4. **UI & Vector Visual System (`Views/` & `App.xaml`)**:
   - `FlyoutWindow.xaml` / `.xaml.cs`: Draggable floating flyout with `HWND_TOPMOST` layering, monochrome dark theme, two device tiles (click one to make it the default output), Lucide vector icons, expandable settings panel, and master volume slider.
   - Typeface: Inter (Regular/Medium/SemiBold static TTFs, SIL OFL) embedded as a WPF resource from `Fonts/`, with Segoe UI Variable as fallback.
   - `App.xaml` / `App.xaml.cs`: Application entry point managing Windows Notification Area (System Tray) icon (`NotifyIcon`), offscreen host window for keeping the WPF message pump alive in tray-only mode, and single-instance handling (`Core/SingleInstance.cs`: `Local\` mutex plus a "show" event, so launching the exe again opens the running copy's flyout).

5. **Tests (`SoundSwitcher.Tests/`)**: a plain console runner (no NuGet packages). `dotnet run --project SoundSwitcher.Tests` runs the unit tests in a temp folder with a fake startup entry, so the real settings and registry are never touched. Add `-- --app <path to SoundSwitcher.exe>` to also start the real exe twice and check the second-launch behaviour (close any running copy first).

---

## Changelog & Evolution

### Version 1.0.0
- **Initial Architecture & CoreAudio Integration**:
  - Implemented COM interop with Windows CoreAudio (`IMMDeviceEnumerator`, `IPolicyConfig`, `IAudioEndpointVolume`).
  - Created minimalist floating pill widget with toggle switch between Device 1 (Off state) and Device 2 (On state).
  - Built expandable inline settings panel with device dropdown selectors, master volume slider, mute toggle, and autostart switch.
  - Implemented system tray integration (`NotifyIcon`) with left-click toggle/flyout and right-click context menu.
  - Added vector Lucide `volume-2`, `volume-x`, `settings`, and `sliders` iconography.

### Version 1.0.1 - Low-RAM Working Set Reclaim & Self-Healing Autostart (2026-09-20)
- **Low-RAM Working Set Reclaim Engine (`Core/MemoryOptimizer.cs`)**:
  - Fixed memory retention issue where opening the flyout widget from the tray caused RAM to rise to ~60 MB and remain high indefinitely.
  - Implemented automated working set reduction on flyout dismiss (`IsVisibleChanged` / `Closing` event hooks) via `SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1)`.
  - Added background 2-minute idle memory trim timer.
  - Memory footprint now drops back to **~8–9 MB** immediately upon closing the flyout widget.
- **Self-Healing Windows Autostart Engine (`Core/SettingsManager.cs`)**:
  - Implemented dynamic executable path resolution via `GetExecutablePath()` with multi-level parent directory searching.
  - Added automatic path verification and update in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\SoundSwitcher` during startup and setting updates.
  - Resolves broken autostart when repository folder is relocated or renamed.
- **System32 Working Directory Hardening & Crash Logging (`App.xaml.cs`)**:
  - Resolved relative path issues for `App.ico` and `crash.log` by anchoring all paths to `AppDomain.CurrentDomain.BaseDirectory` / `%APPDATA%`, preventing crashes when Windows boots applications with `C:\Windows\System32` as the working directory.
- **CoreAudio COM Notification Client Hardening (`Core/AudioNative.cs`, `Core/AudioController.cs`)**:
  - Refactored `IMMNotificationClient` interface method signatures to `[PreserveSig] int` to prevent CLR COM interop marshaling exceptions when audio endpoints change dynamically.
- **WPF Dispatcher & Lifecycle Stability (`App.xaml.cs`)**:
  - Configured offscreen host window to guarantee the WPF dispatcher message pump persists cleanly during tray-only execution.

### Version 1.0.2 - Notification Threading, Tray Menu & Responsiveness Fixes (2026-11-04)
- **CoreAudio callback deadlock risk removed (`Core/AudioController.cs`)**:
  - `IMMNotificationClient` callbacks arrive on a CoreAudio MTA thread. The flyout handled them with a blocking `Dispatcher.Invoke`, and the tray handler enumerated (and released) devices inside the callback; both break CoreAudio's callback rules, and a blocked callback can deadlock `UnregisterEndpointNotificationCallback` on exit.
  - Callbacks now only queue a notification. Bursts (one callback per role on each switch, plus property changes, plus the manual notify after `SetDefaultEndpoint`) are coalesced into one `DevicesChanged` raised asynchronously on the UI thread ~120 ms later. Previously a single switch could trigger 8+ full device enumerations.
- **Volume slider no longer re-enumerates every device per drag step (`Views/FlyoutWindow.xaml.cs`)**: the default device is cached by `RefreshUI`. `RefreshUI` and `ToggleBetween` also stopped enumerating twice.
- **Toggle switch stayed disabled** after "No audio devices detected" until restart; it is re-enabled when devices return.
- **Tray context menu (`Core/TrayManager.cs`)**: one menu rebuilt on open and attached to the `NotifyIcon` instead of a new `ContextMenuStrip` + fonts on every right-click (handle leak). Attaching it also lets WinForms make the tray window foreground so the menu closes when clicking elsewhere.
- Mouse wheel over the volume slider adjusts volume in 2 % steps.
- Mute/settings icon geometries and brushes are parsed once and frozen instead of re-created on every refresh.
- `MemoryOptimizer` disposes the `Process` object it creates on each trim.

### Version 1.1.0 - Minimal UI Redesign (2026-11-17)
- Picked direction **E (device tiles), Mono accent, Dark, Inter** from the redesign board (`design/redesign-board.html`, which has the other directions in case they are revisited).
- The red/green toggle is replaced by two device tiles showing role ("Speakers") and adapter ("Realtek Audio") separately; the active one has a white outline and check badge.
- New palette (neutral greys, white as the only accent), thin volume slider, label-left switches, flat device pickers, footer text links.
- Tile icons come from the endpoint form factor, with a name-based fallback because many USB headsets report "Speakers".
- Inter is embedded (~1.2 MB in the exe); `Fonts/LICENSE.txt` carries the OFL.
- **Themes**: colours moved to `Themes/Dark.xaml` and `Themes/Stormtrooper.xaml` (white shell, black details, the active tile inverts to solid black). `Core/ThemeManager.cs` swaps the merged dictionary at runtime; all themed colours use `DynamicResource`, so switching in Settings → Theme restyles the open flyout instantly. The choice is saved as `Theme` in `settings.json`.

### Version 1.2.0 - Armour Stormtrooper, Sharper Rendering, App Icon (2026-11-17)
- Picked **D (Armour)** and **icon 4 (toggle, enlarged) on a Stormtrooper tile** from `design/stormtrooper-icon-board.html`.
- **Stormtrooper theme is now "Armour"**: white shell, Google-hardware greys (`#E8EAED` plates), and bold 2 px pure-black outlines on the window, every settings card, both tiles, the theme selector, switches and the device dropdown. Dividers are 2 px black, the volume track is 6 px with a white, black-ringed thumb. Outline weights live in the theme files (`OutlineThickness`, `TileBorderThickness`, `ControlBorderThickness`, `DividerHeight`, `TrackHeight`, `ThumbSize` …), so Dark is unchanged.
- **Fixed: light theme looked pixelated.** The drop shadow was an `Effect` on the panel that held all the content, so every line of text was rendered through an offscreen bitmap; on white that showed as jagged text. The shadow is now its own layer behind the panel. Text uses `Ideal` formatting with grayscale anti-aliasing (`Display` mode looks crunchy without ClearType, which layered windows don't get, and isn't meant for scaled text).
- **Flyout 10 % bigger**: one `UiScale` layout transform on the whole panel (and on the dropdown, which is its own popup), so everything stays vector-sharp. Window width 344 → 378.
- **App icon**: `generate_icon.ps1` (repo root) draws a white tile with a black outline and a large toggle switch, flipped on, in the same flat style as MacroMaster and SmartNotes. Every size is rendered natively with outlines snapped to whole pixels. Output: `SoundSwitcher/Assets/app.ico` / `app.png` and `App.ico` in the repo root.
  - The icon is now the exe icon (`ApplicationIcon`) and embedded as a resource; the tray loads it at the tray's real size. The app no longer writes a generated `App.ico` next to the exe (the old blue gradient `IconGenerator.GenerateAppIcon` is removed).
- **Tray right-click menu redone (`Views/TrayMenuWindow.xaml`)**: the old WinForms `ContextMenuStrip` (purple/mint 1.0-era colours, device list, toggle, startup) is replaced by a small themed WPF window with a single **Exit** item. It uses the same theme resources, Inter, outline and 110 % scale as the flyout, so it matches Dark and Stormtrooper. It opens at the cursor (bottom-right corner on the cursor, kept on the cursor's monitor, placed in physical pixels for mixed-DPI setups) and closes on Esc or clicking elsewhere. Device switching and settings stay in the flyout. The custom `ModernMenuRenderer` / `ModernColorTable` are removed.
- **README rewritten (1.2.0)**: short and minimal, one screenshot (`docs/screenshot.png`, both themes side by side, rendered from the real XAML at 2x), the new icon, a direct download link to `SoundSwitcher.exe` on the latest release (the old link pointed at a versioned zip name that would break on every release).

### Version 1.2.1 - Safer Settings, Visible Failures, Second Launch (2026-11-25)
Applied the SoundSwitcher items from `..\Lessons-from-Blur-AutoClicker.md`.
- **Fixed: a crash during a save could switch autostart off.** `settings.json` was written in place, so a crash or power cut could leave it empty. The next start then loaded defaults and deleted the Windows Run entry, silently losing autostart and the device pair. Saves are now atomic with a `.bak`, an unreadable file falls back to the backup, and the Run entry is only changed from a real choice; otherwise the Start with Windows switch shows what Windows actually has.
- **Fixed: refused device switches went unnoticed.** `SetDefaultEndpoint`'s HRESULT was ignored, so a refused switch counted as success and the tile showed the wrong device. Every role's result is now checked; on failure the tile goes back, the header shows "Couldn't switch to …" for 5 s (tray direct-toggle shows a balloon instead), and the codes are logged.
- **Second launch opens the flyout** of the running copy instead of exiting silently. A copy started at sign-in (`--autostart`) still exits quietly. The mutex keeps its old name, so a 1.2.0 copy that is still running is detected too.
- **errors.log** in `%APPDATA%\SoundSwitcher` replaces the unbounded `crash.log` next to the exe; saving failures show "Couldn't save settings" in the flyout.
- Settings are sanitised on load (unknown theme → Dark, blank device IDs → none).
- **Tests**: new `SoundSwitcher.Tests` (16 tests, all passing): atomic save and backup, brief and permanent file locks, broken file with and without backup, first run with an existing Run entry, Run entry repair, autostart off, sanitising, log cap, single instance (including an abandoned claim), a refused switch through real CoreAudio, and the real exe launched twice.
- Not checked: the "Couldn't switch" notice and balloon on screen (only the code path and log were tested), and whether a running 1.2.0 copy gets detected (relies on Windows putting unprefixed names in the `Local\` namespace).
- Memory trimming (`MemoryOptimizer`) is unchanged: the lessons suggest measuring whether it helps before removing it.
