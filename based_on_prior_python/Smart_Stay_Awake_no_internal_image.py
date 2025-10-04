#!/usr/bin/env python3
# =============================================================================
# Stay_Awake — simple Windows tray app to keep the PC awake, with minimal GUI.
#
# Summary
# -------
# - Keeps the machine awake while running (via wakepy).
# - Optional auto-quit timer by duration (--for) or by local wall-clock time (--until).
# - Small Tk window for status + image; system tray icon with right-click menu.
# - Native/vanilla look for buttons; image auto-scales to fit.
#
# Power Management
# ----------------
# - On start: acquires a wake lock (via wakepy) to prevent sleep.
# - On quit: releases the lock and restores normal power management.
# - Useful diagnostics: in a console, run `powercfg -requests` to see any blockers.
#
# OS & Python & Dependencies
# --------------------------
# Tested on Windows 11.
# Dependencies:
#   - Python 3.13+ recommended
#   - pip install these:
#         pip install wakepy --no-cache-dir --upgrade --check-build-dependencies --upgrade-strategy eager --verbose
#         pip install pystray --no-cache-dir --upgrade --check-build-dependencies --upgrade-strategy eager --verbose
#         pip install Pillow --no-cache-dir --upgrade --check-build-dependencies --upgrade-strategy eager --verbose
#         pip install pyinstaller --no-cache-dir --upgrade --check-build-dependencies --upgrade-strategy eager --verbose
#
# Notes
# -----
# - Renaming the source .py to .pyw and running that (or using pythonw.exe) hides the console window.
# - Some tray environments clip edges.
#
# Command-line Usage
# ------------------
#   Stay_Awake.py [--icon PATH] [--for DURATION | --until "YYYY-MM-DD HH:MM:SS"]
#
# --icon PATH
#   - Overrides the built-in image for both the window and tray icon.
#   - Supports PNG/JPG/JPEG/WEBP/BMP/GIF/ICO.
#   - Image Source Priority (first usable wins)
#       1) CLI override:      --icon "PATH"
#       2) Internal base64:   EYE_IMAGE_BASE64 (if non-empty and decodes)
#       3) File fallbacks (in the script folder, in this order):
#           Stay_Awake_icon.png
#           Stay_Awake_icon.jpg
#           Stay_Awake_icon.jpeg
#           Stay_Awake_icon.webp
#           Stay_Awake_icon.bmp
#           Stay_Awake_icon.gif
#           Stay_Awake_icon.ico
#   - Images/icons are made Square using outer edge pixel row/column replication
#
# --for DURATION
#   - Auto-quit after a duration. Examples:
#       --for 45m         (45 minutes)
#       --for 2h          (2 hours)
#       --for 1h30m       (1 hour 30 minutes)
#       --for 3600s       (3600 seconds)
#       --for 3d4h5s      (3 days, 4 hours, 5 seconds)
#       --for 0           (disable auto-quit)
#   - Bounds enforced: at least 10 seconds; at most 366 days.
#   - Implementation detail: we compute a target wall-time (now rounded up to the next
#     whole second + parsed duration) and then re-ceil again right before arming the timer
#     for best alignment.
#
# --until "YYYY-MM-DD HH:MM:SS"
#   - Auto-quit at a specific local wall-clock time (24h clock).
#   - Relaxed parsing: allows extra spaces and 1–2 digit month/day/hour/min/sec, e.g.:
#       "2025-01-02 23:22:21"
#       "2025- 1- 2 03:02:01"
#       "2025-1-2 3:2:1"
#   - Strict validation:
#       * Invalid dates/times (e.g., 2025-02-31) are rejected.
#       * DST edge cases are handled with a two-pass local-time check:
#           - Nonexistent local times (spring-forward gap) → error.
#           - Ambiguous local times (fall-back overlap)    → error.
#   - Bounds enforced: at least 10 seconds into the future; at most 366 days.
#   - Implementation detail: we convert the target to a local epoch, then re-ceil
#     from “now” again immediately before arming the timer to minimize drift.
#
# Mutually exclusive:
#   --for and --until cannot be used together (the CLI enforces this).
#
# Window & Tray Behavior
# ----------------------
# - Main window shows:
#     * Scaled image (max size = MAX_DISPLAY_PX, preserves aspect ratio).
#     * Blurb text.
#     * Buttons: “Minimize to System Tray” and “Quit”.
#     * If auto-quit is active:
#         - ETA line (“Auto-quit at: …”)
#         - Countdown line (“Time remaining: DDDd HH:MM:SS”)
#         - Cadence line (“Timer update cadence: HH:MM:SS” — only updates when cadence changes)
# - Title-bar minimize (“_”) maps to “minimize to system tray”.
# - Right-click tray icon menu includes “Show Window” / “Hide Window” / “Quit”.
# - Window close (“X”) performs a graceful full exit.
#
# Countdown Cadence & CPU Friendliness
# ------------------------------------
# - Update cadence adapts to remaining time (seconds):
#     > 1h:  every 10 min (example; see COUNTDOWN_CADENCE)
#     > 30m: every 5 min
#     > 15m: every 1 min
#     > 10m: every 30 s
#     >  5m: every 15 s
#     >  2m: every 10 s
#     >  1m: every 5  s
#     > 30s: every 2  s
#     else:  every 1  s
#   (Exact values are controlled by COUNTDOWN_CADENCE in code.)
# - When the window is not visible, updates are throttled (e.g., to 30s minimum) except
#   during the final N seconds.
# - When far from the deadline (e.g., ≥ 60s), the first update is “snapped” to the next
#   cadence boundary so the countdown appears more “round” to the user
#   (HARD_CADENCE_SNAP_TO_THRESHOLD_SECONDS controls this).
#
# Exit Codes
# ----------
# - 0 on normal exit.
# - 2 on CLI validation errors (bad --for/--until, out of bounds, invalid local time).
#
# Maintenance Pointers (search for these names)
# ---------------------------------------------
# - Duration parser:              parse_duration_to_seconds()
# - Local time parser (DST-safe): parse_until_to_epoch()
# - Auto-quit bounds:             MIN_AUTO_QUIT_SECS / MAX_AUTO_QUIT_SECS
# - Image sizing cap:             MAX_DISPLAY_PX
# - Cadence configuration:        COUNTDOWN_CADENCE
# - Snap-to-boundary threshold:   HARD_CADENCE_SNAP_TO_THRESHOLD_SECONDS
# - Hidden-window backoff:        HIDDEN_CADENCE_MIN_MS / HIDDEN_BACKOFF_UNTIL_SECS
#
# Troubleshooting
# ---------------
# - If the tray icon doesn’t appear immediately, give it a second; the icon runs on a
#   background thread. If Windows Explorer had issues, restarting Explorer can help.
# - If the window doesn’t show an image, ensure a valid image source exists (see sourcing
#   order above); check console output for “Loading icon from file: …”.
# - To verify sleep blockers: `powercfg -requests` (look for this process under SYSTEM).
#
# Packaging (PyInstaller)
# -----------------------
#       set "root=%~dp0"
#       set "targetIcon=%root%Stay_Awake_icon.ico"
#       set "targetIcon_option="
#       set "sourceImage="
#       del /f "!targetIcon!" >NUL 2>&1
#       REM Check for image files in order of preference
#       for %%f in ("Stay_Awake_icon.png" "Stay_Awake_icon.jpg" "Stay_Awake_icon.jpeg" "Stay_Awake_icon.webp" "Stay_Awake_icon.bmp" "Stay_Awake_icon.gif") do (
#           if exist "%root%%%~f" (
#               set "sourceImage=%root%%%~f"
#               echo Create ICON from PNG: Found source image: !sourceImage!
#               goto :found_image
#           )
#       )
#       :found_image
#
# Either A. One-file EXE (no console window):
#       :build_onefile
#       rmdir /s /q .\dist  >NUL 2>&1
#       rmdir /s /q .\build >NUL 2>&1
#       del /f      .\Stay_Awake.spec >NUL 2>&1
#       del /f      .\Stay_Awake.zip  >NUL 2>&1
#       if exist "!targetIcon!" (
#           echo.
#           echo pyinstaller --clean --onefile --windowed --noconsole --icon "!targetIcon!" --name "Stay_Awake" Stay_Awake.py
#           echo.
#           pyinstaller --clean --onefile --windowed --noconsole --icon "!targetIcon!" --name "Stay_Awake" Stay_Awake.py
#           echo.
#           echo ********** pyinstaller created onefile Stay_Awake WITH system tray .ico icon file
#           echo.
#       ) ELSE (
#           echo.
#           echo pyinstaller --clean --onefile --windowed --noconsole --name "Stay_Awake" Stay_Awake.py
#           pyinstaller --clean --onefile --windowed --noconsole --name "Stay_Awake" Stay_Awake.py
#           echo.
#           echo ********** pyinstaller created onefile Stay_Awake WITHOUT system tray .ico icon file
#           echo.
#       )
#       REM Place optional icon images next to the EXE (inside the app folder):
#       COPY /Y "!sourceImage!" ".\dist\" >NUL 2>&1
#       COPY /Y "!targetIcon!" ".\dist\" >NUL 2>&1
#       REM ---------------------------------------------------------------------------
#       REM Zip the onefile output (PowerShell 5.1 compatible)
#       REM   - Using the contents of distso the ZIP root is the app itself
#       REM ---------------------------------------------------------------------------
#       set "target_zip_file=.\Stay_Awake_onefile.zip"
#       echo powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -Sta -NonInteractive  -Command "Compress-Archive -Path '.\dist\*' -DestinationPath '!target_zip_file!' -Force -CompressionLevel Optimal" 
#       powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -Sta -NonInteractive  -Command "Compress-Archive -Path '.\dist\*' -DestinationPath '!target_zip_file!' -Force -CompressionLevel Optimal" 
#       set "ERR=%ERRORLEVEL%"
#       echo ==== onefile Compress-Archive exit code: %ERR%
#
# Or B. One-dir app folder (no console window):
#       :build_onedir
#       rmdir /s /q .\dist  >NUL 2>&1
#       rmdir /s /q .\build >NUL 2>&1
#       del /f      .\Stay_Awake.spec >NUL 2>&1
#       del /f      .\Stay_Awake.zip  >NUL 2>&1
#       if exist "!targetIcon!" (
#           echo.
#           echo pyinstaller --clean --onedir --windowed --noconsole --icon "!targetIcon!" --name "Stay_Awake" Stay_Awake.py
#           echo.
#           pyinstaller --clean --onedir --windowed --noconsole --icon "!targetIcon!" --name "Stay_Awake" Stay_Awake.py
#           echo.
#           echo ********** pyinstaller created onedir Stay_Awake WITH system tray .ico icon file
#           echo.
#       ) ELSE (
#           echo.
#           echo pyinstaller --clean --onedir --windowed --noconsole --name "Stay_Awake" Stay_Awake.py
#           echo.
#           pyinstaller --clean --onedir --windowed --noconsole --name "Stay_Awake" Stay_Awake.py
#           echo.
#           echo ********** pyinstaller created onedir Stay_Awake WITHOUT system tray .ico icon file
#           echo.
#       )
#       REM Place optional icon images next to the EXE (inside the app folder):
#       COPY /Y "!sourceImage!" ".\dist\Stay_Awake\" >NUL 2>&1
#       COPY /Y "!targetIcon!"  ".\dist\Stay_Awake\" >NUL 2>&1
#       REM ---------------------------------------------------------------------------
#       REM Zip the onedir output (PowerShell 5.1 compatible)
#       REM   - Using the contents of dist\Stay_Awake so the ZIP root is the app itself
#       REM ---------------------------------------------------------------------------
#       set "target_zip_file=.\Stay_Awake_onedir.zip"
#       echo powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -Sta -NonInteractive -Command "Compress-Archive -Path '.\dist\Stay_Awake\*' -DestinationPath '!target_zip_file!' -Force -CompressionLevel Optimal"
#       powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -Sta -NonInteractive -Command "Compress-Archive -Path '.\dist\Stay_Awake\*' -DestinationPath '!target_zip_file!' -Force -CompressionLevel Optimal"
#       set "ERR=%ERRORLEVEL%"
#       echo ==== onedir Compress-Archive exit code: %ERR%
#
# Sundry Notes
# ------------
# Zipping the p[yinstaller built application (PowerShell 5.1 compatible)
#   # ZIP contains the CONTENTS of dist (no top-level 'dist' folder inside):
#   powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -Sta -NonInteractive -WindowStyle Hidden -Command "Compress-Archive -Path '.\dist\*' -DestinationPath '.\Stay_Awake.zip' -Force -CompressionLevel Optimal"
#
# Zipping the p[yinstaller built application (PowerShell 7+ only; strongest compression)
#   # ZIP contains the CONTENTS of dist (no top-level 'dist' folder inside):
#   pwsh -NoLogo -NoProfile -Command "Compress-Archive -Path '.\dist\*' -DestinationPath '.\Stay_Awake.zip' -Force -CompressionLevel SmallestSize"
#
# Zipping the p[yinstaller built application (include 'dist' as a top-level folder inside the ZIP; note '.\dist' rather than '.\dist\*')
#   powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -Sta -NonInteractive -WindowStyle Hidden -Command "Compress-Archive -Path '.\dist' -DestinationPath '.\Stay_Awake.zip' -Force -CompressionLevel Optimal"
#
# =============================================================================

