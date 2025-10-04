# Smart Stay Awake 2

![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-lightgrey)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![License](https://img.shields.io/badge/license-AGPL--3.0-green)
![Language](https://img.shields.io/badge/language-C%23-239120)
![Status](https://img.shields.io/badge/status-Under%20Development-orange)

<p align="center">
  <img src="./Assets/Smart_Stay_Awake_2_icon.png" width="256" alt="Smart Stay Awake 2 - Eye of Horus icon">
</p>

<p align="center">
  <strong>A lightweight Windows system tray utility that prevents your computer from sleeping or hibernating while allowing your display monitor to sleep normally.</strong>
</p>

---

## Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Requirements](#requirements)
- [Download & Installation](#download--installation)
- [Usage](#usage)
  - [Command-line Options](#command-line-options)
  - [Examples](#examples)
- [How It Works](#how-it-works)
  - [Power Request API](#power-request-api)
  - [Timer Update Frequency](#timer-update-frequency)
- [Architecture](#architecture)
- [Building from Source](#building-from-source)
- [Troubleshooting](#troubleshooting)
- [The Eye of Horus](#the-eye-of-horus)
- [License](#license)
- [Contributing](#contributing)
- [Acknowledgments](#acknowledgments)

---

## Overview

Smart Stay Awake 2 is a modern .NET 8 Windows Forms application that uses the Windows Power Request API to prevent system sleep and hibernation while you work. Unlike simple execution state toggles, this application uses the proper Windows power management APIs (`PowerCreateRequest`, `PowerSetRequest`) to politely request that the system stays awake.

**What makes it different:**
- Uses modern Windows Power Request APIs (not the legacy `SetThreadExecutionState`)
- Prevents **system sleep/hibernation** only—your **display can still sleep** to save power
- Shows up properly in `powercfg /requests` for IT admin visibility
- Lightweight system tray app with minimal resource usage
- Smart countdown timer with adaptive update frequency
- Clean auto-quit behavior with no lingering processes

---

## Features

### Core Functionality
- **Blocks system sleep & hibernation** while running, using Windows Power Request API
- **Allows display monitor to sleep** normally according to your power plan
- **Three operating modes:**
  - **Indefinite:** Runs until you manually quit
  - **For Duration:** Auto-quits after a specified time (e.g., 2 hours, 90 minutes)
  - **Until Timestamp:** Auto-quits at a specific date/time (e.g., "2025-12-31 23:59:59")

### User Interface
- **System tray icon** with right-click menu:
  - Show Window
  - Quit
- **Main window features:**
  - Eye of Horus icon display
  - Current mode indicator
  - Real-time countdown display (for timed modes)
  - Auto-quit timestamp (local time)
  - Time remaining (formatted as days, hours, minutes, seconds)
  - Live update frequency indicator
  - Minimize to system tray button
  - Quit button
- **Window behavior:**
  - Minimize (titlebar `-` button) → hides to system tray
  - Close (titlebar `X` button) → exits application completely
  - Minimize to System Tray button → hides to system tray

### Smart Countdown Timer
- **Adaptive update frequency** that balances accuracy with CPU efficiency:
  - Updates every **5 minutes** when more than 2 hours remain
  - Updates every **1 minute** when 30 minutes to 2 hours remain
  - Updates every **10 seconds** when 1 to 30 minutes remain
  - Updates every **1 second** when less than 1 minute remains
- **Throttled updates when minimized** to reduce CPU usage
- **Smooth time boundary snapping** for a polished user experience
- **Monotonic deadline tracking** to prevent timer drift

### Power Management Visibility
When Smart Stay Awake 2 is running, the Windows command `powercfg /requests` shows:
```
SYSTEM:
[PROCESS] \Device\HarddiskVolume3\Path\To\Smart_Stay_Awake_2.exe
Smart Stay Awake 2: Preventing automatic sleep & hibernation (display monitor may sleep) as requested (auto-quit at 2025-10-04 15:30:00).
```

This ensures IT administrators and power users can see exactly what's preventing sleep and when it will release.

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
- **.NET 8 Desktop Runtime** (x64)
  - Download: [https://dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
  - The installer will prompt you if the runtime is missing

### Permissions
- **No administrator rights required** for normal operation
- Standard user permissions are sufficient

---

## Download & Installation

### Option 1: Standalone Executable (Recommended)

1. Download the latest release ZIP from the [Releases](../../releases) page
2. Extract all files to a folder of your choice:
   - `Smart_Stay_Awake_2.exe`
   - `Microsoft.Windows.CsWin32.dll`
   - `System.Memory.dll`
   - `System.Runtime.CompilerServices.Unsafe.dll`
   - `Microsoft.Bcl.AsyncInterfaces.dll`
   - `Assets/` folder (contains icon resources)
3. **Keep all files together** in the same folder
4. Run `Smart_Stay_Awake_2.exe`

**Important:** All 7 items (exe + 5 DLLs + Assets folder) must remain in the same directory for the application to work.

### Option 2: Build from Source

See [Building from Source](#building-from-source) section below.

---

## Usage

### Basic Usage

**Double-click to run indefinitely:**
```cmd
Smart_Stay_Awake_2.exe
```
The app starts in Indefinite mode and runs until you quit it manually.

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

### Command-line Options

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

## How It Works

### Power Request API

Smart Stay Awake 2 uses the modern Windows Power Request API instead of the legacy `SetThreadExecutionState` function. This approach provides:

**Better system integration:**
- Appears in `powercfg /requests` with a descriptive reason string
- Allows IT administrators to monitor what's preventing sleep
- Properly releases power requests when the app exits
- Cooperates cleanly with other power management tools

**How it works internally:**

1. **On startup**, the app calls `PowerCreateRequest()` with a `REASON_CONTEXT` containing:
   ```
   "Smart Stay Awake 2: Preventing automatic sleep & hibernation (display monitor may sleep) as requested (auto-quit at 2025-10-04 15:30:00)."
   ```

2. **The app sets the power request** using `PowerSetRequest()` with the `PowerRequestSystemRequired` flag:
   - This blocks **system sleep/hibernation**
   - This does **NOT** block display sleep (`PowerRequestDisplayRequired` is not used)

3. **On exit** (manual quit or auto-quit), the app:
   - Calls `PowerClearRequest()` to release the system power request
   - Calls `CloseHandle()` to clean up the power request handle
   - Returns control to Windows, allowing normal sleep behavior to resume

**Why the display can still sleep:**

The app only requests `PowerRequestSystemRequired`, not `PowerRequestDisplayRequired`. This means:
- Your **CPU, disk, network** stay active
- Your **display** can sleep according to your power plan settings
- Energy-efficient for long-running tasks that don't need the screen

### Timer Update Frequency

To balance **visual responsiveness** with **CPU efficiency**, the countdown timer uses an adaptive update strategy. The update frequency depends on how much time remains until auto-quit.

#### Update Frequency Table

| Time Remaining | Update Frequency | Reason |
|----------------|------------------|--------|
| **> 2 hours** | Every **5 minutes** | Far from deadline; high precision not needed |
| **30 min to 2 hours** | Every **1 minute** | Approaching deadline; moderate precision |
| **1 min to 30 min** | Every **10 seconds** | Close to deadline; higher precision needed |
| **< 1 minute** | Every **1 second** | Very close; maximum precision for accuracy |

**Additional optimizations:**

- **When minimized to system tray:** Updates are throttled further to reduce CPU usage (updates every 30-60 seconds regardless of time remaining)
- **Boundary snapping:** The timer "snaps" to neat time boundaries (e.g., exactly on the minute mark) for a polished feel
- **Monotonic deadline:** The app uses a monotonic clock to calculate the deadline, preventing timer drift even if the system clock changes

#### Why this matters

Consider a scenario where you run `Smart_Stay_Awake_2.exe --for 8h`:

- **First 6 hours:** The display updates every 5 minutes (only 72 updates)
- **Hours 6-7.5:** Updates every 1 minute (90 updates)
- **Minutes 90-98:** Updates every 10 seconds (48 updates)
- **Last minute:** Updates every 1 second (60 updates)

**Total updates:** ~270 updates over 8 hours instead of 28,800 updates (if updated every second the whole time)

This reduces CPU wake-ups by **98.5%** while still providing a responsive countdown display when it matters most.

#### What the user sees

In the main window, you'll see a line like:

```
Update cadence: Every 5 minutes
```

This tells you how frequently the countdown is being refreshed right now. As the deadline approaches, you'll see this change to:
- "Every 1 minute"
- "Every 10 seconds"  
- "Every 1 second"

---

## Architecture

### Project Structure

```
Smart_Stay_Awake_2/
├── Smart_Stay_Awake_2.csproj          # .NET 8 Windows Forms project file
├── Program.cs                          # Application entry point
├── MainForm.cs                         # Main window UI and logic
├── AppState.cs                         # Application state model
├── PowerManagement/
│   ├── PowerRequestManager.cs          # Power Request API wrapper
│   └── ExecutionStateManager.cs        # Execution state management
├── Utilities/
│   ├── CommandLineParser.cs            # CLI argument parsing
│   ├── DurationParser.cs               # Duration string parsing (3d4h5m)
│   ├── DateTimeParser.cs               # Datetime string parsing
│   └── TimeFormatter.cs                # Human-friendly time formatting
├── Assets/
│   └── Smart_Stay_Awake_2_icon.png     # Application icon
├── NativeMethods.txt                   # CsWin32 API surface area
└── README.md                           # This file
```

### Key Technologies

- **.NET 8** (Windows Desktop Framework)
- **Windows Forms** (UI framework)
- **CsWin32** (source-generated P/Invoke bindings)
  - Generates type-safe Windows API calls from metadata
  - No manual P/Invoke declarations needed
- **Windows Power Request API**
  - `PowerCreateRequest()`
  - `PowerSetRequest()`
  - `PowerClearRequest()`
  - `CloseHandle()`

### Design Patterns

- **Singleton pattern** for `PowerRequestManager` (ensures only one active power request)
- **Dispose pattern** for proper cleanup of native resources (power request handles)
- **MVC-style separation** between UI (`MainForm`), state (`AppState`), and business logic (`PowerRequestManager`)
- **Immutable configuration** via `AppState` to prevent accidental state corruption

---

## Building from Source

### Prerequisites

1. **Visual Studio 2022** (Community Edition or higher)
   - Workload: ".NET Desktop Development"
   - Component: ".NET 8 SDK"
2. **Git** (for cloning the repository)

### Build Steps

**Clone the repository:**
```cmd
git clone https://github.com/yourusername/Smart_Stay_Awake_2.git
cd Smart_Stay_Awake_2
```

**Open in Visual Studio:**
```cmd
start Smart_Stay_Awake_2.sln
```

**Restore NuGet packages:**

Visual Studio will automatically restore packages on first build. Packages include:
- `Microsoft.Windows.CsWin32`
- Supporting libraries for CsWin32

**Build the project:**

- Press `Ctrl + Shift + B` or
- Menu: Build → Build Solution

**Run/Debug:**

- Press `F5` to run with debugging, or
- Press `Ctrl + F5` to run without debugging

### Output Location

After building, the compiled executable and dependencies are in:
```
Smart_Stay_Awake_2\bin\Debug\net8.0-windows\
```

or for Release builds:
```
Smart_Stay_Awake_2\bin\Release\net8.0-windows\
```

**Deployment bundle:**
- Copy the entire folder contents (exe + DLLs + Assets folder) together to deploy

---

## Troubleshooting

### Common Issues

**Application won't start:**
- Ensure all 7 items are in the same folder (exe + 5 DLLs + Assets folder)
- Verify Windows 10 version 2004+ (run `winver` to check)
- Try running from Command Prompt to see error messages:
  ```cmd
  cd C:\Path\To\Smart_Stay_Awake_2
  Smart_Stay_Awake_2.exe
  ```

**No tray icon appears:**
- Check the system tray overflow area (click the `^` arrow)
- Enable "Always show all icons in the taskbar" in Windows settings:
  1. Right-click taskbar → Taskbar settings
  2. Scroll to "System tray" → "Select which icons appear on the taskbar"
  3. Enable Smart_Stay_Awake_2 or toggle "Always show all icons"

**System still sleeps:**
- Verify the app is running: Check `powercfg /requests` in Command Prompt
  ```cmd
  powercfg /requests
  ```
  You should see Smart Stay Awake 2 listed under "SYSTEM"
- Another power policy or OEM tool may override system requests
- Some laptops have hardware sleep buttons that bypass software blocks
- Check Windows power plan advanced settings for conflicting policies

**Auto-quit timer didn't fire exactly on time:**
- Minor drift (±1 second) is normal due to Windows scheduler granularity
- The app uses monotonic timers to maintain accuracy over long durations
- If the timer is off by more than 2-3 seconds, please file a bug report

**SmartScreen or antivirus blocks the exe:**
- Unsigned binaries may trigger SmartScreen on first run
- Click "More info → Run anyway" after reviewing the source code yourself
- Add an exclusion in Windows Defender if you trust the application
- Submit false positive reports to Microsoft to improve reputation

**Want more detailed logging:**
- The application writes trace logs to the Windows Event Viewer (Application log)
- You can also run the app from Command Prompt with debug output:
  ```cmd
  Smart_Stay_Awake_2.exe --verbose
  ```
  (if verbose mode is implemented in your version)

### Verifying Power Request Status

To confirm the app is working:

**Open Command Prompt or PowerShell:**
```cmd
powercfg /requests
```

**Look for output like:**
```
SYSTEM:
[PROCESS] \Device\HarddiskVolume3\Path\To\Smart_Stay_Awake_2.exe
Smart Stay Awake 2: Preventing automatic sleep & hibernation (display monitor may sleep) as requested (indefinitely).
```

If you see this, the power request is active and working correctly.

---

## The Eye of Horus

<p align="center">
  <img src="./Assets/Smart_Stay_Awake_2_icon.png" width="256" alt="Eye of Horus - Wadjet">
</p>

<p align="center">
  <em>The ancient Egyptian "Eye of Horus" (Wadjet) symbolizes protection, healing, and restoration—an eye that, metaphorically, never sleeps.</em>
</p>

The Eye of Horus was worn as an amulet to provide protection and health to the wearer. In the context of this application, it represents vigilant protection of your work by preventing unwanted system sleep. Just as the Eye of Horus never closes, Smart Stay Awake 2 keeps your system alert and ready.

---

## License

This project is licensed under the **GNU Affero General Public License v3.0 (AGPL-3.0)**.

See [LICENSE](LICENSE) for full license text.

### Summary

You are free to:
- **Use** this software for any purpose
- **Modify** the source code
- **Distribute** copies of the original or modified software

Under the following conditions:
- **Same license:** If you distribute modified versions, you must release the source code under AGPL-3.0
- **Network use:** If you run a modified version on a server and let others interact with it, you must make the source code available to those users
- **State changes:** You must clearly indicate what changes were made
- **No warranty:** The software is provided "as is" without warranty

**Why AGPL-3.0?**

The AGPL license ensures that improvements and modifications to this tool remain open source and benefit the community, even when used in network/server environments.

---

## Contributing

Contributions are welcome and appreciated! Here's how you can help:

### Reporting Issues

Found a bug or have a feature request?

1. Check [existing issues](../../issues) to avoid duplicates
2. Create a new issue with:
   - Clear, descriptive title
   - Steps to reproduce (for bugs)
   - Expected vs actual behavior
   - Your Windows version and .NET version
   - Screenshots if applicable

### Submitting Code

1. **Fork** the repository
2. **Create a feature branch:**
   ```cmd
   git checkout -b feature/your-feature-name
   ```
3. **Make your changes** with clear, descriptive commits:
   ```cmd
   git commit -m "Add feature: descriptive message"
   ```
4. **Push to your fork:**
   ```cmd
   git push origin feature/your-feature-name
   ```
5. **Open a Pull Request** with:
   - Description of changes
   - Reference to related issues (if any)
   - Screenshots for UI changes

### Code Style

- Follow standard C# naming conventions (PascalCase for public members, camelCase for private fields)
- Add XML documentation comments for public APIs
- Keep methods focused and single-purpose
- Write self-documenting code with clear variable names
- Add comments for complex logic or non-obvious behavior

### Testing

Before submitting a PR:
- Build and test on Windows 10 and Windows 11 if possible
- Test all three modes (Indefinite, For Duration, Until Timestamp)
- Verify power request shows up in `powercfg /requests`
- Check that auto-quit works correctly
- Ensure no memory leaks or unclosed handles

---

## Acknowledgments

- Built with [.NET 8](https://dotnet.microsoft.com/) and [Windows Forms](https://docs.microsoft.com/en-us/dotnet/desktop/winforms/)
- Windows API bindings generated by [CsWin32](https://github.com/microsoft/CsWin32)
- Icon inspired by the ancient Egyptian Eye of Horus (Wadjet) symbol
- Thanks to the open-source community for feedback and contributions

---

## Additional Resources

- [Windows Power Management API Documentation](https://docs.microsoft.com/en-us/windows/win32/power/power-management-portal)
- [CsWin32 GitHub Repository](https://github.com/microsoft/CsWin32)
- [.NET 8 Documentation](https://docs.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8)
- [Windows Forms Documentation](https://docs.microsoft.com/en-us/dotnet/desktop/winforms/)

---

<p align="center">
  Made with ☕ and ❤️ for the Windows community
</p>
