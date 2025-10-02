# Revised Timer Specification v02
## Smart Stay Awake 2 - Timer & Countdown System

**Document Version:** v02 (Updated 2025-10-02)  
**Date:** 2025-10-02  
**Status:** Approved for Implementation  
**Change Log:** Fixed performance metrics to use #if DEBUG instead of runtime const

---

## 1. Overview

This specification defines the timer and countdown display system for Smart Stay Awake 2. The design uses **two independent timers** with different purposes and threading models:

1. **Auto-Quit Timer** - Single-shot background timer that exits the app at a precise moment
2. **Countdown Display Timer** - Smart rescheduling UI timer that updates countdown fields efficiently

**Core Principles:**
- ✅ **NO polling loops** - Only smart, calculated single-shot timers
- ✅ **Zero CPU overhead** - Infrequent wakeups based on adaptive cadence
- ✅ **Precise timing** - Two-stage ceiling for accuracy
- ✅ **Thread-safe** - UI timer on UI thread, no marshaling needed

---

## 2. Auto-Quit Timer (Module C.1)

### 2.1 Purpose

Execute a **single callback** at a precise future moment to gracefully quit the application. Used when `--for` or `--until` CLI options are specified.

### 2.2 Implementation

**Timer Type:** `System.Threading.Timer` (background ThreadPool thread)

**Characteristics:**
- Fires **exactly once** (one-shot mode: `period = Timeout.Infinite`)
- Callback runs on background thread (safe for cleanup, must marshal UI calls)
- Automatically disposed on fire or manual cancellation

### 2.3 Precision Requirements

**Target Precision:** Whole seconds (no subseconds)

**Examples:**
- Current time: `00:01:02.05` + `--for 10s` → Fire at: `00:01:12.00`
- `--until "2025-10-05 00:01:02"` → Fire at: `2025-10-05 00:01:02.00`

**Two-Stage Ceiling (Accuracy Strategy):**

1. **Stage 1 - CLI Parsing:**
   ```csharp
   // For --for: Calculate target epoch from parsed duration
   double nowCeil = Math.Ceiling(GetCurrentEpochSeconds());
   double targetEpoch = nowCeil + parsedSeconds;
   ```
   
   ```csharp
   // For --until: Target epoch from parsed local timestamp
   double targetEpoch = ConvertLocalTimeToEpoch(parsedDateTime);
   ```

2. **Stage 2 - Just Before Timer Arm (in MainForm.OnShown):**
   ```csharp
   // Re-calculate seconds from NOW to target (accounts for startup overhead)
   double nowCeil = Math.Ceiling(GetCurrentEpochSeconds());
   int finalSeconds = (int)Math.Ceiling(targetEpoch - nowCeil);
   
   if (finalSeconds <= 0) {
       // Target already passed, quit immediately
       QuitApplication("Timer.AlreadyExpired");
       return;
   }
   
   // Arm timer with final precise delay
   _autoQuitTimer = new Timer(OnAutoQuitCallback, null, finalSeconds * 1000, Timeout.Infinite);
   ```

**Why Two Stages?**
- Stage 1 accounts for CLI parsing overhead
- Stage 2 accounts for app initialization (form creation, image loading, etc.)
- Result: Timer fires at **exact promised moment**, not early

### 2.4 Monotonic Time for Countdown Math

**Wall-Clock Time (Display Only):**
- Used for `_fldUntil` ETA field: "Auto-quit at: 2025-10-05 00:01:02"
- Source: `DateTime.Now` or parsed `--until` value

**Monotonic Time (Countdown Calculations):**
- Used for countdown math: immune to clock changes, DST, NTP adjustments
- Source: `Stopwatch.GetTimestamp()` or `Environment.TickCount64`
- Storage: `_autoQuitDeadlineTicks` (monotonic deadline)

**Pattern:**
```csharp
// At timer arm:
long monotonicNow = Stopwatch.GetTimestamp();
_autoQuitDeadlineTicks = monotonicNow + (finalSeconds * Stopwatch.Frequency);
_autoQuitWallClockEta = new DateTime(targetEpoch in ticks); // For display only

// During countdown updates:
long monotonicNow = Stopwatch.GetTimestamp();
long remainingTicks = Math.Max(0, _autoQuitDeadlineTicks - monotonicNow);
int remainingSeconds = (int)(remainingTicks / Stopwatch.Frequency);
```

### 2.5 Callback Implementation

```csharp
private void OnAutoQuitCallback(object? state)
{
    Trace.WriteLine("Auto-quit timer expired, quitting application...");
    
    // Marshal to UI thread (callback runs on ThreadPool thread)
    if (this.InvokeRequired)
    {
        this.Invoke(new Action(() => QuitApplication("Timer.AutoQuit")));
    }
    else
    {
        QuitApplication("Timer.AutoQuit");
    }
}
```

### 2.6 Cleanup

```csharp
// In QuitApplication():
if (_autoQuitTimer != null)
{
    try
    {
        _autoQuitTimer.Dispose();
    }
    catch (Exception ex)
    {
        Trace.WriteLine($"Error disposing auto-quit timer: {ex.Message}");
    }
    finally
    {
        _autoQuitTimer = null;
    }
}
```

---

## 3. Countdown Display Timer (Module C.2)

### 3.1 Purpose

Update countdown display fields (`_fldRemaining`, `_fldCadence`) at adaptive intervals based on remaining time. Provides efficient CPU usage through infrequent updates when far from deadline.

### 3.2 Implementation

**Timer Type:** `System.Windows.Forms.Timer` (UI thread, no marshaling needed)

**Characteristics:**
- Fires on UI thread (safe to update labels directly)
- Rescheduled after each tick with new calculated interval
- Automatically stopped when form closes or app quits

**Pattern (Stop/Recalculate/Restart):**
```csharp
private void OnCountdownTick(object? sender, EventArgs e)
{
    // 1. Stop current timer
    _countdownTimer.Stop();
    
    // 2. Do work (update fields if visible)
    if (this.Visible)
    {
        UpdateCountdownFields();
    }
    
    // 3. Calculate next interval
    int nextIntervalMs = CalculateNextInterval();
    
    // 4. Restart with new interval
    _countdownTimer.Interval = nextIntervalMs;
    _countdownTimer.Start();
}
```

### 3.3 Cadence Bands (Adaptive Update Frequency)

**Goal:** Update more frequently as deadline approaches, less frequently when far away.

**Configuration (AppConfig.cs):**
```csharp
// Each tuple: (ThresholdSeconds, CadenceMilliseconds)
// Rule: if remaining > threshold, use this cadence
public static readonly (int ThresholdSeconds, int CadenceMs)[] COUNTDOWN_CADENCE = new[]
{
    (3600, 600_000),  // > 60 min:  update every 10 minutes
    (1800, 300_000),  // > 30 min:  update every  5 minutes
    ( 900,  60_000),  // > 15 min:  update every  1 minute
    ( 600,  30_000),  // > 10 min:  update every 30 seconds
    ( 300,  15_000),  // >  5 min:  update every 15 seconds
    ( 120,  10_000),  // >  2 min:  update every 10 seconds
    (  60,   5_000),  // >  1 min:  update every  5 seconds
    (  30,   2_000),  // > 30 sec:  update every  2 seconds
    (  -1,   1_000),  // ≤ 30 sec:  update every  1 second (catch-all)
};
```

**Selection Algorithm:**
```csharp
private int GetBaseCadenceMs(int remainingSeconds)
{
    // Iterate through bands, return first match
    foreach (var (threshold, cadenceMs) in AppConfig.COUNTDOWN_CADENCE)
    {
        if (remainingSeconds > threshold)
        {
            return cadenceMs;
        }
    }
    
    // Should never reach here (catch-all threshold = -1), but safety fallback
    return 1_000;
}
```

### 3.4 Snap-to-Boundary Logic

**Goal:** When far from deadline, align first update to a "round" cadence boundary so countdown appears cleaner.

