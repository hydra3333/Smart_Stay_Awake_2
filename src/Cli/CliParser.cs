// File: src/Stay_Awake_2/Cli/CliParser.cs
// Purpose: Parse and validate command-line arguments into CliOptions.
// Heavily commented and defensive, with clear error messages via CliParseException.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Stay_Awake_2
{
    /// <summary>
    /// Thrown when command-line parsing or validation fails.
    /// Program.Main will catch this and route to Fatal().
    /// </summary>
    internal sealed class CliParseException : Exception
    {
        public CliParseException(string message) : base(message) { }
    }

    internal static class CliParser
    {
        // ---- Bounds for auto-quit behavior (per spec v11) --------------------

        // Regex to parse duration tokens like "3d4h5s", "1h30m", "90m", "3600s".
        // We allow any order but will sum each unit found.
        private static readonly Regex DurationPattern = new(
            @"(?ix)                           # ignore case, allow comments
              (?:
                 (?<days>\d+)\s*d
               | (?<hours>\d+)\s*h
               | (?<minutes>\d+)\s*m
               | (?<seconds>\d+)\s*s
              )", RegexOptions.Compiled);

        /// <summary>
        /// Main entry: parse args into CliOptions and validate.
        /// Throws CliParseException on error.
        /// </summary>
        public static CliOptions Parse(string[] args)
        {
            Trace.WriteLine("Entered CliParser.Parse ...");
            try
            {
                // -----------------------------------------------------------------
                // 0) Quick path: no args → default options
                // -----------------------------------------------------------------
                if (args == null) args = Array.Empty<string>();

                string? iconPath = null;
                bool verbose = false;
                TimeSpan? forDuration = null;
                DateTime? untilLocal = null;
                bool showHelp = false;

                // Basic tokenization/iteration. We accept switches like:
                // --icon PATH
                // --for DURATION
                // --until "YYYY-M-D H:m:s"
                // --verbose
                for (int i = 0; i < args.Length; i++)
                {
                    string token = args[i].Trim();

                    if (token.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                        token.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
                        token.Equals("/?"))
                    {
                        showHelp = true;
                        // We still continue to parse in case user combined flags; harmless.
                        continue;
                    }

                    if (token.Equals("--verbose", StringComparison.OrdinalIgnoreCase))
                    {
                        verbose = true;
                        continue;
                    }

                    if (token.Equals("--icon", StringComparison.OrdinalIgnoreCase))
                    {
                        // Expect a path after --icon
                        if (++i >= args.Length)
                            throw new CliParseException("Missing value for --icon. Expected a file path.");

                        string rawPath = args[i].Trim().Trim('"');

                        if (string.IsNullOrWhiteSpace(rawPath))
                            throw new CliParseException("Empty path provided for --icon.");

                        string fullPath;
                        try
                        {
                            fullPath = Path.GetFullPath(rawPath);
                        }
                        catch (Exception ex)
                        {
                            throw new CliParseException($"Invalid --icon path: {rawPath}\n{ex.Message}");
                        }

                        if (!File.Exists(fullPath))
                            throw new CliParseException($"--icon file not found: {fullPath}");

                        string ext = Path.GetExtension(fullPath);
                        if (!AppConfig.ALLOWED_ICON_EXTENSIONS.Contains(ext))
                            throw new CliParseException($"--icon extension '{ext}' not supported. Allowed: .png .jpg .jpeg .bmp .gif .ico");

                        iconPath = fullPath;
                        Trace.WriteLine($"CliParser: IconPath accepted: {iconPath}");
                        continue;
                    }

                    if (token.Equals("--for", StringComparison.OrdinalIgnoreCase))
                    {
                        if (++i >= args.Length)
                            throw new CliParseException("Missing value for --for. Example: --for 1h30m");

                        string raw = args[i].Trim().ToLowerInvariant();
                        // Bare number (no unit) means minutes
                        if (IsAllDigits(raw))
                        {
                            if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out int mins))
                                throw new CliParseException($"--for value '{raw}' is not a valid integer.");
                            forDuration = TimeSpan.FromMinutes(mins);
                        }
                        else
                        {
                            // Sum up tokens like 3d4h5s, 1h30m, 90m etc.
                            forDuration = ParseDuration(raw);
                        }

                        ValidateDurationBounds(forDuration.Value);
                        Trace.WriteLine($"CliParser: ForDuration accepted: {forDuration}");
                        continue;
                    }

                    if (token.Equals("--until", StringComparison.OrdinalIgnoreCase))
                    {
                        if (++i >= args.Length)
                            throw new CliParseException("Missing value for --until. Example: --until \"2025-1-2 3:4:5\"");

                        string raw = args[i].Trim().Trim('"', '\'');  // relaxed quotes
                        var parsed = ParseUntilLocalRelaxed(raw);
                        // Validate bounds relative to "now":
                        DateTime nowLocal = DateTime.Now;
                        TimeSpan delta = parsed - nowLocal;

                        if (delta.TotalSeconds < AppConfig.MIN_AUTO_QUIT_SECONDS)
                            throw new CliParseException($"--until must be at least {AppConfig.MIN_AUTO_QUIT_SECONDS} seconds in the future.");

                        if (delta.TotalSeconds > AppConfig.MAX_AUTO_QUIT_SECONDS)
                            throw new CliParseException($"--until must be within {AppConfig.MAX_AUTO_QUIT_SECONDS} seconds from now (~365 days).");

                        untilLocal = parsed;
                        Trace.WriteLine($"CliParser: UntilLocal accepted: {untilLocal:yyyy-MM-dd HH:mm:ss}");
                        continue;
                    }

                    // Unknown token → friendly error
                    throw new CliParseException($"Unknown argument: {token}");
                }

                // -----------------------------------------------------------------
                // 1) Cross-argument validation
                // -----------------------------------------------------------------
                if (forDuration.HasValue && untilLocal.HasValue)
                    throw new CliParseException("--for and --until are mutually exclusive; specify only one.");

                // Everything looks good → build result
                var result = new CliOptions
                {
                    IconPath = iconPath,
                    Verbose = verbose,
                    ForDuration = forDuration,
                    UntilLocal = untilLocal,
                    ShowHelp = showHelp
                };

                Trace.WriteLine("Exiting CliParser.Parse (success).");
                return result;
            }
            catch (CliParseException)
            {
                Trace.WriteLine("Caught CliParseException in CliParser.Parse.");
                Trace.WriteLine("Exiting CliParser.Parse (error).");
                throw;
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Caught unexpected exception in CliParser.Parse: " + ex);
                Trace.WriteLine("Exiting CliParser.Parse (error).");
                throw new CliParseException("Unexpected error while parsing arguments: " + ex.Message);
            }
        }

        // ------------------- Helpers -------------------

        private static bool IsAllDigits(string s)
        {
            foreach (char c in s) if (c < '0' || c > '9') return false;
            return s.Length > 0;
        }

        private static TimeSpan ParseDuration(string raw)
        {
            // "Start of duration TRY"
            Trace.WriteLine("Start of ParseDuration TRY");
            try
            {
                long days = 0, hours = 0, minutes = 0, seconds = 0;

                foreach (Match m in DurationPattern.Matches(raw))
                {
                    if (m.Groups["days"].Success) days += long.Parse(m.Groups["days"].Value, CultureInfo.InvariantCulture);
                    if (m.Groups["hours"].Success) hours += long.Parse(m.Groups["hours"].Value, CultureInfo.InvariantCulture);
                    if (m.Groups["minutes"].Success) minutes += long.Parse(m.Groups["minutes"].Value, CultureInfo.InvariantCulture);
                    if (m.Groups["seconds"].Success) seconds += long.Parse(m.Groups["seconds"].Value, CultureInfo.InvariantCulture);
                }

                // If regex found nothing, the format is invalid.
                if (days == 0 && hours == 0 && minutes == 0 && seconds == 0)
                    throw new CliParseException($"Invalid --for format '{raw}'. Examples: 3d4h5s, 1h30m, 90m, 3600s");

                // Build a TimeSpan safely (check for overflow)
                try
                {
                    var ts = TimeSpan.FromDays(days)
                           + TimeSpan.FromHours(hours)
                           + TimeSpan.FromMinutes(minutes)
                           + TimeSpan.FromSeconds(seconds);
                    Trace.WriteLine("End of ParseDuration TRY (success)");
                    return ts;
                }
                catch (OverflowException)
                {
                    throw new CliParseException("--for duration is too large.");
                }
            }
            catch (CliParseException)
            {
                Trace.WriteLine("Caught   ParseDuration CATCH (CliParseException)");
                Trace.WriteLine("End of   ParseDuration TRY");
                throw;
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Caught   ParseDuration CATCH (Unexpected): " + ex);
                Trace.WriteLine("End of   ParseDuration TRY");
                throw new CliParseException("Unexpected error parsing --for: " + ex.Message);
            }
        }

        // private static IFormatProvider CultureBox => CultureInfo.InvariantCulture;

        private static void ValidateDurationBounds(TimeSpan value)
        {
            double secs = value.TotalSeconds;
            if (secs < AppConfig.MIN_AUTO_QUIT_SECONDS)
                throw new CliParseException($"--for must be at least {AppConfig.MIN_AUTO_QUIT_SECONDS} seconds.");
            if (secs > AppConfig.MAX_AUTO_QUIT_SECONDS)
                throw new CliParseException($"--for must be <= {AppConfig.MAX_AUTO_QUIT_SECONDS} seconds (~365 days).");
        }

        private static DateTime ParseUntilLocalRelaxed(string raw)
        {
            // We accept various spacing (e.g., "2025- 1- 2  3: 2: 1").
            // Strategy: compress multiple spaces to single space; then rely on DateTime parsing with current culture,
            // but force 24-hour assumption by favoring formats with seconds.
            string compressed = Regex.Replace(raw, @"\s+", " ").Trim();

            // Try ISO-ish first:
            if (DateTime.TryParseExact(
                    compressed,
                    new[] {
                        "yyyy-M-d H:m:s",
                        "yyyy-MM-dd HH:mm:ss",
                        "yyyy-M-d HH:mm:ss",
                        "yyyy-MM-dd H:m:s"
                    },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces,
                    out DateTime dt))
            {
                return dt;
            }

            // Fallback: last resort general parse (still local)
            if (DateTime.TryParse(compressed, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out dt))
                return dt;

            throw new CliParseException($"--until value '{raw}' could not be parsed as a local 24-hour timestamp (yyyy-M-d H:m:s).");
        }
    }
}
