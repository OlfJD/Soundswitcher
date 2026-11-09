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
     - Ensures all crash logging and file I/O use `AppDomain.CurrentDomain.BaseDirectory` or `%APPDATA%`, preventing silent write crashes during Windows boot when the operating system sets the working directory to `C:\Windows\System32`.

4. **UI & Vector Visual System (`Views/` & `App.xaml`)**:
   - `FlyoutWindow.xaml` / `.xaml.cs`: Draggable floating pill widget with `HWND_TOPMOST` layering, dynamic neon obsidian theme, custom Lucide vector audio icons, smooth device toggle switch, expandable settings panel, and master volume slider.
   - `App.xaml` / `App.xaml.cs`: Application entry point managing Windows Notification Area (System Tray) icon (`NotifyIcon`), offscreen host window for keeping the WPF message pump alive in tray-only mode, single-instance mutex, and global hotkeys.

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