**Example:** 
- Remaining: `4500s` (75 minutes)
- Current cadence: `600s` (10 minutes)
- Current phase: `4500 % 600 = 300s` (5 minutes past last boundary)
- **Without snap:** Next update in 10 minutes at `4500 - 600 = 3900s` (65 min)
- **With snap:** Next update in 5 minutes at `4500 - 300 = 4200s` (70 min, "round" number)

**Configuration (AppConfig.cs):**
```csharp
// Snap-to only applies when remaining >= this threshold
public const int HARD_CADENCE_SNAP_TO_THRESHOLD_SECONDS = 60;

// Micro-sleep protection: if snap interval < this, add one full cadence
public const int SNAP_TO_MIN_INTERVAL_MS = 200;
```

**Algorithm:**
```csharp
private int CalculateNextInterval()
{
    // Get remaining time
    long nowTicks = Stopwatch.GetTimestamp();
    long remainingTicks = Math.Max(0, _autoQuitDeadlineTicks - nowTicks);
    int remainingSeconds = (int)(remainingTicks / Stopwatch.Frequency);
    
    // Get base cadence for current remaining time
    int baseCadenceMs = GetBaseCadenceMs(remainingSeconds);
    int nextIntervalMs = baseCadenceMs;
    
    // Apply snap-to-boundary (only when remaining >= threshold)
    if (remainingSeconds >= AppConfig.HARD_CADENCE_SNAP_TO_THRESHOLD_SECONDS)
    {
        int cadenceSeconds = Math.Max(1, baseCadenceMs / 1000);
        int phaseSeconds = remainingSeconds % cadenceSeconds;
        
        if (phaseSeconds != 0)
        {
            int snapMs = phaseSeconds * 1000;
            
            // Micro-sleep protection: avoid firing too soon
            if (snapMs < AppConfig.SNAP_TO_MIN_INTERVAL_MS)
            {
                snapMs += cadenceSeconds * 1000;
            }
            
            // Only snap if sooner than regular cadence
            if (snapMs < baseCadenceMs)
            {
                nextIntervalMs = snapMs;
                Trace.WriteLine($"Snap-to applied: remaining={remainingSeconds}s, phase={phaseSeconds}s, snap={snapMs}ms");
            }
        }
    }
    
    return nextIntervalMs;
}
```

### 3.5 Window Visibility Handling (Simplified)

**Decision:** Timer fires regardless of window shown/hidden (simplified from Python approach).

**Rationale:**
- Timer fires are already infrequent (10min, 5min, 1min max)
- Recalc + reschedule is cheap CPU-wise
- Simpler logic, fewer edge cases

**Behavior:**

| State | Timer Fires? | Updates Fields? | Reschedules? |
|-------|--------------|-----------------|--------------|
| **Window Visible** | ✅ Yes | ✅ Yes | ✅ Yes |
| **Window Hidden** | ✅ Yes | ❌ No | ✅ Yes |

**Implementation:**
```csharp
private void OnCountdownTick(object? sender, EventArgs e)
{
    _countdownTimer.Stop();
    
    // Only update fields if window is visible
    if (this.Visible)
    {
        UpdateCountdownFields();
    }
    
    // Always recalculate and reschedule (regardless of visibility)
    int nextIntervalMs = CalculateNextInterval();
    _countdownTimer.Interval = nextIntervalMs;
    _countdownTimer.Start();
}
```

**Edge Case (Timer fires during show transition):**
- **Not a problem:** `System.Windows.Forms.Timer` fires on UI thread sequentially
- Events cannot overlap (UI thread is single-threaded)
- No locking or state flags needed

### 3.6 Field Update Logic

**Three Display Fields:**

1. **`_fldUntil` (Static):**
   - Shows target timestamp: "2025-10-05 00:01:02"
   - Set once at startup, never changes
   - Format: `yyyy-MM-dd HH:mm:ss`

2. **`_fldRemaining` (Dynamic):**
   - Shows live countdown: "0d 01:23:45" or "00:23:45"
   - Updated every tick (if window visible)
   - Format: `DDDd HH:mm:ss` (omit days if 0)

3. **`_fldCadence` (Dynamic, Low-Churn):**
   - Shows current update frequency: "00:10:00" (10 minutes)
   - Only updated when cadence **value changes** (not every tick)
   - Format: `HH:mm:ss`

**Implementation:**
```csharp
private int _lastCadenceSeconds = -1;  // Track last shown cadence

private void UpdateCountdownFields()
{
    // Calculate remaining time
    long nowTicks = Stopwatch.GetTimestamp();
    long remainingTicks = Math.Max(0, _autoQuitDeadlineTicks - nowTicks);
    int remainingSeconds = (int)(remainingTicks / Stopwatch.Frequency);
    
    // Update "Time remaining" (every tick)
    _fldRemaining.Text = FormatDHMS(remainingSeconds);
    
    // Update "Timer update frequency" (only when changed)
    int baseCadenceMs = GetBaseCadenceMs(remainingSeconds);
    int cadenceSeconds = Math.Max(1, baseCadenceMs / 1000);
    
    if (cadenceSeconds != _lastCadenceSeconds)
    {
        _fldCadence.Text = FormatHMS(cadenceSeconds);
        _lastCadenceSeconds = cadenceSeconds;
    }
}

private string FormatDHMS(int totalSeconds)
{
    int days = totalSeconds / 86400;
    int remainder = totalSeconds % 86400;
    int hours = remainder / 3600;
    remainder %= 3600;
    int minutes = remainder / 60;
    int seconds = remainder % 60;
    
    return (days > 0) 
        ? $"{days}d {hours:D2}:{minutes:D2}:{seconds:D2}"
        : $"{hours:D2}:{minutes:D2}:{seconds:D2}";
}

private string FormatHMS(int totalSeconds)
{
    int hours = totalSeconds / 3600;
    int remainder = totalSeconds % 3600;
    int minutes = remainder / 60;
    int seconds = remainder % 60;
    
    return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
}
```

### 3.7 Lifecycle Management

**Initialization (OnShown, after keep-awake armed):**
```csharp
// Only create countdown timer if auto-quit is active
if (_state.Mode != PlannedMode.Indefinite && _autoQuitDeadlineTicks > 0)
{
    _countdownTimer = new System.Windows.Forms.Timer();
    _countdownTimer.Tick += OnCountdownTick;
    
    // Set initial interval and start
    int initialIntervalMs = CalculateNextInterval();
    _countdownTimer.Interval = initialIntervalMs;
    _countdownTimer.Start();
    
    Trace.WriteLine($"Countdown timer started with initial interval: {initialIntervalMs}ms");
}
```

**Cleanup (QuitApplication):**
```csharp
// Stop and dispose countdown timer
if (_countdownTimer != null)
{
    try
    {
        _countdownTimer.Stop();
        _countdownTimer.Tick -= OnCountdownTick;
        _countdownTimer.Dispose();
    }
    catch (Exception ex)
    {
        Trace.WriteLine($"Error disposing countdown timer: {ex.Message}");
    }
    finally
    {
        _countdownTimer = null;
    }
}
```

---

## 4. Timezone & Local Time Handling (Robust Requirements)

### 4.1 Purpose

Correctly parse `--until "YYYY-MM-DD HH:MM:SS"` local timestamps with full DST edge-case handling.

### 4.2 Requirements

**Must Handle:**
- ✅ Calendar validation (reject Feb 31, invalid dates)
- ✅ DST nonexistent times (spring-forward gap) → ERROR
- ✅ DST ambiguous times (fall-back overlap) → ERROR
- ✅ Timezone-aware conversion to UTC/epoch

**Don't Skimp:** This is a startup-only cost, not performance-critical. Get it right.

### 4.3 Two-Pass Validation Strategy (C# Equivalent of Python's mktime)

**Python Pattern:**
```python
# Try with DST=0 (standard time)
epoch_std = mktime(..., tm_isdst=0)
# Try with DST=1 (daylight time)
epoch_dst = mktime(..., tm_isdst=1)
# Compare round-trips to detect nonexistent/ambiguous
```

