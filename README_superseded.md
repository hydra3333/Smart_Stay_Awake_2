<h1 align="center">
  <img src="./Smart_Stay_Awake_2_icon.png" width="256" alt="Smart Stay Awake icon">
  <br>Smart_Stay_Awake_2
  <br>** UNDER DEVELOPMENT **
</h1>

A tiny Windows tray utility that keeps your computer **awake** (blocks sleep/hibernation) while still allowing the **display monitor to sleep**. Built with C# in Visual Studio Community.

## What it does

- While Smart\_Stay\_Awake runs, it requests the OS to **not sleep/hibernate**. Your **display monitor can still sleep** normally if your power plan allows it.
- When you **quit** (or when an **auto-quit** timer fires), the app **releases** the request and your PC can sleep again.
- A small **windows system-tray icon** provides **Show Window** and **Quit**.

## Key Features

- **Prevents system sleep/hibernation** while running; auto-restores normal behavior on exit.
- **System tray** icon with a simple menu (Show Window / Quit).
- **Minimize behavior:** both the title-bar **“\_”** and the **Minimize to System Tray** button minimise the app to the system-tray.
- **Close (X)** in the main window exits the app completely.
- **Icon / image priority** (for both the window and tray):
  1. `--icon PATH`** (explicit override)
  2. Embedded base64 in the pythin script which may be empty)
  3. A file named **`Smart_Stay_Awake_icon.*`** next to the EXE/script (PNG/JPG/JPEG/WEBP/BMP/GIF/ICO)
  4. A small internal fallback glyph (so it never crashes)
- **Auto-scaling image:** in the window into a square (by edge replication): longest side ≤ **512 px** .
- **Three operating modes:** keep awake indefinitely, or for a fixed duration using `--for` or `--until`
  - **Indefinite:** Runs until you manually quit
  - **For Duration:** Auto-quits the application after a specified time (e.g., 2 hours 90 minutes `2h90m`)
  - **Until Timestamp:** Auto-quits the application at a specific date/time (e.g., `2025-12-31 23:59:59`)
- **Low-resource-use countdown:** updates window display less often when plenty of time remains; updates faster as it nears zero; and **“snaps”** to neat time boundaries so it feels calm and rounded.
- **Display countdown shown:**
  - **Auto-quit at:** local ETA
  - **Time remaining:** `Xd HH:MM:SS` (days appear when applicable)
  - **Time remaining update frequency:** displays current update frequency for the 'Time remaining'

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
> * `--for` and `--until` are **mutually exclusive**; provide only **one or the other**.
> * A small (±1s) variation near the very end can occur due to Windows timer jitter - this is normal.

### Examples

**Run for a fixed time**

```cmd
.\Smart_Stay_Awake.exe --for 2h
.\Smart_Stay_Awake.exe --for 45m
.\Smart_Stay_Awake.exe --for 3d4h5s
```

**Run until a local date/time**

```cmd
.\Smart_Stay_Awake.exe --until "2026-01-02 23:22:21"
```

**Interesting one-liner using powershell (better doable via `--for`)**

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
  
<h3 align="center">
  <img src="./Smart_Stay_Awake_2_main_window.jpg" width="256" alt="Smart Stay Awake main window">
</h3>

---

## Smart Auto-quit Timer
- **Adaptive time remaining update frequency** that balances accuracy with CPU resource usage:
- **Smooth time boundary snapping** for a polished user experience
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
| **≤ 30 seconds** | Every **1 second** | Final countdown; maximum precision |


## Visibility in Power Management queries
When Smart Stay Awake 2 is running, the Windows (Admin) command `powercfg /requests` shows, for example:
```
SYSTEM:
[PROCESS] \Device\HarddiskVolume3\Path\To\Smart_Stay_Awake_2.exe
Smart Stay Awake 2: Preventing automatic sleep & hibernation (display monitor may sleep) as requested (auto-quit at 2025-10-04 15:30:00).
```

This ensures IT administrators and Admin/Power users can see exactly what's preventing system hibernation and sleep and when it will release.









---

## Download & Run (Windows)

Each Release includes **three** ZIPs:

### 1) Onefile ZIP — single EXE (+ optional icon files)

* **What’s inside:** `Smart_Stay_Awake.exe` (and possibly `Smart_Stay_Awake_icon.*`).
* **Extract (Explorer):** Right-click ZIP -> **Extract All…** -> open the folder.
* **Extract (PowerShell):**
```powershell
Expand-Archive -Path .\Smart_Stay_Awake_<ver>_windows_onefile.zip -DestinationPath .\Smart_Stay_Awake_onefile -Force
```
* **Run:** double-click `Smart_Stay_Awake.exe`.
  Optionally place `Smart_Stay_Awake_icon.*` alongside it to control the window/tray image.

> Tip: Don’t run from inside the ZIP viewer. Always extract first.

### 2) Onedir ZIP — full app folder (EXE + optional icon files + python runtime files in a subfolder)

