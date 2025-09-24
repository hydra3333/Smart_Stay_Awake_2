# Stay\_Awake - C# WinForms Re-implementation SPECIFICATION

## 0) Goal (What we’re doing)

We are re-imagining and re-developing the **Stay\_Awake** utility
originally written in Python (tkinter + Pillow + pystray + wakepy) into **C# (.NET 8, WinForms)**.

This is specifically **not** a line-for-line re-implementation of the example python program (although the 
visible GUI must be close to or perhaps nearly visibly identical).  It is a re-development into
C# using Visual Studio Community edition with specific intent to
* redesign the structure/code to make it easiler for novice developers and maintainers to manage
* avoid false positives by antivirus products on pyinstaller-created .exe's
* provide the same visible interface and the same functional outcome as the example python program

**From:** Python, script-based, interpreted, depending on Pillow, wakepy, pstray, tkinter, and windows system calls.    
**To:** C# .NET 8 (WinForms) compiled desktop app that builds to a portable, self-contained, standalone, single-file EXE which works on Windows 10/11 (x64 only)    

**Not** a direct translation of the example working python program: instead, a **clear and novice-friendly rewrite**, leveraging    
- C# idioms and WinForms with clear and expansive commenting to make it easiler for novice developers and maintainers to manage
- while matching the example python program (in commandline and visible GUI and behaviour and outcomes, though not necessarily code nor even structure):
  * all *CLI/GUI behavior*; GUI, image processing, tray icon, CLI options, etc
  * stay-awake behaviors, windows timer calls and countdowns, gui-update cadance bands, etc)

**Primary features requiring parity in outcome and visible GUI**:
  * **Prevent system sleep/hibernation** while the app runs (**display monitor sleep remains allowed**); Restores defaults on exit.
  * Main window hosts a squared (and possibly resized) image + labels + countdown fields (countdown related labels and fields are conditionally visible)
  * Windows System-tray icon (based on the final image) with Show/Hide/Quit
  * CLI options `--icon`, `--for`, `--until`, `--verbose`, with the same parsing rules, bounds, and mutual exclusivity of `--for`/`--until`
  * Low-cpu-usage timer/cadence behaviors (via time bands) near/far from deadline (adaptive, throttled when hidden); refer to the example python program

---

## 1) Coding rules (style + diagnostics)

* **Comprehensively commented program**: the code must be extremely well-commented and clarifying (eg including purpose and commandline options and examples etc at the top of the program) specifically to assist novice developers/maintainers who are unfamiliar with C#.
* **Novice-friendly comments**: clear descriptive comments intended to assist novice developers/maintainers
* **Novice-friendly code**: descriptively named methods and variables and types etc intended to assist novice developers/maintainers 
* **Novice-friendly Method/Function decriptions**: every method/function must have a block comment at the top describing its purpose, inputs, outputs, and side-effects

* Wherever possible: when making or using system calls or library calls or return values or constants or enums etc
  * import and use the formal structures, constants, enums, etc
  * where not possible, define/use them precisely as the system or library etc specifies
  * ensure 100% compliance with formal definitions and usage

* **Defensive programming**: always use **safe practices** in all approaches wherever possible
  * wrap meaningful blocks in `try/catch`
  * in `catch`, always also log **exception type, message, and stack trace** if debug tracing is on.
  * fail with a single **centralized `Fatal()` if recovery is not possible
    * Show a modal `MessageBox` with error details.
    * Clear stay-awake state (if set).
    * Cleans up
    * Exit with code 1.
  * Always use strict comprehensive null/argument validation
    * prefer `ArgumentException`/`InvalidOperationException` with helpful messages
  * Always use strict comprehensive return-value validation

* Diagnostics & tracing (developer + runtime):
  * Use Debugging traces everywhere.
  * Refer to Section 4.11 Tracing & Verbose Logging Policy
  * Apply this style of debug tracing messaging at a minimum and more messaging where appropriate to complex coding situations:
    * Enter/exit function: `"Entered function X: purpose: XYZ..."`, `"Exiting function X."`.
    * Start/End of code blocks: `"Start of code block ZZZ, purpose: XYZ..."`, `"End of code block block ZZZ."`.
    * TRY/CATCH blocks: `"Start of WWW TRY"`, `"Caught WWW CATCH: {ex}"`, `"End of WWW TRY"`.
    * Variable changes: `"Function X: Variable Y changed from A to B"`.
  * We always compile with TRACE defined. 
  * Runtime tracing is controlled per 4.11: --verbose and/or the developer-only global boolean FORCED_TRACE switch. 
  * When tracing is off, we clear all Trace listeners so nothing is emitted.

* **Naming Consistency**:
   * For constants use ALL_CAPS (UPPERCASE with words separated by `_`) 
   * For global variables use ALL_CAPS (UPPERCASE with words separated by `_`) 
   * For local variables use camelCase (camelCase means: first word lowercase, subsequent words capitalized)
   * For Enum members use PascalCase (PascalCase means: capitalize the first letter of every word, no underscores)
   * For methods, properties, and class names use PascalCase (PascalCase means: capitalize the first letter of every word, no underscores)

* **Iterative development** with testing in between iterations

* **Refer to the Python program for examples only** (only examples !) of what some things look like (eg the GUI) or how some things may be calculated or images are resized by edge replication, or how an icon created from image

---

## 2) Requirements & operation

### 2.1 Purpose

* Prevent **system sleep/hibernation** while running.
* Optionally, as specified (or not) on the CLI, **auto-quit** after a duration or at a local timestamp.
* Allow **display monitor to sleep**.
* Exit cleanly when requested or when timer expires, cleanup and release any 'stay-awake' state if necessary.

### 2.2 Command-line interface (exact semantics)

#### 2.2.1 `--icon PATH`
  * Use a specific image for the window and tray icon.
  * Search order priority:
    1. CLI `--icon PATH`
    2. embedded base64 variable (if non-empty)
    3. file named `Stay_Awake_icon.*` next to the EXE/script; supported types: **PNG, JPG/JPEG, BMP, GIF, ICO**
    4. Validation:    
      * File must exist.
      * Extension must be one of above (case-insensitive).
      * If unsupported: fatal error message, listing supported types.

#### 2.2.2 `--for DURATION`
  * Run for fixed duration, then quit gracefully.
  * Token form: **no spaces**.
  * Allowed syntax encompasses mixed numbers of days hours minutes seconds or parts thereof; examples:
    * `3d4h2m5s`, `2h`, `90m`, `3600s`, `1h30m3s`
    * Mixed units allowed (`1h30m15s`)
    * Bare number means **minutes**
    * Duplicate units forbidden (`1h2h`)
  * Bounds: **>= 10 seconds**, **<= \~366 days** (configurable global constants).
  * Value `0` disables auto-quit (runs indefinitely, no countdown etc visible in the gui).
  * Mutually exclusive with `--until`.
  * Before arming**, re-ceil the remaining time to whole seconds for a calm countdown.

#### 2.2.3 `--until "YYYY-MM-DD HH:MM:SS"`
  * Run until the given **local timestamp**, then quit gracefully.
  * Local timezone timestamp in **24-hour** format.
  * Quoted string, tolerant of extra spaces and 1–2 digit parts:
    * `"2025-01-02 23:22:21"`
    * `"2025- 1- 2  3: 2: 1"`
    * `"2025-1-2 3:2:1"`
  * Validation:
    * Date must exist (reject Feb 30, etc.).
    * Time must be >= 10s in future, <= \~366 days.
    * Reject DST *nonexistent* times (spring forward gap).
    * Reject DST *ambiguous* times (fall back overlap).
  * Mutually exclusive with `--for`.
  * Before arming**, re-ceil the remaining time to whole seconds for a calm countdown.

#### 2.2.4 `--verbose`
  * Used in computing `TRACE_ENABLED = FORCED_TRACE || args.Has("--verbose")`
  * When enabled, a trace log is written per 4.11.

#### 2.2.5 **Exit codes**
  * 0 = normal
  * 0 = normal timed-out auto-quit also exits with 0
  * non-zero = validation or runtime error

### 2.3 UI and Global behavior

* **Main Window**:
  * DPI-aware for maximum high quality image display fidelity   .
  * Contains:
    * Centered **image** (maximum size per global variable, initially 512 px; image is pre auto-sized to a square by **edge replication** (no solid-color padding) so it never need be stretched afterward).
    * Labels for: blurb, ETA, remaining time, cadence (frequency of gui countdown update, adjusts based on bands of time remaining, throttles when hidden, timer display snaps to neat boundaries when far from the deadline (calm UI)).
  * Window re-sized to cater for image size plus other labels and fields.
  * User Resizing disabled (via `FormBorderStyle.FixedSingle`, `MaximizeBox=false`).
* **Stay-Awake logic**:
  * On ready: call `SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_AWAYMODE_REQUIRED)`.
  * On quit: call `SetThreadExecutionState(ES_CONTINUOUS)` to clear.
  * please check/confirm this is the case with the working python program as the example .
* **`Fatal()` logic**: 
  * Explicitly, Fatal() must work pre-UI (using MessageBox.Show without depending on Form or NotifyIcon).
  * This avoids confusion later (when deciding if CLI validation can call Fatal before UI exists).
* **NotifyIcon visibility timing**:
  * The context menu may be created early, but `NotifyIcon.Visible = true` should only be set after the icon image is ready (to avoid flashing the default blank icon). This prevents confusion when coding step 1.4 vs 2.x.
* **Wrap every disposable** (Bitmap, Graphics, Stream, Icon) in `using()` unless ownership must persist.
  * This avoids GDI leaks.

---

## 3) Architecture outline (phased, iterative)

### 3.1 Main outline

1. **Setup/Init**    
  * Parse CLI -> validate (`--icon`, `--for`, `--until`, `--verbose`).    
  * Configure runtime tracing (see 4.11)    
    * Decide if tracing is enabled:  global variable enableTracing = FORCED_TRACE || (--verbose present)    
    * If enabled: create/overwrite a log file and attach a TextWriterTraceListener; set Trace.AutoFlush = true    
    * If disabled: Trace.Listeners.Clear() so no output goes anywhere    
  * Validate inputs    
  * Initialize the UI    
    * Initialize and configure WinForms (DPI-aware and ensuring AutoScaleMode = Dpi in the form) to provide message dialogs, error popups, Fatal(msg), main window with controls etc.    
    * User Resizing disabled hook basic keyboard/mouse.    
    * The form layout and labels and buttons are to be positioned and look exactly like as used in the example python program    
    * Build context menu ("Show/Minimize/Quit") per the python example    
    * Fatal(msg) on error: modal error dialog, **clear stay-awake** if set, exit.    
      * Note: Fatal() must be callable before the Form exists. Pre-UI Fatal must only show a MessageBox and exit; no tray or form interaction.    
    * The Image and the Tray icon will be done in a future step and not here    
  * Initialize Time/Timezone helpers:    
    * use always-safe approach; `TimeZoneInfo.Local`with added Win32 verification for DST edge cases.    

