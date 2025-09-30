// File: src/Smart_Stay_Awake_2/UI/TrayManager.cs
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;            // <-- needed for MemoryStream
using System.Windows.Forms;

namespace Smart_Stay_Awake_2.UI
{
    internal sealed class TrayManager : IDisposable
    {
        private readonly AppState _state;
        private readonly Form _ownerForm;

        private NotifyIcon? _tray;
        private ContextMenuStrip? _menu;
        private bool _initialized;

        // Owned icon resources: we dispose them in Dispose()
        private Icon? _icon;
        private MemoryStream? _iconStream;

        private bool _disposed; // simple guard

        // ================================================
        // Events: allow MainForm to own restore/quit behavior via unified handlers
        // ================================================
        /// <summary>
        /// Raised when user requests to show/restore the main window.
        /// Triggered by: tray left-click, or tray menu "Show Window".
        /// </summary>
        public event EventHandler? ShowRequested;

        /// <summary>
        /// Raised when user requests to quit the application.
        /// Triggered by: tray menu "Quit".
        /// </summary>
        public event EventHandler? QuitRequested;

        public TrayManager(AppState state, Form ownerForm)
        {
            Trace.WriteLine("Smart_Stay_Awake_2: TrayManager: Entered ctor ...");
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _ownerForm = ownerForm ?? throw new ArgumentNullException(nameof(ownerForm));
            Trace.WriteLine("TrayManager: Exiting ctor (success).");
        }

        /// <summary>
        /// Build tray icon + menu (no-op if called twice).
        /// </summary>
        public void Initialize()
        {
            Trace.WriteLine("Smart_Stay_Awake_2: TrayManager: Initialize: Entered ...");
            if (_disposed) throw new ObjectDisposedException(nameof(TrayManager));
            if (_initialized)
            {
                Trace.WriteLine("Smart_Stay_Awake_2: TrayManager: Initialize: Already initialized; exiting.");
                return;
            }

            // Minimal placeholder context menu (items will be wired later).
            // === replaced by the code below ===
            //_menu = new ContextMenuStrip();
            //_menu.Items.Add("Show Window", null, (s, e) => Trace.WriteLine("Smart_Stay_Awake_2: TrayManager: Show Window clicked (stub)."));
            //_menu.Items.Add("Minimize to Tray", null, (s, e) => Trace.WriteLine("Smart_Stay_Awake_2: TrayManager: Minimize clicked (stub)."));
            //_menu.Items.Add(new ToolStripSeparator());
            //_menu.Items.Add("Quit", null, (s, e) => Trace.WriteLine("Smart_Stay_Awake_2: TrayManager: Quit clicked (stub)."));
            // === start of new iteration 2 code below ===
            // Context menu: Show Window, Quit (simplified per Iteration 2)
            _menu = new ContextMenuStrip();
            var miShow = new ToolStripMenuItem("Show Window");
            miShow.Click += (s, e) =>
            {
                Trace.WriteLine("Smart_Stay_Awake_2: TrayManager: Context menu 'Show Window' clicked => raising ShowRequested event");
                ShowRequested?.Invoke(this, EventArgs.Empty);
            };
            _menu.Items.Add(miShow);
            var miQuit = new ToolStripMenuItem("Quit");
            miQuit.Click += (s, e) =>
            {
                Trace.WriteLine("Smart_Stay_Awake_2: TrayManager: Context menu 'Quit' clicked => raising QuitRequested event");
                QuitRequested?.Invoke(this, EventArgs.Empty);
            };
            _menu.Items.Add(miQuit);
            Trace.WriteLine("Smart_Stay_Awake_2: TrayManager: Context menu built (2 items: Show Window, Quit)");
            // === end of new iteration 2 code above ===

            _tray = new NotifyIcon
            {
                Visible = false, // we’ll call Show() when ready
                ContextMenuStrip = _menu,
                Text = _state.AppDisplayName,
                // Icon: we’ll assign a real Icon later from imaging pipeline.
                // For now, use the app’s default icon if available.
                Icon = _ownerForm.Icon ?? SystemIcons.Application
            };
            // === start of new iteration 2 code below ===
            // Left-click on tray icon => restore main window
            // Right-click shows context menu (standard NotifyIcon behavior)
            _tray.MouseClick += OnTrayIconMouseClick;
            Trace.WriteLine("Smart_Stay_Awake_2: TrayManager: Left-click handler registered");
            // === end of new iteration 2 code above ===

            _initialized = true;
            Trace.WriteLine("Smart_Stay_Awake_2: TrayManager: Initialize: Exiting (success).");
        }

