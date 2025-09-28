// File: src/Stay_Awake_2/Imaging/FallbackImageFactory.cs
// Purpose: Dev-friendly fallback image (distinct “eye over checkerboard”) when no image source is available.

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;

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
    }
}