2. **Image and Icon preparation and insertion**    
  * Load and prepare image in memory, if required making image square by edge-replication (max px size according to a global parameter, adjust size if and as required eg aligning with the working example python program)
  * Using the image, build a multi-size icon in memory (sizes=[(16,16),(20,20),(24,24),(32,32),(40,40),(48,48),(64,64),(128,128),(256,256)]) for use as the windows system-tray icon.
  * **Image and Icon insertion**    
    * Insert squared image in memory into PictureBox.
    * Use multi-size icon as the icon in the windows system-tray.

3. **Timers & updates**    
  * Arm countdown timer(s) based on validated `--for` or `--until`.
  * Update labels and tray tooltip.
  * Use adaptive cadence (frequency of gui countdown update, adjusts based on bands of time remaining, throttles when hidden, timer display snaps to neat boundaries when far from the deadline (calm UI))

4. **Stay-Awake logic**    
  * Set thread execution state.
  * Ensure revert on quit.

5. **Cleanup**
  * Dispose tray icon.
  * Dispose trace listener if attached
  * Exit application.

### 3.2 Alternative outline notation, hopefully consistent with 3.1

```
1. Setup/Init
   1.1 Parse CLI -> validate (log paths, image file, etc.)
   1.2 Configure runtime tracing (see 4.11)
       ├─ Decide if tracing is enabled:  global variable enableTracing = FORCED_TRACE || (--verbose present)
       ├─ If enabled: create/overwrite a log file and attach a TextWriterTraceListener; set Trace.AutoFlush = true
       └─ If disabled: Trace.Listeners.Clear() so no output goes anywhere
   1.3 Validate inputs (paths, duration/DT bounds, exclusivity)
       └─ On error: Fatal(msg) - modal MessageBox, pre-UI safe
   1.4 This requirement has been discarded: single-instance mutex
   1.5 Initialize the UI (WinForms)
       ├─ Ensure STA and ApplicationConfiguration.Initialize()
       ├─ Provide message dialogs, error popups (shows modal MessageBox with error?)
       ├─ fatal(msg): centralized, no dependency on Form/NotifyIcon
       └─ Prepare main form shell (FixedSingle, AutoScaleMode=Dpi) and contents
            ├─ The form layout and labels and buttons are to be positioned and look exactly like as used in the example python program
            ├─ Image widget (PictureBox or custom control)
            ├─ Label fields for calculated values
            ├─ Hook keyboard/mouse events
            ├─ Disable user resize and maximize button
            ├─ Create system tray menu (NotifyIcon + ContextMenuStrip)
            └─ "Show Window" / "Minimize to System Tray" / "Quit"
   1.6 Init time/timezone helpers
       └─ TimeZoneInfo.Local + Win32 check for DST edges
2. Image preparation and insertion
     ├─ Load image (from an internal base64 variable, or --icon, falling back to images in disk, whatever order is specified in this spec, fallback to the order the python program does it)
     ├─ manipulate image in memory to make it square and even number of pixels WxH using edge replication (no solid-color padding)
     ├─ in memory (sizes=[(16,16),(20,20),(24,24),(32,32),(40,40),(48,48),(64,64),(128,128),(256,256)]) for use as the windows system-tray icon
     ├─ Adjust window size if required
     ├─ Insert squared image in memory into PictureBox.
     ├─ Use multi-size icon as the icon in the windows system-tray; Assign NotifyIcon.Icon and set Visible = true
     └─ Adjust window size if required
3. Timers & Updates (System.Windows.Forms.Timer (UI thread), not System.Timers.Timer)
   ├─ Start timers (System.Windows.Forms.Timer (UI thread), not System.Timers.Timer) (Timers are on the UI thread, simplifying updates.)
   ├─ Periodically recalc and update labels according to cadence (countdown update frequency) and related specifications
   └─ Throttle updates to avoid repaints storms
4. Stay-Awake Logic
   ├─ Once UI + timers are stable, call SetThreadExecutionState
   └─ On quit/exit: Clear, restore defaults, cleanup, exit
5. Cleanup
   ├─ Dispose of NotifyIcon
   └─ Application.Exit()
```

---

## 4) Technical Clarifications

### 4.1 CLI parsing & validation

* Simple parser supporting:
  * Flags: `--verbose`
  * Key/Value: `--icon=PATH`, `--for=...`, `--until="..."`
* Mutual exclusion: `--for` **xor** `--until`.
* Validate:
  * `--icon`: exists and is supported format (we’ll load with **ImageSharp** or System.Drawing on Windows).
  * `--for`: parse composite tokens to total seconds; apply min/max bounds; 0 disables. (parity with Python)
  * `--until`: tolerate whitespace and 1–2 digit parts; turn into a **local epoch**; reject **nonexistent** and **ambiguous** times (DST transitions) with clear messages. (parity with Python)

### 4.2 Time/Timezone strategy (reliable)

* Primary: `TimeZoneInfo.Local` + `DateTimeOffset` for everyday logic.
* DST edge validation (mirrors Python’s two-pass logic):
  * Attempt two conversions (as if STD vs DST).
  * If neither maps to the same civil components -> nonexistent -> error.
  * If both map but produce different instants -> ambiguous -> error. (guide the user to choose another time.)
* Verification via Win32 `GetDynamicTimeZoneInformation` for zone bias/DST rules.

### 4.3 Image/icon handling

* Load image from source priority order
  * CLI --icon PATH
  * embedded base64
  * Stay_Awake_icon.* next to EXE
    * Supported file types: PNG, JPG/JPEG, BMP, GIF, ICO. Reject others with a clear error.
* Square by Edge Replication (no solid-color padding)
  * Compute delta = max(width,height) − min(width,height)
  * Create a square canvas of size max(width,height) in 32bpp ARGB
  * Draw the source centered
  * If portrait (height > width):
    * Fill left margin by stretching the leftmost 1px column of the drawn image to the canvas edge
    * Fill right margin by stretching the rightmost 1px column of the drawn image to the canvas edge
  * If landscape (width > height):
    * Fill top margin by stretching the topmost 1px row to the canvas edge
    * Fill bottom margin by stretching the bottommost 1px row to the canvas edge
  * Note: If delta is odd, place the extra 1px on right/bottom to make it square
  * Result: a square image with replicated edges; no solid-color bars; no subject distortion
  * Finally, if the result not an even number of pixels vertically then make it an even numbered square
    * Edge replicate 1 additional bottommost row and 1 rightmost column of pixels to make an even numbered square image
* Then if necessary scale the resulting squared image for:
  * Window image: scale the squared image so it is 512px (configurable via a global variable) using high-quality resampling
* Build one multi-size **Icon** from the scaled squared image in memory (sizes=[(16,16),(20,20),(24,24),(32,32),(40,40),(48,48),(64,64),(128,128),(256,256)] PNG-compressed) for use both as
  * NotifyIcon in the windows system-tray (NotifyIcon.Visible should be set only after the multi-size icon is ready, to avoid flashing the default icon.)
  * the app icon (ie when creating a shortcut to the app on the desktop) 

> For clarification, here is some pseudocode to square the image by edge replication:
```
loadOriginal(pathOrEmbedded) -> Bitmap src
if src.Width == src.Height:
    square = src
else:
    size = max(src.W, src.H)
    square = new Bitmap(size, size, PixelFormat32ARGB)
    using g = Graphics.FromImage(square):
        g.InterpolationMode = HighQualityBicubic
        // draw centered
        x = (size - src.W) / 2
        y = (size - src.H) / 2
        g.DrawImage(src, x, y, src.W, src.H)
        if src.W < src.H: // portrait -> replicate left/right columns
            // left strip
            leftSrc = Rectangle(x, y, 1, src.H)
            leftDst = Rectangle(0, y, x, src.H)
            g.DrawImage(square, leftDst, leftSrc, Unit.Pixel)
            // right strip
            rightSrc = Rectangle(x+src.W-1, y, 1, src.H)
            rightDst = Rectangle(x+src.W, y, size-(x+src.W), src.H)
            g.DrawImage(square, rightDst, rightSrc, Unit.Pixel)
        else: // landscape -> replicate top/bottom rows
            topSrc = Rectangle(x, y, src.W, 1)
            topDst = Rectangle(x, 0, src.W, y)
            g.DrawImage(square, topDst, topSrc, Unit.Pixel)
            botSrc = Rectangle(x, y+src.H-1, src.W, 1)
            botDst = Rectangle(x, y+src.H, src.W, size-(y+src.H))
            g.DrawImage(square, botDst, botSrc, Unit.Pixel)
iconSizes = [16,20,24,32,40,48,64,128,256]
icons = [ resize(square, s, s) for s in iconSizes ]
ico = buildIcoContainer(icons, pngFor256=true)
return square, ico
```

#### 4.3.1 Multi-image ICO (no external dependencies)

**Objective:** From one arbitrary input image (PNG/JPG/JPEG/BMP/GIF/ICO), produce:

* A **squared master bitmap** using *edge replication* (no solid padding).
* A **multi-image .ico** containing these frames (all PNG inside the ICO):
  **sizes = \[16, 20, 24, 32, 40, 48, 64, 128, 256]** (pixels, square).

**Rationale:**

* Keeps the project **dependency-free** (no ImageSharp).
* Windows 10/11 fully support **PNG frames** in ICOs.
* One clean implementation path (no DIB/AND-mask quirks).
* NotifyIcon.Visible should be set only after the multi-size icon is ready, to avoid flashing the default icon.

##### A) Responsibilities (3 tiny helpers)

1. **ImageLoader** (input orchestration)

   * **Inputs:** CLI `--icon` path (preferred), embedded base64 (optional), `Stay_Awake_icon.*` next to EXE (fallback), internal glyph (last resort).
   * **Outputs:** `Bitmap original` (32bpp ARGB).
   * **Rules:**
     * Supported extensions: `.png`, `.jpg/.jpeg`, `.bmp`, `.gif`, `.ico`.
     * For GIF: first frame only.
     * For ICO input: pick the **largest** frame in the file as `original`.
     * Validate file exists and is readable; else fail with `Fatal()`.

2. **ImageSquareReplicator** (edge replication, no distortion)

   * **Input:** `Bitmap original` (any WxH).
   * **Output:** `Bitmap square` (SxS where `S = max(W, H)`, 32bpp ARGB).
   * **Algorithm (paint-friendly, no per-pixel loops):**
     1. Create a new **square canvas** `S × S` (32bpp ARGB).
     2. Draw the source **centered** (high-quality resampling **off** here-this draw is 1:1).
     3. If portrait (`H > W`): fill the left and right margins by **stretching** the **leftmost** and **rightmost 1-pixel columns** of the drawn image, respectively, to the canvas edges.
        If landscape (`W > H`): likewise stretch the **topmost** and **bottommost 1-pixel rows** to fill margins.
     4. If the difference is **odd**, put the extra pixel on **right** (horizontal) or **bottom** (vertical) for determinism.
   * **Quality:** use 32bpp ARGB surfaces; preserve alpha; dispose `Graphics` and `Bitmap` with `using`.