* **What’s inside:** Smart_Stay_Awake.exe` (and possibly `Smart_Stay_Awake_icon.*`) at the ZIP root, python runtime files in a subfolder.
* **Important:** **extract everything** and keep the folder structure intact.
* **Extract (Explorer):** Right-click ZIP -> **Extract All…**
* **Extract (PowerShell):**
```powershell
Expand-Archive -Path .\Smart_Stay_Awake_<ver>_windows_onedir.zip -DestinationPath .\Smart_Stay_Awake_onedir -Force
```
* **Run:** in the extracted folder, double-click `Smart_Stay_Awake.exe`.

> Tip: Don’t run from inside the ZIP viewer. Always extract first.

### 3) Source ZIP — run from Python (if Python 3.13+ and pip dependencies are installed)

* **What’s inside:** `Smart_Stay_Awake.py` (and optionally `Smart_Stay_Awake_icon.png`).
* **Install dependencies (after python 3.13+ installed):**
```cmd
pip install wakepy --no-cache-dir --upgrade --check-build-dependencies --upgrade-strategy eager --verbose
pip install pystray --no-cache-dir --upgrade --check-build-dependencies --upgrade-strategy eager --verbose
pip install Pillow  --no-cache-dir --upgrade --check-build-dependencies --upgrade-strategy eager --verbose
```
* **Run with console (to see debug info printed to the console):**
```cmd
python .\Smart_Stay_Awake.py
```

* **Run with no console:**
```cmd
pythonw .\Smart_Stay_Awake.py
```

* NOTE: CLI options (`--for`, `--until`, `--icon`) work the same as with the EXE.

---

## Behavior & Tips

* **Tray icon hidden?** It may be in the overflow area; show hidden icons or set “Always show all icons in the taskbar.”
* **Why didn’t my PC sleep?** While Smart\_Stay\_Awake runs, DOS command `powercfg -requests` shows it under **SYSTEM**. Quit the app to release the block.
* **Minimize didn’t hide to windows system-tray?** Ensure you’re on the latest release; both **“\_”** and **Minimize to System Tray** hide to the windows system-tray.
* **ETA alignment & countdown:** the ETA shown in the window is computed from the exact target epoch (from `--until` or internally from `--for`). The countdown updates at low cadence far out (minutes), then faster as it nears the end, throttling further when the window is hidden to minimise CPU.
* **Exit codes:** normal exit returns 0; argument validation errors use a non-zero exit.

---

## Release Automation (GitHub Actions)

* On **Release -> Published**, the workflow builds **onefile** and **onedir**, zips them, lists ZIP contents, and attaches them to the release.
* ZIP names are derived from the **release tag** (sanitized).
* Any `Smart_Stay_Awake_icon.*` at the repo root are copied into both deliverables and the workflow prints exactly which icon files were included.
* The workflow also pre-builds a multi-size `Smart_Stay_Awake_icon.ico` for PyInstaller.

---

## Security / SmartScreen

Unsigned, freshly built executables can be flagged by Windows SmartScreen/Defender. If blocked:

* Review the python code yourself, then the github workflow build code, to ensure it is safe
* Click **More info -> Run anyway**, or
* Add an exclusion in Defender, or
* Submit a **false-positive** report to Microsoft or your antivirus provider.

> `onedir` is known to cause less false positives in lesser-known antivirus products than `onefile`

---

## Troubleshooting

* **No tray icon?** Show hidden icons or allow all icons in the taskbar.
* **Sleeps anyway?** Another power manager may override; check your power plan or OEM tools.
* **Auto-quit didn’t trigger exactly on the second?** A tiny (±1s) drift can occur due to timer jitter; the app uses a monotonic deadline to stay accurate overall.

---

## The EYE OF HORUS

<p align="center">
  <em>The ancient Egyptian "Eye of Horus" (aka "Wadjet") is a symbol of protection, healing, and restoration—an eye that, metaphorically, never sleeps.</em>
</p>

---

## Appendix: Build Locally (PyInstaller)

> These steps are only needed if you want to produce your own EXEs. Most users can just download the Release ZIPs.

### Onefile

```cmd
rmdir /s /q .\dist  2>$null
rmdir /s /q .\build 2>$null
del /q .\Smart_Stay_Awake.spec 2>$null

# Optional: pass a multi-size .ico if you have one
pyinstaller --clean --onefile --windowed --noconsole --name "Smart_Stay_Awake" Smart_Stay_Awake.py --icon "Smart_Stay_Awake_icon.ico"

# Place optional image/icon next to the EXE (inside the app folder):
copy /y Smart_Stay_Awake_icon.* ".\dist\"

# Zip onefile contents (rooted, no top folder)
powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -Sta -NonInteractive  -Command "Compress-Archive -Path '.\dist\*' -DestinationPath '.\Smart_Stay_Awake_onefile.zip' -Force -CompressionLevel Optimal" 

```

### Onedir

```cmd
rmdir /s /q .\dist  2>$null
rmdir /s /q .\build 2>$null
del /q .\Smart_Stay_Awake.spec 2>$null

# Optional: pass a multi-size .ico if you have one
pyinstaller --clean --onedir --windowed --noconsole --name "Smart_Stay_Awake" Smart_Stay_Awake.py --icon "Smart_Stay_Awake_icon.ico"

# Place optional image/icon next to the EXE (inside the app folder):
copy /y Smart_Stay_Awake_icon.* ".\dist\Smart_Stay_Awake\"

# Zip onedir contents (rooted, no top folder)
powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -Sta -NonInteractive -Command "Compress-Archive -Path '.\dist\Smart_Stay_Awake\*' -DestinationPath '.\Smart_Stay_Awake_onedir.zip' -Force -CompressionLevel Optimal"
```

> The official CI workflow under `.github/workflows/` automates all of this on creating a new Release.