import sys
import os
import time
from datetime import datetime
import ctypes
import threading
import tkinter as tk
from tkinter import ttk, messagebox
from wakepy import keep
import pystray
from pystray import MenuItem as item
from PIL import Image, ImageDraw, ImageTk, ImageOps
import atexit
import signal
import base64
import io
from pathlib import Path
import argparse
import re  # for --for and --until duration parsing
import math
import traceback

# --------------------------------------------------------------------
# Config
# --------------------------------------------------------------------
MAX_DISPLAY_PX = 512  # max long-side pixels for images in windows

APP_BLURB = (
    "WEDJAT  :  THE EYE OF HORUS\n"
    "\n"
    "Prevents system sleep & hibernation while active.\n"
    "Display Monitor sleep is allowed.\n"
    "Closing this app re-allows sleep & hibernation."
)

# For decoding --for days minutes, seconds:
_RE_DURATION_TOKEN = re.compile(r'\s*(\d+)\s*([dhmsDHMS]?)')

# For decoding --until relaxed local timestamp:
# Accepts: "YYYY-MM-DD HH:MM:SS" with optional spaces and 1–2 digit M/D/h/m/s.
# Examples:
#   2025-01-02 23:22:21
#   2025- 1- 2 03:02:01
#   2025-1-2 3:2:1
_RE_UNTIL_TOKEN = re.compile(
    r"""^\s*
        (\d{4})\s*-\s*       # year
        (\d{1,2})\s*-\s*     # month (1..12)
        (\d{1,2})\s+         # day   (1..31; validated later)
        (\d{1,2})\s*:\s*     # hour  (0..23)
        (\d{1,2})\s*:\s*     # min   (0..59)
        (\d{1,2})\s*         # sec   (0..59)
        $""",
    re.VERBOSE,
)