3. **IconWriter** (multi-image, all-PNG ICO composer)

   * **Inputs:** `Bitmap square`, `int[] sizes = {16,20,24,32,40,48,64,128,256}`.
   * **Outputs:** `Icon multiImageIcon` (usable for **NotifyIcon** and as **Application Icon**).
   * **Process:**
     1. **For each** size in `sizes`:
        * Resample **directly from `square`** (never chain resizes) with **HighQualityBicubic**.
        * Encode the result as **PNG bytes** into a `MemoryStream` (`bitmap.Save(ms, ImageFormat.Png)`).
        * Record: width, height (cap at 255 for ICO directory fields; special-case 256 -> write 0 per ICO spec), bit depth (use 32), data length, and a placeholder for the data offset.
     2. Write ICO container into a new `MemoryStream`:
        * `ICONDIR` header (type=1 icon, count=frames).
        * `ICONDIRENTRY` array: one entry per size, **with data offsets computed after the directory**.
        * Append each PNG payload sequentially; patch offsets/sizes as needed.
     3. Construct an `Icon` from the stream (keep the stream alive as needed), or copy to a new stream for an independent `Icon`.
   * **Policy:** **All frames as PNG** (including 16–128). This is fully supported on Win10/11; performance impact is negligible.

##### B) DPI & runtime selection

* **Multi-image ICO lets Windows pick the best frame** for the current DPI automatically (Explorer/taskbar/tray).
* Sizes chosen cover common DPI scales:
  * 100% -> 16, 125% -> 20, 150% -> 24, 200% -> 32, 250% -> 40, 300% -> 48, 400% -> 64, etc.
* You can still query DPI (Per-Monitor V2) if you want to **verify** which frame is used, but it’s not required.

##### C) Memory & lifetime (important)

* `NotifyIcon.Icon` should be assigned an `Icon` whose backing stream stays alive **until dispose** (if constructing from a stream).
* If you ever use `Icon.FromHandle(hIcon)` (not required here), you **must** call `DestroyIcon(hIcon)` when done to avoid leaking GDI handles.
* Always `Dispose()` of:
  * All temporary `Bitmap` instances,
  * All temporary `Graphics`,
  * The `NotifyIcon` at shutdown.

##### D) Edge cases & validation

* **Tiny images** (e.g., 12×12): square them, then allow upscale-cap excessive upscale (e.g., ≤ 4×) for the **window** image to avoid visible blur; for **icons**, you still generate all sizes (the small source will blur at larger frames-acceptable for v1).
* **Huge images**: consider a **max source size** (e.g., downscale to ≤ 1024×1024) before squaring to keep memory bounded.
* **Color profile/EXIF**: System.Drawing ignores most profiles; acceptable for utility use. (Document this.)
* **Alpha**: ensure backgrounds remain transparent-no premultiplied color fringes (use ARGB canvases only).
* **GIF**: first frame only.

##### E) Public APIs (no code, just contracts)

* `class ImageLoader`
  * `Bitmap LoadOriginalFromSources(string? cliPathOrNull)`
    *Resolves the priority chain (CLI -> embedded base64 -> sidecar file -> fallback glyph), validates extension, loads and returns a 32bpp ARGB bitmap. Throws on error.*
* `class ImageSquareReplicator`
  * `Bitmap MakeSquareByEdgeReplication(Bitmap original)`
    *Returns a new ARGB bitmap (S×S) with replicated edges; the caller owns disposal.*
* `class IconWriter`
  * `Icon BuildMultiImageIconFromSquare(Bitmap square, IReadOnlyList<int> sizes)`
    *Returns an in-memory `Icon` containing **PNG frames** at the specified sizes. The caller disposes the returned `Icon`.*
* **Optional convenience**:
  * `Icon BuildDefaultMultiImageIcon(Bitmap original)` -> internally calls `MakeSquareByEdgeReplication()` then `BuildMultiImageIconFromSquare()` with the default sizes array.

##### F) Application integration points

* **Tray**:
  `notifyIcon.Icon = multiImageIcon;`
  `notifyIcon.Text = "Stay Awake - running (ETA 12:34)";` *(tooltip updated by timers)*
* **Window title & form icon** (optional):
  `this.Icon = multiImageIcon;` *(WinForms window chrome icon)*
* **Application Icon** (Explorer/shortcuts):
  * At **build time** you can still pick a design-time ICO in Project Properties; or
  * At **runtime** you can also save the memory ICO to disk once (e.g., `%LocalAppData%`) and set it for shortcuts you create programmatically (if any).
  * For publishing a static EXE, it’s typical to set a **design-time ICO** in Project Properties that matches what you build at runtime.

##### G) Tests (what "done" looks like)

1. **Load**: PNG/JPG/BMP/GIF/ICO source paths load; non-existent path -> fatal with clear message.
2. **Square**: non-square inputs produce perfectly square outputs with **replicated edges**, no solid bars, no distortion.
3. **Even**: make the square an even number of pixels.
4. **Resize**: if necessary, resize the image to the specified number of pixels.
5. **Icon**: resulting `.ico` has frames at exactly `[16,20,24,32,40,48,64,128,256]`.
6. **Visual**:
   * On 100%/125%/150%/200% DPI monitors, tray and taskbar icons look **crisp** (no fuzzy scaling).
   * Explorer shows a sharp **256×256 thumbnail** for the app if you set it as Application Icon.
7. **Resource hygiene**: pin a breakpoint in `FormClosing` and confirm `NotifyIcon.Dispose()` is called; no GDI handle leaks.

##### H) Future options

* **DIB for small frames** (hybrid encoding): match historical toolchains (≤48 as DIB + AND mask, 256 as PNG). Not necessary for Win10/11; only if you need maximum cross-tool compatibility.

### 4.4 Stay-Awake logic (Win32)

* Use **`SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_AWAYMODE_REQUIRED)`** when armed, and call again with `ES_CONTINUOUS` on **quit** to clear.
* Verify these states with what is used in the working example python program
* In testing, verify requests with `powercfg -requests` (docs mention this in Python README).

### 4.5 Timers & cadence

* Calculate a **deadline** using a **monotonic** clock concept (`Stopwatch`/`Environment.TickCount64`) to avoid system clock jumps, while also maintaining a **wall-clock ETA** for display.
* Adaptive cadence (GUI update frequency), e.g. 10 min cadence (GUI update frequency) far out -> 1 sec cadence (GUI update frequency) near the end
  * When far from the deadline, align (snap) the next tick to a clean boundary (eg the nearest 00 or 30 seconds) to avoid visual jitter
  * Throttle the cadence (GUI update frequency) when the main window is hidden - parity with the example working Python program’s behavior.
* Use System.Windows.Forms.Timer to avoid cross-thread Invoke() calls when updating labels/tooltip.

### 4.6 Conditional (per a global boolean variable) Single-instance guard

* This single-instance requirement has been discarded.

### 4.7 Error handling & `Fatal()` policy

* A single `Fatal(string msg, Exception? ex = null)`:
  * Writes to Debug + optional log path from CLI.
  * Shows `MessageBox` with helpful text.
  * If stay-awake is armed, **clears** it .
  * Possibly calls `Environment.Exit(1)`.

### 4.8 QuitMode enum

Define and use a simple enum to clarify flow:

```csharp
enum QuitMode
{
    None,
    ForDuration,
    UntilDateTime
}
```

### 4.9 AppState Globals

```csharp
internal static class AppState
{
    public static string AppName = "Stay_Awake";
    public static bool DEBUG_IS_ON = false;

    public static string? ImagePath;
    public static string? LogPath;

    public static QuitMode QuitMode = QuitMode.None;
    public static DateTimeOffset? TargetLocal;
    public static TimeSpan? ForDuration;

    // and, I suppose, any other necessary global variables here ?
    // and, I suppose, a few to query:
    // ? long? TargetMonotonicTicks (deadline for stable countdown)
    // ? Icon/Image buffers : keep resized variants cached in memory to avoid repeated work.
}
```

### 4.10 ImageAssets struct

```csharp
struct ImageAssets
{
    public Bitmap Original;
    public Bitmap Squared;
    public Icon MultiSizeIcon;
}
```

### 4.11 Verbose tracing & log file (runtime; FORCED_TRACE + --verbose)

**Goal**: 
* Provide ample developer and user-visible diagnostics without external tools. 

**Using**
* Use Trace.WriteLine(...) everywhere (not Debug.WriteLine). 
* We do not toggle the compile-time TRACE symbol at runtime; instead, we attach/detach Trace listeners based on `--verbose` and global variable or constant `FORCED_TRACE` set to true or false by the developer.

#### 4.11.1 Controls & globals

* Developer-only boolean switch:
  * FORCED_TRACE (bool; default false). If true, tracing is always enabled regardless of CLI.
* Runtime flag:
  * `TRACE_ENABLED` (bool) - computed at startup:
  * `TRACE_ENABLED = FORCED_TRACE || args.Has("--verbose")`
* Paths (globals):
  * `LOG_FILE_BASENAME` = `"Stay_Awake_Trace_{yyyyMMdd}.log"` (zero-padded date fields)
  * `LOG_PRIMARY_DIR` = `<directory containing the EXE>`
  * `LOG_FALLBACK_DIR` = `%LocalAppData%\Stay_Awake\Logs`
  * `LOG_FULL_PATH` (resolved final path; stored in AppState)
* Listener name: `"FileTraceListener"`

#### 4.11.2 Runtime setup (executed in Setup/Init 3.1.2)