**C# Equivalent:**
```csharp
public static DateTime ParseUntilTimestamp(string timestamp, out double epochSeconds)
{
    // Step 1: Parse with regex (relaxed spacing, 1-2 digit components)
    var match = Regex.Match(timestamp, @"^\s*(\d{4})\s*-\s*(\d{1,2})\s*-\s*(\d{1,2})\s+(\d{1,2})\s*:\s*(\d{1,2})\s*:\s*(\d{1,2})\s*$");
    
    if (!match.Success)
        throw new ArgumentException("Invalid --until format. Use: YYYY-MM-DD HH:MM:SS");
    
    int year = int.Parse(match.Groups[1].Value);
    int month = int.Parse(match.Groups[2].Value);
    int day = int.Parse(match.Groups[3].Value);
    int hour = int.Parse(match.Groups[4].Value);
    int minute = int.Parse(match.Groups[5].Value);
    int second = int.Parse(match.Groups[6].Value);
    
    // Step 2: Calendar validation (DateTime constructor will throw on invalid dates)
    DateTime localDt;
    try
    {
        localDt = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);
    }
    catch (ArgumentOutOfRangeException ex)
    {
        throw new ArgumentException($"Invalid calendar date/time in --until: {ex.Message}", ex);
    }
    
    // Step 3: DST validation via TimeZoneInfo
    TimeZoneInfo localZone = TimeZoneInfo.Local;
    
    // Check if this local time is ambiguous or invalid
    if (localZone.IsInvalidTime(localDt))
    {
        throw new ArgumentException(
            "--until is not a valid local time (nonexistent due to DST spring-forward gap). " +
            "Please choose a different time.");
    }
    
    if (localZone.IsAmbiguousTime(localDt))
    {
        throw new ArgumentException(
            "--until is ambiguous (falls in the repeated DST fall-back hour). " +
            "Please choose a different time.");
    }
    
    // Step 4: Convert to UTC (guaranteed safe now)
    DateTime utcDt = TimeZoneInfo.ConvertTimeToUtc(localDt, localZone);
    
    // Step 5: Convert to epoch seconds
    epochSeconds = (utcDt - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
    
    return localDt;  // Return original local time for display
}
```

### 4.4 Usage in CLI Parser

```csharp
// In CliParser.Parse():
if (untilTimestampProvided)
{
    DateTime localTarget = ParseUntilTimestamp(untilString, out double targetEpoch);
    
    // Validate bounds (at least MIN_AUTO_QUIT_SECS in future, at most MAX_AUTO_QUIT_SECS)
    double nowCeil = Math.Ceiling(GetCurrentEpochSeconds());
    int deltaSeconds = (int)(targetEpoch - nowCeil);
    
    if (deltaSeconds < AppConfig.MIN_AUTO_QUIT_SECONDS)
        throw new CliParseException($"--until must be at least {AppConfig.MIN_AUTO_QUIT_SECONDS} seconds in the future (got {deltaSeconds}s)");
    
    if (deltaSeconds > AppConfig.MAX_AUTO_QUIT_SECONDS)
        throw new CliParseException($"--until must be within {AppConfig.MAX_AUTO_QUIT_SECONDS / 86400} days from now");
    
    return new CliOptions
    {
        UntilLocal = localTarget,
        UntilTargetEpoch = targetEpoch  // Store for two-stage ceiling
    };
}
```

---

## 5. Constants & Configuration (AppConfig.cs)

**All timer-related constants:**

```csharp
// Timer cadence configuration
public static readonly (int ThresholdSeconds, int CadenceMs)[] COUNTDOWN_CADENCE = new[]
{
    (3600, 600_000),  // > 60 min:  update every 10 minutes
    (1800, 300_000),  // > 30 min:  update every  5 minutes
    ( 900,  60_000),  // > 15 min:  update every  1 minute
    ( 600,  30_000),  // > 10 min:  update every 30 seconds
    ( 300,  15_000),  // >  5 min:  update every 15 seconds
    ( 120,  10_000),  // >  2 min:  update every 10 seconds
    (  60,   5_000),  // >  1 min:  update every  5 seconds
    (  30,   2_000),  // > 30 sec:  update every  2 seconds
    (  -1,   1_000),  // ≤ 30 sec:  update every  1 second (catch-all)
};

// Snap-to-boundary configuration
public const int HARD_CADENCE_SNAP_TO_THRESHOLD_SECONDS = 60;
public const int SNAP_TO_MIN_INTERVAL_MS = 200;

// Auto-quit bounds (already exist, listed for completeness)
public const int MIN_AUTO_QUIT_SECONDS = 10;
public const int MAX_AUTO_QUIT_SECONDS = 365 * 24 * 3600;  // ~365 days
```

---

## 6. Files to Create/Modify

### 6.1 New Files

1. **src/Time/CountdownPlanner.cs** (NEW)
   - Methods: `GetBaseCadenceMs()`, `CalculateNextInterval()` with snap-to logic
   - Helpers: `FormatDHMS()`, `FormatHMS()`
   - Static utility class

2. **src/Time/TimezoneHelpers.cs** (NEW)
   - Methods: `ParseUntilTimestamp()`, `GetCurrentEpochSeconds()`
   - DST validation logic
   - Static utility class

### 6.2 Modified Files

1. **src/Config/AppConfig.cs** (MODIFY)
   - Add: COUNTDOWN_CADENCE array
   - Add: HARD_CADENCE_SNAP_TO_THRESHOLD_SECONDS
   - Add: SNAP_TO_MIN_INTERVAL_MS

2. **src/Cli/CliOptions.cs** (MODIFY)
   - Add: `UntilTargetEpoch` property (double?)
   - Existing: `UntilLocal` property (DateTime?)

3. **src/UI/MainForm.cs** (MODIFY)
   - Add: Timer field declarations (`_autoQuitTimer`, `_countdownTimer`)
   - Add: Monotonic deadline tracking (`_autoQuitDeadlineTicks`)
   - Add: `OnCountdownTick()` event handler
   - Add: `UpdateCountdownFields()` method
   - Modify: `OnShown()` - arm both timers
   - Modify: `QuitApplication()` - dispose both timers

4. **src/Cli/CliParser.cs** (MODIFY)
   - Integrate: `TimezoneHelpers.ParseUntilTimestamp()` for --until
   - Store: `UntilTargetEpoch` in CliOptions

---

## 7. Testing Verification

### 7.1 Auto-Quit Timer Tests

```
Test 1: --for 10s (short duration)
- Start at: 14:23:45.123
- Expected quit: 14:23:55.000 (exactly, no early fire)
- Verify: Check trace log for "Auto-quit timer expired" at correct time

Test 2: --until "2025-10-05 14:30:00"
- Current: 14:25:00
- Expected quit: 14:30:00 (exactly)
- Verify: Check trace log timestamp

Test 3: DST nonexistent time
- Input: --until "2025-03-09 02:30:00" (spring-forward gap in US)
- Expected: CLI error "nonexistent due to DST"

Test 4: DST ambiguous time
- Input: --until "2025-11-02 01:30:00" (fall-back overlap in US)
- Expected: CLI error "ambiguous (repeated DST hour)"
```

### 7.2 Countdown Display Timer Tests

```
Test 5: Snap-to boundary
- Start with: --for 4500s (75 minutes)
- Expected first update: ~5 minutes (snap to 70-minute mark)
- Verify: Trace log shows "Snap-to applied"

Test 6: Cadence transitions
- Start with: --for 3700s (>60 min)
- Watch countdown cross thresholds:
  * > 3600s: 10-minute updates
  * > 1800s: 5-minute updates
  * etc.
- Verify: _fldCadence updates correctly

Test 7: Hidden window behavior
- Minimize to tray with --for 10m
- Verify: Trace log shows timer still firing
- Restore window after 5 minutes
- Verify: Fields update immediately with correct time

Test 8: Field visibility (no timer)
- Run with NO --for or --until
- Verify: _fieldsTable.Visible = false (fields hidden)
```

---

## 8. Summary & Key Points

**What Makes This Design Efficient:**

