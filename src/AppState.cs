// File: src/Stay_Awake_2/AppState.cs
// Purpose: Carry runtime state derived from CLI + environment (trace flags, version, log path).
// Keep it read-only from the outside to reduce accidental coupling.

using System;
using System.Diagnostics;
using System.Reflection;

namespace Stay_Awake_2
{
    [DebuggerDisplay("{AppVersion}, Trace={TraceEnabled}, Log={LogFullPath ?? \"<none>\"}")]
    internal sealed class AppState
    {
        public bool TraceEnabled { get; }
        public string? LogFullPath { get; }
        public CliOptions Options { get; }
        public string AppVersion { get; }

        private AppState(bool traceEnabled, string? logFullPath, CliOptions options, string appVersion)
        {
            TraceEnabled = traceEnabled;
            LogFullPath = logFullPath;
            Options = options;
            AppVersion = appVersion;
        }

        public static AppState Create(CliOptions opts, bool traceEnabled, string? logPath)
        {
            Trace.WriteLine("Entered AppState.Create ...");

            // Extract a friendly version (prefer InformationalVersion)
            string version =
                (Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion)
                ?? (Assembly.GetEntryAssembly()?.GetName()?.Version?.ToString() ?? "0.0.0");

            var state = new AppState(traceEnabled, logPath, opts, version);

            Trace.WriteLine($"AppState.Create: Version={version}, TraceEnabled={traceEnabled}, LogPath={(logPath ?? "<none>")}");
            Trace.WriteLine("Exiting AppState.Create.");
            return state;
        }

        public override string ToString()
            => $"Version={AppVersion}, TraceEnabled={TraceEnabled}, LogFullPath={(LogFullPath ?? "<none>")}, Options=[{Options}]";
    }
}
