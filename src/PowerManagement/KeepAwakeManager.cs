// File: src/Smart_Stay_Awake_2/PowerManagement/KeepAwakeManager.cs
// Purpose: High-level keep-awake manager with state tracking.
//          Provides clean interface for arming/disarming keep-awake from anywhere in the app.
//          Wraps low-level ExecutionState.cs with business logic and idempotency.

using System;
using System.Diagnostics;

namespace Smart_Stay_Awake_2.PowerManagement
{
    /// <summary>
    /// High-level manager for keep-awake functionality.
    /// Tracks armed/disarmed state and provides idempotent Arm/Disarm methods.
    /// Thread-safe (uses lock), though threading is unnecessary for this tiny app.
    /// Singleton pattern: static class with global state.
    /// </summary>
    internal static class KeepAwakeManager
    {
        // State tracking
        private static bool _isArmed = false;
        private static readonly object _lock = new object();

        /// <summary>
        /// Gets whether keep-awake is currently armed.
        /// Thread-safe property (lock not needed for bool read, but included for consistency).
        /// </summary>
        public static bool IsArmed
        {
            get
            {
                lock (_lock)
                {
                    return _isArmed;
                }
            }
        }

        /// <summary>
        /// Arms keep-awake: prevents system sleep and hibernation.
        /// Idempotent: safe to call multiple times (no-op if already armed).
        /// Logs state changes verbosely.
        /// </summary>
        /// <returns>True if armed successfully (or already armed), false if Win32 call failed.</returns>
        public static bool Arm()
        {
            Trace.WriteLine("Smart_Stay_Awake_2: PowerManagement.KeepAwakeManager: Entered Arm ...");

            lock (_lock)
            {
                // Idempotency check: already armed?
                if (_isArmed)
                {
                    Trace.WriteLine("Smart_Stay_Awake_2: PowerManagement.KeepAwakeManager: Arm: Already armed (no-op)");
                    Trace.WriteLine("Smart_Stay_Awake_2: PowerManagement.KeepAwakeManager: Exiting Arm (already armed, returning true)");
                    return true;
                }

                Trace.WriteLine("Smart_Stay_Awake_2: PowerManagement.KeepAwakeManager: Arm: Not armed yet, calling ExecutionState.ArmKeepAwake ...");

                // Call low-level Win32 wrapper
                bool success = ExecutionState.ArmKeepAwake();

                if (success)
                {
                    _isArmed = true;
                    Trace.WriteLine("Smart_Stay_Awake_2: PowerManagement.KeepAwakeManager: Arm: SUCCESS - Keep-awake is now ARMED");
                    Trace.WriteLine("Smart_Stay_Awake_2: PowerManagement.KeepAwakeManager: Arm: State changed: _isArmed=false -> true");
                }
                else
                {
                    Trace.WriteLine("Smart_Stay_Awake_2: PowerManagement.KeepAwakeManager: Arm: FAILED - ExecutionState.ArmKeepAwake returned false");
                    Trace.WriteLine("Smart_Stay_Awake_2: PowerManagement.KeepAwakeManager: Arm: State unchanged: _isArmed=false");
                }

                Trace.WriteLine($"Smart_Stay_Awake_2: PowerManagement.KeepAwakeManager: Exiting Arm (success={success})");
                return success;
            }
        }

        /// <summary>
        /// Disarms keep-awake: allows system sleep and hibernation again.
        /// Idempotent: safe to call multiple times (no-op if already disarmed).
        /// Logs state changes verbosely.
        /// Always call this on app quit to restore normal power management.
        /// </summary>
        /// <returns>True if disarmed successfully (or already disarmed), false if Win32 call failed.</returns>
        public static bool Disarm()
        {
            Trace.WriteLine("Smart_Stay_Awake_2: PowerManagement.KeepAwakeManager: Entered Disarm ...");

            lock (_lock)
            {
                // Idempotency check: already disarmed?
                if (!_isArmed)
                {
                    Trace.WriteLine("Smart_Stay_Awake_2: PowerManagement.KeepAwakeManager: Disarm: Already disarmed (no-op)");
                    Trace.WriteLine("Smart_Stay_Awake_2: PowerManagement.KeepAwakeManager: Exiting Disarm (already disarmed, returning true)");
                    return true;
                }

                Trace.WriteLine("Smart_Stay_Awake_2: PowerManagement.KeepAwakeManager: Disarm: Currently armed, calling ExecutionState.DisarmKeepAwake ...");

                // Call low-level Win32 wrapper
                bool success = ExecutionState.DisarmKeepAwake();

                if (success)
                {
                    _isArmed = false;
                    Trace.WriteLine("Smart_Stay_Awake_2: PowerManagement.KeepAwakeManager: Disarm: SUCCESS - Keep-awake is now DISARMED");
                    Trace.WriteLine("Smart_Stay_Awake_2: PowerManagement.KeepAwakeManager: Disarm: State changed: _isArmed=true -> false");
                }
                else
                {
                    Trace.WriteLine("Smart_Stay_Awake_2: PowerManagement.KeepAwakeManager: Disarm: FAILED - ExecutionState.DisarmKeepAwake returned false");
                    Trace.WriteLine("Smart_Stay_Awake_2: PowerManagement.KeepAwakeManager: Disarm: State unchanged: _isArmed=true (CRITICAL: keep-awake still active!)");
                }

                Trace.WriteLine($"Smart_Stay_Awake_2: PowerManagement.KeepAwakeManager: Exiting Disarm (success={success})");
                return success;
            }
        }

        /// <summary>
        /// Gets a human-readable status string for display/logging.
        /// Example: "Armed" or "Disarmed"
        /// </summary>
        /// <returns>Status string describing current state.</returns>
        public static string GetStatusString()
        {
            lock (_lock)
            {
                return _isArmed ? "Armed" : "Disarmed";
            }
        }
    }
}