1. **Adaptive Cadence** - Far fewer wakeups when far from deadline (10min intervals vs 1s)
2. **Smart Rescheduling** - Each tick calculates optimal next interval (no fixed polling)
3. **Single-Threaded UI** - WinForms timer on UI thread, no marshaling overhead
4. **Precise Auto-Quit** - Two-stage ceiling compensates for startup overhead
5. **Robust Timezone** - Full DST validation prevents edge-case bugs

**CPU Impact:**
- Indefinite mode: Zero timer overhead (no countdown)
- 60+ minutes remaining: One wakeup per 10 minutes
- Final minute: One wakeup per second (acceptable for short burst)

**Thread Safety:**
- Auto-quit timer: Background thread, marshals to UI for quit
- Countdown timer: UI thread only, no locking needed
- No race conditions possible (UI thread is sequential)

---

# Appendix A: Threading Model & Thread Safety
## Smart Stay Awake 2 - Understanding Our Single-Threaded Design

**Audience:** Novice developers and future maintainers  
**Purpose:** Explain threading decisions, guarantees, and potential issues

---

## 1. The Problem: Why Threading Matters

### 1.1 What Is Threading?

A **thread** is like a separate worker in your program that can do tasks simultaneously with other workers. 

**Simple Analogy:**
- **Single-threaded program:** One chef in a kitchen, cooking one dish at a time
- **Multi-threaded program:** Multiple chefs in the same kitchen, each cooking different dishes at once

**The Challenge:** When multiple chefs (threads) need to use the same cutting board (shared data), they can collide and create chaos!

### 1.2 Common Threading Problems

**Race Conditions:**
```csharp
// BAD: Two threads updating the same label at once
Thread 1: _fldRemaining.Text = "00:01:00";  // Started first
Thread 2: _fldRemaining.Text = "00:00:59";  // Runs before Thread 1 finishes
// Result: Who knows what the text will be? Maybe garbage, maybe a crash!
```

**Deadlocks:**
```csharp
// BAD: Two threads waiting for each other forever
Thread 1: Locks resource A, waits for resource B
Thread 2: Locks resource B, waits for resource A
// Result: Both threads frozen forever (app hangs)
```

**Cross-Thread UI Access:**
```csharp
// BAD: Background thread trying to update UI label
new Thread(() => {
    _fldRemaining.Text = "Oops!";  // CRASH! UI labels can only be touched by UI thread
}).Start();
```

---

## 2. Our Solution: Single-Threaded UI (STA)

### 2.1 What We Chose

**WinForms applications MUST use Single-Threaded Apartment (STA) mode** for the main UI thread. This is the default for WinForms and is required for features like Clipboard, drag-and-drop, and Windows common dialogs (like OpenFileDialog).

**What This Means:**
- ✅ **One UI thread** - All form updates, button clicks, label changes happen on ONE thread
- ✅ **No collisions** - Events are processed sequentially (one after another, never overlapping)
- ✅ **Simple reasoning** - You can read code top-to-bottom without worrying about "what if this runs at the same time as that?"

### 2.2 The `[STAThread]` Attribute

In our `Program.cs`, you'll see:

```csharp
[STAThread]
static void Main(string[] args)
{
    // ...
}
```

This attribute tells .NET: "Initialize the COM threading model for this application as single-threaded apartment (STA)." It MUST be applied to the Main() entry point method.

**What It Guarantees:**
- All COM objects in the single-threaded apartment can receive method calls only from the one thread that belongs to that apartment
- All method calls are synchronized with the Windows message queue
- Windows Forms apps must be single-threaded if they use system features like Clipboard or shell dialogs

**Critical Rule:** Do NOT remove or change the `[STAThread]` attribute - it has no effect on other methods, only Main().

---

## 3. Our Threading Architecture

### 3.1 Overview: Two Threads Total

```
┌─────────────────────────────────────────────────────────────┐
│                    PROCESS: Smart_Stay_Awake_2.exe          │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  Thread 1: UI THREAD (Main Thread)                    [STA]  │
│  ─────────────────────────────────────────────────────────── │
│  • Created by: Main()                                        │
│  • Runs: Application.Run() message loop                      │
│  • Handles:                                                   │
│    - Form events (Load, Shown, Closing)                      │
│    - Button clicks (Minimize, Quit)                          │
│    - Tray icon clicks                                        │
│    - System.Windows.Forms.Timer events (countdown ticker)    │
│    - All UI updates (label text changes)                     │
│  • Lifetime: Entire app lifetime                             │
│                                                               │
│  ─────────────────────────────────────────────────────────── │
│                                                               │
│  Thread 2: AUTO-QUIT TIMER THREAD (ThreadPool)        [MTA]  │
│  ─────────────────────────────────────────────────────────── │
│  • Created by: System.Threading.Timer                        │
│  • Runs: OnAutoQuitCallback() method (ONE TIME ONLY)         │
│  • Handles:                                                   │
│    - Fires ONCE when auto-quit timer expires                 │
│    - Marshals QuitApplication() call back to UI thread       │
│  • Lifetime: Created on form load, fires once, then dies     │
│  • Thread-safe pattern: Uses Invoke() to call UI thread      │
│                                                               │
└─────────────────────────────────────────────────────────────┘
```

### 3.2 Why Two Threads?

**UI Thread (Thread 1):**
- **Why:** WinForms requires STA for all UI operations - this is a Windows/COM requirement, not a choice
- **Benefit:** All UI code is naturally thread-safe (no locking needed)
- **Trade-off:** If we do long-running work here, the UI freezes

**Auto-Quit Timer Thread (Thread 2):**
- **Why:** Precise one-shot timer without blocking the UI thread
- **Benefit:** Can fire at exact moment even if UI is busy
- **Trade-off:** Must marshal back to UI thread for actual quit (see Section 4)

**Countdown Timer (NOT a separate thread!):**
- Uses `System.Windows.Forms.Timer` - fires events on the UI thread, designed for single-threaded environments
- No marshaling needed, directly updates labels
- Automatically thread-safe (runs on Thread 1)

---

## 4. Thread-Safe Communication: The Invoke Pattern

### 4.1 The Problem

System.Threading.Timer executes callbacks on a ThreadPool thread, but **WinForms controls can only be accessed from the UI thread**. Trying to update a label from the timer thread causes a crash:

```csharp
// BAD CODE - DO NOT DO THIS!
private void OnAutoQuitCallback(object? state)
{
    // This runs on Thread 2 (ThreadPool)
    QuitApplication("Timer.AutoQuit");  // CRASH! MainForm lives on Thread 1
}
```

### 4.2 The Solution: Form.Invoke()

`Form.Invoke()` is like a safe mail system between threads:
1. Timer thread writes a note: "Please call QuitApplication()"
2. Note is placed in UI thread's mailbox (message queue)
3. UI thread reads the note and executes the call on itself
4. Result: Method runs on correct thread, no crash!

**Our Implementation:**

```csharp
// GOOD CODE - THREAD-SAFE!
private void OnAutoQuitCallback(object? state)
{
    Trace.WriteLine("Auto-quit timer expired (running on ThreadPool thread)");
    
    // Check if we need to marshal to UI thread
    if (this.InvokeRequired)  // True if called from non-UI thread
    {
        // Marshal: "UI thread, please call QuitApplication() for me"
        this.Invoke(new Action(() => QuitApplication("Timer.AutoQuit")));
    }
    else
    {
        // Already on UI thread (shouldn't happen, but safe)
        QuitApplication("Timer.AutoQuit");
    }
}
```

**Key Properties:**
- `InvokeRequired`: Returns `true` if caller is NOT on the UI thread
- `Invoke()`: Blocks until UI thread processes the call (synchronous)
- `BeginInvoke()`: Returns immediately (asynchronous) - we don't use this

### 4.3 Why Countdown Timer Doesn't Need Invoke

```csharp
// This is SAFE because System.Windows.Forms.Timer fires on UI thread!
private void OnCountdownTick(object? sender, EventArgs e)
{
    // Already on Thread 1 (UI thread), can directly update labels
    _fldRemaining.Text = FormatDHMS(remainingSeconds);
    _fldCadence.Text = FormatHMS(cadenceSeconds);
    // No Invoke() needed!
}
```

