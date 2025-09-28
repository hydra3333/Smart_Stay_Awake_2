// File: src/Stay_Awake_2/Imaging/FallbackImageFactory.cs
// Purpose: Dev-friendly fallback image (distinct “eye over checkerboard”) when no image source is available.

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ListView;

namespace Stay_Awake_2.Imaging
{
    internal static class FallbackImageFactory
    {
        public static Bitmap CreateEyeCheckerboard(int size = 256)
        {
            Trace.WriteLine($"FallbackImageFactory: Entered CreateEyeCheckerboard({size}) ...");
            int s = Math.Max(64, Math.Min(1024, size));
            var bmp = new Bitmap(s, s);

            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Color.DimGray);

            // Checkerboard
            int tile = Math.Max(8, s / 10);
            for (int y = 0; y < s; y += tile)
            {
                for (int x = 0; x < s; x += tile)
                {
                    bool dark = ((x / tile) + (y / tile)) % 2 == 0;
                    using var brush = new SolidBrush(dark ? Color.Gray : Color.LightGray);
                    g.FillRectangle(brush, x, y, tile, tile);
                }
            }

            // Simple “eye”
            var center = new PointF(s / 2f, s / 2f);
            float eyeW = s * 0.70f;
            float eyeH = s * 0.45f;

            using (var pen = new Pen(Color.White, Math.Max(2, s / 100)))
            using (var pen2 = new Pen(Color.Black, Math.Max(2, s / 150)))
            using (var irisBrush = new SolidBrush(Color.SteelBlue))
            using (var pupilBrush = new SolidBrush(Color.Black))
            {
                var eyeRect = new RectangleF(center.X - eyeW / 2, center.Y - eyeH / 2, eyeW, eyeH);
                g.DrawArc(pen, eyeRect, 200, 140);    // upper lid
                g.DrawArc(pen, eyeRect, 20, 140);     // lower lid

                float irisR = s * 0.14f;
                float pupilR = irisR * 0.45f;
                g.FillEllipse(irisBrush, center.X - irisR, center.Y - irisR, 2 * irisR, 2 * irisR);
                g.DrawEllipse(pen2, center.X - irisR, center.Y - irisR, 2 * irisR, 2 * irisR);
                g.FillEllipse(pupilBrush, center.X - pupilR, center.Y - pupilR, 2 * pupilR, 2 * pupilR);
            }

            Trace.WriteLine("FallbackImageFactory: Exiting CreateEyeCheckerboard (success).");
            return bmp;
        }

        /// <summary>
        /// LEFT Eye of Horus, gold outline on white, centered.
        /// The eye’s path is scaled uniformly to fill 99% of the square (1% border).
        /// It includes the almond-shaped eye, iris, pupil, brow, teardrop, spiral,
        /// and horizontal line details typical of the Eye of Horus symbol.
        /// All drawing is scaled proportionally to the bitmap size.
        /// Colors and pen thicknesses are chosen for clarity and style.
        /// </summary>
        public static Bitmap CreateEyeOfHorusBitmap(int size = 256, Color? ink = null)
        {
            int s = Math.Max(64, Math.Min(1024, size));
            var bmp = new Bitmap(s, s);
            Trace.WriteLine($"FallbackImageFactory: Entered CreateEyeOfHorusBitmap({size}) ...");

            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.White);

                float border = s * 0.01f;      // 1% free border
                float drawW = s - 2 * border; // drawable square
                float drawH = drawW;

                // Helper maps normalized [0..1] to drawable pixel space
                PointF N(float x, float y) => new PointF(border + x * drawW, border + y * drawH);

                // Build all subpaths in normalized coords (0..1)
                var paths = new[]
                {
                MakeBezier(0.82f,0.53f, 0.68f,0.30f, 0.40f,0.28f, 0.20f,0.45f), // upper lid
                MakeBezier(0.20f,0.55f, 0.36f,0.68f, 0.60f,0.68f, 0.82f,0.53f), // lower lid
                MakeBezier(0.14f,0.32f, 0.42f,0.16f, 0.68f,0.22f, 0.88f,0.32f), // brow
                MakeBezier(0.28f,0.56f, 0.27f,0.72f, 0.27f,0.86f, 0.28f,0.90f), // teardrop
                MakeBezier(0.28f,0.60f, 0.45f,0.82f, 0.72f,0.82f, 0.86f,0.72f), // cheek sweep
                MakeBezier(0.86f,0.72f, 0.96f,0.66f, 0.97f,0.58f, 0.92f,0.57f), // spiral part 1
                MakeBezier(0.92f,0.57f, 0.88f,0.56f, 0.86f,0.60f, 0.88f,0.63f)  // spiral part 2
            };