        /// <summary>
        /// Assign a new tray icon built from a multi-image ICO stream.
        /// Ownership: TrayManager disposes <paramref name="icon"/> and <paramref name="backingStream"/> in Dispose().
        /// </summary>
        public void SetIcon(Icon icon, MemoryStream backingStream)
        {
            Trace.WriteLine("Smart_Stay_Awake_2: TrayManager: SetIcon ...");
            if (_disposed) throw new ObjectDisposedException(nameof(TrayManager));
            if (!_initialized) Initialize();

            // Detach & dispose previous icon/stream first
            try { if (_tray != null) _tray.Icon = null; } catch { /* ignore */ }
            try { _icon?.Dispose(); } catch { /* ignore */ }
            try { _iconStream?.Dispose(); } catch { /* ignore */ }

            _icon = icon ?? throw new ArgumentNullException(nameof(icon));
            _iconStream = backingStream ?? throw new ArgumentNullException(nameof(backingStream));

            if (_tray != null) _tray.Icon = _icon;
        }

        /// <summary>
        /// Handles mouse clicks on the tray icon.
        /// Left-click: restore main window (raises ShowRequested event).
        /// Right-click: shows context menu automatically (NotifyIcon default behavior).
        /// </summary>
        private void OnTrayIconMouseClick(object? sender, MouseEventArgs e)
        {
            Trace.WriteLine("Smart_Stay_Awake_2: TrayManager: OnTrayIconMouseClick: Entered ...");
            try
            {
                if (e.Button == MouseButtons.Left)
                {
                    Trace.WriteLine("Smart_Stay_Awake_2: TrayManager: Tray icon left-clicked => raising ShowRequested event");
                    ShowRequested?.Invoke(this, EventArgs.Empty);
                }
                // Right-click is handled automatically by NotifyIcon (shows ContextMenuStrip)
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Smart_Stay_Awake_2: TrayManager: OnTrayIconMouseClick FAILED: {ex.GetType().Name}");
                Trace.WriteLine($"Smart_Stay_Awake_2: TrayManager: OnTrayIconMouseClick error message: {ex.Message}");
                Trace.WriteLine($"Smart_Stay_Awake_2: TrayManager: OnTrayIconMouseClick stack trace: {ex.StackTrace}");
            }
            finally
            {
                Trace.WriteLine("Smart_Stay_Awake_2: TrayManager: OnTrayIconMouseClick: Exiting.");
            }
        }

        /// <summary>
        /// Make the tray icon visible.
        /// </summary>
        public void Show()
        {
            Trace.WriteLine("Smart_Stay_Awake_2: TrayManager: Show: Entered ...");
            if (_disposed) throw new ObjectDisposedException(nameof(TrayManager));
            if (!_initialized) Initialize();
            if (_tray != null) _tray.Visible = true;
            Trace.WriteLine("Smart_Stay_Awake_2: TrayManager: Show: Exiting.");
        }

        /// <summary>
        /// Hide the tray icon.
        /// </summary>
        public void Hide()
        {
            Trace.WriteLine("Smart_Stay_Awake_2: TrayManager: Hide: Entered ...");
            if (_tray != null) _tray.Visible = false;
            Trace.WriteLine("Smart_Stay_Awake_2: TrayManager: Hide: Exiting.");
        }

        public void Dispose()
        {
            if (_disposed) return;
            Trace.WriteLine("Smart_Stay_Awake_2: TrayManager: Dispose: Entered ...");

            // Unsubscribe event handler to prevent leaks
            if (_tray != null)
            {
                _tray.MouseClick -= OnTrayIconMouseClick;
                Trace.WriteLine("Smart_Stay_Awake_2: TrayManager: MouseClick event handler unsubscribed");
            }

            try
            {
                // Always hide first to avoid “ghost” tray icons.
                try { if (_tray != null) _tray.Visible = false; } catch { /* ignore */ }

                // Dispose managed UI objects
                try { _tray?.Dispose(); } catch { /* ignore */ } finally { _tray = null; }
                try { _menu?.Dispose(); } catch { /* ignore */ } finally { _menu = null; }

                // Dispose our icon resources
                try { _icon?.Dispose(); } catch { /* ignore */ } finally { _icon = null; }
                try { _iconStream?.Dispose(); } catch { /* ignore */ } finally { _iconStream = null; }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Smart_Stay_Awake_2: TrayManager: Dispose: Caught exception: " + ex);
            }
            finally
            {
                _disposed = true;
                Trace.WriteLine("Smart_Stay_Awake_2: TrayManager: Dispose: Exiting.");
            }
        }
    }
}