System.Windows.Forms.Timer is designed for use in a single-threaded environment and executes on the UI thread.

---

## 5. Guarantees & Verification

### 5.1 Single-Threaded Guarantee

**Question:** How do we KNOW our UI is single-threaded?

**Answer:** The .NET Framework **guarantees** this for WinForms apps:

1. `[STAThread]` attribute on Main() forces single-threaded apartment initialization
2. Single-threaded apartments consist of exactly one thread, so all COM objects (including WinForms controls) can receive method calls only from that one thread
3. Windows Forms programs use single-threaded apartment state - MTA is not supported

**Verification at Runtime:**

```csharp
// Add this to MainForm constructor (debug only):
#if DEBUG
int uiThreadId = Thread.CurrentThread.ManagedThreadId;
Trace.WriteLine($"UI Thread ID: {uiThreadId}");
Trace.WriteLine($"Apartment State: {Thread.CurrentThread.GetApartmentState()}");
// Expected output: "Apartment State: STA"
#endif
```

### 5.2 Race Condition Prevention

**Question:** Can two timer events fire at the same time?

**Answer:** NO, because:

1. `System.Windows.Forms.Timer` fires events on the UI thread sequentially
2. All method calls to objects in a single-threaded apartment are synchronized with the Windows message queue
3. Windows message queue processes events **one at a time, in order**

**Example:**

```
Time: 00:00:00.000 - Countdown timer fires, OnCountdownTick() starts
Time: 00:00:00.100 - User clicks Quit button (event queued)
Time: 00:00:00.200 - OnCountdownTick() finishes
Time: 00:00:00.201 - Button click event processed, QuitApplication() runs
```

Events never overlap - guaranteed!

### 5.3 Deadlock Prevention

**Question:** Can our app deadlock?

**Answer:** Extremely unlikely, because:

1. **UI thread never blocks waiting for timer thread** - timer uses `Invoke()` which is safe
2. **Timer thread never blocks waiting for UI thread** - fires once and dies
3. **No shared locks** - we don't use `lock()` statements anywhere
4. **No circular dependencies** - simple call chain: Timer → Invoke → UI

**The Only Risk:** If you add `lock()` statements in future code, test carefully!

---

## 6. Visual Studio Settings: DO NOT CHANGE

### 6.1 Project Configuration (Already Correct)

In `Smart_Stay_Awake_2.csproj`:

```xml
<PropertyGroup>
  <OutputType>WinExe</OutputType>              <!-- GUI app, not console -->
  <UseWindowsForms>true</UseWindowsForms>      <!-- Enables WinForms -->
  <ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>
</PropertyGroup>
```

**DO NOT ADD:**
- ❌ `<STAThreading>false</STAThreading>` - Would break WinForms
- ❌ `<MTAThreading>true</MTAThreading>` - Not supported for WinForms

### 6.2 Main Method Attribute (Already Correct)

In `Program.cs`:

```csharp
[STAThread]  // ← CRITICAL! Do not remove or change!
static void Main(string[] args)
{
    ApplicationConfiguration.Initialize();  // Sets up high-DPI, visual styles
    // ...
}
```

**Alternative Attributes (DO NOT USE):**
- ❌ `[MTAThread]` - MTA is not supported for Windows Forms apps
- ❌ Remove `[STAThread]` - Will cause exception: "Current thread must be set to single thread apartment (STA) mode"

### 6.3 Why Settings Are Already Optimal

The Windows Forms Application template for C# automatically adds the STAThreadAttribute to projects. Our project uses these defaults, which are:

- ✅ **Correct for WinForms** - STA is required for UI features
- ✅ **Compatible with our timer** - System.Threading.Timer works fine in STA process
- ✅ **No performance penalty** - STA doesn't slow down our app

**Bottom Line:** Leave all threading settings as-is. They're optimal for our architecture.

---

## 7. Common Pitfalls & How We Avoid Them

### 7.1 Pitfall: Accessing UI from Background Thread

**Problem:**
```csharp
// BAD: Creating a new thread that touches UI
new Thread(() => {
    _fldRemaining.Text = "Boom!";  // CRASH!
}).Start();
```

**Our Solution:**
- We don't create any manual threads
- System.Threading.Timer callback uses `Invoke()` to marshal back to UI thread
- System.Windows.Forms.Timer automatically fires on UI thread

### 7.2 Pitfall: Long-Running Work on UI Thread

**Problem:**
```csharp
// BAD: Blocking UI thread with slow operation
private void Button_Click(object sender, EventArgs e)
{
    Thread.Sleep(10_000);  // UI freezes for 10 seconds!
}
```

**Our Solution:**
- Keep-awake uses `SetThreadExecutionState()` - instant, no delays
- Countdown timer fires infrequently (10 minutes max at start)
- No network calls, file I/O, or database operations on UI thread

### 7.3 Pitfall: Timer Thread Trying to Update UI Directly

**Problem:**
```csharp
// BAD: Timer thread directly calling form methods
private void OnAutoQuitCallback(object? state)
{
    QuitApplication("Timer.AutoQuit");  // CRASH if called directly!
}
```

**Our Solution:**
- **Always use `Invoke()`** when timer thread needs to call UI methods
- Check `InvokeRequired` property first (defensive programming)
- Trace logs show which thread is running (helps debugging)

---

## 8. Testing for Thread Safety

### 8.1 Quick Verification Script

Add this diagnostic code to MainForm constructor (debug builds only):

```csharp
#if DEBUG
private void VerifyThreadSafety()
{
    // Capture UI thread ID
    int uiThreadId = Thread.CurrentThread.ManagedThreadId;
    Trace.WriteLine($"[THREAD CHECK] UI Thread ID: {uiThreadId}");
    Trace.WriteLine($"[THREAD CHECK] Apartment State: {Thread.CurrentThread.GetApartmentState()}");
    
    // Verify it's STA
    Debug.Assert(Thread.CurrentThread.GetApartmentState() == ApartmentState.STA, 
        "UI thread MUST be STA!");
    
    // Hook all events to log thread ID
    this.Load += (s, e) => Trace.WriteLine($"[THREAD CHECK] Load event: Thread {Thread.CurrentThread.ManagedThreadId}");
    this.Shown += (s, e) => Trace.WriteLine($"[THREAD CHECK] Shown event: Thread {Thread.CurrentThread.ManagedThreadId}");
    
    // All should show same thread ID (the UI thread)
}
#endif
```

### 8.2 What to Look For

**Expected trace output:**
```
[THREAD CHECK] UI Thread ID: 1
[THREAD CHECK] Apartment State: STA
[THREAD CHECK] Load event: Thread 1
[THREAD CHECK] Shown event: Thread 1
[THREAD CHECK] Button click: Thread 1
[THREAD CHECK] Timer tick: Thread 1
```

**Red flag (should never happen):**
```
[THREAD CHECK] Timer tick: Thread 5  ← WRONG! Should be Thread 1
```

If you see different thread IDs for UI events, something is **very wrong** - review your timer implementation!

---

## 9. Future Maintainers: Quick Reference

### 9.1 Safe Patterns (Copy These)

**Pattern 1: Updating UI from UI Thread (System.Windows.Forms.Timer)**
```csharp
private System.Windows.Forms.Timer _uiTimer;

private void InitializeTimer()
{
    _uiTimer = new System.Windows.Forms.Timer();
    _uiTimer.Interval = 1000;
    _uiTimer.Tick += OnTick;  // Fires on UI thread - SAFE!
    _uiTimer.Start();
}

private void OnTick(object? sender, EventArgs e)
{
    // Can directly update UI - we're on UI thread
    _fldRemaining.Text = "Updated!";
}
```

**Pattern 2: Background Timer Calling UI (System.Threading.Timer)**
```csharp
private System.Threading.Timer _backgroundTimer;

private void InitializeTimer()
{
    _backgroundTimer = new Timer(OnCallback, null, 5000, Timeout.Infinite);
}

private void OnCallback(object? state)
{
    // Runs on ThreadPool thread - MUST marshal to UI thread!
    if (this.InvokeRequired)
    {
        this.Invoke(new Action(() => UpdateUI()));
    }
    else
    {
        UpdateUI();
    }
}

private void UpdateUI()
{
    // Now safe - on UI thread
    _fldRemaining.Text = "Updated!";
}
```

