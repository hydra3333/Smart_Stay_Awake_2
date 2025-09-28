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
    // Accessibility matches internal AppState to avoid CS0051.
    internal partial class MainForm : Form
    {
        private readonly AppState _state;

        // System tray owner (already a stub with Initialize/SetIcon/Show/Hide).
        private TrayManager? _tray;

        // Simple image host – fills the client area. We assign a cloned Bitmap to it.
        private PictureBox? _picture;

        // Constructor is lightweight: build controls, wire events, set fixed window policy.
        internal MainForm(AppState state)
        {
            Trace.WriteLine("Stay_Awake_2: UI.MainForm: Entered MainForm ctor ...");
            _state = state ?? throw new ArgumentNullException(nameof(state));

            InitializeComponent(); // designer baseline: AutoScaleMode=Dpi, etc.

            // Window policy (fixed dialog-like frame; no maximize)
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.StartPosition = FormStartPosition.CenterScreen;

            // Title uses AppState
            this.Text = $"{_state.AppDisplayName} — v{_state.AppVersion}";

            // Cleanup hook (tray disposal, etc.)
            this.FormClosed += MainForm_FormClosed;

            // Build tray and a basic context menu (Show/Minimize/Quit will be wired later)
            _tray = new TrayManager(_state, this);
            _tray.Initialize();

            // Add an image host (fills the client area)
            _picture = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black
            };
            this.Controls.Add(_picture);

            Trace.WriteLine($"UI.MainForm: Using TraceEnabled={_state.TraceEnabled}");
            Trace.WriteLine("Stay_Awake_2: UI.MainForm: Exiting MainForm ctor.");
        }

        /// <summary>
        /// Prefer doing the image pipeline here (once) instead of the ctor.
        /// At this point the form handle exists; scaling/DPIs are fully resolved.
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Trace.WriteLine("UI.MainForm: OnShown: Entered.");
            TryLoadPrepareAndApplyImageAndIcon();
            Trace.WriteLine("UI.MainForm: OnShown: Exiting.");
        }

        // Dispose tray in FormClosed (no duplicate Dispose override).
        private void MainForm_FormClosed(object? sender, FormClosedEventArgs e)
        {
            Trace.WriteLine("Stay_Awake_2: UI.MainForm: Entered MainForm_FormClosed ...");
            try { _tray?.Dispose(); _tray = null; }
            catch (Exception ex) { Trace.WriteLine("UI.MainForm: Tray dispose error: " + ex); }
            Trace.WriteLine("Stay_Awake_2: UI.MainForm: Exiting MainForm_FormClosed ...");
        }

        /// <summary>
        /// Full imaging pipeline with very verbose tracing:
        /// 1) Source selection (priority): CLI --icon → embedded base64 → EXE neighbor → checkerboard fallback
        /// 2) Square by edge replication (no solid bars), then ensure even pixels (replicate right/bottom if odd)
        /// 3) Window bitmap: ≤512 stays as-is; >512 downscales to 512 (high quality)
        /// 4) Multi-size PNG ICO (16..256): apply to Form and Tray
        /// 5) Resize window client area to match final display bitmap (leaving future room optional)
        /// </summary>
        private void TryLoadPrepareAndApplyImageAndIcon()
        {
            Trace.WriteLine("UI.MainForm: Entered TryLoadPrepareAndApplyImageAndIcon ...");

            Bitmap? src = null;
            Bitmap? squared = null;
            Bitmap? evenSquare = null;
            Bitmap? display = null;
            Icon? multiIcon = null;
            MemoryStream? icoStream = null;

            try
            {
                // --------- 1) Source selection (spec v11 order) --------------------
                // Priority: CLI --icon → embedded base64 → Stay_Awake_icon.* next to EXE → checkerboard
                Trace.WriteLine("UI.MainForm: Source selection start.");

                // (1) CLI --icon PATH
                if (!string.IsNullOrWhiteSpace(_state.Options?.IconPath))
                {
                    Trace.WriteLine($"UI.MainForm: Trying CLI --icon file: {_state.Options.IconPath}");
                    src = ImageLoader.LoadBitmapFromPath(_state.Options.IconPath);
                }
                // (2) Embedded base64
                else if (Base64ImageLoader.HasEmbeddedImage())
                {
                    Trace.WriteLine("UI.MainForm: Using embedded base64 image.");
                    src = Base64ImageLoader.LoadEmbeddedBitmap();
                }
                // (3) File named Stay_Awake_icon.* next to EXE (Assets folder)
                else
                {
                    string exeDir = AppContext.BaseDirectory;
                    Trace.WriteLine("UI.MainForm: Searching EXE neighbor 'Assets/Stay_Awake_icon.*' files.");
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
                            Trace.WriteLine("UI.MainForm: Found EXE-neighbor asset: " + c);
                            src = ImageLoader.LoadBitmapFromPath(c);
                            break;
                        }
                    }

                    // (4) Synthetic checkerboard if nothing else
                    if (src == null)
                    {
                        Trace.WriteLine("UI.MainForm: No CLI/embedded/asset image found; using synthetic checkerboard fallback.");
                        src = ImageLoader.CreateFallbackBitmap(160);
                    }
                }

                Trace.WriteLine($"UI.MainForm: Source image: {src.Width}x{src.Height}");

                // --------- 2) Square (edge replication) + enforce even pixels -------
                squared = ImageSquareReplicator.MakeSquareByEdgeReplication(src);
                Trace.WriteLine($"UI.MainForm: After square (replication): {squared.Width}x{squared.Height}");

                // If odd, replicate one right column and one bottom row to make it even-sized
                if ((squared.Width % 2) != 0 || (squared.Height % 2) != 0)
                {
                    Trace.WriteLine("UI.MainForm: Even-enforcement needed (odd dimension). Replicating right & bottom edges by +1px.");
                    evenSquare = AddRightAndBottomReplication(squared);
                    Trace.WriteLine($"UI.MainForm: After even-enforcement: {evenSquare.Width}x{evenSquare.Height}");
                }
                else
                {
                    evenSquare = new Bitmap(squared);
                    Trace.WriteLine("UI.MainForm: Already even-sized; cloned squared image.");
                }

                // --------- 3) Window image (≤512 keep, >512 downscale to 512) ------
                const int MAX_EDGE = 512; // spec knob (could be AppConfig.WINDOW_MAX_IMAGE_EDGE_PX)
                if (evenSquare.Width > MAX_EDGE) // (square, so width==height)
                {
                    Trace.WriteLine($"UI.MainForm: Window image too large ({evenSquare.Width}); downscaling to {MAX_EDGE}.");
                    display = ImageSquareReplicator.ResizeSquare(evenSquare, MAX_EDGE);
                }
                else
                {
                    display = new Bitmap(evenSquare);
                    Trace.WriteLine($"UI.MainForm: Window image kept as-is: {display.Width}x{display.Height} (<= {MAX_EDGE}).");
                }

                // Assign to PictureBox (clone again to detach from our disposal scope)
                if (_picture != null)
                {
                    _picture.Image = new Bitmap(display);
                    Trace.WriteLine("UI.MainForm: PictureBox image assigned.");
                }

                // Resize form client area to exactly fit the display image (for now).
                // Later we'll add reserved height for labels/buttons.
                ResizeClientToBitmap(display);

                // --------- 4) Multi-size ICO (16..256), apply to form & tray -------
                var (icon, stream) = IcoBuilder.BuildMultiSizePngIco(evenSquare, AppConfig.TRAY_ICON_SIZES);
                multiIcon = icon;
                icoStream = stream;

                // Apply to Form (title bar / taskbar)
                this.Icon = multiIcon;

                // Apply to Tray
                _tray?.SetIcon(multiIcon, icoStream);
                // Optional: _tray?.Show();

                Trace.WriteLine("UI.MainForm: Exiting TryLoadPrepareAndApplyImageAndIcon (success).");

                // NOTE: Do NOT dispose multiIcon or icoStream here; TrayManager holds refs.
                // The Form will also use this.Icon. They are disposed when the form closes.
            }
            catch (Exception ex)
            {
                Trace.WriteLine("UI.MainForm: TryLoadPrepareAndApplyImageAndIcon ERROR: " + ex);
                MessageBox.Show("Failed to load/prepare image/icon.\n" + ex.Message,
                    _state.AppDisplayName + " — Image Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);

                // If we failed before passing icon/stream to tray, we can clean them now.
                try { multiIcon?.Dispose(); } catch { /* ignore */ }
                try { icoStream?.Dispose(); } catch { /* ignore */ }
            }
            finally
            {
                // We cloned into the PictureBox, so these locals can be safely disposed.
                try { display?.Dispose(); } catch { }
                try { evenSquare?.Dispose(); } catch { }
                try { squared?.Dispose(); } catch { }
                try { src?.Dispose(); } catch { }
            }
        }

        /// <summary>
        /// Ensures the square image has even width/height by replicating the
        /// rightmost column and bottommost row by 1px if needed.
        /// </summary>
        private static Bitmap AddRightAndBottomReplication(Bitmap srcSquare)
        {
            Trace.WriteLine("UI.MainForm: Entered AddRightAndBottomReplication ...");
            if (srcSquare == null) throw new ArgumentNullException(nameof(srcSquare));
            if (srcSquare.Width != srcSquare.Height)
                throw new ArgumentException("AddRightAndBottomReplication expects a square image.");

            int w = srcSquare.Width;
            int h = srcSquare.Height;
            int newW = (w % 2 == 0) ? w : (w + 1);
            int newH = (h % 2 == 0) ? h : (h + 1);

            if (newW == w && newH == h)
            {
                Trace.WriteLine("UI.MainForm: Even size already; returning clone.");
                return new Bitmap(srcSquare);
            }

            var dst = new Bitmap(newW, newH);
            using (var g = Graphics.FromImage(dst))
            {
                g.Clear(Color.Transparent);
                g.DrawImage(srcSquare, 0, 0, w, h);
            }

            // If width grew by 1, copy the last original column into the new rightmost column.
            if (newW > w)
            {
                int srcX = w - 1;
                int dstX = newW - 1;
                for (int y = 0; y < h; y++)
                {
                    Color c = dst.GetPixel(srcX, y);
                    dst.SetPixel(dstX, y, c);
                }
            }

            // If height grew by 1, copy the last original row into the new bottom row.
            if (newH > h)
            {
                int srcY = h - 1;
                int dstY = newH - 1;
                for (int x = 0; x < newW; x++)
                {
                    // If we also added a column, make sure to read from the last
                    // valid column (w-1) for x >= w.
                    int sx = Math.Min(x, w - 1);
                    Color c = dst.GetPixel(sx, srcY);
                    dst.SetPixel(x, dstY, c);
                }
            }

            Trace.WriteLine($"UI.MainForm: AddRightAndBottomReplication → {dst.Width}x{dst.Height}");
            return dst;
        }

        /// <summary>
        /// Resize the form’s client area to match the bitmap exactly (for now).
        /// You can add extra reserved height here later for labels/buttons.
        /// </summary>
        private void ResizeClientToBitmap(Bitmap bmp)
        {
            if (bmp == null) return;
            Trace.WriteLine($"UI.MainForm: ResizeClientToBitmap: target client={bmp.Width}x{bmp.Height}");

            // Current non-client borders
            int ncW = this.Width - this.ClientSize.Width;
            int ncH = this.Height - this.ClientSize.Height;

            // Requested client area: image width/height
            int targetW = bmp.Width;
            int targetH = bmp.Height;

            // Apply
            this.Size = new Size(targetW + ncW, targetH + ncH);
            Trace.WriteLine($"UI.MainForm: New Window Size={this.Width}x{this.Height} (Client={this.ClientSize.Width}x{this.ClientSize.Height})");
        }
    }
}
