// File: src/Stay_Awake_2/Program.cs
// Purpose: Entry point. Initialize WinForms defaults, parse CLI, configure tracing, build AppState, run MainForm.
// NOTE: No single-instance/mutex in v11 plan.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Policy;
using System.Windows.Forms;

namespace Stay_Awake_2
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>

        // Developer override: set to true to force tracing even without --verbose.
        // Use centralized default
        private const bool FORCED_TRACE = AppConfig.FORCED_TRACE_DEFAULT;

        [STAThread]
        static void Main(string[] args)
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            // Modern WinForms defaults (high DPI plumbing, default fonts, etc.)
            // Apply modern WinForms defaults (visual styles, text rendering, DPI via ApplicationHighDpiMode).
            ApplicationConfiguration.Initialize();

            try
            {
                Trace.WriteLine("Entered Program.Main ...");

                // 1) Parse CLI -> may throw CliParseException with a friendly message.
                Trace.WriteLine("Start of CLI parse TRY");
                CliOptions opts = CliParser.Parse(args);
                Trace.WriteLine("End of   CLI parse TRY (success)");

                // 2) Configure tracing per spec (§4.11)
                bool enableTrace = FORCED_TRACE || opts.Verbose;
                string? logFullPath = null;

                Trace.WriteLine("Start of tracing config TRY");
                try
                {
                    Trace.Listeners.Clear(); // always clear first; avoid OutputDebugString noise

                    if (enableTrace)
                    {
                        // Build log path: prefer beside EXE; fall back to LocalAppData.
                        string exeDir = AppContext.BaseDirectory;
                        string today = DateTime.Now.ToString(AppConfig.LOG_FILE_DATE_FORMAT, System.Globalization.CultureInfo.InvariantCulture);
                        string fileName = $"{AppConfig.LOG_FILE_BASENAME_PREFIX}{today}.log";
                        // Try next to the EXE first:
                        string candidate = Path.Combine(exeDir, fileName);
                        // Fallback dir:
                        string fallbackDir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            AppConfig.LOG_FALLBACK_SUBDIR);
                        // CanOpenForWrite() creates the file to test writability
                        if (CanOpenForWrite(candidate))
                        {
                            logFullPath = candidate;
                        }
                        else
                        {
                            Directory.CreateDirectory(fallbackDir);
                            logFullPath = Path.Combine(fallbackDir, fileName);
                        }
                        // Overwrite each run to keep logs tight.
                        // But CanOpenForWrite() creates the file to test writability anyway, so no need to open/truncate.
                        // TextWriterTraceListener will create the file if missing and append if present;
                        // since CanOpenForWrite has already created a fresh file, you’ll get a clean file anyway.
                        // using (File.Create(logFullPath)) { /* truncate/close */ }

                        Trace.Listeners.Add(new TextWriterTraceListener(logFullPath));
                        Trace.AutoFlush = true;

                        Trace.WriteLine("==================================================");
                        Trace.WriteLine($"[Init] {AppConfig.APP_INTERNAL_NAME} start {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        // Grab version like AppState.Create(...) would:
                        var asm = System.Reflection.Assembly.GetEntryAssembly();
                        var ver = asm?.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion
                                  ?? asm?.GetName()?.Version?.ToString() ?? "0.0.0";
                        Trace.WriteLine($"[Init] Version: {ver}");
                        Trace.WriteLine("[Init] Tracing enabled.");
                        Trace.WriteLine("[Init] Log file: " + logFullPath);
                        Trace.WriteLine("[Init] Args: " + string.Join(" ", args));
                    }
                }
                catch (Exception ex)
                {
                    // If tracing setup fails, continue without tracing.
                    // The app should still run; we just note it via a best-effort MessageBox.
                    MessageBox.Show(
                        "Warning: Failed to initialize trace logging.\n" + ex.Message,
                        "Stay_Awake_2 — Warning",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                Trace.WriteLine("End of   tracing config TRY");

                // 3) Create AppState (collects version, options, trace flags)
                AppState state = AppState.Create(opts, enableTrace, logFullPath);

                // 4) Run the (currently blank) MainForm
                Trace.WriteLine("Starting UI.MainForm ...");
                Application.Run(new UI.MainForm());
                Trace.WriteLine("UI.MainForm exited normally.");
                Trace.WriteLine("Exiting Program.Main (success).");
            }
            catch (CliParseException ex)
            {
                // Known, friendly CLI error (bad args, etc.)
                Trace.WriteLine("Caught CliParseException in Program.Main.");
                FatalHelper.Fatal("Command-line error:\n" + ex.Message, exitCode: 2);
            }
            catch (Exception ex)
            {
                // Unexpected, catch-all path.
                Trace.WriteLine("Caught unexpected exception in Program.Main: " + ex);
                FatalHelper.Fatal("Unexpected error: " + ex.Message, exitCode: 99);
            }
        }

        private static bool CanOpenForWrite(string fullPath)
        {
            try
            {
                // If directory doesn't exist, create it.
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                // Try open exclusive write, then close.
                using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
