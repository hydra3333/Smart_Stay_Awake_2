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
using System.IO;
using Stay_Awake_2.Imaging;


namespace Stay_Awake_2.UI
{
    public partial class MainForm : Form
    {
        private readonly AppState _state;
        private TrayManager? _tray;
        private PictureBox? _picture;

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

            // Build a simple PictureBox to show the image
            _picture = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black
            };
            this.Controls.Add(_picture);

            // Run the full imaging pipeline now
            TryLoadPrepareAndApplyImageAndIcon();

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

        private void TryLoadPrepareAndApplyImageAndIcon()
        {
            Trace.WriteLine("UI.MainForm: Entered TryLoadPrepareAndApplyImageAndIcon ...");

            Bitmap? src = null;
            Bitmap? squared = null;
            Bitmap? display = null;
            Icon? multiIcon = null;
            MemoryStream? icoStream = null;
            try
            {
                // --------- Source priority -----------------------------------------
                // 1) Embedded base64
                if (Base64ImageLoader.HasEmbeddedImage())
                {
                    Trace.WriteLine("UI.MainForm: Using embedded base64 image (priority #1).");
                    src = Base64ImageLoader.LoadEmbeddedBitmap();
                }
                // 2) CLI --icon PATH
                else if (!string.IsNullOrWhiteSpace(_state.Options?.IconPath))
                {
                    Trace.WriteLine("UI.MainForm: Using CLI --icon file (priority #2).");
                    src = ImageLoader.LoadBitmapFromPath(_state.Options.IconPath);
                }
                // 3) Assets next to EXE
                else
                {
                    Trace.WriteLine("UI.MainForm: Looking for Assets-based fallback (priority #3).");
                    string exeDir = AppContext.BaseDirectory;
                    string[] candidates =
                    {
                Path.Combine(exeDir, "Assets", "Stay_Awake_icon.png"),
                Path.Combine(exeDir, "Assets", "Stay_Awake_icon.jpg"),
                Path.Combine(exeDir, "Assets", "Stay_Awake_icon.jpeg"),
                Path.Combine(exeDir, "Assets", "Stay_Awake_icon.bmp"),
                Path.Combine(exeDir, "Assets", "Stay_Awake_icon.gif"),
                Path.Combine(exeDir, "Assets", "Stay_Awake_icon.ico"),
            };
                    foreach (var c in candidates)
                    {
                        if (File.Exists(c))
                        {
                            Trace.WriteLine("UI.MainForm: Found Assets fallback: " + c);
                            src = ImageLoader.LoadBitmapFromPath(c);
                            break;
                        }
                    }

                    // 4) Synthetic checkerboard
                    if (src == null)
                    {
                        Trace.WriteLine("UI.MainForm: No file/embedded image found; using synthetic checkerboard (priority #4).");
                        src = ImageLoader.CreateFallbackBitmap(160);
                    }
                }
                Trace.WriteLine($"UI.MainForm: Source image size = {src.Width}x{src.Height}");

                // --------- Squaring (edge replication) -----------------------------
                squared = ImageSquareReplicator.MakeSquareByEdgeReplication(src);
                Trace.WriteLine($"UI.MainForm: Squared size = {squared.Width}x{squared.Height}");

                // --------- Window display (max edge) -------------------------------
                display = ImageSquareReplicator.ResizeSquare(squared, AppConfig.WINDOW_MAX_IMAGE_EDGE_PX);
                if (_picture != null)
                {
                    // Assign a *copy* so we can dispose 'display'
                    _picture.Image = new Bitmap(display);
                    Trace.WriteLine("UI.MainForm: PictureBox image assigned.");
                }

                // --------- Multi-size ICON (16..256, all-PNG) ----------------------
                var (icon, stream) = IcoBuilder.BuildMultiSizePngIco(squared, AppConfig.TRAY_ICON_SIZES);
                multiIcon = icon;
                icoStream = stream;

                // Apply to Form (title bar / taskbar)
                this.Icon = multiIcon;

                // Apply to Tray
                _tray?.SetIcon(multiIcon, icoStream);

                Trace.WriteLine("UI.MainForm: Exiting TryLoadPrepareAndApplyImageAndIcon (success).");
                // NOTE: Do NOT dispose multiIcon or icoStream here; TrayManager holds refs.
                // The Form will also use this.Icon. Dispose them on FormClosed via TrayManager.
            }
            catch (Exception ex)
            {
                Trace.WriteLine("UI.MainForm: TryLoadPrepareAndApplyImageAndIcon ERROR: " + ex);
                MessageBox.Show("Failed to load/prepare image/icon.\n" + ex.Message,
                    _state.AppDisplayName + " — Image Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                // Clean up partially created icon/stream if tray wasn’t set
                try
                {
                    if (multiIcon != null && (_tray == null))
                        multiIcon.Dispose();
                }
                catch { /* ignore */ }
                try
                {
                    if (icoStream != null && (_tray == null))
                        icoStream.Dispose();
                }
                catch { /* ignore */ }
            }
            finally
            {
                // We copied 'display' into PictureBox, safe to dispose local
                try { display?.Dispose(); } catch { }
                // 'squared' and 'src' are no longer needed
                try { squared?.Dispose(); } catch { }
                try { src?.Dispose(); } catch { }
            }
        }
    }
}
