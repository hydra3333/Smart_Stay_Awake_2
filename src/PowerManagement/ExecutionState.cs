// File: src/Smart_Stay_Awake_2/PowerManagement/ExecutionState.cs
// Purpose: Low-level Win32 interop wrapper for SetThreadExecutionState.
//          Uses CsWin32-generated bindings (formal definitions from Windows SDK metadata).
//          Stateless: just wraps the Win32 API calls.

using System;
using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.System.Power;

namespace Smart_Stay_Awake_2.PowerManagement
{
    /// <summary>
    /// Low-level wrapper for Win32 SetThreadExecutionState API.
    /// Uses CsWin32-generated type-safe bindings from Windows SDK metadata.
    /// Zero CPU overhead: fire-and-forget API calls, no timers or polling.
    /// </summary>
    internal static class ExecutionState
    {
        /// <summary>
        /// Arms keep-awake: prevents system sleep and hibernation.
        /// Uses ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_AWAYMODE_REQUIRED.
        /// Call this once when app starts or when user activates keep-awake.
        /// </summary>
        /// <returns>True if successful, false if SetThreadExecutionState failed.</returns>
        public static bool ArmKeepAwake()
        {
            Trace.WriteLine("Smart_Stay_Awake_2: PowerManagement.ExecutionState: Entered ArmKeepAwake ...");

            // Use formal EXECUTION_STATE enum from Windows.Win32.System.Power namespace
            // (CsWin32-generated from Windows SDK metadata)
            EXECUTION_STATE flags = EXECUTION_STATE.ES_CONTINUOUS
                                  | EXECUTION_STATE.ES_SYSTEM_REQUIRED
                                  | EXECUTION_STATE.ES_AWAYMODE_REQUIRED;

            Trace.WriteLine($"Smart_Stay_Awake_2: PowerManagement.ExecutionState: ArmKeepAwake: Calling SetThreadExecutionState with flags=0x{(uint)flags:X8}");
            Trace.WriteLine($"Smart_Stay_Awake_2: PowerManagement.ExecutionState: ArmKeepAwake: Flags breakdown: ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_AWAYMODE_REQUIRED");

            // Call Win32 API via CsWin32-generated PInvoke wrapper
            EXECUTION_STATE result = PInvoke.SetThreadExecutionState(flags);

            // Per Win32 docs: returns 0 on failure, previous state on success
            bool success = result != 0;

            if (success)
            {
                Trace.WriteLine($"Smart_Stay_Awake_2: PowerManagement.ExecutionState: ArmKeepAwake: SUCCESS - Previous state was 0x{(uint)result:X8}");
                Trace.WriteLine("Smart_Stay_Awake_2: PowerManagement.ExecutionState: ArmKeepAwake: System sleep/hibernation now BLOCKED");
            }
            else
            {
                Trace.WriteLine("Smart_Stay_Awake_2: PowerManagement.ExecutionState: ArmKeepAwake: FAILED - SetThreadExecutionState returned 0");
                Trace.WriteLine("Smart_Stay_Awake_2: PowerManagement.ExecutionState: ArmKeepAwake: System sleep/hibernation NOT blocked (API call failed)");
            }

            Trace.WriteLine($"Smart_Stay_Awake_2: PowerManagement.ExecutionState: Exiting ArmKeepAwake (success={success})");
            return success;
        }

        /// <summary>
        /// Disarms keep-awake: allows system sleep and hibernation again.
        /// Uses ES_CONTINUOUS alone to clear previous flags.
        /// Call this when app quits or when user deactivates keep-awake.
        /// </summary>
        /// <returns>True if successful, false if SetThreadExecutionState failed.</returns>
        public static bool DisarmKeepAwake()
        {
            Trace.WriteLine("Smart_Stay_Awake_2: PowerManagement.ExecutionState: Entered DisarmKeepAwake ...");

            // Clear previous flags by setting ES_CONTINUOUS alone
            // (Per Win32 docs: this resets to normal power management behavior)
            EXECUTION_STATE flags = EXECUTION_STATE.ES_CONTINUOUS;

            Trace.WriteLine($"Smart_Stay_Awake_2: PowerManagement.ExecutionState: DisarmKeepAwake: Calling SetThreadExecutionState with flags=0x{(uint)flags:X8}");
            Trace.WriteLine($"Smart_Stay_Awake_2: PowerManagement.ExecutionState: DisarmKeepAwake: Flags breakdown: ES_CONTINUOUS (clears previous flags)");

            // Call Win32 API via CsWin32-generated PInvoke wrapper
            EXECUTION_STATE result = PInvoke.SetThreadExecutionState(flags);

            // Per Win32 docs: returns 0 on failure, previous state on success
            bool success = result != 0;

            if (success)
            {
                Trace.WriteLine($"Smart_Stay_Awake_2: PowerManagement.ExecutionState: DisarmKeepAwake: SUCCESS - Previous state was 0x{(uint)result:X8}");
                Trace.WriteLine("Smart_Stay_Awake_2: PowerManagement.ExecutionState: DisarmKeepAwake: System sleep/hibernation now ALLOWED");
            }
            else
            {
                Trace.WriteLine("Smart_Stay_Awake_2: PowerManagement.ExecutionState: DisarmKeepAwake: FAILED - SetThreadExecutionState returned 0");
                Trace.WriteLine("Smart_Stay_Awake_2: PowerManagement.ExecutionState: DisarmKeepAwake: System sleep/hibernation state UNCHANGED (API call failed)");
            }

            Trace.WriteLine($"Smart_Stay_Awake_2: PowerManagement.ExecutionState: Exiting DisarmKeepAwake (success={success})");
            return success;
        }
    }
}
