using Smart_Stay_Awake_2.Imaging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.LinkLabel;

namespace Smart_Stay_Awake_2.UI
{
    // Accessibility matches internal AppState to avoid CS0051.
    internal partial class MainForm : Form
    {
        private readonly AppState _state;

        // System tray owner (already a stub with Initialize/SetIcon/Show/Hide).
        private TrayManager? _tray;

        // Simple image host – fills the client area. We assign a cloned Bitmap to it.
        private PictureBox? _picture;

        // Unified quit guard: prevents recursive calls during shutdown
        private bool _isClosing = false;

        // Constructor is lightweight: build controls, wire events, set fixed window policy.
        internal MainForm(AppState state)
        {
            Trace.WriteLine("Smart_Stay_Awake_2: UI.MainForm: Entered MainForm ctor ...");
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

            // Wire tray events to unified handlers
            _tray.ShowRequested += (s, e) =>
            {
                Trace.WriteLine("Smart_Stay_Awake_2: UI.MainForm: Tray.ShowRequested => RestoreFromTray");
                RestoreFromTray();
            };
            _tray.QuitRequested += (s, e) =>
            {
                Trace.WriteLine("Smart_Stay_Awake_2: UI.MainForm: Tray.QuitRequested => QuitApplication");
                QuitApplication("Tray.Quit");
            };

            // Add an image host (fills the client area)
            _picture = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black
            };
            this.Controls.Add(_picture);

            Trace.WriteLine($"UI.MainForm: Using TraceEnabled={_state.TraceEnabled}");
            Trace.WriteLine("Smart_Stay_Awake_2: UI.MainForm: Exiting MainForm ctor.");
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

        private void MainForm_Load(object sender, EventArgs e)
        {
            Trace.WriteLine("Smart_Stay_Awake_2: UI.MainForm_Load: Entered MainForm_Load ...");
            Trace.WriteLine("Smart_Stay_Awake_2: UI.MainForm_Load: Exiting MainForm_Load ...");
        }

        // Dispose tray in FormClosed (no duplicate Dispose override).
        private void MainForm_FormClosed(object? sender, FormClosedEventArgs e)
        {
            Trace.WriteLine("Smart_Stay_Awake_2: UI.MainForm: Entered MainForm_FormClosed ...");
            try { _tray?.Dispose(); _tray = null; }
            catch (Exception ex) { Trace.WriteLine("UI.MainForm: Tray dispose error: " + ex); }
            Trace.WriteLine("Smart_Stay_Awake_2: UI.MainForm: Exiting MainForm_FormClosed ...");
        }

        // =====================================================================
        // Unified Handlers: All restore/minimize/quit paths funnel through here
        // =====================================================================

        /// <summary>
        /// Restore main window from system tray.
        /// Called by: tray left-click, tray menu "Show Window", Show Window button (future).
        /// </summary>
        private void RestoreFromTray()
        {
            Trace.WriteLine("Smart_Stay_Awake_2: UI.MainForm: Entered RestoreFromTray ...");
            try
            {
                if (_isClosing)
                {
                    Trace.WriteLine("Smart_Stay_Awake_2: UI.MainForm: RestoreFromTray: Already closing; ignoring.");
                    return;
                }

                // Show and activate the window
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.Activate();
                this.BringToFront();

                // Hide tray icon (window is now visible)
                if (_tray != null)
                {
                    _tray.Hide();
                    Trace.WriteLine("Smart_Stay_Awake_2: UI.MainForm: RestoreFromTray: Tray icon hidden");
                }

                Trace.WriteLine("Smart_Stay_Awake_2: UI.MainForm: RestoreFromTray: Window restored and activated");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Smart_Stay_Awake_2: UI.MainForm: RestoreFromTray FAILED: {ex.GetType().Name}");
                Trace.WriteLine($"Smart_Stay_Awake_2: UI.MainForm: RestoreFromTray error message: {ex.Message}");
                Trace.WriteLine($"Smart_Stay_Awake_2: UI.MainForm: RestoreFromTray stack trace: {ex.StackTrace}");
            }
            finally
            {
                Trace.WriteLine("Smart_Stay_Awake_2: UI.MainForm: Exiting RestoreFromTray");
            }
        }