* If `TRACE_ENABLED == true`:
  1. `Trace.Listeners.Clear()` (remove default listener; nothing goes to OS debug stream).
  2. Compute LOG_FULL_PATH:
      - Try primary: `<EXE folder>\Stay_Awake_Trace_YYYYMMDD.log` with overwrite semantics (FileMode.Create).
      - If access denied or IO error, create the folder `%LocalAppData%\Stay_Awake\Logs` and set LOG_FULL_PATH to that location.
  3. Create a `TextWriterTraceListener(LOG_FULL_PATH) named `"FileTraceListener"`.
  4. `Trace.Listeners.Add(...)`; `Trace.AutoFlush = true` (recommended: `Trace.UseGlobalLock = true`).
  5. Emit a header line with absolute path and UTC/local timestamp, app version, CLI args (minus secrets), and DPI/machine basics.
* If `TRACE_ENABLED == false`:
  1. `Trace.Listeners.Clear()` and do not add any listener (no output anywhere).
* When tracing is off, we clear all Trace listeners so nothing is emitted.

#### 4.11.3 Usage guidance

* Use `Trace.WriteLine(...)` consistently for all diagnostics (including what was formerly considered "Debug").
* Use and Add consistent context prefixes (e.g., `[Init]`, `[Image]`, `[Icon]`, `[Timer]`, `[Stay_Awake]`) to the start of each trace line, for easy grepping.
* Example patterns (not code):
  - "`[Init] Entered ParseCli()`", "`[Init] Exiting ParseCli()`"
  - "`[Image] MakeSquare: 320×200 -> 320×320 (replicated edges)`"
  - "`[Timer] Next tick in 10s; cadence=1s; remaining=00:12:34`"

#### 4.11.4 Error handling & fallbacks

* If both primary and fallback log paths fail (very rare), keep `Trace.Listeners.Clear()` and provide code for (but disabled with a fixed boolean False) show a one-liner `MessageBox` stating: "Verbose logging could not be started; continuing without a log file."
* Do not `Fatal()` just because logging failed.

#### 4.11.5 Lifecycle

* On normal exit or fatal exit, flush/close the "listener" (disposing the `TextWriterTraceListener`) so the file handle is released.
* When auto-quit triggers (valid `--for/--until`): exit code 0 (success), log a final line "`graceful exit`"

#### 4.11.6 Security/PII

* Avoid logging full file contents or secrets.
* Truncate very long CLI strings. 
* If credentials are used, redact them with `*`.
* The log file preverably lives next to the EXE, or fallback in `%LocalAppData%` - document the path in the first log line.

---

## 5) Testing plan (iterative)

### Iteration 1 (Setup/Init only)

* `--help` prints usage.
* `--icon C:\badpath.png` -> error.
* `--for 2h`, `--for 90m`, `--for 0` all valid.
* `--for nonsense` -> error.
* `--until "2025-1-2 3:2:1"` accepted.
* `--until "2025-02-30 12:00:00"` rejected.
* Launch twice -> second exits with message.

### Iteration 2+

* Tray icon displays.
* Tooltip updates with ETA.
* Timers tick at adaptive cadence.
* Stay-Awake state toggled.

### DST tests

* Pass nonexistent local time (spring forward) -> reject.
* Pass ambiguous local time (fall back) -> reject.

---

## 6) Development Environment & Toolchain (Hyper-V VM)

### 6.1 Baseline

* **Hyper-V VM**: Windows 11 Enterprise Evaluation (Gen2).
* **Visual Studio 2022 Community (latest)** with workload: **.NET desktop development**.
* **.NET 8 SDK** + Windows 11 SDK installed.
* Project targets `net8.0-windows`.
* Manifest enables `PerMonitorV2` DPI awareness.

### 6.2 Publish profile (for portable EXE)

* Visual Studio -> **Publish...** -> **Folder** profile:
   * **Mode**: Folder.
  * **Target runtime**: **win-x64**
  * **Deployment mode**: **Self-contained**
  * **Produce single file** = on
  * **Trim** = **off** initially (safer for WinForms)
  * **ReadyToRun** = on (faster startup, larger file)
  * **Configuration**: Release

### 6.3 Exporting/copying things

* **Export VS settings**: *Tools -> Import and Export Settings -> Export selected environment settings* -> save `.vssettings`. Copy to host via shared folder/ISO/OneDrive/etc.

* **Share/copy solution**:
  * Zip the solution folder (exclude `.vs` if you like) and copy to host.

* **Copy published EXE from VM to host**:
  * Use Hyper-V **Enhanced Session** to access `\\tsclient\<drive>` (clipboard/drive redirection), or
  * Create a **shared folder** via SMB on the host and map it from VM, or
  * Use cloud drive (OneDrive/Dropbox), or
  * Mount a temporary **VHDX** as a transfer volume.

* **VM hygiene**:
  * Use **Checkpoints** before major installs.
  * Keep a **"golden" VM** (fresh VS + SDKs + Windows updated) and clone for experiments.

---

## 7) Developer guidance (novice-friendly)

* **Think like a maintainer**: assume the next dev is a beginner.
* **Prefer clarity over performance**.
* **Name things verbosely and with clarity of purpose**.
* **Never skip comments**: no code should rely solely on context/content.
* **Avoid premature optimization**: instead favor **clarity** and **comments**.
* **Encapsulate** tray logic in a helper class - don’t bloat `Form1.cs`.
* **Keep all cross-cutting logic in one place** to avoid leaks. (fatal, stay-awake on/off) .
* **Prefer pure methods** for parsing/formatting (easy to unit test later).
* **Unit-test parsing functions separately** (e.g., ForDuration parser).
* **Iterate**: implement, run, and test small slices before proceeding ; In Visual Studio after each step, **F5** and verify in the Output window.

---

# Appendix A - Behavior examples (CLI)

Assuming we define global constants along the lines:
```csharp
MIN_AUTO_QUIT_SECS = 10         // 10 seconds
MAX_AUTO_QUIT_SECS = 31557600   // (=365.25 days * 24 * 60 * 60)
```
then the Bounds below are controlled by constants `MIN_AUTO_QUIT_SECS` and `MAX_AUTO_QUIT_SECS`.
```text
--icon PATH
    Use a specific image file for the window/tray icon.
    Supports: PNG, JPG/JPEG, BMP, GIF, ICO.

--for DURATION
    Keep awake for a fixed time, then quit gracefully.
    DURATION accepts days/hours/minutes/seconds in one token (no spaces):
      3d4h5s, 2h, 90m, 3600s, 1h30m
    A bare number means minutes. 0 disables auto-quit.
    Bounds: >= MIN_AUTO_QUIT_SECS and <= MAX_AUTO_QUIT_SECS.
    Mutually exclusive with --until.

--until "YYYY-MM-DD HH:MM:SS"
    Local 24-hour timestamp. Relaxed spacing & 1–2 digit parts accepted:
      "2025-01-02 23:22:21"
      "2025- 1- 2  3: 2: 1"
      "2025-1-2 3:2:1"
    Honors DST rules; rejects nonexistent/ambiguous local times.
    Bounds: >= MIN_AUTO_QUIT_SECS and <= MAX_AUTO_QUIT_SECS.
    Mutually exclusive with --for.
```


(These mirror the documented Python app and source logic.)

---

# Appendix B — Draft initial Visual Studio file–project structure for consideration

> Target: **.NET 8**, **WinForms**, single-EXE publish (self-contained optional).
> Namespace: `Stay_Awake.
> Resources you said you have: a **PNG image** (window content) and a **PNG icon** (used for desktop shortcut/App Icon). We’ll also embed a tiny fallback glyph.


## B.1 Solution & project layout

```
Stay_Awake/                              <- Solution root (on disk)
├─ Stay_Awake.sln                        <- Double-click this to open in VS
├─ .gitignore                            <- (optional, for Git)
├─ README.md                             <- (optional, project docs)
└─ docs/                                 <- Source code container
└─ src/                                  <- Source code container
   └─ Stay_Awake/                        <- WinForms project root
      ├─ Stay_Awake.csproj               <- Project file
      ├─ Program.cs                      
      ├─ AppState.cs                     
      ├─ Fatal.cs                        
      ├─ PowerManagement/                <- Namespace: Stay_Awake.PowerManagement
      │  └─ ExecutionState.cs
      ├─ Cli/                            <- Namespace: Stay_Awake.Cli
      │  ├─ CliOptions.cs
      │  └─ CliParser.cs
      ├─ Imaging/                        <- Namespace: Stay_Awake.Imaging
      │  ├─ ImageLoader.cs
      │  ├─ ImageSquareReplicator.cs
      │  └─ IconWriter.cs
      ├─ Time/                           <- Namespace: Stay_Awake.Time
      │  ├─ TimezoneHelpers.cs
      │  └─ CountdownPlanner.cs
      ├─ UI/                             <- Namespace: Stay_Awake.UI
      │  ├─ MainForm.cs
      │  ├─ MainForm.Designer.cs
      │  └─ TrayManager.cs
      ├─ Properties/
      │  └─ Resources.resx
      ├─ Assets/
      │  ├─ Stay_Awake_icon.png
      │  └─ Stay_Awake_icon.ico
      ├─ Publishing/
      │  └─ FolderProfile.pubxml
      ├─ app.manifest
      └─ NativeMethods.txt               <- For CsWin32
```

### Notes

