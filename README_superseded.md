<h1 align="center">
  <img src="./Smart_Stay_Awake_2_icon.png" width="256" alt="Smart Stay Awake icon">
  <br>Smart_Stay_Awake_2
  <br>![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-lightgrey)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![License](https://img.shields.io/badge/license-AGPL--3.0-green)
![Language](https://img.shields.io/badge/language-C%23-239120)
![Status](https://img.shields.io/badge/status-Under%20Development-orange)
![Status](https://img.shields.io/badge/status-Initial%20Release-green)
</h1>

A tiny Windows tray utility that keeps your computer **awake** (blocks sleep/hibernation) while still allowing the **display monitor to sleep**. Built with C# in Visual Studio Community.

---

## What it does

- While Smart\_Stay\_Awake runs, it requests the OS to **not sleep/hibernate**. Your **display monitor can still sleep** normally if your power plan allows it.
- When you **quit** (or when an **auto-quit** timer fires), the app **releases** the request and your PC can sleep again.
- A small **windows system-tray icon** provides **Show Window** and **Quit**.

---

## Key Features

- **Prevents system sleep/hibernation** while running; auto-restores normal behavior on exit.
- **System tray** icon with a simple menu (Show Window / Quit).
- **Three operating modes:** keep awake indefinitely, or for a fixed duration using `--for` or `--until`
  - **Indefinite:** Runs until you manually quit
  - **For Duration:** Auto-quits the application after a specified time (e.g., 2 hours 90 minutes `2h90m`)
  - **Until Timestamp:** Auto-quits the application at a specific date/time (e.g., `2025-12-31 23:59:59`)
- **Display countdown shown:**
  - **Auto-quit at:** local ETA
  - **Time remaining:** `Xd HH:MM:SS` (days appear when applicable)
  - **Time remaining update frequency:** displays current update frequency for the 'Time remaining'
- **Low-resource-use countdown:** updates window display less often when plenty of time remains; updates faster as it nears zero; and **“snaps”** to neat time boundaries so it feels calm and rounded.
- **Minimize behavior:** both the title-bar **“\_”** and the **Minimize to System Tray** button minimise the app to the system-tray.
- **Close (X)** in the main window exits the app completely.
- **Icon / image priority** (for both the window and tray):
  1. `--icon PATH`** (explicit override)
  2. Embedded base64 in the pythin script which may be empty)
  3. A file named **`Smart_Stay_Awake_icon.*`** next to the EXE/script (PNG/JPG/JPEG/WEBP/BMP/GIF/ICO)
  4. A small internal fallback glyph (so it never crashes)
- **Auto-scaling image:** in the window into a square (by edge replication): longest side <= **512 px** .

---

## User Interface
- **Main window features:**
  - Eye of Horus image display
  - Real-time countdown display (for timed auto-quit modes)
  - Auto-quit timestamp (local time)  (for timed auto-quit modes)
  - Time remaining (formatted as days, hours, minutes, seconds)  (for timed auto-quit modes)
  - Live `Time remaining update frequency` (for timed auto-quit modes)
  - Minimize to system tray button
  - Quit button
- **Window behavior:**
  - Minimize (titlebar `-` button) -> hides to system tray
  - Close (titlebar `X` button) -> exits application completely
  - Minimize to System Tray button -> hides to system tray
- **System tray icon** (when minimised to the windows system tray) using right-click on the icon:
  - Show Window
  - Help
  - Quit
  
<h3 align="left">
  <img src="./Smart_Stay_Awake_2_main_window.jpg" width="512" alt="Smart Stay Awake main window">
</h3>

---

## Command-line options

```text
--icon PATH
    Use a specific image file for the window/tray icon.
    Supports: PNG, JPG/JPEG, WEBP, BMP, GIF, ICO.

--for DURATION
    Keep awake for a fixed time, then quit gracefully.
    DURATION accepts days/hours/minutes/seconds in the form of a single string (no spaces):
      3d4h5s, 2h, 90m, 3600s, 1h30m
    A bare number means minutes. Use 0 to disable the timer for maximum duration.
    Bounds: at least MIN_AUTO_QUIT_SECS (default 10s), at most MAX_AUTO_QUIT_SECS (default ~365 days).
    The app re-ceils the remaining time right before arming the timer, for accuracy.
    Mutually exclusive with --until.

--until "YYYY-MM-DD HH:MM:SS"
    DATETIME in the form of a single quoted string, all field parts must be present.
    Keep awake until the given local **24-hour** timestamp, then quit gracefully.
    Examples (relaxed spacing & 1–2 digit parts accepted):
      "2025-01-02 23:22:21"
      "2025- 1- 2  3: 2: 1"
      "2025-1-2 3:2:1"
    Daylight Saving Time and local windows timezone are honored.
    Bounds: at least MIN_AUTO_QUIT_SECS in the future, at most MAX_AUTO_QUIT_SECS from now.
    Mutually exclusive with --for.

--help
    Display this help and stays awake according to the other commandlne parameters.
```

> **Notes**
> * A small (±1s) variation near the very end can occur due to Windows timer jitter - this is normal.

### Basic Usage

**Double-click to run indefinitely:**
```cmd
Smart_Stay_Awake_2.exe
```
The app starts in Indefinite mode and runs until you quit it manually or the system restarts.

**Run for a specific duration:**
```cmd
Smart_Stay_Awake_2.exe --for 2h
Smart_Stay_Awake_2.exe --for 90m
Smart_Stay_Awake_2.exe --for 3d4h30m15s
```

**Run until a specific date/time:**
```cmd
Smart_Stay_Awake_2.exe --until "2025-10-04 23:59:59"
```

### Parameter formats explained

#### `--for <duration>`

Keep the system awake for a fixed duration, then quit gracefully.

**Duration format:**
- Combine days (`d`), hours (`h`), minutes (`m`), and seconds (`s`)
- Examples: `3d`, `2h`, `90m`, `3600s`, `1h30m`, `3d4h5m10s`
- A bare number (no unit) is treated as **minutes**
- Minimum: 10 seconds
- Maximum: ~365 days

**Examples:**
```cmd
Smart_Stay_Awake_2.exe --for 2h          # 2 hours
Smart_Stay_Awake_2.exe --for 45m         # 45 minutes
Smart_Stay_Awake_2.exe --for 90          # 90 minutes (bare number)
Smart_Stay_Awake_2.exe --for 3d4h5s      # 3 days, 4 hours, 5 seconds
```

#### `--until <datetime>`

Keep the system awake until a specific local date/time, then quit gracefully.

**Datetime format:**
- Must be enclosed in quotes
- Format: `"YYYY-MM-DD HH:MM:SS"` (24-hour time)
- Relaxed spacing and 1-2 digit parts are accepted
- Honors local timezone and Daylight Saving Time
- Must be at least 10 seconds in the future
- Must be no more than ~365 days in the future

**Examples:**
```cmd
Smart_Stay_Awake_2.exe --until "2025-10-04 23:30:00"
Smart_Stay_Awake_2.exe --until "2025-12-31 23:59:59"
Smart_Stay_Awake_2.exe --until "2025-1-2 3:2:1"        # Relaxed format
```

#### Mutual Exclusivity

**`--for` and `--until` are mutually exclusive.** Provide only one or the other, not both.

---

## Smart Auto-quit timer and Time remaining update frequency
- **Coundown** to Auto-quit, shows ETA for Auto-quit and a time remaining countdown
- **Adaptive time remaining update frequency** that balances accuracy with CPU resource usage:
- **Smooth time boundary snapping** for a cleaner easier-to-read display
- **Monotonic deadline tracking** to prevent timer drift

| Time remaining update frequency | Update Frequency | Reason |
|----------------|------------------|--------|
| **> 60 minutes** | Every **10 minutes** | Far from deadline; high precision not needed |
| **30 to 60 minutes** | Every **5 minutes** | Distant deadline; minimal updates conserve resources |
| **15 to 30 minutes** | Every **1 minute** | Approaching deadline; moderate precision |
| **10 to 15 minutes** | Every **30 seconds** | Getting closer; increased update rate |
| **5 to 10 minutes** | Every **15 seconds** | Close to deadline; higher precision needed |
| **2 to 5 minutes** | Every **10 seconds** | Very close; frequent updates for accuracy |
| **1 to 2 minutes** | Every **5 seconds** | Almost there; high-frequency updates |
| **30 seconds to 1 minute** | Every **2 seconds** | Final approach; near real-time updates |
| **<= 30 seconds** | Every **1 second** | Final countdown |

---

## Visibility in Power Management queries
When Smart Stay Awake 2 is running, the Windows (Admin) command `powercfg /requests` shows, for example:
```
SYSTEM:
[PROCESS] \Device\HarddiskVolume3\Path\To\Smart_Stay_Awake_2.exe
Smart Stay Awake 2: Preventing automatic sleep & hibernation (display monitor may sleep) as requested (auto-quit at 2025-10-04 15:30:00).
```

This ensures that IT administrators and Admin/Power users can see exactly what's preventing system hibernation and sleep and when it will release.

---

## Requirements

### Operating System
- **Windows 10** version 2004 (May 2020 Update) or later
- **Windows 11** (all versions)

To check your Windows version:
1. Press `Win + R`
2. Type `winver` and press Enter
3. Look for "Version 2004" or higher

### Runtime
- **Microtosft .NET 8+ Desktop Runtime** (x64)
  - Ask your system administrator, this is a 'normal' thing ...
  - Download: [https://dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
  - The installer will prompt you if the runtime is missing

### Permissions to run
- **No administrator rights required** for normal operation
- Standard user permissions are sufficient

---



























---

## **Interesting example one-liner using .bat and powershell (doable via `--for`)**

> NOTE: a .BAT (needs to double the % signs in `for`)

```bat
@echo off
@setlocal ENABLEDELAYEDEXPANSION
@setlocal enableextensions
set "minutes_from_now=5"
for /f "usebackq delims=" %%T in (`powershell -NoProfile -Command "(Get-Date).AddMinutes(!minutes_from_now!).ToString('yyyy-MM-dd HH:mm:ss')"`) do (
    set "datetime_ahead=%%T"
)
python ".\Smart_Stay_Awake.py" --until "!datetime_ahead!"
```

---

## Behavior & Tips

* **Tray icon hidden?** It may be in the overflow area; show hidden icons or set “Always show all icons in the taskbar.”
* **Why didn’t my PC sleep?** While Smart\_Stay\_Awake runs, DOS command `powercfg -requests` shows it under **SYSTEM**. Quit the app to release the block.
* **Minimize didn’t hide to windows system-tray?** Ensure you’re on the latest release; both **“\_”** and **Minimize to System Tray** hide to the windows system-tray.
* **ETA alignment & countdown:** the ETA shown in the window is computed from the exact target epoch (from `--until` or internally from `--for`). The countdown updates at low cadence far out (minutes), then faster as it nears the end, throttling further when the window is hidden to minimise CPU.
* **Exit codes:** normal exit returns 0; argument validation errors use a non-zero exit.
* **No tray icon?** Show hidden icons or allow all icons in the taskbar.
* **Sleeps anyway?** Another power manager may override; check your power plan or OEM tools.
* **Auto-quit didn’t trigger exactly on the second?** A tiny (±1s) drift can occur due to timer jitter; the app uses a monotonic deadline to stay accurate overall.

---

## Tech stuff



---

## The EYE OF HORUS

<p align="center">
  <em>The ancient Egyptian "Eye of Horus" (aka "Wadjet") is a symbol of protection, healing, and restoration—an eye that, metaphorically, never sleeps.</em>
</p>

---