### 9.2 Unsafe Patterns (NEVER Do These)

```csharp
// NEVER #1: Create manual threads that touch UI
new Thread(() => {
    _fldRemaining.Text = "Boom!";  // CRASH!
}).Start();

// NEVER #2: Use Task.Run() without Invoke
Task.Run(() => {
    _fldRemaining.Text = "Boom!";  // CRASH!
});

// NEVER #3: Remove [STAThread] attribute
// [STAThread]  ← DO NOT COMMENT OUT!
static void Main(string[] args) { }

// NEVER #4: Background thread with shared state (no lock)
private int _counter = 0;  // Shared between threads
Thread1: _counter++;       // Race condition!
Thread2: _counter++;       // Data corruption!
```

---

## 10. Summary: Why This Design Is Solid

### 10.1 What We Achieved

✅ **Predictable behavior** - Events process sequentially, one at a time  
✅ **No race conditions** - UI thread owns all UI state  
✅ **No deadlocks** - Simple call chain, no circular dependencies  
✅ **Easy debugging** - Trace logs show clear execution order  
✅ **Maintainable** - Future developers can reason about code linearly  

### 10.2 Key Principles We Follow

1. **UI thread does UI work** - Never block it with slow operations
2. **Background threads marshal to UI** - Use `Invoke()` when crossing thread boundaries
3. **Prefer UI timers** - Use `System.Windows.Forms.Timer` when possible (auto-safe)
4. **Trace thread IDs** - Log which thread is running for debugging
5. **Keep it simple** - Fewer threads = fewer problems

### 10.3 The Cost of Simplicity

**What We Give Up:**
- ❌ Can't do heavy computation without freezing UI (we don't need to)
- ❌ Can't use multiple cores for UI updates (UI is inherently sequential)
- ❌ Can't have truly parallel event processing (Windows doesn't allow this anyway)

**What We Gain:**
- ✅ Zero threading bugs (if we follow the patterns)
- ✅ Simple code (no `lock()` statements, no `Mutex`, no `Semaphore`)
- ✅ Easy testing (events fire predictably, in order)

---

## 11. When to Worry (Red Flags for Future Changes)

### 11.1 Changes That Break Thread Safety

🚨 **Adding new timers:**
- If you add more timers, use `System.Windows.Forms.Timer` (UI thread) by default
- Only use `System.Threading.Timer` if you need precise one-shot background timing
- Always use `Invoke()` pattern if background timer needs to touch UI

🚨 **Adding background work:**
- If you add `Task.Run()`, `Thread.Start()`, or `ThreadPool.QueueUserWorkItem()`
- You MUST use `Invoke()` to update UI
- Test thoroughly - threading bugs are hard to reproduce!

🚨 **Adding shared state:**
- If two timers/threads need to read/write the same variable
- You MUST use `lock()` or other synchronization primitives
- Consider refactoring to avoid shared state entirely (simpler!)

### 11.2 Safe Changes (No Threading Impact)

✅ **Adding UI controls** - Labels, buttons, panels (all owned by UI thread)  
✅ **Adding event handlers** - Button clicks, form events (all run on UI thread)  
✅ **Adding helper methods** - Pure functions with no side effects  
✅ **Updating constants** - AppConfig.cs changes (no runtime impact)  

**End of Appendix A**

---

# Appendix B: Performance Validation
## Smart Stay Awake 2 - Measuring Timer Efficiency

**Purpose:** Validate that our adaptive cadence system achieves "zero CPU overhead" claims  
**Audience:** Developers testing performance before release  

---

## 1. Performance Goals

### 1.1 What "Zero Overhead" Means

**Our Claims:**
- ✅ **Infrequent wakeups** - Far fewer than polling (10min vs 1s intervals when idle)
- ✅ **Fast execution** - Each tick completes in <1ms
- ✅ **No memory leaks** - Zero allocations per tick (no GC pressure)
- ✅ **Negligible CPU** - App uses <0.1% CPU average over time

**Not Claiming:**
- ❌ "Zero CPU ever" (impossible - we DO wake up periodically)
- ❌ "Zero memory" (app must exist in RAM)
- ❌ "Instant updates" (cadence is intentionally delayed)

### 1.2 Why This Matters

**Bad Timer Design (Polling Loop):**
```csharp
// ANTI-PATTERN: Wakes every 100ms regardless of need
var timer = new Timer(100);  // 10 wakeups per second
timer.Tick += (s, e) => {
    if (ShouldUpdate()) UpdateUI();  // Wastes CPU checking constantly
};
```
**Result:** 36,000 wakeups per hour, constant CPU usage

**Our Design (Adaptive Cadence):**
```csharp
// GOOD: Wakes only when needed
// At 60+ minutes remaining: 6 wakeups per hour (every 10 minutes)
// At final minute: 60 wakeups per minute (every 1 second)
```
**Result:** 6-60 wakeups per hour (depending on phase), minimal CPU

---

## 2. Measurement Configuration

### 2.1 Enable Performance Metrics

**Performance metrics are automatically enabled in DEBUG builds only.**

No configuration needed - controlled by build configuration (Debug vs Release):
- **Debug builds:** Performance metrics active, `[PERF]` logs written
- **Release builds:** Performance metrics stripped (zero overhead)

**What This Does (in DEBUG builds only):**
- Wraps tick execution in `Stopwatch` timing
- Measures memory allocations via `GC.GetTotalMemory()`
- Logs detailed metrics to trace file
- Verifies thread ID (should always be UI thread)

**Implementation pattern:**
```csharp
// In OnCountdownTick() method:
private void OnCountdownTick(object? sender, EventArgs e)
{
    _countdownTimer.Stop();
    
#if DEBUG
    // Performance metrics (debug builds only)
    var sw = System.Diagnostics.Stopwatch.StartNew();
    long memBefore = GC.GetTotalMemory(false);
#endif
    
    // Only update fields if window is visible
    if (this.Visible)
    {
        UpdateCountdownFields();
    }
    
    // Always recalculate and reschedule
    int nextIntervalMs = CalculateNextInterval();
    _countdownTimer.Interval = nextIntervalMs;
    _countdownTimer.Start();
    
#if DEBUG
    // Log performance metrics
    sw.Stop();
    long memAfter = GC.GetTotalMemory(false);
    int threadId = Thread.CurrentThread.ManagedThreadId;
    
    Trace.WriteLine($"[PERF] Tick: {sw.ElapsedTicks} ticks ({sw.Elapsed.TotalMilliseconds:F3}ms), " +
                    $"Thread: {threadId}, Mem: {memAfter - memBefore} bytes, Visible: {this.Visible}");
#endif
}
```

### 2.2 Test Scenarios

**Scenario 1: Long Duration (Low Frequency)**
```
Purpose: Validate 10-minute cadence with minimal wakeups
Command: Smart_Stay_Awake_2.exe --for 65m
Window: Keep visible for first 5 minutes, then minimize
Duration: Watch first 10 ticks (captures snap-to + normal cadence)
Expected: <1ms per tick, 0 memory allocation
```

**Scenario 2: Medium Duration (Mid Frequency)**
```
Purpose: Validate cadence transitions through bands
Command: Smart_Stay_Awake_2.exe --for 15m
Window: Keep visible entire time
Duration: Full 15 minutes (observe all band transitions)
Expected: <1ms per tick, smooth cadence changes (60s → 30s → 15s → 10s → 5s → 2s → 1s)
```

**Scenario 3: Short Duration (High Frequency)**
```
Purpose: Validate 1-second ticks don't cause performance issues
Command: Smart_Stay_Awake_2.exe --for 90s
Window: Keep visible entire time
Duration: Full 90 seconds (final 30s = 1-second ticks)
Expected: <1ms per tick even at 1Hz frequency, 0 memory allocation
```

