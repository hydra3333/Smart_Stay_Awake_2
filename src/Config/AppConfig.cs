// File: src/Stay_Awake_2/Config/AppConfig.cs
// Purpose: Single home for app-wide constants and defaults.
// Naming: per your rule, CONSTANTS in UPPER_CASE_WITH_UNDERSCORES.

using System;
using System.Collections.Generic;

namespace Stay_Awake_2
{
    internal static class AppConfig
    {
        // ---- App identity ----------------------------------------------------
        public const string APP_INTERNAL_NAME = "Stay_Awake_2";
        public const string APP_DISPLAY_NAME = "Stay Awake 2";

        // ---- Tracing defaults -------------------------------------------------
        // Developer-only hard override (forces tracing even without --verbose).
        public const bool FORCED_TRACE_DEFAULT = false;

        // Where/how trace logs are written when tracing is ON.
        public const string LOG_FILE_BASENAME_PREFIX = "Stay_Awake_2_Trace_";
        public const string LOG_FILE_DATE_FORMAT = "yyyyMMdd"; // zero-padded y-M-d
        public const string LOG_FALLBACK_SUBDIR = "Stay_Awake_2\\Logs"; // under %LocalAppData%

        // ---- Auto-quit bounds (seconds) --------------------------------------
        public const int MIN_AUTO_QUIT_SECONDS = 10;                 // ≥ 10s
        public const int MAX_AUTO_QUIT_SECONDS = 365 * 24 * 3600;    // ≤ ~365d

        // ---- Allowed image/icon extensions -----------------------------------
        public static readonly HashSet<string> ALLOWED_ICON_EXTENSIONS =
            new(StringComparer.OrdinalIgnoreCase)
            { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".ico" };

        // ---- Icon imaging knobs ---------------------------------------------------------
        public const int WINDOW_MAX_IMAGE_EDGE_PX = 512;
        public static readonly int[] TRAY_ICON_SIZES = new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
    }
}