* **UI split**: `MainForm` focuses on UI, `TrayManager` on NotifyIcon/menu.
* **Imaging split** keeps responsibilities crisp and matches 4.3/4.3.1.
* **Cli/** isolates parsing so `Program.cs` remains short and readable.
* **PowerManagement/** holds SetThreadExecutionState wrapper and the “armed” boolean for `Fatal()` cleanup.

---

## B.2 Application icon & runtime tray icon

* **Build-time Application Icon**
  * Use **Project -> Properties -> Application -> Icon** and point to a prebuilt **.ico** (multi-image) you already have.
  * Keep the **source PNG** you used for that .ico under `src/Stay_Awake/Assets/app_icon_256.png` (for provenance).

* **Runtime tray icon**
  * Built at startup in memory (`IconWriter`) from whichever image `ImageLoader` chooses (CLI -> embedded -> sidecar -> fallback).
  * `TrayManager` sets it only **after** the icon is ready (avoid blank icon flash).

---

## B.3 Resources vs content

* **Embedded** (via `Resources.resx`):
  * A very small fallback glyph (PNG) used when no file is provided/found.
  * Pro: always available; Con: increases assembly size slightly.

* **Content on disk** (`Assets/`):
  * Your window PNG and source PNG for the app icon; mark them **Copy to Output Directory -> Copy if newer** only if you want them deployed alongside the EXE for testing.
  * Final distribution can rely on embedded fallback + CLI `--icon`.

---

## B.4 csproj essentials (snippets)

**Target framework, WinForms, and Application Icon**

```xml
<Project Sdk="Microsoft.NET.Sdk.WindowsDesktop">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>

    <!-- High DPI via manifest; app icon set in VS UI (generates this) -->
    <ApplicationIcon>Assets\AppIcon.ico</ApplicationIcon>

    <!-- Useful in Release publish -->
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  </PropertyGroup>

  <ItemGroup>
    <!-- Optional: include content files for dev runs -->
    <Content Include="Assets\window_image.png">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
    <Content Include="Assets\AppIcon.ico" />
  </ItemGroup>
</Project>
```

> If your .ico lives elsewhere, update the `<ApplicationIcon>` path accordingly.
> Your **embedded fallback PNG** goes into `Properties/Resources.resx`, not via `<Content>`.

---

## B.5 Publish profile (Folder)

`src/Stay_Awake/Publishing/FolderProfile.pubxml`

```xml
<Project>
  <PropertyGroup>
    <Configuration>Release</Configuration>
    <Platform>Any CPU</Platform>
    <PublishDir>..\..\..\dist\Stay_Awake\</PublishDir>
    <PublishProtocol>FileSystem</PublishProtocol>
    <TargetFramework>net8.0-windows</TargetFramework>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <IncludeAllContentForSelfExtract>true</IncludeAllContentForSelfExtract>
    <DebugType>none</DebugType>
    <!-- Trim left off initially to avoid trimming surprises -->
    <PublishTrimmed>false</PublishTrimmed>
  </PropertyGroup>
</Project>
```

> Use **Build -> Publish** and select this profile.
> You can later enable trimming after tests (your spec keeps it off for now).

---

## B.6 Where each responsibility lives (quick map)

* **Program.cs**
  * `[STAThread]`, `ApplicationConfiguration.Initialize()`.
  * Parse CLI -> **7 tracing setup** -> basic validation -> create `AppState` -> `Application.Run(new MainForm(appState))`.

* **AppState.cs**
  * Readonly config and runtime flags (e.g., `TRACE_ENABLED`, `LOG_FULL_PATH`, timer plan, quit mode).
  * Constants: min/max duration bounds, default window size, DPI policy, etc.

* **Fatal.cs**
  * `Fatal(string msg, int exitCode=1)`; pre-UI safe; clears stay-awake if armed; shows `MessageBox` (no icon/tray deps); logs one line if tracing.

* **Cli/**
  * `CliParser` converts `string[] args` -> `CliOptions`.
  * Enforces mutual exclusivity of `--for` / `--until`, validates bounds, sanitizes `--icon`.
  * Returns human-readable errors used by `Fatal()`.

* **Imaging/**
  * `ImageLoader`: picks source (CLI -> embedded -> sidecar -> fallback), loads to 32bpp ARGB `Bitmap`.
  * `ImageSquareReplicator`: edge replication to square (no color bars).
  * `IconWriter`: creates **multi-image, all-PNG .ico** (16,20,24,32,40,48,64,128,256).

* **Time/**
  * `TimezoneHelpers`: `TimeZoneInfo.Local`, DST boundary checks vs Win32.
  * `CountdownPlanner`: `--for/--until` -> absolute target -> cadence schedule.

* **Stay_Awake/**
  * `ExecutionState`: P/Invoke to `SetThreadExecutionState`, tracks “armed” flag so `Fatal()` can revert safely.

* **UI/**
  * `MainForm`: constructs layout (PictureBox, labels), hooks keyboard/mouse; hosts a **`System.Windows.Forms.Timer`** for updates (UI thread).
  * `TrayManager`: creates `NotifyIcon`, `ContextMenuStrip` (“Show”, “Minimize”, “Quit”). Sets `Visible=true` **only after** icon is ready.

---

## B.7 Where to put your two PNGs (your note)

* **Shortcut/App icon**: keep the final **`.ico`** at `src/Stay_Awake/Assets/AppIcon.ico` and set it in **Project Properties -> Application**.
  * Keep your **source `app_icon_256.png`** alongside for reference, but not required at runtime.

* **Window image**: put your **`window_image.png`** in `Assets/` for dev; the production app will normally get the image from `--icon` or embedded fallback.

---

## B.8 Housekeeping

* **app.manifest**: ensure PerMonitorV2 DPI + longPathAware are enabled (already in your spec).
* **Tracing**: as per 7—`FORCED_TRACE` and `--verbose` logic; `Trace.Listeners.Clear()` when off.
* **Disposal**: every `Bitmap/Graphics/Icon/Stream/NotifyIcon` in `using` or disposed explicitly; `ExecutionState` reverted on exit.

## B.9 Housekeeping — Visual Studio Community Edition IDE settings we changed

These are **developer-environment** settings (inside Visual Studio), not code.     
They ensure the **WinForms project** behaves consistently across reopens and on new machines.

### B.9.1 High DPI & scaling

* **Where:** `Project -> Properties -> Application -> Manifest`
* **What we did:**
  * Switched from “Embed manifest with default settings” to **“Use a custom manifest”**.
  * In `app.manifest`, enabled:
    ```xml
    <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">PerMonitorV2</dpiAware>
    <longPathAware xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">true</longPathAware>
    ```
* **Why:** Ensures crisp DPI scaling on Win10/11 multi-monitor setups, and avoids path-length issues.

### B.9.2 WinForms Designer availability

* **Where:** `Tools -> Options -> Windows Forms Designer` (and/or `Project -> Properties`)
* **What we checked:**
  * Confirmed the **out-of-process WinForms Designer** is available and loading (sometimes fails unless correct targeting pack installed).
  * Made sure the project targets **.NET 8.0 (Windows)**, not “.NET Framework 4.x” (old tech).
* **Why:** Designer initially failed to open; fixed once .NET 8 Desktop runtime and Windows Desktop Targeting Pack were properly recognized.

### B.9.3 Application Icon

* **Where:** `Project -> Properties -> Application -> Resources -> Icon and manifest`
* **What we did:**
  * Set the **Application Icon** to a prebuilt `.ico` (multi-size) in `Assets\AppIcon.ico`.
  * Left runtime tray icon to be set programmatically (see `TrayManager` & `IconWriter`).
* **Why:** Guarantees shortcuts/Explorer show the correct branding.

### B.9.4 Debug/trace behavior

* **Where:** `Project -> Properties -> Build` and code in `Program.cs`.
* **What we did:**
  * Agreed to control tracing with **`--verbose`** flag + optional developer-only `FORCED_TRACE`.
  * Use `Trace.Listeners.Clear()` then add a `TextWriterTraceListener` that logs to `StayAwake_Trace_YYYYMMDD.log` in the EXE folder.
* **Why:** Ensures **Debug/Trace output** is visible even outside Visual Studio.

### B.9.5 Publish settings

* **Where:** `Project -> Publish -> Edit Profile` (or directly in `Publishing/FolderProfile.pubxml`).
* **What we did:**
  * Enabled **Single-file, self-contained, win-x64** publish.
  * Disabled trimming for now (`<PublishTrimmed>false</PublishTrimmed>`).
* **Why:** Produces one portable `.exe` for testing/distribution, avoids trim-related surprises.

### B.9.6 Designer form defaults

* **Where:** Inside Form Designer, or in code (`MainForm.cs`).
* **What we did:**
  * `AutoScaleMode = Dpi`.
  * `FormBorderStyle = FixedSingle`.
  * `MaximizeBox = false; MinimizeBox = true;`.
* **Why:** Matches Python app’s behavior: fixed-size window, DPI-scaled layout, no resize clutter.


## B.10 Visual Studio **Installer** – workloads & components to tick

Open **Visual Studio Installer -> Modify** for VS 2022 Community:

### B.10.1 Workloads (tab: *Workloads*)

* ✅ **.NET desktop development**
  *(This is the core workload that brings WinForms/WPF tooling.)*

> You don’t need “Desktop development with C++” for this project, and you don’t need “Universal Windows Platform” or “Mobile development” for WinForms on .NET 8.

### B.10.2 Individual components (tab: *Individual components*)

Make sure these are installed (some come with the workload; check if they’re already present):
* ✅ **.NET 8.0 SDK** (x64)
* ✅ **.NET 8.0 Runtime** (x64) *(usually installed with SDK)*
* ✅ **.NET 8.0 Windows Desktop Runtime / Targeting Pack**
  *(This is what the WinForms designer needed when it failed to load earlier.)*
* ✅ **Windows 11 SDK** *(provides up-to-date headers/tools, helpful for manifests and P/Invoke structs)*

Nice-to-haves (optional):
* ☐ **Git for Windows** *(if you don’t already have it; VS integrates with Git out of the box)*
* ☐ **NuGet package manager** *(comes with VS; verify it’s present)*
* ☐ **Developer PowerShell for VS** *(handy terminal with env pre-set)*

> If you have interop or pinned structs for Win32 calls, having the **Windows 11 SDK** avoids mismatches.

## B.10.3 IDE options that help this project (optional but handy)

* **Tools -> Options -> Projects and Solutions -> .NET Core**
  * Ensure **Use previews of the .NET SDK** = **Off** (unless you *want* to test previews).
* **Tools -> Options -> Environment -> Preview Features**
  * Keep defaults; no special previews needed for WinForms on .NET 8.
* **Tools -> Options -> Windows Forms Designer**
  * Verify the **designer** opens without errors for `.NET 8 (Windows)` projects. No extra toggle usually needed, but this is where you’d look if it regresses.

## B.10.4 Project property recap (quick check)

* **Project -> Properties -> Application**
  * Target framework: **.NET 8.0 (Windows)**
  * **Icon and manifest**:
    * **Icon** = your prebuilt multi-image `.ico` in `Assets\AppIcon.ico`
    * **Manifest** = **Use a custom manifest** -> `app.manifest` contains:
      ```xml
      <application xmlns="urn:schemas-microsoft-com:asm.v3">
        <windowsSettings>
          <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">PerMonitorV2</dpiAware>
          <longPathAware xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">true</longPathAware>
        </windowsSettings>
      </application>
      ```
* **Project -> Properties -> Build**
  * `TRACE` defined (default); `DEBUG` only for Debug config (default).
* **Project -> Publish** (or your `Publishing/FolderProfile.pubxml`)
  * **Self-contained**, **Single file**, `win-x64`
  * **Trim** = **Off** initially (`PublishTrimmed=false`)
  * `IncludeAllContentForSelfExtract=true` *(helps single-file startup; you already set this in the example pubxml)*

## B.10.5 Visual Studio Environment Checklist

```
Stay_Awake — Visual Studio Environment Checklist
================================================

Setup IDE (Visual Studio Installer)
-----------------------------------
[ ] Workloads -> .NET desktop development
[ ] Individual components -> .NET 8.0 SDK (x64)
[ ] Individual components -> .NET 8.0 Windows Desktop Runtime / Targeting Pack
[ ] Individual components -> Windows 11 SDK (optional but recommended)
[ ] Ensure NuGet + Git for Windows present

Verify Targeting
----------------
[ ] Project -> Properties -> Target Framework = .NET 8.0 (Windows)
[ ] Debug build defines DEBUG + TRACE
[ ] Release build defines TRACE only

Manifest (High DPI + Long Paths)
--------------------------------
[ ] Project -> Properties -> Application -> Icon and manifest -> Use custom manifest
[ ] In app.manifest, set:
    <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">PerMonitorV2</dpiAware>
    <longPathAware xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">true</longPathAware>

Designer
--------
[ ] Confirm Form1.cs [Design] opens without error
[ ] Tools -> Options -> Windows Forms Designer shows correct options
[ ] Ensure AutoScaleMode = Dpi

Icon
----
[ ] Add Assets\AppIcon.ico (multi-size ICO, prebuilt)
[ ] Project -> Properties -> Application -> Icon = AppIcon.ico
[ ] Leave runtime tray icon to be set in code (TrayManager / IconWriter)

Build
-----
[ ] FormBorderStyle = FixedSingle
[ ] MaximizeBox = false
[ ] MinimizeBox = true
[ ] Debug messages go to Trace.WriteLine (respecting --verbose + FORCED_TRACE)

Publish
-------
[ ] Project -> Publish -> Folder profile
[ ] Runtime Identifier = win-x64
[ ] Deployment = Self-contained
[ ] File mode = Single file
[ ] PublishTrimmed = false
[ ] IncludeAllContentForSelfExtract = true

------------------------------------------------
When all boxes are ticked, the environment matches spec (Appendix B.9)
and then you can safely begin coding.
```

---

# Appendix C - Timer Countdown Related Matters in the Python Program

The purpose of this appendix is to provide an overview of the current timer system's behavior,
including exact thresholds, algorithmic details, and real-world examples showing how the
python program responds to various time progressions and user actions.

## C.1 Window Visibility Management

### C.1.1 Minimize to System Tray Behavior

When the user minimizes the window to the system tray 
(either via the "Minimize to System Tray" button or the title bar minimize button "_"):

1. The main window is withdrawn using `self.main_window.withdraw()`
2. `self.window_visible` flag is set to `False`
3. The window becomes completely hidden (not just iconified)
4. **Countdown update throttling activates** (see Section 3.3 for details)
5. The system tray icon remains visible and functional
6. All countdown calculations continue running in the background

### C.1.2 Show Window from System Tray Behavior

When the user shows the window from the system tray (via right-click menu "Show Window" or double-clicking the tray icon):

1. The window is restored using `self.main_window.deiconify()`
2. Window is brought to front with `self.main_window.lift()`
3. Focus is forced to the window with `self.main_window.focus_force()`
4. `self.window_visible` flag is set to `True`
5. **Immediate countdown reschedule** occurs via `self._schedule_countdown_tick()`
6. The countdown display updates immediately to show current values
7. Normal (non-throttled) cadence resumes based on remaining time

### C.1.3 Important Notes on Window State

- The title bar close button "X" performs a **full application exit**, not minimize
- Window maximize and resize are disabled (window is resizable but user-controlled)
- The window intercepts OS iconify events and redirects them to system tray hiding

## C.2 GUI Countdown Fields - Visibility and Update Conditions

### C.2.1 The Three Countdown Display Fields

The countdown area contains exactly three display fields, arranged in a two-column table format (label | value):

**Field 1: Auto-quit ETA**
- **Label**: "Auto-quit at:"
- **Value**: Wall-clock timestamp in format "YYYY-MM-DD HH:MM:SS"
- **Example**: "2025-09-23 15:30:45"

**Field 2: Time Remaining**
- **Label**: "Time remaining:"
- **Value**: Countdown in format "DDDd HH:MM:SS" (days shown only if >0)
- **Example**: "2d 03:45:22" or "00:45:22"

**Field 3: Timer Update Frequency (Cadence)**
- **Label**: "Timer update frequency:"
- **Value**: Current cadence interval in format "HH:MM:SS"
- **Example**: "00:10:00" (updates every 10 minutes)

### C.2.2 Visibility Conditions

**When All Three Fields Are VISIBLE:**
- `--for DURATION` was specified on command line AND duration > 0
- OR `--until "TIMESTAMP"` was specified on command line
- The fields appear in the main window below the status hint text
- All three fields are created together during window initialization

**When All Three Fields Are HIDDEN:**
- No `--for` or `--until` parameter was provided
- OR `--for 0` was specified (explicitly disabled auto-quit)
- In this case, the countdown frame is never created at all

### C.2.3 Update Behavior for Each Field

**Field 1 (Auto-quit ETA):**
- Set **once** during timer initialization
- **Never updates** after initial display
- Uses `self.auto_quit_walltime` (wall-clock epoch) for display
- Remains static even if system clock changes

**Field 2 (Time Remaining):**
- Updates on **every countdown tick** (when window is visible)
- Calculated from: `max(0, int(round(self.auto_quit_deadline - time.monotonic())))`
- Uses monotonic time to avoid system clock change issues
- Format changes dynamically: shows days only when remaining time ≥ 1 day
- When window is hidden, updates continue but are throttled (see Section 3.3)

**Field 3 (Timer Update Frequency):**
- Updates **only when the cadence value changes** (not on every tick)
- Tracked via `self._last_cadence_s` to prevent unnecessary updates
- Shows the current update interval being used (not the remaining time)
- Example: if updating every 5 minutes, displays "00:05:00"
- Only updates when window is visible

## C.3 Cadence System - Adaptive Update Frequency

### C.3.1 Core Cadence Mechanism

The cadence system dynamically adjusts how frequently the countdown display updates based on the remaining time. This conserves CPU resources while maintaining appropriate user feedback.

**The 9 Cadence Bands (from least to most frequent):**

| Threshold (seconds) | Condition | Update Interval | Interval (seconds) |
|---------------------|-----------|-----------------|-------------------|
| 3,600 | remaining > 60 min | Every 10 minutes | 600s |
| 1,800 | remaining > 30 min | Every 5 minutes | 300s |
| 900 | remaining > 15 min | Every 1 minute | 60s |
| 600 | remaining > 10 min | Every 30 seconds | 30s |
| 300 | remaining > 5 min | Every 15 seconds | 15s |
| 120 | remaining > 2 min | Every 10 seconds | 10s |
| 60 | remaining > 1 min | Every 5 seconds | 5s |
| 30 | remaining > 30 sec | Every 2 seconds | 2s |
| -1 (catch-all) | remaining ≤ 30 sec | Every 1 second | 1s |

**How Band Selection Works:**
1. Calculate remaining seconds: `rem = max(0, int(round(self.auto_quit_deadline - time.monotonic())))`
2. Iterate through bands from top to bottom
3. Use the first band where `rem > threshold`
4. The catch-all band (threshold = -1) always matches if no earlier band does

### C.3.2 Snap-to-Boundary Mechanism

**Purpose**: When far from the deadline, align the first update to a "round" cadence boundary so the countdown appears cleaner to users.

**When Snap-to Applies:**
- **Condition**: `remaining >= HARD_CADENCE_SNAP_TO_THRESHOLD_SECONDS` (60 seconds)
- **Only affects**: The very next tick after this condition becomes true
- **Does not apply**: When remaining < 60 seconds (final minute uses exact updates)

**How Snap-to-Boundary Works:**

1. Determine current cadence interval in seconds: `cadence_s = max(1, next_ms // 1000)`
2. Calculate phase offset: `phase = rem % cadence_s`
   - This is how many seconds we are "past" the last boundary
3. Calculate snap interval: `snap_ms = phase * 1000`
   - This brings the next tick to the boundary
4. Apply micro-sleep protection: if `snap_ms < 200`, add one full cadence interval
   - Prevents thrashing at boundaries
5. Only use snap if it's sooner than the regular cadence: `if snap_ms < next_ms`

**Example 1: Starting with `--for 2h` (7200 seconds)**
- Initial remaining: 7200s
- Current cadence: 600s (10 minutes) because 7200 > 3600
- Phase: 7200 % 600 = 0 (already on boundary)
- Snap: 0ms → too small, add 600s → 600,000ms
- Result: First update in 10 minutes (at 7200-600=6600s remaining)

**Example 2: Starting with `--for 75m` (4500 seconds)**
- Initial remaining: 4500s
- Current cadence: 600s (10 minutes) because 4500 > 3600
- Phase: 4500 % 600 = 300 (5 minutes past boundary)
- Snap: 300s = 300,000ms
- 300,000 < 600,000, so use snap
- Result: First update in 5 minutes (at 4200s = 70min, a "round" number)

**Example 3: Countdown at 45 seconds remaining**
- Remaining: 45s
- 45 < 60 (HARD_CADENCE_SNAP_TO_THRESHOLD_SECONDS)
- Snap-to is **disabled**
- Current cadence: 2s (because 30 < 45 ≤ 60)
- Result: Regular 2-second updates, no snapping

### C.3.3 Hidden Window Throttling

**Purpose**: Reduce CPU usage when the window is minimized to system tray while maintaining responsiveness near deadline.

**Throttling Rules:**

1. **Normal Updates (Window Visible)**:
   - Use cadence bands exactly as specified in Section 3.1
   - All updates occur on schedule

2. **Throttled Updates (Window Hidden)**:
   - **Condition**: Window is not viewable AND remaining > 60s
   - **Minimum interval**: 60,000ms (60 seconds)
   - **Application**: If calculated cadence < 60s, override to 60s
   - **Example**: At 10 minutes remaining (normal cadence = 30s), hidden cadence becomes 60s

3. **No Throttling (Final Minute)**:
   - **Condition**: Remaining ≤ 60s (HIDDEN_BACKOFF_UNTIL_SECS)
   - **Behavior**: Normal cadence applies even when hidden
   - **Reason**: Ensures timer fires promptly near deadline

**Throttling Constants:**
- `HIDDEN_CADENCE_MIN_MS = 60,000` (60 seconds minimum when hidden)
- `HIDDEN_BACKOFF_UNTIL_SECS = 60` (disable throttling in final 60 seconds)

**Sequence Example: User Minimizes at 15 Minutes Remaining**

1. **Before minimize** (window visible, 900s remaining):
   - Cadence: 60s (1 minute updates) because 900 > 600
   - Updates every 60 seconds

2. **User minimizes window**:
   - Window visibility: `False`
   - Remaining: ~900s (still > 60s)
   - Calculated cadence: 60s
   - Throttle check: 60s ≥ 60s minimum → no override needed
   - Continues 60s updates (happens to match)

3. **Countdown reaches 8 minutes (480s)**:
   - Normal cadence would be: 30s (because 480 > 300)
   - Window still hidden, 480 > 60
   - Throttle applies: max(30s, 60s) → 60s
   - Updates every 60 seconds (throttled from normal 30s)

4. **Countdown reaches 55 seconds**:
   - Normal cadence: 2s (because 30 < 55 ≤ 60)
   - Window still hidden, but 55 ≤ 60
   - Throttling **disabled** (final minute exception)
   - Updates every 2 seconds despite hidden window

5. **User shows window at 30 seconds**:
   - Window visibility: `True`
   - `_schedule_countdown_tick()` called immediately
   - Cadence: 2s (because 30 ≤ 30)
   - Display updates instantly, then continues every 2s

### C.3.4 Cadence Display Field Update Logic

**Update Trigger**: The "Timer update frequency" field only updates when the cadence value **changes**.

**Tracking Mechanism**:
- Current cadence stored in: `self._last_cadence_s` (integer seconds)
- New cadence calculated: `cad_s = max(1, int(round(next_ms / 1000)))`
- Comparison: `if self._last_cadence_s != cad_s:`

**Update Conditions** (ALL must be true):
1. `self._cadence_value` exists (GUI label is created)
2. Window is visible (`visible == True`)
3. Calculated cadence differs from last shown value

**Why This Matters**:
- Prevents visual churn when cadence doesn't change
- Reduces UI updates from potentially every tick to only ~9 times maximum (band transitions)
- The countdown value updates every tick, but cadence display is more stable

**Example Sequence: `--for 90m` (5400 seconds)**

| Remaining Time | Cadence Band | Cadence Value | Cadence Display Updates? |
|----------------|--------------|---------------|--------------------------|
| 5400s (90min) | 600s | "00:10:00" | YES (initial display) |
| 4800s (80min) | 600s | "00:10:00" | NO (unchanged) |
| 3000s (50min) | 600s | "00:10:00" | NO (unchanged) |
| 1900s (31.6min) | 300s | "00:05:00" | YES (band change) |
| 1200s (20min) | 300s | "00:05:00" | NO (unchanged) |
| 850s (14.1min) | 60s | "00:01:00" | YES (band change) |
| 550s (9.1min) | 30s | "00:00:30" | YES (band change) |
| 250s (4.1min) | 15s | "00:00:15" | YES (band change) |
| 100s (1.6min) | 10s | "00:00:10" | YES (band change) |
| 55s | 5s | "00:00:05" | YES (band change) |
| 28s | 2s | "00:00:02" | YES (band change) |
| 15s | 1s | "00:00:01" | YES (band change) |

## C.4 Timer Scheduling and Precision

### C.4.1 Dual Time Systems

**Monotonic Time (for countdown accuracy):**
- Used for: `self.auto_quit_deadline` (when timer will fire)
- Function: `time.monotonic()`
- Advantage: Unaffected by system clock changes, DST, or NTP adjustments
- Calculation: `self.auto_quit_deadline = time.monotonic() + seconds`

**Wall-Clock Time (for user display):**
- Used for: `self.auto_quit_walltime` (ETA display field)
- Function: `time.time()` and `math.ceil()`
- Purpose: Shows user-readable timestamp
- Calculation: `self.auto_quit_walltime = math.ceil(time.time()) + seconds`
  - OR (for `--until`): `self.auto_quit_walltime = target_epoch` (from CLI parsing)

### C.4.2 Timer Re-ceiling Strategy

**Purpose**: Maximize accuracy by recalculating just before timer activation.

**Two-Stage Calculation:**

1. **During CLI Parsing** (for `--for`):
   - Parse duration to seconds
   - Calculate: `target_epoch = math.ceil(time.time()) + seconds`
   - Store as: `auto_quit_target_epoch`
   - This accounts for parsing overhead

2. **Just Before Timer Activation** (in `run()` method):
   - Recalculate: `secs_to_run = int(math.ceil(target_epoch - time.time()))`
   - This accounts for UI creation overhead
   - Ensures timer fires at the exact target moment

**Why This Matters:**
- First ceil: aligns to whole-second boundary after parsing
- Second ceil: compensates for any delays in startup/UI creation
- Result: Timer fires precisely when promised, not early

### C.4.3 Countdown Tick Scheduling Flow

**Each Tick Follows This Sequence:**

1. **Validate Countdown Active**:
   - Check: `self.auto_quit_deadline` exists
   - Check: `self.main_window` exists and is valid
   - If either fails → cancel pending ticks and exit

2. **Determine Visibility**:
   - `visible = bool(self.main_window.winfo_viewable())`
   - Catches exceptions (defaults to `True` if check fails)

3. **Calculate Remaining Time**:
   - `now = time.monotonic()`
   - `rem = max(0, int(round(self.auto_quit_deadline - now)))`

4. **Update Display** (if visible):
   - `self._countdown_value.configure(text=self._format_dhms(rem))`
   - Format: "DDDd HH:MM:SS" or "HH:MM:SS"

5. **Determine Next Cadence**:
   - Iterate through `COUNTDOWN_CADENCE` bands
   - Select first band where `rem > threshold`
   - Get `next_ms` (milliseconds to next update)

6. **Apply Snap-to-Boundary** (if applicable):
   - Check: `rem >= 60` (HARD_CADENCE_SNAP_TO_THRESHOLD_SECONDS)
   - Calculate phase offset and adjust `next_ms`
   - See Section 3.2 for full algorithm

7. **Apply Hidden Window Throttling** (if applicable):
   - Check: `not visible and rem > 60`
   - Override: `next_ms = max(next_ms, 60000)`

8. **Update Cadence Display** (if changed and visible):
   - Calculate: `cad_s = max(1, int(round(next_ms / 1000)))`
   - If `cad_s != self._last_cadence_s`:
     - Update `self._cadence_value` label
     - Store `self._last_cadence_s = cad_s`

9. **Cancel Previous Tick**:
   - `self.main_window.after_cancel(self._countdown_after_id)`
   - Prevents duplicate scheduled callbacks

10. **Schedule Next Tick**:
    - `self._countdown_after_id = self.main_window.after(next_ms, self._schedule_countdown_tick)`
    - Creates recursive loop until countdown ends

## C.5 Edge Cases and Special Behaviors

### C.5.1 Startup Sequence for Auto-Quit

**Critical Ordering** (from `run()` method):

1. **Activate wake lock** (`self.start_Stay_Awake()`)
2. **Recalculate seconds** (re-ceil from target epoch)
3. **Start auto-quit timer** (`self._start_auto_quit_timer(secs_to_run)`)
   - Sets `self.auto_quit_deadline` (monotonic)
   - Sets `self.auto_quit_walltime` (wall-clock)
4. **Create main window** (`self.create_main_window()`)
   - Reads `self.auto_quit_walltime` for ETA field
   - Schedules first countdown tick (250ms delay)
5. **Start tray icon** (background thread)
6. **Enter Tk main loop**

**Why This Order Matters**:
- Timer must be set before window creation (ETA field needs `self.auto_quit_walltime`)
- Recalculation must happen last (accounts for all startup overhead)
- Initial tick delay (250ms) allows window to become viewable

### C.5.2 Timer Cancellation

**When Timer is Cancelled**:

1. **During Cleanup** (`cleanup()` method):
   - Cancels `self._auto_quit_timer` (threading.Timer)
   - Cancels `self._countdown_after_id` (Tk after() callback)
   - Prevents timer firing during shutdown

2. **On Manual Quit**:
   - User clicks "Quit" button or closes window
   - `cleanup()` runs via atexit or explicit call
   - Both timers cancelled before exit

3. **On Signal** (SIGINT/SIGTERM):
   - Signal handler calls `cleanup()`
   - Timers cancelled, then `sys.exit(0)`

### C.5.3 DST and Local Time Handling (for `--until`)

**The `--until` parameter uses a robust two-pass mktime validation**:

1. **Parse Timestamp**:
   - Regex extracts: year, month, day, hour, minute, second
   - Validates calendar correctness (rejects Feb 31, etc.)

2. **DST Validation** (prevents ambiguous/nonexistent times):
   - **Pass 1**: Try mktime with `tm_isdst=0` (standard time)
   - **Pass 2**: Try mktime with `tm_isdst=1` (daylight time)
   - Round-trip each: convert epoch back to local time
   - Check if wall-clock components match original

3. **Decision Logic**:
   - **Both fail** → Nonexistent time (spring-forward gap) → error
   - **Both succeed with different epochs** → Ambiguous time (fall-back hour) → error
   - **One succeeds** → Valid time, use that epoch
   - **Both succeed with same epoch** → Normal time (no DST), use epoch

**Result**: `auto_quit_target_epoch` is a validated local epoch that will fire at the exact specified wall-clock time.

### C.5.4 Bounds Enforcement

**All auto-quit times must satisfy**:
- Minimum: `MIN_AUTO_QUIT_SECS = 10` (at least 10 seconds)
- Maximum: `MAX_AUTO_QUIT_SECS = 31,622,400` (366 days)

**Validation Points**:
1. After parsing `--for` duration
2. After parsing `--until` timestamp (difference from now)
3. Before timer activation (final recalculation)

**Exit Code**: `sys.exit(2)` on validation failure (CLI error)

## C.6 Complete Example Scenarios

### C.6.1 Scenario 1: `--for 45m` with Window Minimize/Restore

**Initial State** (45 minutes = 2700 seconds):
- ETA field: "2025-09-23 16:15:00" (static)
- Time remaining: "00:45:00"
- Cadence: "00:05:00" (5 minutes, because 2700 > 1800)
- Update interval: 300s
- Snap-to check: 2700 % 300 = 0 → already on boundary
- Snap adjustment: 0 → add 300s → first update in 5 minutes

**At 40 minutes remaining** (2400s):
- Time remaining: "00:40:00" (updated)
- Cadence: "00:05:00" (unchanged)
- Next update: 5 minutes

**At 32 minutes remaining** (1920s):
- Time remaining: "00:32:00" (updated)
- Cadence: "00:05:00" (unchanged, still 1920 > 1800)
- Next update: 5 minutes

**At 28 minutes remaining** (1680s):
- Time remaining: "00:28:00" (updated)
- Band change: 1680 < 1800 but 1680 > 900
- Cadence: "00:01:00" (changed to 1 minute)
- Cadence display updates to show "00:01:00"
- Next update: 1 minute

**User minimizes window at 25 minutes** (1500s):
- Window visibility: `False`
- Time remaining continues updating (not displayed)
- Cadence: 60s (normal for this band)
- Throttle check: 60s ≥ 60s minimum → no override
- Updates continue every 60s in background

**At 8 minutes remaining** (480s, still hidden):
- Band change: 480 < 900 but 480 > 300
- Normal cadence: 30s
- Window hidden and 480 > 60
- Throttle override: max(30s, 60s) → 60s
- Updates every 60s (throttled)

**At 45 seconds remaining** (still hidden):
- Band change: 45 < 60 but 45 > 30
- Normal cadence: 5s
- Window hidden but 45 ≤ 60 (final minute exception)
- Throttle disabled
- Updates every 5s despite hidden window

**User shows window at 30 seconds**:
- Window visibility: `True`
- `_schedule_countdown_tick()` called immediately
- Time remaining updates to current value: "00:00:30"
- Band: 30 ≤ 30 → cadence 2s
- Cadence display updates to "00:00:02"
- Updates every 2 seconds until timeout

### C.6.2 Scenario 2: `--until "2025-09-23 17:00:00"` Started at 16:47:35

**CLI Parsing**:
- Current time: 16:47:35 (actual)
- Target: 17:00:00 (specified)
- Difference: 745 seconds (12 minutes 25 seconds)
- Validation: 745 ≥ 10 (pass), 745 ≤ 31,622,400 (pass)
- `auto_quit_target_epoch`: 1695484800.0 (example epoch for 17:00:00)

**Timer Activation** (in `run()`, ~1 second later at 16:47:36):
- Now: 16:47:36
- Recalculate: ceil(1695484800.0 - now) = 744 seconds
- `auto_quit_deadline`: monotonic() + 744
- `auto_quit_walltime`: 1695484800.0 (exact target)

**Initial Display**:
- ETA: "2025-09-23 17:00:00"
- Time remaining: "00:12:24" (744s)
- Cadence determination: 744 < 900 but 744 > 600 → 30s
- Snap-to: 744 ≥ 60 → enabled
  - Phase: 744 % 30 = 24
  - Snap: 24s = 24,000ms
  - 24,000 < 30,000 → use snap
- First update: in 24 seconds (at 00:12:00 remaining)

**At 00:12:00 remaining** (720s):
- Time remaining: "00:12:00" (snapped to round number)
- Cadence: "00:00:30" (unchanged)
- Next update: 30s (normal cadence)

**At 00:09:30 remaining** (570s):
- Band change: 570 < 600 but 570 > 300 → 30s
- Cadence: "00:00:30" (unchanged, same band)
- Next update: 30s

**At 00:04:45 remaining** (285s):
- Band change: 285 < 300 but 285 > 120 → 15s
- Cadence: "00:00:15" (changed)
- Cadence display updates
- Next update: 15s

**Countdown continues through all bands until timeout at 17:00:00**

---

# Appendix D - Pre-Development Gap Analysis & Resolutions

## D.1 Overview

This appendix documents the critical gaps identified in the specification before development
commenced, along with their researched solutions and agreed resolutions.
These decisions establish the technical foundation for the C# implementation.


## D.2 Gap #1: System.Windows.Forms.Timer Translation Strategy

### Issue
The Python program uses Tk's `after()` method with recursive rescheduling to implement adaptive cadence.
The C# specification mandated `System.Windows.Forms.Timer` but did not detail the translation
strategy for this fundamentally different timing mechanism.

### Analysis
**Python pattern (Tk):**
- One-shot callbacks scheduled with `after(milliseconds, callback)`
- Must cancel previous timer and reschedule on every tick
- Recursive pattern: callback schedules itself

**C# pattern (WinForms.Timer):**
- Continuous timer with `Tick` event
- No need to cancel/reschedule; just adjust `Interval` property
- Simpler than Python: timer keeps running, interval changes dynamically

### Resolution
Use `System.Windows.Forms.Timer` with dynamic interval adjustment:

```csharp
private Timer countdownTimer;

private void SetupCountdownTimer()
{
    countdownTimer = new Timer();
    countdownTimer.Tick += CountdownTimer_Tick;
    countdownTimer.Interval = CalculateNextCadenceMs();
    countdownTimer.Start();
}

private void CountdownTimer_Tick(object sender, EventArgs e)
{
    UpdateCountdownDisplay();
    int nextIntervalMs = CalculateNextCadenceMs();
    if (countdownTimer.Interval != nextIntervalMs)
    {
        countdownTimer.Interval = nextIntervalMs;
    }
}
```

**Benefits:**
- Cleaner than Python's recursive pattern
- WinForms.Timer automatically marshals to UI thread (no Invoke needed)
- Achieves identical cadence behavior with less complexity

## D.3 Gap #2: SetThreadExecutionState Flags Verification

### Issue
Specification Section 4.4 required verification of flags against the Python program, but Python uses `wakepy` wrapper which obscures the actual Win32 calls.

### Research Findings
**wakepy's actual implementation:**
```python
# wakepy uses three flags:
ES_CONTINUOUS       = 0x80000000
ES_SYSTEM_REQUIRED  = 0x00000001
ES_AWAYMODE_REQUIRED = 0x00000040
```

**Original specification had:**
```
ES_CONTINUOUS | ES_SYSTEM_REQUIRED
```

**Missing flag:** `ES_AWAYMODE_REQUIRED`

### Resolution
**Updated Section 4.4 to include all three flags:**
```csharp
SetThreadExecutionState(
    ES_CONTINUOUS | 
    ES_SYSTEM_REQUIRED | 
    ES_AWAYMODE_REQUIRED
);
```

**Flag purposes:**
- `ES_CONTINUOUS`: Maintains state until explicitly cleared
- `ES_SYSTEM_REQUIRED`: Prevents system sleep/hibernation
- `ES_AWAYMODE_REQUIRED`: Enables "away mode" (media center feature; harmless if unsupported)

**Exact parity with Python achieved.**

## D.4 Gap #3: Single-Instance Enforcement Complexity

### Issue
Specification Section 4.6 suggested "bring existing window forward" via IPC (Win32 messages or named pipes), but implementation complexity was unclear.

### Analysis
**Bringing window forward requires:**
- Win32 IPC (50-100 lines of interop code)
- `FindWindow`, `SetForegroundWindow`, or WM_COPYDATA messages
- Complex for novice maintainers
- Low value for a utility app

**Specification already provided fallback:**
> "Fallback: show a MessageBox in the second instance ('already running') and exit"

### Resolution
**Requirement removed entirely.** User may legitimately want multiple instances with different timers.

**Actions taken:**
- Removed all mutex/single-instance references from consideration
- Deleted from: Sections 2.3, 3.1, 4.6, 4.9, Appendix B.1
- No `SingleInstance.cs` file will be created

## D.5 Gap #4: Form Layout Constants & DPI Handling

### Issue
Specification required GUI to "look exactly like Python program" but provided no pixel measurements, spacing values, or font specifications.

### Analysis - Extracted from Python Source
**Python code reveals:**
```python
container = ttk.Frame(self.main_window, padding=(16, 16, 16, 16))
ttk.Separator(...).pack(fill="x", pady=(0, 8))
ttk.Button(...).pack(side=tk.LEFT, padx=(0, 8))
MAX_DISPLAY_PX = 512
```

**Tk defaults on Windows:**
- Font: System default (Segoe UI 9pt on Win10/11)
- Button padding: ~8px horizontal, ~4px vertical

**C# WinForms equivalents:**
```csharp
const int FORM_PADDING = 16;
const int CONTROL_SPACING = 8;
const int SEPARATOR_BOTTOM_MARGIN = 8;
const int MAX_IMAGE_DISPLAY_PX = 512;
```

### Resolution
**Layout constants defined in `AppState.cs` (global scope):**
- Values extracted directly from Python source
- `AutoScaleMode = AutoScaleMode.Dpi` handles all DPI scaling automatically
- No separate 4K/HD layouts needed; Windows DPI awareness handles it

## D.6 Gap #5: Fallback Glyph Implementation

### Issue
Specification mentioned "tiny fallback glyph" but did not provide the image or drawing code.

### Analysis - Python Source
**Found in Python program:**
```python
def _fallback_draw_eye(self, size=(640, 490)):
    # Draws monochrome outline of open eye using PIL
    # Arc for upper/lower eye curves
    # Filled ellipse for pupil
    # Small white highlight dot
```

### Resolution
**C# GDI+ translation prepared:**
```csharp
private Bitmap DrawFallbackEyeGlyph(int width = 640, int height = 490)
{
    Bitmap img = new Bitmap(width, height, PixelFormat.Format32bppArgb);
    using (Graphics g = Graphics.FromImage(img))
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (Pen pen = new Pen(Color.Black, 18))
        {
            // Lower/upper arcs (eye outline)
            // Tear duct line
            // Corner detail arc
        }
        // Pupil (black filled circle)
        // Highlight (white dot)
    }
    return img;
}
```

**Direct functional translation; will be used when no other image source available.**

## D.7 Gap #6: Win32 API Integration - "Formal" Definitions

### Issue - Critical Requirement
Specification Section 1 mandates:
> "Wherever possible: when making or using system calls... import and use the formal structures, constants, enums... ensure 100% compliance with formal definitions"

**Question:** Does this require importing from Windows SDK, or is manual definition acceptable?

### Research - C# and Windows SDK Reality

**Key finding:** C# cannot directly `#include` Windows SDK headers (those are C/C++ only).

**Three official approaches identified:**

#### Option A: Microsoft.Windows.CsWin32 (Chosen)
- **NuGet Package:** `Microsoft.Windows.CsWin32`
- **How it works:** Source generator auto-creates P/Invoke signatures from Windows Metadata
- **Workflow:**
  1. Add NuGet package
  2. Create `NativeMethods.txt` listing required APIs (e.g., `SetThreadExecutionState`)
  3. Build-time generator creates type-safe bindings from official Windows SDK metadata

**Example usage:**
```csharp
using Windows.Win32;
using Windows.Win32.System.Power;

PInvoke.SetThreadExecutionState(
    EXECUTION_STATE.ES_CONTINUOUS | 
    EXECUTION_STATE.ES_SYSTEM_REQUIRED | 
    EXECUTION_STATE.ES_AWAYMODE_REQUIRED
);
```

#### Option B: Manual P/Invoke Definition
- Standard .NET practice
- Define constants with SDK documentation references
- Zero dependencies

#### Option C: Microsoft.Windows.SDK.Contracts
- Unsuitable (covers WinRT, not classic Win32 APIs)

### Resolution
**Selected: Option A (CsWin32)**

**Rationale:**
- Most "official" - created by Microsoft Windows team
- Auto-generated from same metadata as C++ SDK headers
- Zero manual constant definition; values guaranteed correct
- Type-safe enums, IntelliSense support
- Satisfies "import formal structures" requirement as closely as C# allows

**Trade-off accepted:**
- Adds one NuGet dependency (Microsoft-maintained)
- Requires source generator in build process

**This is the modern Microsoft-recommended approach for Win32 interop in C#.**

## D.8 Gap #7: Project Structure - Namespace Collision

### Issue
Appendix B.1 proposed:
```
Stay_Awake/                    <- Project folder
├─ Stay_Awake/                 <- Subfolder (collision!)
│  └─ ExecutionState.cs
```

**Problem:** Creates namespace `Stay_Awake.Stay_Awake.ExecutionState` (confusing for novice developers).

### Resolution
**Renamed subfolder to `PowerManagement/`:**
```
Stay_Awake/                    <- Project folder
├─ PowerManagement/            <- Clear, descriptive name
│  └─ ExecutionState.cs        <- Namespace: Stay_Awake.PowerManagement
```

**Benefits:**
- Clear namespace hierarchy
- Self-documenting folder name
- No duplication

**Also removed:** `SingleInstance.cs` (no longer needed per D.4)


## D.9 Gap #8: Trace Log Failure Handling

### Issue
Section 4.11.4 states:
> "provide code for (but disabled with a fixed boolean False) show a one-liner MessageBox..."

**Ambiguity:** What does "disabled with a fixed boolean False" mean precisely?

### Resolution
**Developer-toggleable constant:**
```csharp
// In AppState.cs or tracing module
const bool SHOW_LOG_FAILURE_MESSAGEBOX = false;  // Developer toggle

// In log setup code
if (logSetupFailed && SHOW_LOG_FAILURE_MESSAGEBOX)
{
    MessageBox.Show(
        "Verbose logging could not be started; continuing without a log file.",
        "Logging Notice", 
        MessageBoxButtons.OK, 
        MessageBoxIcon.Information
    );
}
```

**Purpose:** Developer can enable for debugging; disabled by default to avoid user confusion.

## D.10 Summary - All Gaps Resolved

| Gap | Status | Resolution |
|-----|--------|------------|
| Timer translation | ✅ Resolved | WinForms.Timer with dynamic Interval property |
| SetThreadExecutionState flags | ✅ Resolved | Added ES_AWAYMODE_REQUIRED (3 flags total) |
| Single-instance enforcement | ✅ Removed | Requirement discarded; multiple instances allowed |
| Form layout constants | ✅ Resolved | Extracted from Python; defined in AppState.cs |
| Fallback glyph | ✅ Resolved | C# GDI+ translation of Python drawing code |
| Win32 "formal" definitions | ✅ Resolved | CsWin32 NuGet package (Microsoft-official) |
| Namespace collision | ✅ Resolved | Folder renamed: Stay_Awake/ → PowerManagement/ |
| Log failure MessageBox | ✅ Resolved | Toggleable const SHOW_LOG_FAILURE_MESSAGEBOX |

**All technical ambiguities clarified. Development can proceed with confidence.**

## D.11 Developer Setup - CsWin32 Quick Reference

**When implementing Win32 calls, follow these steps:**

1. **Install package (once per project):**
   ```bash
   dotnet add package Microsoft.Windows.CsWin32
   ```

2. **Create `NativeMethods.txt` in project root:**
   ```
   SetThreadExecutionState
   ```

3. **Build project** - source generator runs automatically

4. **Use generated code:**
   ```csharp
   using Windows.Win32;
   using Windows.Win32.System.Power;
   
   // Code as shown in D.7
   ```

**Step-by-step guidance will be provided during Iteration 1 implementation.**

---
