<div align="center">

# 🔊 SoundSwitcher

**A Sleek, Lightweight Windows Audio Device Switcher & Floating Pill Widget**

[![Download Latest Release](https://img.shields.io/badge/Download-Latest_Release_(Windows)-8B5CF6?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/OlfJD/Soundswitcher/releases/latest/download/SoundSwitcher-v1.0.0-win-x64.zip)
[![GitHub Release](https://img.shields.io/github/v/release/OlfJD/Soundswitcher?style=for-the-badge&color=10B981)](https://github.com/OlfJD/Soundswitcher/releases/latest)
[![Platform](https://img.shields.io/badge/Platform-Windows_10_%2F_11-3B82F6?style=for-the-badge&logo=windows)](https://github.com/OlfJD/Soundswitcher)

### 📥 [👉 Click Here to Download SoundSwitcher (.zip)](https://github.com/OlfJD/Soundswitcher/releases/latest/download/SoundSwitcher-v1.0.0-win-x64.zip)
*or download the standalone [**`SoundSwitcher.exe`**](https://github.com/OlfJD/Soundswitcher/releases/latest/download/SoundSwitcher.exe)*

</div>

---

## ⚡ Quick Start for Users

1. **[Download SoundSwitcher (.zip)](https://github.com/OlfJD/Soundswitcher/releases/latest/download/SoundSwitcher-v1.0.0-win-x64.zip)** or the direct [**`SoundSwitcher.exe`**](https://github.com/OlfJD/Soundswitcher/releases/latest/download/SoundSwitcher.exe).
2. Run **`SoundSwitcher.exe`**.
3. Look for the 🔊 icon in your Windows system tray (bottom-right near the clock).
4. Click the tray icon to open the floating toggle pill, or click **⚙ (Settings)** to assign your audio devices (e.g. Headphones & Speakers)!

---

## ✨ Features

- **Ultra-Low Memory Footprint**: Idles at ~**6–10 MB of RAM** and 0% CPU.
- **System Tray Background App**: Sits discreetly in the Windows taskbar notification area.
- **Minimalist Floating Pill Widget & Quick Toggle**:
  - **Always on Top**: Pinned above other windows (`HWND_TOPMOST`).
  - **Stays Open Until Closed**: Won't disappear unexpectedly; stays open until you click **✕** or toggle the tray icon.
  - **Draggable Floating Widget**: Click and drag anywhere on the pill to place it anywhere on your screens.
  - **Toggle Pill Switch**: Instant one-click flip between your two selected audio devices.
  - **Crisp Vector Icon**: Powered by Lucide `volume-2` icon design for crisp high-DPI clarity.
- **Integrated Settings & Device Chooser**:
  - Click the **⚙ (Settings)** button next to the toggle to expand preferences.
  - **Choose Audio Devices for Toggle**: Select exactly which device corresponds to Toggle OFF (Device 1) and Toggle ON (Device 2).
  - **Master Volume & Mute**: Slider with live percentage feedback and vector mute states.
  - **Start with Windows**: Toggleable system boot launch.
  - **Direct Tray Toggle**: Optional instant audio flip on tray click without opening flyout.
- **Automatic System Sync**: Synchronizes in real-time with Windows CoreAudio events (`IMMNotificationClient`) when devices are plugged, unplugged, or changed.
