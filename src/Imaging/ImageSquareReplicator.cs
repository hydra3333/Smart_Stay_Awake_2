// File: src/Stay_Awake_2/Imaging/ImageSquareReplicator.cs
// Purpose: Make a bitmap square by *edge replication* (no solid padding).
// Strategy:
//   * Target side = max(width, height)
//   * Create a new square bitmap; draw original centered.
//   * Replicate the left/right columns outward to fill horizontal padding (if width < height).
//   * Replicate the top/bottom rows outward to fill vertical padding (if height < width).
//   * If final dimension is off by 1 (odd/even concerns), we add one more replicated col/row.
// Performance: Uses GetPixel/SetPixel for clarity (images are small for tray/window). We can optimize later.

using System;
using System.Diagnostics;
using System.Drawing;

namespace Stay_Awake_2.Imaging
{
    internal static class ImageSquareReplicator
    {
        public static Bitmap MakeSquareByEdgeReplication(Bitmap src)
        {
            Trace.WriteLine("ImageSquareReplicator: Entered MakeSquareByEdgeReplication ...");
            if (src is null) throw new ArgumentNullException(nameof(src));

            int w = src.Width;
            int h = src.Height;
            if (w == h)
            {
                Trace.WriteLine("ImageSquareReplicator: Source already square; returning clone.");
                return new Bitmap(src); // return a copy to keep ownership clear
            }

            int target = Math.Max(w, h);
            var dst = new Bitmap(target, target);
            using (var g = Graphics.FromImage(dst))
            {
                g.Clear(Color.Transparent);
                // Draw original centered
                int offsetX = (target - w) / 2;
                int offsetY = (target - h) / 2;
                g.DrawImage(src, offsetX, offsetY, w, h);
            }

            // Replicate horizontally if needed
            if (w < target)
            {
                int pad = (target - w) / 2;
                int leftX = (target - w) / 2;
                int rightX = leftX + w - 1;

                // replicate left pad outward using left-most column
                for (int x = leftX - 1; x >= 0; x--)
                {
                    int srcX = leftX; // left-most original column
                    for (int y = 0; y < target; y++)
                    {
                        Color c = dst.GetPixel(srcX, y);
                        dst.SetPixel(x, y, c);
                    }
                }
                // replicate right pad outward using right-most column
                for (int x = rightX + 1; x < target; x++)
                {
                    int srcX = rightX; // right-most original column
                    for (int y = 0; y < target; y++)
                    {
                        Color c = dst.GetPixel(srcX, y);
                        dst.SetPixel(x, y, c);
                    }
                }
            }

            // Replicate vertically if needed
            if (h < target)
            {
                int pad = (target - h) / 2;
                int topY = (target - h) / 2;
                int botY = topY + h - 1;

                // replicate top pad using top-most row
                for (int y = topY - 1; y >= 0; y--)
                {
                    int srcY = topY;
                    for (int x = 0; x < target; x++)
                    {
                        Color c = dst.GetPixel(x, srcY);
                        dst.SetPixel(x, y, c);
                    }
                }
                // replicate bottom pad using bottom-most row
                for (int y = botY + 1; y < target; y++)
                {
                    int srcY = botY;
                    for (int x = 0; x < target; x++)
                    {
                        Color c = dst.GetPixel(x, srcY);
                        dst.SetPixel(x, y, c);
                    }
                }
            }

            Trace.WriteLine("ImageSquareReplicator: Exiting MakeSquareByEdgeReplication (success).");
            return dst;
        }

        /// <summary>
        /// Resize preserving aspect (square in, square out) with high-quality sampling.
        /// </summary>
        public static Bitmap ResizeSquare(Bitmap srcSquare, int targetSize)
        {
            Trace.WriteLine($"ImageSquareReplicator: Entered ResizeSquare to {targetSize} ...");
            if (srcSquare is null) throw new ArgumentNullException(nameof(srcSquare));
            if (srcSquare.Width != srcSquare.Height)
                throw new ArgumentException("ResizeSquare expects a square image.", nameof(srcSquare));

            int s = Math.Max(8, Math.Min(4096, targetSize));
            var dst = new Bitmap(s, s);
            using var g = Graphics.FromImage(dst);
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.DrawImage(srcSquare, 0, 0, s, s);
            Trace.WriteLine("ImageSquareReplicator: Exiting ResizeSquare (success).");
            return dst;
        }
    }
}
