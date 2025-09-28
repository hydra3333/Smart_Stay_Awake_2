// File: src/Stay_Awake_2/UI/TrayManager.cs
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;            // <-- needed for MemoryStream
using System.Windows.Forms;

namespace Stay_Awake_2.UI
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

        public TrayManager(AppState state, Form ownerForm)
        {
            Trace.WriteLine("Stay_Awake_2: TrayManager: Entered ctor ...");
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _ownerForm = ownerForm ?? throw new ArgumentNullException(nameof(ownerForm));
            Trace.WriteLine("TrayManager: Exiting ctor (success).");
        }

        /// <summary>
        /// Build tray icon + menu (no-op if called twice).
        /// </summary>
        public void Initialize()
        {
            Trace.WriteLine("Stay_Awake_2: TrayManager: Initialize: Entered ...");
            if (_disposed) throw new ObjectDisposedException(nameof(TrayManager));
            if (_initialized)
            {
                Trace.WriteLine("Stay_Awake_2: TrayManager: Initialize: Already initialized; exiting.");
                return;
            }

            // Minimal placeholder context menu (items will be wired later).
            _menu = new ContextMenuStrip();
            _menu.Items.Add("Show Window", null, (s, e) => Trace.WriteLine("Stay_Awake_2: TrayManager: Show Window clicked (stub)."));
            _menu.Items.Add("Minimize to Tray", null, (s, e) => Trace.WriteLine("Stay_Awake_2: TrayManager: Minimize clicked (stub)."));
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add("Quit", null, (s, e) => Trace.WriteLine("Stay_Awake_2: TrayManager: Quit clicked (stub)."));

            _tray = new NotifyIcon
            {
                Visible = false, // we’ll call Show() when ready
                ContextMenuStrip = _menu,
                Text = _state.AppDisplayName,
                // Icon: we’ll assign a real Icon later from imaging pipeline.
                // For now, use the app’s default icon if available.
                Icon = _ownerForm.Icon ?? SystemIcons.Application
            };

            _initialized = true;
            Trace.WriteLine("Stay_Awake_2: TrayManager: Initialize: Exiting (success).");
        }

        /// <summary>
        /// Assign a new tray icon built from a multi-image ICO stream.
        /// Ownership: TrayManager disposes <paramref name="icon"/> and <paramref name="backingStream"/> in Dispose().
        /// </summary>
        public void SetIcon(Icon icon, MemoryStream backingStream)
        {
            Trace.WriteLine("Stay_Awake_2: TrayManager: SetIcon ...");
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
        /// Make the tray icon visible.
        /// </summary>
        public void Show()
        {
            Trace.WriteLine("Stay_Awake_2: TrayManager: Show: Entered ...");
            if (_disposed) throw new ObjectDisposedException(nameof(TrayManager));
            if (!_initialized) Initialize();
            if (_tray != null) _tray.Visible = true;
            Trace.WriteLine("Stay_Awake_2: TrayManager: Show: Exiting.");
        }

        /// <summary>
        /// Hide the tray icon.
        /// </summary>
        public void Hide()
        {
            Trace.WriteLine("Stay_Awake_2: TrayManager: Hide: Entered ...");
            if (_tray != null) _tray.Visible = false;
            Trace.WriteLine("Stay_Awake_2: TrayManager: Hide: Exiting.");
        }

        public void Dispose()
        {
            if (_disposed) return;
            Trace.WriteLine("Stay_Awake_2: TrayManager: Dispose: Entered ...");

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
                Trace.WriteLine("Stay_Awake_2: TrayManager: Dispose: Caught exception: " + ex);
            }
            finally
            {
                _disposed = true;
                Trace.WriteLine("Stay_Awake_2: TrayManager: Dispose: Exiting.");
            }
        }
    }
}
