using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Stay_Awake_2.UI
{
    public partial class MainForm : Form
    {
        private readonly AppState _state;
        private TrayManager? _tray;
        // public MainForm(AppState state)
        internal MainForm(AppState state)
        {
            Trace.WriteLine("Stay_Awake_2: UI.MainForm: Entered MainForm ctor ...");
            _state = state ?? throw new ArgumentNullException(nameof(state));

            InitializeComponent();

            // DPI is already set by the Designer; rest of our window policy:
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            // Optional niceties:
            this.StartPosition = FormStartPosition.CenterScreen;
            // this.KeyPreview = true; // if you’ll handle ESC / shortcuts globally

            // Example: use state to set title / initial labels
            this.Text = $"{_state.AppDisplayName} — v{_state.AppVersion}";

            // Wire a form event to do cleanup (since Designer owns Dispose(bool))
            this.FormClosed += MainForm_FormClosed;

            // Create and initialize the tray manager (still a stub; no show/minimize yet)
            _tray = new TrayManager(_state, this);
            _tray.Initialize();
            // _tray.Show();  // call later when you implement minimize-to-tray

            Trace.WriteLine($"UI.MainForm: Using Mode={_state.Mode}, TraceEnabled={_state.TraceEnabled}");
            Trace.WriteLine("Stay_Awake_2: UI.MainForm: Exiting MainForm ctor ...");
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            Trace.WriteLine("Stay_Awake_2: UI.MainForm_Load: Entered MainForm_Load ...");
            Trace.WriteLine("Stay_Awake_2: UI.MainForm_Load: Exiting MainForm_Load ...");
        }

        // Dispose tray in FormClosed (no duplicate Dispose override).
        private void MainForm_FormClosed(object? sender, FormClosedEventArgs e)
        {
            Trace.WriteLine("Stay_Awake_2: UI.MainForm: Entered MainForm_FormClosed ...");
            try { _tray?.Dispose(); _tray = null; }
            catch (Exception ex) { Trace.WriteLine("UI.MainForm: Tray dispose error: " + ex); }
            Trace.WriteLine("Stay_Awake_2: UI.MainForm: Exiting MainForm_FormClosed ...");
        }

        // Optional alternative: override OnFormClosed instead of subscribing.
        // protected override void OnFormClosed(FormClosedEventArgs e)
        // {
        //     Trace.WriteLine("Stay_Awake_2: UI.MainForm: OnFormClosed ...");
        //     try { _tray?.Dispose(); _tray = null; } catch { }
        //     base.OnFormClosed(e);
        // }
    }
}