**Scenario 4: Hidden Window (Validation)**
```
Purpose: Confirm hidden window behavior (timer still fires, no field updates)
Command: Smart_Stay_Awake_2.exe --for 10m
Window: Minimize to tray immediately
Duration: Watch first 5 ticks
Expected: Same timing as visible window (we simplified throttling away)
```

---

## 3. Reading Performance Metrics

### 3.1 Trace Log Format

**Expected Output:**
```
[PERF] Tick: 2843 ticks (0.284ms), Thread: 1, Mem: 0 bytes, Visible: True
[PERF] Tick: 1923 ticks (0.192ms), Thread: 1, Mem: 0 bytes, Visible: True
[PERF] Tick: 2156 ticks (0.215ms), Thread: 1, Mem: 0 bytes, Visible: False
```

**Fields Explained:**
- **Ticks:** `Stopwatch.ElapsedTicks` (raw counter value)
- **Milliseconds:** `Stopwatch.Elapsed.TotalMilliseconds` (human-readable time)
- **Thread:** `Thread.CurrentThread.ManagedThreadId` (should ALWAYS be 1 = UI thread)
- **Mem:** Memory delta in bytes (`GC.GetTotalMemory()` after - before)
- **Visible:** `this.Visible` (window shown vs minimized)

### 3.2 Good Performance Indicators

✅ **Time: <1ms per tick**
```
[PERF] Tick: 2843 ticks (0.284ms), ...  ← GOOD!
[PERF] Tick: 1923 ticks (0.192ms), ...  ← GOOD!
```

✅ **Memory: 0 bytes or small positive (GC noise)**
```
[PERF] ..., Mem: 0 bytes, ...       ← PERFECT!
[PERF] ..., Mem: +24 bytes, ...     ← OK (GC noise)
[PERF] ..., Mem: -120 bytes, ...    ← OK (GC collected)
```

✅ **Thread: Always 1 (UI thread)**
```
[PERF] ..., Thread: 1, ...          ← CORRECT!
```

✅ **Consistent timing across visible/hidden**
```
[PERF] Tick: 2156 ticks (0.215ms), ..., Visible: True
[PERF] Tick: 2089 ticks (0.208ms), ..., Visible: False
  ↑ Similar times = no extra work when hidden
```

### 3.3 Red Flags (Performance Issues)

🚩 **Time: >1ms consistently**
```
[PERF] Tick: 45821 ticks (4.582ms), ...  ← TOO SLOW!
```
**Possible Causes:**
- Heavy computation in `CalculateNextInterval()`
- Expensive string formatting in `FormatDHMS()`
- Too much tracing (disable `FORCED_TRACE_DEFAULT` and retest)

🚩 **Memory: Positive allocation every tick**
```
[PERF] ..., Mem: +8192 bytes, ...   ← MEMORY LEAK!
[PERF] ..., Mem: +8192 bytes, ...   ← ALLOCATING EVERY TICK!
```
**Possible Causes:**
- Creating new strings/objects in hot path
- Lambda captures allocating closures
- LINQ operations in tick code (avoid LINQ in hot paths)

🚩 **Thread: Not 1**
```
[PERF] ..., Thread: 5, ...          ← WRONG THREAD!
```
**Possible Causes:**
- Using wrong timer type (`System.Threading.Timer` instead of `System.Windows.Forms.Timer`)
- Event handler not wired correctly
- **CRITICAL BUG - FIX IMMEDIATELY!**

🚩 **Inconsistent timing (visible vs hidden)**
```
[PERF] Tick: 2156 ticks (0.215ms), ..., Visible: True
[PERF] Tick: 45821 ticks (4.582ms), ..., Visible: False
  ↑ Hidden window taking 20x longer = BUG!
```
**Possible Causes:**
- Accidentally doing extra work when hidden
- Should be identical (we simplified throttling away)

---

## 4. Performance Analysis Tools

### 4.1 Manual Analysis (Trace Logs)

**Step 1: Find your trace log**
```
Location: Next to Smart_Stay_Awake_2.exe
Filename: Smart_Stay_Awake_2_Trace_YYYYMMDD.log
```

**Step 2: Filter to perf metrics**
```
Search for: [PERF]
Expected: One line per tick (e.g., 6 lines in 60 minutes if cadence = 10min)
```

**Step 3: Calculate averages**
```powershell
# PowerShell: Extract timing from last 10 ticks
Get-Content .\Smart_Stay_Awake_2_Trace_20251002.log | 
  Select-String '\[PERF\].*\(([\d.]+)ms\)' | 
  Select-Object -Last 10 |
  ForEach-Object { $_.Matches.Groups[1].Value } |
  Measure-Object -Average

# Expected output:
# Average: 0.25 (ms)
```

### 4.2 Windows Task Manager (Live Monitoring)

**Step 1: Open Task Manager**
```
Ctrl+Shift+Esc → Details tab → Find Smart_Stay_Awake_2.exe
```

**Step 2: Add useful columns**
```
Right-click header → Select columns:
  ☑ CPU
  ☑ Memory (Private Working Set)
  ☑ Threads
```

**Step 3: Observe over time**
```
Expected values (with --for 65m):
  CPU: 0.0% (spikes briefly to 0.1% every 10 minutes)
  Memory: ~15-25 MB (stable, no growth)
  Threads: 2 (UI thread + timer thread)
```

**Red Flags:**
- 🚩 CPU constantly >0.5% (something polling)
- 🚩 Memory growing over time (leak)
- 🚩 Threads >2 (creating threads unnecessarily)

### 4.3 Windows Performance Monitor (Advanced)

**For detailed CPU/memory graphs:**

```
1. Win+R → perfmon
2. Add counters:
   - Processor → % Processor Time → Smart_Stay_Awake_2
   - Memory → Private Bytes → Smart_Stay_Awake_2
3. Run test scenario (e.g., --for 15m)
4. Observe graph:
   - Should see periodic spikes (tick events)
   - Baseline should be flat at ~0%
```

---

## 5. Performance Thresholds

### 5.1 Acceptance Criteria

**Must Meet (Hard Requirements):**

| Metric | Threshold | Test |
|--------|-----------|------|
| **Tick Time** | <1ms average | All scenarios |
| **Memory Growth** | 0 bytes per tick (±50 noise) | 10-minute test |
| **Thread ID** | Always 1 (UI thread) | All ticks |
| **CPU Usage** | <0.5% average | Task Manager, 10min |

**Should Meet (Soft Goals):**

| Metric | Goal | Test |
|--------|------|------|
| **Tick Time** | <0.5ms typical | Scenario 1 & 2 |
| **Wakeup Frequency** | ≤6 per hour at idle | Scenario 1 (first hour) |
| **Memory Footprint** | <30 MB total | Task Manager |

### 5.2 If Thresholds Not Met

**Time >1ms:**
1. Disable `FORCED_TRACE_DEFAULT` and retest (tracing adds overhead)
2. Profile `CalculateNextInterval()` - is snap-to logic too complex?
3. Check string formatting - are `FormatDHMS()` / `FormatHMS()` optimized?

**Memory >0 bytes consistently:**
1. Review hot path code - any `new` allocations?
2. Check for LINQ usage (avoid `.Select()`, `.Where()` in ticks)
3. Verify string formatting doesn't create intermediate strings

**CPU >0.5% average:**
1. Verify cadence is actually adapting (not stuck at 1-second ticks)
2. Check Task Manager - are there multiple timers running?
3. Review trace log - unexpected extra events firing?

---

## 6. Test Procedures

### 6.1 Quick Smoke Test (5 Minutes)

**Goal:** Verify basic performance is acceptable

```bash
# 1. Build in Debug configuration (performance metrics auto-enabled)
#    In Visual Studio: Set build configuration to "Debug"
#    Or command line: dotnet build -c Debug

# 2. Build and run
Smart_Stay_Awake_2.exe --for 65m

# 3. Wait 5 minutes (see first 3 ticks: snap-to + 2 normal)

# 4. Check trace log
#    Look for [PERF] lines
#    Expected: 3 lines, all <1ms, 0 memory, Thread 1

# 5. Pass/Fail
#    PASS if all 3 ticks meet thresholds
#    FAIL if any tick >1ms or wrong thread
```