        /// <summary>
        /// Minimize main window to system tray (hide window, show tray icon).
        /// Called by: title bar minimize button, Minimize button (future).
        /// </summary>
        private void MinimizeToTray()
        {
            Trace.WriteLine("Smart_Stay_Awake_2: UI.MainForm: Entered MinimizeToTray ...");
            try
            {
                if (_isClosing)
                {
                    Trace.WriteLine("Smart_Stay_Awake_2: UI.MainForm: MinimizeToTray: Already closing; ignoring.");
                    return;
                }

                // Show tray icon first (so user knows where the window went)
                if (_tray != null)
                {
                    _tray.Show();
                    Trace.WriteLine("Smart_Stay_Awake_2: UI.MainForm: MinimizeToTray: Tray icon shown");
                }

                // Hide the window completely (cleaner than WindowState.Minimized)
                this.Hide();
                Trace.WriteLine("Smart_Stay_Awake_2: UI.MainForm: MinimizeToTray: Window hidden");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Smart_Stay_Awake_2: UI.MainForm: MinimizeToTray FAILED: {ex.GetType().Name}");
                Trace.WriteLine($"Smart_Stay_Awake_2: UI.MainForm: MinimizeToTray error message: {ex.Message}");
                Trace.WriteLine($"Smart_Stay_Awake_2: UI.MainForm: MinimizeToTray stack trace: {ex.StackTrace}");
            }
            finally
            {
                Trace.WriteLine("Smart_Stay_Awake_2: UI.MainForm: Exiting MinimizeToTray");
            }
        }

        /// <summary>
        /// Unified quit/shutdown handler. Cleans up tray, stops timers (future), exits application.
        /// Called by: title bar close X, Alt+F4, tray Quit, Quit button (future), session end.
        /// </summary>
        /// <param name="source">Trace-friendly description of quit trigger source.</param>
        private void QuitApplication(string source)
        {
            Trace.WriteLine($"Smart_Stay_Awake_2: UI.MainForm: Entered QuitApplication (source={source}) ...");
            try
            {
                // Guard against recursive calls (e.g., Close() triggering FormClosing which calls this again)
                if (_isClosing)
                {
                    Trace.WriteLine("Smart_Stay_Awake_2: UI.MainForm: QuitApplication: Already closing; ignoring recursive call.");
                    return;
                }

                _isClosing = true;
                Trace.WriteLine("Smart_Stay_Awake_2: UI.MainForm: QuitApplication: Guard flag set (_isClosing = true)");

                // TODO (future iterations): Stop timers, clear keep-awake state, etc.

                // Hide and dispose tray icon
                if (_tray != null)
                {
                    try
                    {
                        _tray.Hide();
                        _tray.Dispose();
                        _tray = null;
                        Trace.WriteLine("Smart_Stay_Awake_2: UI.MainForm: QuitApplication: Tray disposed");
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"Smart_Stay_Awake_2: UI.MainForm: QuitApplication: Tray disposal error: {ex.Message}");
                    }
                }

                // Close the form (will trigger FormClosing/FormClosed, but _isClosing guard prevents recursion)
                Trace.WriteLine("Smart_Stay_Awake_2: UI.MainForm: QuitApplication: Calling this.Close()");
                this.Close();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Smart_Stay_Awake_2: UI.MainForm: QuitApplication FAILED: {ex.GetType().Name}");
                Trace.WriteLine($"Smart_Stay_Awake_2: UI.MainForm: QuitApplication error message: {ex.Message}");
                Trace.WriteLine($"Smart_Stay_Awake_2: UI.MainForm: QuitApplication stack trace: {ex.StackTrace}");

                // Last resort: force exit
                try
                {
                    Trace.WriteLine("Smart_Stay_Awake_2: UI.MainForm: QuitApplication: Forcing Application.Exit() as last resort");
                    Application.Exit();
                }
                catch { }
            }
            finally
            {
                Trace.WriteLine("Smart_Stay_Awake_2: UI.MainForm: Exiting QuitApplication");
            }
        }