# Candidate image file names (same folder as this script/EXE) in this order
IMAGE_CANDIDATES = [
    "Stay_Awake_icon.png",
    "Stay_Awake_icon.jpg",
    "Stay_Awake_icon.jpeg",
    "Stay_Awake_icon.webp",
    "Stay_Awake_icon.bmp",
    "Stay_Awake_icon.gif",
    "Stay_Awake_icon.ico",
]

# -------------------- Countdown cadence config --------------------
# Each rule is (threshold_seconds, cadence_ms) and is evaluated in order.
# "threshold_seconds" means: if remaining_time_seconds > threshold_seconds → use cadence_ms.
# Keep the last rule as a catch-all with -1.
COUNTDOWN_CADENCE: list[tuple[int, int]] = [
    (3_600, 600_000),  # > 60 min  → update every 600s (10 mins)
    (1_800, 300_000),  # > 30 min  → update every 300s (5 mins)
    (900, 60_000),  # > 15 min  → update every 60s
    (600, 30_000),  # > 10 min  → update every 30s
    (300, 15_000),  # >  5 min  → update every 15s
    (120, 10_000),  # >  2 min  → update every 10s
    ( 60,  5_000),  # >  1 min  → update every 5s
    ( 30,  2_000),  # > 30 sec  → update every 2s
    (-1,  1_000),   # ≤ 30 sec  → update every 1s (catch-all)
]
#
#for threshold, cadence in COUNTDOWN_CADENCE:
#    print(f"[DEBUG] COUNTDOWN_CADENCE (lower bound seconds={threshold}, update frequency seconds)={cadence/1000}", flush=True)
# When the window isn't viewable, throttle updates to at least this interval,
# except during the final N seconds (so short runs still tick fast near the end).
HIDDEN_CADENCE_MIN_MS        = 60_000
HIDDEN_BACKOFF_UNTIL_SECS    = 60
#
# if time_remaining >= HARD_CADENCE_SNAP_TO_THRESHOLD_SECONDS and seconds of time_remaining is not at the multiple of an update interval for the current cadence,
# then fire appropriately so timer next appears at a multiple of an update interval for the current cadence
HARD_CADENCE_SNAP_TO_THRESHOLD_SECONDS = 60 
#
# Bounds applied to BOTH --for and --until:
# - must be at least MIN_AUTO_QUIT_SECS seconds in the future
# - must be no more than MAX_AUTO_QUIT_SECS seconds in the future
MIN_AUTO_QUIT_SECS = 10                     # at least 10s
MAX_AUTO_QUIT_SECS = 366 * 24 * 60 * 60     # ≤ 366 days

# --------------------------------------------------------------------
# PASTE YOUR BASE64 HERE (leave empty to use file/CLI fallback)
# --------------------------------------------------------------------
EYE_IMAGE_BASE64 = ( "" )