### 6.2 Full Validation (30 Minutes)

**Goal:** Comprehensive performance validation across all scenarios

```bash
# Test 1: Long duration (10 ticks over ~60 minutes)
Smart_Stay_Awake_2.exe --for 65m
  → Keep visible for 5 min, minimize for 55 min
  → Verify: 10 ticks, all <1ms, visible/hidden timing consistent

# Test 2: Cadence transitions (observe all bands)
Smart_Stay_Awake_2.exe --for 15m
  → Keep visible entire time
  → Verify: Smooth transitions through cadence bands, no spikes

# Test 3: High frequency (stress test 1-second ticks)
Smart_Stay_Awake_2.exe --for 90s
  → Keep visible entire time
  → Verify: Final 30 seconds = 30 ticks, all <1ms

# Test 4: Task Manager validation
Smart_Stay_Awake_2.exe --for 10m
  → Watch Task Manager for 10 minutes
  → Verify: CPU <0.5% average, Memory stable, 2 threads
```

### 6.3 Regression Testing (After Code Changes)

**When to Run:**
- After modifying tick processing code
- After changing cadence logic or snap-to algorithm
- Before each release

**Quick Regression Test:**
```bash
# Run Quick Smoke Test (Section 6.1)
# Compare results to baseline:
#   - Timing should be similar (±0.1ms acceptable)
#   - Memory should still be 0 bytes
#   - Thread should still be 1
```

---

## 7. Interpreting Results

### 7.1 Example: Good Performance

**Trace Log Excerpt (Scenario 1: --for 65m):**
```
[Init] Smart_Stay_Awake_2 start 2025-10-02 14:00:00
[Init] Tracing enabled.
Smart_Stay_Awake_2: UI.MainForm: OnShown: Arming timers...
[SNAP] Applied snap-to: remaining=3900s, phase=300s, snap=300000ms
[PERF] Tick: 2843 ticks (0.284ms), Thread: 1, Mem: 0 bytes, Visible: True
  ↑ First tick after 5 minutes (snap-to boundary)
[PERF] Tick: 1923 ticks (0.192ms), Thread: 1, Mem: 0 bytes, Visible: True
  ↑ Second tick after 10 minutes (normal cadence)
[PERF] Tick: 2089 ticks (0.208ms), Thread: 1, Mem: 0 bytes, Visible: False
  ↑ Third tick after 10 minutes (user minimized)
```

**Analysis:**
✅ **Snap-to worked** - First tick at 5min mark (phase=300s)  
✅ **Timing good** - All <0.3ms  
✅ **No memory leaks** - All 0 bytes  
✅ **Correct thread** - All Thread 1  
✅ **Hidden OK** - Visible vs hidden timing consistent  

**Verdict: PASS**

### 7.2 Example: Bad Performance (Memory Leak)

**Trace Log Excerpt:**
```
[PERF] Tick: 2843 ticks (0.284ms), Thread: 1, Mem: +8192 bytes, Visible: True
[PERF] Tick: 1923 ticks (0.192ms), Thread: 1, Mem: +8192 bytes, Visible: True
[PERF] Tick: 2156 ticks (0.215ms), Thread: 1, Mem: +8192 bytes, Visible: True
  ↑ Allocating 8KB every tick = 48KB per hour = LEAK!
```

**Analysis:**
🚩 **Memory leak detected** - Consistent +8192 bytes every tick  
❌ **Root cause:** Likely allocating new objects in hot path  
❌ **Impact:** Over 65 minutes = 48KB wasted, GC pressure  

**Action Required:**
1. Review `UpdateCountdownFields()` - creating new strings?
2. Review `CalculateNextInterval()` - LINQ operations?
3. Add `#if DEBUG` blocks around suspect code, retest

**Verdict: FAIL - Fix before release**

### 7.3 Example: Bad Performance (Wrong Thread)

**Trace Log Excerpt:**
```
[PERF] Tick: 2843 ticks (0.284ms), Thread: 5, Mem: 0 bytes, Visible: True
  ↑ Thread 5 = ThreadPool thread = WRONG!
```

**Analysis:**
🚩 **CRITICAL BUG** - Countdown timer running on background thread  
❌ **Root cause:** Used `System.Threading.Timer` instead of `System.Windows.Forms.Timer`  
❌ **Impact:** Will crash when trying to update UI labels  

**Action Required:**
1. Check timer creation code - wrong timer type?
2. Verify event handler wiring
3. **FIX IMMEDIATELY** - this will cause crashes!

**Verdict: FAIL - Critical bug, do not release**

---

## 8. Optimization Tips

### 8.1 String Formatting (Hot Path)

**Avoid:**
```csharp
// Creates intermediate string objects
_fldRemaining.Text = $"{days}d {hours:D2}:{minutes:D2}:{seconds:D2}";
```

**Better:**
```csharp
// Use string.Create for zero-allocation formatting (advanced)
// Or just accept that string allocation is cheap for infrequent updates
// At 10-minute cadence, string allocation is negligible
```

**Reality Check:** String formatting is NOT the bottleneck at our cadence. Don't micro-optimize unless profiling shows it's a problem.

### 8.2 Avoid LINQ in Hot Paths

**Avoid:**
```csharp
// LINQ creates enumerators (allocates)
var cadence = COUNTDOWN_CADENCE.FirstOrDefault(x => remaining > x.Threshold);
```

**Better:**
```csharp
// Simple foreach loop (zero allocation)
foreach (var (threshold, cadenceMs) in COUNTDOWN_CADENCE)
{
    if (remaining > threshold)
        return cadenceMs;
}
```

### 8.3 Cache Calculations

**If a value doesn't change, don't recalculate it:**

```csharp
// Cache monotonic deadline (set once)
private long _autoQuitDeadlineTicks;

// Don't recalculate every tick - it's constant!
private int GetRemainingSeconds()
{
    long nowTicks = Stopwatch.GetTimestamp();
    long remainingTicks = Math.Max(0, _autoQuitDeadlineTicks - nowTicks);
    return (int)(remainingTicks / Stopwatch.Frequency);
}
```

---

## 9. Release Build Configuration

**Before Release:**

```csharp
// In AppConfig.cs:
public const bool FORCED_TRACE_DEFAULT = false;         // Disable traces
```

**Build in Release configuration:**

```bash
# Visual Studio: Set build configuration to "Release"
# Or command line:
dotnet build -c Release
```

**Verify Release Build:**
1. Run app with `--for 1m`
2. Check trace log - should NOT exist (or be empty if FORCED_TRACE_DEFAULT=false)
3. Verify no `[PERF]` logs appear (stripped by `#if DEBUG`)
4. Check binary size - should be smaller than Debug build

**Why This Matters:**
- Performance measurement code completely removed (zero overhead)
- Trace file I/O eliminated
- Users don't see debug logs in production
- Smaller binary size

---

## 10. Summary Checklist

**Before Releasing Module C:**

- [ ] Run Quick Smoke Test (Section 6.1) in Debug configuration
- [ ] All ticks <1ms average
- [ ] All ticks show 0 memory allocation (±50 bytes noise OK)
- [ ] All ticks show Thread 1 (UI thread)
- [ ] Task Manager shows <0.5% CPU over 10 minutes
- [ ] Task Manager shows 2 threads (stable)
- [ ] Memory stable in Task Manager (no growth)
- [ ] Snap-to fires correctly (trace log shows `[SNAP]` message)
- [ ] Cadence transitions smoothly (no sudden jumps in timing)
- [ ] Hidden window has same performance as visible
- [ ] Set FORCED_TRACE_DEFAULT = false in AppConfig.cs
- [ ] Rebuild in Release configuration
- [ ] Verify no `[PERF]` logs in Release build
- [ ] Verify trace logs don't exist in production build

**If All Checked: Module C Performance Validated! 🎉**

**End of Appendix B**

---

**End of Specification**