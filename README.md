# SoundSwitcher 🔊

A sleek, lightweight audio device switcher designed for Windows.

---

### ✨ Features
- **Ultra-Low Memory Footprint**: Idles at ~**6–10 MB of RAM** and 0% CPU.
- **System Tray Background App**: Sits discreetly in the Windows taskbar notification area (system tray).
- **Minimalist Floating Pill Widget & Quick Toggle**:
  - **Always on Top**: Pinned above all other windows (`HWND_TOPMOST`).
  - **Stays Open Until Closed**: Won't disappear when clicking outside; stays open until you click **✕** (or toggle tray icon / press Esc).
  - **Draggable Floating Widget**: Click and drag anywhere on the pill to position it anywhere on your desktop.
  - **Toggle Pill Switch**: Instant one-click flip between your two selected audio devices (e.g., Speakers & Headphones).
  - **Clean Vector Icon**: Powered by Lucide `volume-2` icon design for crisp high-DPI clarity.
- **Integrated Settings & Device Chooser**:
  - Click the **⚙ (Settings)** button next to the toggle to expand preferences.
  - **Choose Audio Devices for Toggle**: Select exactly which device corresponds to Toggle OFF (Device 1) and Toggle ON (Device 2).
  - **Master Volume & Mute**: Slider with live percentage feedback and vector mute states.
  - **Start with Windows**: Toggleable boot launch.
  - **Direct Tray Toggle**: Optional instant audio flip on tray click without opening flyout.
  - **Windows Sound Settings**: Quick shortcut to Windows native audio settings.
- **Automatic System Sync**: Synchronizes in real-time with Windows CoreAudio events (`IMMNotificationClient`) when devices are plugged, unplugged, or changed.

---

### 🚀 How to Run
- Run [**`SoundSwitcher.exe`**](file:///C:/Users/Levi/Desktop/Soundswitcher/SoundSwitcher.exe) or double-click [**`Launch SoundSwitcher.bat`**](file:///C:/Users/Levi/Desktop/Soundswitcher/Launch%20SoundSwitcher.bat).
- Look for the 🔊 audio icon in the **System Tray** (near the clock in the bottom-right taskbar).
- **Left-click** the icon to open/close the switcher pill!
- Click **⚙** to select which devices you want mapped to the switch.
