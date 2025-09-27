// File: src/Stay_Awake_2/UI/TrayManager.cs
// Purpose: Minimal system-tray coordinator. No real logic yet—just structure & trace.
// Later this will own the NotifyIcon, ContextMenuStrip, and handlers.

using System;
using System.Diagnostics;
using System.Drawing;
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
                Visible = false,                 // we’ll call Show() when ready
                ContextMenuStrip = _menu,
                Text = _state.AppDisplayName,    // tooltip text (keep short)
                // Icon: we’ll assign a real Icon later from imaging pipeline.
                // For now, use the app’s default icon if available.
                Icon = _ownerForm.Icon ?? SystemIcons.Application
            };

            _initialized = true;
            Trace.WriteLine("Stay_Awake_2: TrayManager: Initialize: Exiting (success).");
        }

        /// <summary>
        /// Make the tray icon visible.
        /// </summary>
        public void Show()
        {
            Trace.WriteLine("Stay_Awake_2: TrayManager: Show: Entered ...");
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
            Trace.WriteLine("Stay_Awake_2: TrayManager: Dispose: Entered ...");
            try
            {
                if (_tray != null)
                {
                    _tray.Visible = false;
                    _tray.Dispose();
                    _tray = null;
                }
                if (_menu != null)
                {
                    _menu.Dispose();
                    _menu = null;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Stay_Awake_2: TrayManager: Dispose: Caught exception: " + ex);
            }
            finally
            {
                Trace.WriteLine("Stay_Awake_2: TrayManager: Dispose: Exiting.");
            }
        }
    }
}