        // =====================================================================
        /// <summary>
        /// Intercepts window resize/minimize events.
        /// When user clicks title bar minimize button "_", redirect to MinimizeToTray() instead.
        /// </summary>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            // Only trace and act if actually minimizing (not just resizing)
            if (this.WindowState == FormWindowState.Minimized && !_isClosing)
            {
                Trace.WriteLine("Smart_Stay_Awake_2: UI.MainForm: OnResize: Minimize detected => MinimizeToTray");
                MinimizeToTray();
            }
        }

        /// <summary>
        /// Intercepts form closing events (title bar X, Alt+F4, programmatic Close()).
        /// Routes all close attempts through unified QuitApplication() handler.
        /// Guard flag (_isClosing) prevents infinite recursion.
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Trace.WriteLine($"Smart_Stay_Awake_2: UI.MainForm: OnFormClosing: Entered (CloseReason={e.CloseReason})");

            // If we're already in the quit flow, allow default close behavior
            if (_isClosing)
            {
                Trace.WriteLine("Smart_Stay_Awake_2: UI.MainForm: OnFormClosing: Already closing; allowing default close");
                base.OnFormClosing(e);
                return;
            }

            // For user-initiated closes (X button, Alt+F4), route through unified handler
            if (e.CloseReason == CloseReason.UserClosing ||
                e.CloseReason == CloseReason.WindowsShutDown ||
                e.CloseReason == CloseReason.TaskManagerClosing ||
                e.CloseReason == CloseReason.FormOwnerClosing)
            {
                Trace.WriteLine($"Smart_Stay_Awake_2: UI.MainForm: OnFormClosing: User/system close => QuitApplication (CloseReason={e.CloseReason})");
                e.Cancel = true;  // Cancel the default close
                QuitApplication($"FormClosing:{e.CloseReason}");
                return;
            }

            // Other close reasons (application-initiated, etc.) - allow default
            Trace.WriteLine("Smart_Stay_Awake_2: UI.MainForm: OnFormClosing: Non-user close; allowing default");
            base.OnFormClosing(e);
        }

        /// <summary>
        /// Handles Windows session ending events (shutdown, logoff, restart).
        /// Ensures clean shutdown via unified QuitApplication() handler.
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            const int WM_QUERYENDSESSION = 0x0011;
            const int WM_ENDSESSION = 0x0016;

            if (m.Msg == WM_QUERYENDSESSION || m.Msg == WM_ENDSESSION)
            {
                Trace.WriteLine($"Smart_Stay_Awake_2: UI.MainForm: WndProc: Session ending (Msg=0x{m.Msg:X4}) => QuitApplication");

                if (!_isClosing)
                {
                    QuitApplication($"SessionEnd:0x{m.Msg:X4}");
                }
                // Allow Windows to proceed with shutdown
                m.Result = (IntPtr)1;
                return;
            }

            base.WndProc(ref m);
        }
        // =====================================================================

        // Image/Icon Loading and Preparation Pipeline
        /// <summary>
        /// Full imaging pipeline with very verbose tracing:
        /// 1) Source selection (priority): CLI --icon -> embedded base64 -> EXE neighbor -> checkerboard fallback
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
            Bitmap? display = null;
            Icon? multiIcon = null;
            MemoryStream? icoStream = null;

            try
            {
                // ------------------------------------------------------------
                // SOURCE PRIORITY (Spec v11):
                //   1) CLI --icon PATH
                //   2) Embedded base64 (if non-empty)
                //   3) File 'Smart_Stay_Awake_2_icon.*' next to EXE (supported: png/jpg/jpeg/bmp/gif/ico)
                //   4) Self-generated checkerboard “eye” fallback
                // ------------------------------------------------------------
                // Note: we validate extension for disk files against AppConfig.ALLOWED_ICON_EXTENSIONS
                // ------------------------------------------------------------

                // 1) CLI --icon PATH
                if (!string.IsNullOrWhiteSpace(_state.Options?.IconPath))
                {
                    string path = _state.Options.IconPath!;
                    string ext = Path.GetExtension(path) ?? string.Empty;
                    Trace.WriteLine($"UI.MainForm: TryLoadPrepareAndApplyImageAndIcon: Candidate source (1/CLI): {path} (ext='{ext}')");

                    if (!AppConfig.ALLOWED_ICON_EXTENSIONS.Contains(ext))
                        throw new InvalidOperationException(
                            $"Unsupported --icon extension '{ext}'. Allowed: {string.Join(" ", AppConfig.ALLOWED_ICON_EXTENSIONS)}");

                    src = ImageLoader.LoadBitmapFromPath(path);
                    Trace.WriteLine($"UI.MainForm: TryLoadPrepareAndApplyImageAndIcon: Using CLI image. Size={src.Width}x{src.Height}");
                }
                // 2) Embedded base64
                else if (Base64ImageLoader.HasEmbeddedImage())
                {
                    Trace.WriteLine("UI.MainForm: TryLoadPrepareAndApplyImageAndIcon: Using embedded base64 image (2/embedded).");
                    src = Base64ImageLoader.LoadEmbeddedBitmap();
                    Trace.WriteLine($"UI.MainForm: TryLoadPrepareAndApplyImageAndIcon: Retrieved embedded image. Size={src.Width}x{src.Height}");
                }
                // 3) Next-to-EXE file (Assets optional)
                else
                {
                    Trace.WriteLine("UI.MainForm: TryLoadPrepareAndApplyImageAndIcon: Checking EXE-neighbor image (3/next-to-EXE).");
                    string exeDir = AppContext.BaseDirectory;

                    // Prefer next-to-EXE (root) first, then ./Assets as a courtesy
                    var probeList = new List<string>();
                    foreach (var ext in AppConfig.ALLOWED_ICON_EXTENSIONS)
                        probeList.Add(Path.Combine(exeDir, $"Smart_Stay_Awake_2_icon{ext}"));
                    foreach (var ext in AppConfig.ALLOWED_ICON_EXTENSIONS)
                        probeList.Add(Path.Combine(exeDir, "Assets", $"Smart_Stay_Awake_2_icon{ext}"));

                    string? found = probeList.FirstOrDefault(File.Exists);

                    // ??????????????????????????????????????????????????????????????????????
                    // for testing only: disable EXE-neighbour to force FALLBACK
                    //
                    //found = null; // TEMP: disable EXE-neighbor for now
                    //
                    // ??????????????????????????????????????????????????????????????????????

                    if (found != null)
                    {
                        string ext = Path.GetExtension(found) ?? string.Empty;
                        Trace.WriteLine($"UI.MainForm: TryLoadPrepareAndApplyImageAndIcon: Found EXE-neighbor: {found} (ext='{ext}')");
                        if (!AppConfig.ALLOWED_ICON_EXTENSIONS.Contains(ext))
                            throw new InvalidOperationException(
                                $"Neighbor image extension '{ext}' not allowed. Allowed: {string.Join(" ", AppConfig.ALLOWED_ICON_EXTENSIONS)}");
                        src = ImageLoader.LoadBitmapFromPath(found);
                        Trace.WriteLine($"UI.MainForm: TryLoadPrepareAndApplyImageAndIcon: Using EXE-neighbor image. Size={src.Width}x{src.Height}");
                    }
                    else
                    {
                        // 4) Self-generated checkerboard “eye” fallback
                        Trace.WriteLine("UI.MainForm: TryLoadPrepareAndApplyImageAndIcon: No disk/embedded image found; using final fallback synthetic image (4/checkerboard).");
                        src = FallbackImageFactory.CreateEyeOfHorusBitmap(AppConfig.WINDOW_MAX_IMAGE_EDGE_PX);
                        Trace.WriteLine($"UI.MainForm: TryLoadPrepareAndApplyImageAndIcon: Retrieved final fallback synthetic image. Size={src.Width}x{src.Height}");
                    }
                }

                // --------- Squaring (edge replication) -----------------------------
                Trace.WriteLine($"UI.MainForm: TryLoadPrepareAndApplyImageAndIcon: Source BEFORE SQUARE: {src.Width}x{src.Height}");
                squared = ImageSquareReplicator.MakeSquareByEdgeReplication(src);
                Trace.WriteLine($"UI.MainForm: TryLoadPrepareAndApplyImageAndIcon: Result AFTER SQUARE:        {squared.Width}x{squared.Height}");

                // --------- Ensure even dimensions (spec requirement) ---------------
                // If odd, replicate one more row/col (right/bottom) to make even
                if ((squared.Width % 2) != 0 || (squared.Height % 2) != 0)
                {
                    Trace.WriteLine("UI.MainForm: TryLoadPrepareAndApplyImageAndIcon: Squared image has odd dimension; applying +1 replicate to make even.");
                    using var tmp = squared;
                    int newSize = Math.Max(squared.Width, squared.Height);
                    var even = new Bitmap(newSize + (newSize % 2 == 0 ? 0 : 1), newSize + (newSize % 2 == 0 ? 0 : 1));
                    using (var g = Graphics.FromImage(even))
                    {
                        g.Clear(Color.Transparent);
                        g.DrawImage(tmp, 0, 0);
                    }
                    // replicate rightmost column if needed
                    if ((even.Width % 2) != 0)
                    {
                        for (int y = 0; y < even.Height; y++)
                            even.SetPixel(even.Width - 1, y, even.GetPixel(even.Width - 2, y));
                    }
                    // replicate bottom row if needed
                    if ((even.Height % 2) != 0)
                    {
                        for (int x = 0; x < even.Width; x++)
                            even.SetPixel(x, even.Height - 1, even.GetPixel(x, even.Height - 2));
                    }
                    squared = even;
                    Trace.WriteLine($"UI.MainForm: TryLoadPrepareAndApplyImageAndIcon: AFTER even-fix:      {squared.Width}x{squared.Height}");
                }

                // --------- Window display (max edge) -------------------------------
                int targetEdge = AppConfig.WINDOW_MAX_IMAGE_EDGE_PX; // e.g., 512
                int maxEdge = Math.Max(squared.Width, squared.Height);
                int finalEdge = (maxEdge > targetEdge) ? targetEdge : maxEdge; // shrink if > target; leave as-is if <= target
                Trace.WriteLine($"UI.MainForm: TryLoadPrepareAndApplyImageAndIcon: Display target edge={targetEdge}, chosen final={finalEdge}");

                display = (finalEdge == maxEdge)
                    ? new Bitmap(squared) // copy as-is
                    : ImageSquareReplicator.ResizeSquareMax(squared, finalEdge);

                // Push to PictureBox
                if (_picture != null)
                {
                    // Ensure old image is released (avoid GDI handle leaks while editing live)
                    var old = _picture.Image;
                    _picture.Image = new Bitmap(display);
                    old?.Dispose();

                    Trace.WriteLine($"UI.MainForm: TryLoadPrepareAndApplyImageAndIcon: PictureBox.Image set. Final={display.Width}x{display.Height}");
                }

                // Adjust window client size to exactly fit image for now (later we’ll add bottom controls)
                this.ClientSize = new Size(display.Width, display.Height);
                Trace.WriteLine($"UI.MainForm: TryLoadPrepareAndApplyImageAndIcon: ClientSize set to {this.ClientSize.Width}x{this.ClientSize.Height}");

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
                // If tray/icon not held yet, clean locals
                try { if (multiIcon != null && (_tray == null)) multiIcon.Dispose(); } catch { }
                try { if (icoStream != null && (_tray == null)) icoStream.Dispose(); } catch { }
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

        /// <summary>
        /// Ensures the square image has even width/height by replicating the
        /// rightmost column and bottommost row by 1px if needed.
        /// </summary>
        private static Bitmap AddRightAndBottomReplication(Bitmap srcSquare)
        {
            Trace.WriteLine("UI.MainForm: Entered AddRightAndBottomReplication ...");
            if (srcSquare == null) throw new ArgumentNullException(nameof(srcSquare));
            if (srcSquare.Width != srcSquare.Height)
                throw new ArgumentException("UI.MainForm: AddRightAndBottomReplication expects a square image.");

            int w = srcSquare.Width;
            int h = srcSquare.Height;
            int newW = (w % 2 == 0) ? w : (w + 1);
            int newH = (h % 2 == 0) ? h : (h + 1);

            if (newW == w && newH == h)
            {
                Trace.WriteLine("UI.MainForm: AddRightAndBottomReplication: Even size already; returning clone.");
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

            Trace.WriteLine($"UI.MainForm: AddRightAndBottomReplication -> Widht x Height {dst.Width}x{dst.Height}");
            return dst;
        }

        /// <summary>
        /// Resize the form’s client area to match the bitmap exactly (for now).
        /// You can add extra reserved height here later for labels/buttons.
        /// </summary>
        private void ResizeClientToBitmap(Bitmap bmp)
        {
            if (bmp == null) return;
            Trace.WriteLine("UI.MainForm: Entered  ResizeClientToBitmap ...");
            Trace.WriteLine($"UI.MainForm: ResizeClientToBitmap: target client={bmp.Width}x{bmp.Height}");

            // Current non-client borders
            int ncW = this.Width - this.ClientSize.Width;
            int ncH = this.Height - this.ClientSize.Height;

            // Requested client area: image width/height
            int targetW = bmp.Width;
            int targetH = bmp.Height;

            // Apply
            this.Size = new Size(targetW + ncW, targetH + ncH);
            Trace.WriteLine($"UI.MainForm: ResizeClientToBitmap: New Window Size={this.Width}x{this.Height} (Client={this.ClientSize.Width}x{this.ClientSize.Height})");
            Trace.WriteLine("UI.MainForm: Exiting ResizeClientToBitmap");
        }

        private void MainForm_Load_1(object sender, EventArgs e)
        {

        }
    }
}