                // Short horizontal line
                var line = new[] { new PointF(0.83f, 0.56f), new PointF(0.98f, 0.56f) };

                // Iris (normalized center + radius)
                var irisCenter = new PointF(0.47f, 0.50f);
                float irisR = 0.09f;

                // Compute normalized bbox over all parts to fit 99% (actually the drawable minus border)
                RectangleF nb = GetNormalizedBounds(paths, line, irisCenter, irisR);

                // Build transform: normalized -> fit nb into [border..s-border]^2 uniformly
                var m = new Matrix();

                // Translate so nb min is at origin
                m.Translate(-nb.Left, -nb.Top, MatrixOrder.Append);

                // Uniform scale to max that fits
                float sx = drawW / nb.Width;
                float sy = drawH / nb.Height;
                float scale = Math.Min(sx, sy);
                m.Scale(scale, scale, MatrixOrder.Append);

                // Center inside drawable square, then add border offset
                float usedW = nb.Width * scale;
                float usedH = nb.Height * scale;
                float tx = border + (drawW - usedW) * 0.5f;
                float ty = border + (drawH - usedH) * 0.5f;
                m.Translate(tx, ty, MatrixOrder.Append);

                // Apply transform and draw
                var defaultGold = Color.FromArgb(0xCC, 0xA6, 0x1A);
                var strokeColor = ink ?? defaultGold;                 // use caller-provided color, else gold
                float stroke = Math.Max(2f, s / 56f);
                using var pen = new Pen(strokeColor, stroke)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };



                foreach (var p in paths)
                {
                    using var pc = (GraphicsPath)p.Clone();
                    pc.Transform(m);
                    g.DrawPath(pen, pc);
                }

                // Line
                using (var pc = new GraphicsPath())
                {
                    pc.AddLine(line[0], line[1]);
                    pc.Transform(m);
                    g.DrawPath(pen, pc);
                }

                // Iris
                {
                    // map center & radius with the same transform
                    var pts = new PointF[] { irisCenter, new PointF(irisCenter.X + irisR, irisCenter.Y) };
                    m.TransformPoints(pts);
                    float mappedR = Distance(pts[0], pts[1]); // uniform scale -> radius in pixels
                    var r = mappedR;
                    var c = pts[0];
                    var irisRect = new RectangleF(c.X - r, c.Y - r, 2 * r, 2 * r);
                    g.DrawEllipse(pen, irisRect);
                }
            }

            Trace.WriteLine("FallbackImageFactory: Exiting CreateEyeOfHorusBitmap (success).");
            return bmp;
        }
        // ---- helpers for CreateEyeOfHorusBitmap ----
        private static GraphicsPath MakeBezier(float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3)
        {
            var gp = new GraphicsPath();
            gp.AddBezier(new PointF(x0, y0), new PointF(x1, y1), new PointF(x2, y2), new PointF(x3, y3));
            return gp;
        }
        private static RectangleF GetNormalizedBounds(GraphicsPath[] paths, PointF[] line, PointF irisCenter, float irisR)
        {
            var min = new PointF(float.PositiveInfinity, float.PositiveInfinity);
            var max = new PointF(float.NegativeInfinity, float.NegativeInfinity);
            void Acc(PointF p) { if (p.X < min.X) min.X = p.X; if (p.Y < min.Y) min.Y = p.Y; if (p.X > max.X) max.X = p.X; if (p.Y > max.Y) max.Y = p.Y; }
            foreach (var p in paths) { var b = p.GetBounds(); Acc(new PointF(b.Left, b.Top)); Acc(new PointF(b.Right, b.Bottom)); }
            foreach (var p in line) Acc(p);
            Acc(new PointF(irisCenter.X - irisR, irisCenter.Y - irisR));
            Acc(new PointF(irisCenter.X + irisR, irisCenter.Y + irisR));
            return RectangleF.FromLTRB(min.X, min.Y, max.X, max.Y);
        }

        private static float Distance(PointF a, PointF b)
            => (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    }
}