class Stay_AwakeTrayApp:
    def __init__(self, icon_override_path: str | None = None, auto_quit_seconds: int | None = None, auto_quit_target_epoch: float | None = None):
        # Core state
        self.running = False
        self.icon = None
        self.main_window = None
        self.keep_awake_context = None
        self.window_visible = True
    
        # Tk/PIL caches to prevent GC & repeated work
        self._cached_photo_main = None
        self._pil_base_image = None   # original PIL image cache
        self._tray_icon_image = None  # small tray PIL image cache
    
        # CLI image override
        self.icon_override_path = icon_override_path
    
        # Auto-quit settings
        self.auto_quit_seconds = auto_quit_seconds               # (--for) duration in seconds, or None
        self.auto_quit_target_epoch = auto_quit_target_epoch     # (--until) local epoch seconds, or None
        self._auto_quit_timer = None                             # threading.Timer handle
    
        # Countdown / ETA display state
        self.auto_quit_deadline = None       # monotonic() timestamp when we’ll quit (float seconds)
        self.auto_quit_walltime = None       # wall-time (time.time()) when we’ll quit (for “Auto-quit at:”)
        self._eta_value = None               # ttk.Label for ETA (value cell)
        self._countdown_value = None         # ttk.Label for “Time remaining” (value cell)
        self._countdown_after_id = None      # Tk after() handle so we can cancel/reschedule
        self._cadence_value = None           # ttk.Label for “Timer update frequency”
        self._last_cadence_s = None          # last cadence shown (int seconds), to avoid churn
    
        # Signal/cleanup hooks
        atexit.register(self.cleanup)
        signal.signal(signal.SIGINT, self.signal_handler)
        signal.signal(signal.SIGTERM, self.signal_handler)


    # -------------------- Paths / images --------------------

    def _script_dir(self) -> Path:
        # When frozen by PyInstaller:
        #   - onefile/onedir: sys.executable is the bundled EXE; use its folder
        try:
            if getattr(sys, "frozen", False):
                return Path(sys.executable).resolve().parent
            return Path(__file__).resolve().parent
        except Exception:
            return Path(os.getcwd())

    def _try_decode_base64(self):
        """Return PIL.Image from base64 or None if not decodable/empty."""
        raw = "".join(EYE_IMAGE_BASE64) if isinstance(EYE_IMAGE_BASE64, (list, tuple)) else str(EYE_IMAGE_BASE64 or "")
        if not raw.strip():
            return None
        try:
            return Image.open(io.BytesIO(base64.b64decode(raw))).convert("RGBA")
        except Exception as e:
            print(f"Base64 image decode failed, will try file fallback: {e}", flush=True)
            return None

    def _try_load_override_file(self):
        """If --icon PATH was provided, try to load it first."""
        if not self.icon_override_path:
            return None
        try:
            p = Path(self.icon_override_path).expanduser()
            # If not absolute, resolve relative to current working dir
            if not p.is_absolute():
                p = (Path.cwd() / p).resolve()
            if p.exists():
                print(f"Loading icon from CLI override: {p}", flush=True)
                return Image.open(p).convert("RGBA")
            else:
                print(f"CLI override not found: {p}", flush=True)
        except Exception as e:
            print(f"Failed to load CLI override image: {e}", flush=True)
        return None

    def _try_load_from_files(self):
        """Try the candidate filenames in order; return PIL.Image or None."""
        folder = self._script_dir()
        for name in IMAGE_CANDIDATES:
            p = folder / name
            if p.exists():
                try:
                    print(f"Loading icon from file: {p.name}", flush=True)
                    return Image.open(p).convert("RGBA")
                except Exception as e:
                    print(f"Failed to load {p.name}: {e}", flush=True)
        return None

    def _fallback_draw_eye(self, size=(640, 490)):
        """Last-resort symbolic drawing so the app never crashes."""
        image = Image.new("RGBA", size, (0, 0, 0, 0))
        draw = ImageDraw.Draw(image)
        w, h = size
        draw.arc([10, h//3, w-10, h-40], start=200, end=-20, width=18, fill="black")
        draw.arc([10, 40, w-10, h//1.7], start=30, end=160, width=18, fill="black")
        draw.line([w//3.2, h//1.9, w//2.3, h-10], width=18, fill="black")
        cx, cy = int(w*0.82), int(h*0.83)
        r = int(min(w, h)*0.12)
        draw.arc([cx-r, cy-r, cx+r, cy+r], 60, 370, width=18, fill="black")
        pr = int(min(w, h)*0.11)
        pcx, pcy = int(w*0.48), int(h*0.55)
        draw.ellipse([pcx-pr, pcy-pr, pcx+pr, pcy+pr], fill="black", outline="black")
        draw.ellipse([pcx-12, pcy-12, pcx+3, pcy+3], fill="white", outline="white")
        return image

    def _load_eye_image(self) -> Image.Image:
        """
        Load/cached image using priority:
        1) CLI override (--icon path)
        2) internal base64
        3) files in IMAGE_CANDIDATES
        4) drawn fallback
        """
        if self._pil_base_image is not None:
            return self._pil_base_image
        #
        img = self._try_load_override_file()    # --icon
        if img is None:
            img = self._try_decode_base64()     # internal base64 image
        if img is None:
            img = self._try_load_from_files()   # Stay_Awake_icon.* next to the EXE/script (PNG/JPG/JPEG/WEBP/BMP/GIF/ICO)
        if img is None:
            img = self._fallback_draw_eye()     # A small drawn fallback glyph
        self._pil_base_image = img.convert("RGBA")
        return self._pil_base_image

    @staticmethod
    def _resize_keep_aspect(img: Image.Image, max_px: int) -> Image.Image:
        w, h = img.size
        scale = min(max_px / max(w, 1), max_px / max(h, 1), 1.0)
        new_size = (max(1, int(w * scale)), max(1, int(h * scale)))
        return img if new_size == img.size else img.resize(new_size, Image.LANCZOS)

    def get_display_image_tk(self, max_px=MAX_DISPLAY_PX):
        pil = self._load_eye_image()
        pil = self._resize_keep_aspect(pil, max_px)
        return ImageTk.PhotoImage(pil)

    # -------------------- UI helpers --------------------

    def _center_window(self, win):
        win.update_idletasks()
        w, h = win.winfo_width(), win.winfo_height()
        sw, sh = win.winfo_screenwidth(), win.winfo_screenheight()
        x, y = (sw - w) // 2, (sh - h) // 2
        win.geometry(f"+{x}+{y}")

    def _call_on_main(self, fn, *args, **kwargs):
        if self.main_window and threading.current_thread() is not threading.main_thread():
            self.main_window.after(0, lambda: fn(*args, **kwargs))
            return False
        return True

    # -------------------- Windows --------------------

    def create_main_window(self):
        """Main control window (native look)."""
        self.main_window = tk.Tk()
        self.main_window.title("Stay_Awake")
        self.main_window.resizable(True, True)

        # Main window “X” should EXIT the app (gracefully)
        self.main_window.protocol("WM_DELETE_WINDOW", self.quit_from_window)

        # Redirect title-bar minimize ("_") to system-tray hide
        self.main_window.bind("<Unmap>", self._on_window_unmap)

        # Layout
        container = ttk.Frame(self.main_window, padding=(16, 16, 16, 16))
        container.pack(fill=tk.BOTH, expand=True)

        # Image centered, scaled to <= 512 px
        self._cached_photo_main = self.get_display_image_tk(MAX_DISPLAY_PX)
        ttk.Label(container, image=self._cached_photo_main, anchor="center").pack(side=tk.TOP, pady=(0, 8))

        # Blurb text 
        ttk.Label(container, text=APP_BLURB, justify="center").pack(side=tk.TOP, pady=(0, 12))

        ttk.Separator(container, orient="horizontal").pack(fill="x", pady=(0, 8))

        # Buttons at bottom (vanilla style)
        btns = ttk.Frame(container)
        btns.pack(side=tk.BOTTOM, fill=tk.X, pady=(8, 0))
        ttk.Button(btns, text="Minimize to System Tray", command=self.minimize_to_tray).pack(side=tk.LEFT, padx=(0, 8))
        ttk.Button(btns, text="Quit", command=self.quit_from_window).pack(side=tk.RIGHT, padx=(8, 0))

        # Status frame + countdown area (bottom, left-aligned)
        status_frame = ttk.Frame(container)
        status_frame.pack(side=tk.BOTTOM, fill=tk.X, pady=(8, 0))
    
        # Status hint inside the Status frame
        ttk.Label(
            status_frame,
            text="Right-click the tray icon for options.",
            foreground="gray",
            justify="center"
        ).pack(anchor="center")
        
        # ETA + countdown (only if --for was given) inside the Status frame
        if self.auto_quit_seconds and self.auto_quit_seconds > 0 and self.auto_quit_walltime:
            # Center this whole "table" within the bottom status area
            countdown = ttk.Frame(status_frame)
            countdown.pack(anchor="center", pady=(6, 0))
            # Row 1: "Auto-quit at:"  |  <YYYY-MM-DD HH:MM:SS>
            eta_text = time.strftime("%Y-%m-%d %H:%M:%S", time.localtime(self.auto_quit_walltime))
            ttk.Label(countdown, text="Auto-quit at:", justify="right").grid(row=0, column=0, sticky="e", padx=(0, 8), pady=(0, 2))
            self._eta_value = ttk.Label(countdown, text=eta_text, justify="left")
            self._eta_value.grid(row=0, column=1, sticky="w", pady=(0, 2))
            # Row 2: "Time remaining:" |  <DDDd hh:mm:ss>
            ttk.Label(countdown, text="Time remaining:", justify="right").grid(row=1, column=0, sticky="e", padx=(0, 8))
            self._countdown_value = ttk.Label(countdown, text="—", justify="left")
            self._countdown_value.grid(row=1, column=1, sticky="w")
            # Row 3: "Timer update frequency:" |  <HH:MM:SS>  (starts blank; will be set by ticker)
            self._cadence_label = ttk.Label(countdown, text="Timer update frequency:", justify="right").grid(row=2, column=0, sticky="e", padx=(0, 8))
            self._cadence_value = ttk.Label(countdown, text="—", justify="left")
            self._cadence_value.grid(row=2, column=1, sticky="w")
            # Columns don’t need weights; we want natural width and center as a unit
            # but if you want them to stretch evenly, uncomment:
            # countdown.grid_columnconfigure(0, weight=1)
            # countdown.grid_columnconfigure(1, weight=1)
            # start low-churn countdown updates
            # INITIAL value immediately (even if not yet viewable)
            rem0 = max(0, int(round(self.auto_quit_deadline - time.monotonic())))
            self._countdown_value.configure(text=self._format_dhms(rem0))
            # The tiny delay lets the window become viewable, then the tick function takes over and keeps rescheduling with the cadence
            self._countdown_after_id = self.main_window.after(250, self._schedule_countdown_tick)

        # finally, center the window
        self._center_window(self.main_window)

    # -------------------- Window controls --------------------

    def minimize_to_tray(self):
        if self.main_window:
            self.main_window.withdraw()
            self.window_visible = False

    def show_main_window(self, icon=None, item=None):
        def _impl():
            if self.main_window:
                self.main_window.deiconify()
                self.main_window.lift()
                self.main_window.focus_force()
                self.window_visible = True
                if self.auto_quit_deadline:     # if counting down, reschedule
                    self._schedule_countdown_tick()
        if not self._call_on_main(_impl):
            return
        _impl()

    def quit_from_window(self):
        self.quit_application(None, None)

    # Intercept OS minimize (iconify) and route to system-tray hide
    def _on_window_unmap(self, event):
        try:
            if self.main_window and self.main_window.state() == "iconic":
                self.minimize_to_tray()
        except Exception:
            pass

    # -------------------- Core / lifecycle --------------------

    def start_Stay_Awake(self):
        try:
            self.keep_awake_context = keep.running()
            self.keep_awake_context.__enter__()
            self.running = True
            print("Stay_Awake activated", flush=True)
        except Exception as e:
            if self.main_window:
                messagebox.showerror("Error", f"Failed to activate Stay_Awake: {e}")
            sys.exit(1)

    def cleanup(self):
        # run once only (atexit + signals + manual quit may all hit this)
        if getattr(self, "_cleanup_done", False):
            return
        self._cleanup_done = True
        # 1) cancel the auto-quit timer so it can't fire during shutdown
        t = getattr(self, "_auto_quit_timer", None)
        if t:
            try:
                t.cancel()
            except Exception:
                pass
            finally:
                self._auto_quit_timer = None
        # 2) restore normal power management (wakepy context exit)
        if self.running and self.keep_awake_context:
            print("Cleaning up - restoring normal power management.", flush=True)
            try:
                self.keep_awake_context.__exit__(None, None, None)
                print("Normal power management restored", flush=True)
            except Exception as e:
                print(f"Error during cleanup: {e}", flush=True)
            finally:
                self.running = False
                self.keep_awake_context = None
        # 3) Belt-and-braces UI teardown (usually already handled)
        #    As a last-resort fallback (normally handled in quit/signal paths)
        try:
            if self.icon:
                self.icon.visible = False
                self.icon.stop()
        except Exception:
            pass
        try:
            if self.main_window and self.main_window.winfo_exists():
                # If mainloop is likely still running, schedule destroy on Tk thread
                self.main_window.after(0, self.main_window.destroy)
        except Exception:
            pass
        # cancel any pending scheduled tick (belt-and-braces)
        if self._countdown_after_id:
            try:
                self.main_window.after_cancel(self._countdown_after_id)
            except Exception:
                pass
            finally:
                self._countdown_after_id = None

    def signal_handler(self, signum, frame):
        print(f"Received signal {signum}, cleaning up.", flush=True)
        self.cleanup()
        if self.icon:
            try:
                self.icon.visible = False
                self.icon.stop()
            except Exception:
                pass
        sys.exit(0)

    def quit_application(self, icon, item):
        def _impl():
            print("User requested quit", flush=True)
            self.cleanup()
            if self.main_window:
                try:
                    self.main_window.quit()
                    self.main_window.destroy()
                except Exception:
                    pass
            if self.icon:
                try:
                    self.icon.visible = False
                    self.icon.stop()
                except Exception:
                    pass
            time.sleep(0.1)
            sys.exit(0)
        if not self._call_on_main(_impl):
            return
        _impl()

    # -------------------- Auto-quit with ETA and Countdown --------------------

    def _start_auto_quit_timer(self, seconds: int) -> None:
        """
        Start a one-shot timer that will quit the app after 'seconds'.
        Sets:
          - auto_quit_deadline: precise monotonic deadline for countdown math
          - auto_quit_walltime: human-readable wall-clock ETA (time.time()-based)
        If auto_quit_target_epoch is known (both --until and --for can set it),
        use that exact target for the ETA so the window matches the console log.
        Otherwise, fall back to ceil(time.time()) + seconds to align to whole seconds.
        """
        #
        # !!!!!!!!!!!!!!! NOTE NOTE NOTE IMPORTANT IMPORTANT IMPORTANT !!!!!!!!!!!!!!!
        # If we ever change UI creation order, 
        #   remember that the ETA label pulls from self.auto_quit_walltime
        # which we *set here* ...
        #
        # ... so ALWAYS keep the timer-first sequence.
        # !!!!!!!!!!!!!!! NOTE NOTE NOTE IMPORTANT IMPORTANT IMPORTANT !!!!!!!!!!!!!!!
        #
        if not seconds or seconds <= 0:
            return
        # For countdown math (robust against system clock changes)
        self.auto_quit_deadline = time.monotonic() + seconds
        # For user-visible ETA label
        if self.auto_quit_target_epoch is not None:
            # Use the exact target epoch computed during CLI parsing
            self.auto_quit_walltime = self.auto_quit_target_epoch
        else:
            # Fallback path (no explicit target): keep ETA on whole-second boundary
            self.auto_quit_walltime = math.ceil(time.time()) + seconds
        # Cancel any previous timer before creating a new one
        if getattr(self, "_auto_quit_timer", None):
            try:
                self._auto_quit_timer.cancel()
            except Exception:
                pass
            finally:
                self._auto_quit_timer = None
        def _on_timeout():
            # This runs in the timer thread.
            print(f"Auto-quit timer expired after {int(seconds)}s; quitting…", flush=True)
            try:
                if self.main_window and self.main_window.winfo_exists():
                    # Marshal shutdown onto the Tk main thread; safest for any UI work. (schedule quit on the Tk thread)
                    self.main_window.after(0, lambda: self.quit_application(None, None))
                else:
                    self.quit_application(None, None)
            except Exception:
                # As a last resort, avoid hanging forever if the GUI/thread state is odd.
                try:
                    os._exit(0)
                except Exception:
                    pass
        t = threading.Timer(seconds, _on_timeout)
        # Setting daemon True so the timer thread won't block interpreter shutdown in edge cases
        t.daemon = True
        self._auto_quit_timer = t
        t.start()

    def _format_dhms(self, total_seconds: int) -> str:
        # DDDd hh:mm:ss (omit days if 0)
        if total_seconds < 0:
            total_seconds = 0
        d, r = divmod(total_seconds, 86400)
        h, r = divmod(r, 3600)
        m, s = divmod(r, 60)
        return (f"{d}d {h:02d}:{m:02d}:{s:02d}") if d else (f"{h:02d}:{m:02d}:{s:02d}")

    def _schedule_countdown_tick(self):
        # If countdown isn’t active or window doesn’t exist, cancel any pending tick and bail
        if not self.auto_quit_deadline or not (self.main_window and self.main_window.winfo_exists()):
            if self._countdown_after_id:
                try:
                    self.main_window.after_cancel(self._countdown_after_id)
                except Exception:
                    pass
                finally:
                    self._countdown_after_id = None
            return
        # If the window isn't visible, throttle updates (saves even more CPU)
        # Determine visibility (use Tk’s truth)
        try:
            visible = bool(self.main_window.winfo_viewable())
        except Exception:
            visible = True
        now = time.monotonic()
        rem = max(0, int(round(self.auto_quit_deadline - now)))
        # Update the value cell in the 2-column table
        target = getattr(self, "_countdown_value", None)
        if visible and target:
            target.configure(text=self._format_dhms(rem))
        # Pick next update interval from the global configured cadence
        next_ms = 1_000  # default (should be overridden by the loop)
        for threshold, cadence in COUNTDOWN_CADENCE:
            if rem > threshold:
                next_ms = cadence
                break
        # Snap first update to a cadence boundary when we're still "far out"
        if rem >= HARD_CADENCE_SNAP_TO_THRESHOLD_SECONDS:
            cadence_s = max(1, next_ms // 1000)   # current cadence (sec)
            phase = rem % cadence_s               # how far we are from the next boundary
            if phase != 0:
                snap_ms = phase * 1000            # bring next tick to the boundary
                # avoid micro-sleeps right at the boundary; nudge to the next step if <200ms
                if snap_ms < 200:
                    snap_ms += cadence_s * 1000
                # only snap sooner than the regular cadence
                if snap_ms < next_ms:
                    next_ms = snap_ms
                #print(f"[DEBUG][SNAP to next cadence boundary][tick] rem={rem}s next_ms={next_ms}", flush=True)
        # If not visible, back off to a larger interval unless we're in the last N seconds
        # (define HIDDEN_CADENCE_MIN_MS and HIDDEN_BACKOFF_UNTIL_SECS at module scope if you use this)
        if not visible and rem > HIDDEN_BACKOFF_UNTIL_SECS and next_ms < HIDDEN_CADENCE_MIN_MS:
            next_ms = HIDDEN_CADENCE_MIN_MS

        # Show prevailing update cadence only when it actually changes (saves churn)
        cad_s = max(1, int(round(next_ms / 1000)))
        if getattr(self, "_cadence_value", None) and visible:
            if self._last_cadence_s != cad_s:
                self._cadence_value.configure(text=self._format_dhms(cad_s))
                self._last_cadence_s = cad_s
                #print(f"[DEBUG][tick] rem={rem}s next_ms={next_ms} "
                #    f"countdown='{self._countdown_value.cget('text') if self._countdown_value and self._countdown_value.winfo_exists() else None}' "
                #    f"cadence='{self._cadence_value.cget('text') if self._cadence_value and self._cadence_value.winfo_exists() else None}'", flush=True)
        #---
        # Cancel any previously scheduled tick before scheduling the next one
        if self._countdown_after_id:
            try:
                self.main_window.after_cancel(self._countdown_after_id)
            except Exception:
                pass
        # Chain the next tick
        self._countdown_after_id = self.main_window.after(next_ms, self._schedule_countdown_tick)
        #---

    # -------------------- Tray --------------------

    def _pad_to_square_edge_stretch(self, im: Image.Image) -> Image.Image:
        # Pad an image to a square by replicating outermost edge pixels (no subject stretch).
        im = im.convert("RGBA")
        w, h = im.size
        if w == h:
            return im
        side = max(w, h)
        lp = (side - w) // 2
        rp = side - w - lp
        tp = (side - h) // 2
        bp = side - h - tp
        sq = Image.new("RGBA", (side, side), (0, 0, 0, 0))
        if tp:
            strip = im.crop((0, 0, w, 1)).resize((w, tp), Image.NEAREST)
            sq.paste(strip, (lp, 0))
        if bp:
            strip = im.crop((0, h - 1, w, h)).resize((w, bp), Image.NEAREST)
            sq.paste(strip, (lp, tp + h))
        if lp:
            strip = im.crop((0, 0, 1, h)).resize((lp, h), Image.NEAREST)
            sq.paste(strip, (0, tp))
        if rp:
            strip = im.crop((w - 1, 0, w, h)).resize((rp, h), Image.NEAREST)
            sq.paste(strip, (lp + w, tp))
        sq.paste(im, (lp, tp), im)
        return sq

    def create_tray_icon_image(self):
        """Create tray icon image (down-sized from loaded Eye image) using replicated out edge for squaring."""
        # Determine a DPI-aware tray glyph size (approx: 16@100%, 20@125%, 24@150%, 32@200%).
        default_DPI = 96 
        default_system_tray_icon_size = 64
        windows_baseline_DPI = 96.0 # 96 is Windows baseline DPI = “100%”. All DPI scaling on Windows is defined relative to 96.
        try:
            dpi = 0
            #try:
            #    dpi = ctypes.windll.user32.GetDpiForSystem()  # Win10/11
            #except Exception:
            #    # Fallback for older systems
            #    LOGPIXELSX = 88
            #    hdc = ctypes.windll.user32.GetDC(0)
            #    dpi = ctypes.windll.gdi32.GetDeviceCaps(hdc, LOGPIXELSX)
            #    ctypes.windll.user32.ReleaseDC(0, hdc)
            dpi = ctypes.windll.user32.GetDpiForSystem()  # Win10/11 ... let it crash if can't get the DPI
            if not dpi:
                dpi = default_DPI
                print(f"Unable to determine Display Monitor DPI, defaulting to {default_DPI}", flush=True)
            system_tray_icon_size = max(16, min(64, int(round(16 * dpi / windows_baseline_DPI)))) 
        except Exception:
            system_tray_icon_size = default_system_tray_icon_size
            print(f"Unable to set system tray icon size based on Display Monitor DPI, defaulting to {default_system_tray_icon_size}", flush=True)
        if self._tray_icon_image is None:
            pil = self._load_eye_image()
            # Make the tray source square using edge-replication stretch pads (no subject distortion)
            pil = self._pad_to_square_edge_stretch(pil)
            icon_pil = self._resize_keep_aspect(pil, system_tray_icon_size)
            self._tray_icon_image = icon_pil
        return self._tray_icon_image

    def create_tray_icon(self):
        image = self.create_tray_icon_image()
        menu = pystray.Menu(
            item("Show Window", self.show_main_window, default=True),
                                           
            pystray.Menu.SEPARATOR,
            item("Quit", self.quit_application),
        )
        self.icon = pystray.Icon("Stay_Awake", image, "Stay_Awake - System Awake", menu)
        self.icon.default_action = self.show_main_window
        self.icon.run()

    def run(self):
        """
        Start the wake lock, create UI, and enter the main loop.
        If a target epoch was provided (from --until or --for), recompute the
        seconds from NOW (rounded up via ceil on the difference) just before
        arming the timer to maximize accuracy and to keep the window's ETA
        in sync with the console print.
        """
        self.start_Stay_Awake()
        # Determine seconds to run (final re-ceil right before arming the timer)
        secs_to_run = self.auto_quit_seconds
        if self.auto_quit_target_epoch is not None:
            # Use ceil on the difference so we never fire early
            secs_to_run = int(math.ceil(self.auto_quit_target_epoch - time.time()))
            if secs_to_run <= 0:
                print("Auto-quit reached during startup; quitting…", flush=True)
                self.quit_application(None, None)
                return
        # Arm the one-shot auto-quit timer if requested (do this BEFORE building the window)
        # so auto_quit_walltime/deadline are set in time for the ETA/countdown labels.
        if secs_to_run and secs_to_run > 0:
            self._start_auto_quit_timer(secs_to_run)
        # Build the window after timing is known (so ETA/countdown/cadence labels appear immediately)
        self.create_main_window()
        # Tray icon in a background thread; Tk loop in main thread
        tray_thread = threading.Thread(target=self.create_tray_icon, daemon=True)
        tray_thread.start()
        self.main_window.mainloop()

# -------------------- CLI: duration parsing --------------------

def parse_duration_to_seconds(text: str) -> int:
    """
    Parse '3d4h5s', '2h', '90m', '3600s', or composites with spaces (case-insensitive).
    Bare numbers are minutes. Returns total seconds (int).
    """
    if not text or not str(text).strip():
        raise ValueError("Empty duration")
    s = str(text).strip()
    total = 0
    last_end = 0
    any_match = False
    for m in _RE_DURATION_TOKEN.finditer(s):
        # Disallow non-space junk between tokens
        if m.start() != last_end and s[last_end:m.start()].strip():
            raise ValueError(f"Invalid duration syntax near: {s[last_end:m.start()]!r}")
        val = int(m.group(1))
        unit = (m.group(2) or '').lower()
        if unit == 'd':
            total += val * 86400
        elif unit == 'h':
            total += val * 3600
        elif unit == 'm' or unit == '':
            total += val * 60
        elif unit == 's':
            total += val
        else:
            # Shouldn't occur due to the regex, but keep it defensive
            raise ValueError(f"Unknown unit: {unit!r}")
        last_end = m.end()
        any_match = True
    # Must have matched at least one token and consumed the string (whitespace allowed at the end)
    if not any_match or s[last_end:].strip():
        raise ValueError(f"Invalid duration: {text!r}")
    if total < 0:
        raise ValueError("Duration must be >= 0")
    return total

def parse_until_to_epoch(text: str) -> float:
    """
    Parse a relaxed local timestamp like '2025-01-02 23:22:21', '2025- 1- 2 03:02:01',
    or '2025-1-2 3:2:1' into a *local* epoch seconds (float), with robust DST handling.

    Strategy:
      1) Regex-parse numbers and build a naive datetime (calendar validation happens here).
      2) Two-pass mktime round-trip with tm_isdst=0 and tm_isdst=1:
         - If neither round-trip reproduces the same wall time -> NONEXISTENT local time (spring-forward gap) -> error.
         - If exactly one round-trips correctly            -> valid; use that epoch.
         - If both round-trip and epochs differ            -> AMBIGUOUS local time (fall-back overlap) -> error.
         - If both round-trip and epochs equal             -> normal time; use epoch.

    Raises:
        ValueError on invalid format, invalid calendar date, out-of-range epoch,
        or DST edge cases (nonexistent/ambiguous local time).
    """
    if not text or not str(text).strip():
        raise ValueError("Empty --until value")
    m = _RE_UNTIL_TOKEN.match(text)
    if not m:
        raise ValueError(
            "Invalid --until format. Use: YYYY-MM-DD HH:MM:SS (24h), "
            "with optional extra spaces and 1–2 digit M/D/h/m/s."
        )
    y, mon, d, hh, mm, ss = (int(m.group(i)) for i in range(1, 7))
    # Calendar validation (e.g., rejects 2025-02-31). This *does not* apply a timezone yet.
    try:
        dt = datetime(year=y, month=mon, day=d, hour=hh, minute=mm, second=ss)
    except ValueError as e:
        raise ValueError(f"Invalid calendar date/time in --until: {e}") from e
    # Two-pass mktime round-trip:
    # Build tm tuples with tm_isdst fixed to 0 or 1 (wday/yday=-1 lets C lib compute them).
    def _epoch_if_roundtrips(isdst_flag: int) -> float | None:
        tup = (dt.year, dt.month, dt.day, dt.hour, dt.minute, dt.second, -1, -1, isdst_flag)
        try:
            epoch = time.mktime(tup)  # interpret as *local* time with explicit DST hint
        except (OverflowError, OSError):
            return None
        lt = time.localtime(epoch)
        # Only accept if the wall time truly round-trips to the same civil components.
        if (lt.tm_year, lt.tm_mon, lt.tm_mday, lt.tm_hour, lt.tm_min, lt.tm_sec) == (
            dt.year, dt.month, dt.day, dt.hour, dt.minute, dt.second
        ):
            return float(epoch)
        return None
    epoch_std = _epoch_if_roundtrips(0)  # prefer "standard" interpretation
    epoch_dst = _epoch_if_roundtrips(1)  # prefer "daylight" interpretation
    if epoch_std is None and epoch_dst is None:
        # e.g., "spring forward" gap
        raise ValueError("--until is not a valid local wall-clock time on this system (nonexistent due to DST transition).")
    if epoch_std is not None and epoch_dst is not None:
        # Both interpretations are valid but map to *different* instants -> ambiguous fall-back hour
        if abs(epoch_std - epoch_dst) >= 1.0:
            raise ValueError("--until is ambiguous (falls in the repeated DST hour). Please choose a different time.")
        # If they’re somehow equal (rare/no-DST zones), accept either
        return epoch_std
    # Exactly one pass valid -> use it
    return epoch_std if epoch_std is not None else epoch_dst

# -------------------- CLI: main --------------------

def main():
    # ---------- CLI parsing ----------
    parser = argparse.ArgumentParser(description="Stay_Awake system tray tool")
    # --icon rarely used, if ever
    parser.add_argument("--icon", dest="icon_path", metavar="PATH", help="Path to an image file (PNG/JPG/JPEG/WEBP/BMP/GIF/ICO) used for the app icon & window image.")
    # Mutually exclusive CLI switches : --for OR --until
    group = parser.add_mutually_exclusive_group()
    group.add_argument("--for", dest="for_duration", metavar="DURATION", help="Auto-quit after duration (e.g., 45m, 2h, 1h30m, 3600s, 3d4h5s). Bare number = minutes. Use 0 to disable.")
    group.add_argument("--until", dest="until_timestamp", metavar='"YYYY-MM-DD HH:MM:SS"', help='Local wall-time to auto-quit (24h). Example: "2025-01-02 23:22:21". Relaxed spacing and 1–2 digit M/D/h/m/s allowed.')
    # grab the commandline and parse it
    args = parser.parse_args()
    #
    auto_secs: int | None = None
    auto_target_epoch: float | None = None
    #
    # ----- Handle --until -----
    if args.until_timestamp:
        try:
            target_epoch = parse_until_to_epoch(args.until_timestamp)
        except ValueError as e:
            print(f"Invalid --until value: {e}", flush=True)
            sys.exit(2)
        now_ceil = math.ceil(time.time())
        secs = int(target_epoch - now_ceil)
        if secs < MIN_AUTO_QUIT_SECS:
            print(f"--until must be at least {MIN_AUTO_QUIT_SECS} seconds in the future (got {secs}s).", flush=True)
            sys.exit(2)
        if secs > MAX_AUTO_QUIT_SECS:
            days = MAX_AUTO_QUIT_SECS // 86400
            print(f"--until must be within {days} days from now.", flush=True)
            sys.exit(2)
        auto_secs = secs
        auto_target_epoch = target_epoch
        d, r = divmod(secs, 86400); h, r = divmod(r, 3600); m, s = divmod(r, 60)
        pretty = (f"{d}d {h:02d}:{m:02d}:{s:02d}") if d else (f"{h:02d}:{m:02d}:{s:02d}")
        print(f'--until: will auto-quit at {time.strftime("%Y-%m-%d %H:%M:%S", time.localtime(target_epoch))} ({pretty} from now).', flush=True)
    # ----- Handle --for -----
    elif args.for_duration:
        try:
            secs = parse_duration_to_seconds(args.for_duration)
        except ValueError as e:
            print(f"Invalid --for value: {e}", flush=True)
            sys.exit(2)
        if secs == 0:
            # Zero 0 disables auto-quit
            print("--for: 0 seconds specified, auto-quit is disabled", flush=True)
            auto_secs = None
        else:
            # Enforce bounds
            if secs < MIN_AUTO_QUIT_SECS:
                print(f"--for must be at least {MIN_AUTO_QUIT_SECS} seconds (got {secs}s).", flush=True)
                sys.exit(2)
            if secs > MAX_AUTO_QUIT_SECS:
                days = MAX_AUTO_QUIT_SECS // 86400
                print(f"--for cannot exceed {days} days (got ~{secs/86400:.1f} days).", flush=True)
                sys.exit(2)
            # Record the original seconds and compute a target epoch from NOW (rounded up),
            # so run() can re-ceil precisely just before arming the timer—same as --until.
            now_ceil = math.ceil(time.time())
            auto_target_epoch = float(now_ceil + secs)
            auto_secs = secs
            d, r = divmod(secs, 86400); h, r = divmod(r, 3600); m, s = divmod(r, 60)
            pretty = (f"{d}d {h:02d}:{m:02d}:{s:02d}") if d else (f"{h:02d}:{m:02d}:{s:02d}")
            print(f"--for: will auto-quit after {secs} seconds ({pretty}).", flush=True)
    # ----- Launch app -----
    # define app = None BEFORE  the try: so finally can safely reference it even if construction failed early.
    app = None
    try:
        # auto_target_epoch is None unless --for/--until set
        app = Stay_AwakeTrayApp(
            icon_override_path=args.icon_path,
            auto_quit_seconds=auto_secs,
            auto_quit_target_epoch=auto_target_epoch,
        )
        app.run()
    except KeyboardInterrupt:
        # In practice, your SIGINT handler already calls quit; this is a nice message if Ctrl+C occurs pre-GUI.
        print("\nInterrupted by user", flush=True)
    except SystemExit:
        # Let explicit sys.exit() propagate (e.g., CLI validation exits)
        raise
    except Exception as e:
        # SHOW traceback if you prefer:
        traceback.print_exc()
        print(f"Unexpected error: {e}", flush=True)
    finally:
        # Best-effort, idempotent cleanup (safe even if atexit will also run).
        print("Final cleanup...", flush=True)
        try:
            if app is not None:
                app.cleanup()
        except Exception:
            pass

if __name__ == "__main__":
    main()
