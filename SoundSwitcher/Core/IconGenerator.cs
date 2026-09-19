using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace SoundSwitcher.Core;

public static class IconGenerator
{
    public static void GenerateAppIcon(string filePath)
    {
        try
        {
            int[] sizes = { 16, 32, 48, 64, 128, 256 };
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // ICONDIR Header
            bw.Write((short)0); // Reserved
            bw.Write((short)1); // Type: 1 for ICO
            bw.Write((short)sizes.Length); // Count

            int offset = 6 + (16 * sizes.Length);
            var pngDataList = new byte[sizes.Length][];

            for (int i = 0; i < sizes.Length; i++)
            {
                int size = sizes[i];
                using var bmp = new Bitmap(size, size);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(Color.Transparent);

                    // Pill/Rounded background with gradient
                    using var path = CreateRoundedRectangle(
                        new RectangleF(size * 0.05f, size * 0.05f, size * 0.90f, size * 0.90f),
                        size * 0.26f
                    );
                    using var brush = new LinearGradientBrush(
                        new PointF(0, 0),
                        new PointF(size, size),
                        Color.FromArgb(14, 116, 244),
                        Color.FromArgb(29, 78, 216)
                    );
                    g.FillPath(brush, path);

                    // Draw Lucide Volume-2 Icon inside
                    float pad = size * 0.22f;
                    var iconRect = new RectangleF(pad, pad, size - 2 * pad, size - 2 * pad);
                    DrawLucideVolume2(g, iconRect, Color.White);
                }

                using var pngStream = new MemoryStream();
                bmp.Save(pngStream, ImageFormat.Png);
                pngDataList[i] = pngStream.ToArray();

                // ICONDIRENTRY
                bw.Write((byte)(size == 256 ? 0 : size)); // Width
                bw.Write((byte)(size == 256 ? 0 : size)); // Height
                bw.Write((byte)0); // Color count
                bw.Write((byte)0); // Reserved
                bw.Write((short)1); // Color planes
                bw.Write((short)32); // Bits per pixel
                bw.Write((int)pngDataList[i].Length); // Bytes in resource
                bw.Write((int)offset); // Image offset

                offset += pngDataList[i].Length;
            }

            for (int i = 0; i < sizes.Length; i++)
            {
                bw.Write(pngDataList[i]);
            }

            File.WriteAllBytes(filePath, ms.ToArray());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error generating app icon: {ex.Message}");
        }
    }

    public static void DrawLucideVolume2(Graphics g, RectangleF bounds, Color color)
    {
        float size = Math.Min(bounds.Width, bounds.Height);
        float scale = size / 24f;
        float offsetX = bounds.X + (bounds.Width - size) / 2f;
        float offsetY = bounds.Y + (bounds.Height - size) / 2f;

        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TranslateTransform(offsetX, offsetY);

        float penWidth = Math.Max(1.4f, size * 0.085f);
        using var pen = new Pen(color, penWidth)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        // Speaker body polygon matching Lucide volume-2
        var pts = new PointF[]
        {
            new PointF(11f * scale, 4.702f * scale),
            new PointF(9.797f * scale, 4.204f * scale),
            new PointF(6.413f * scale, 7.587f * scale),
            new PointF(5.416f * scale, 8f * scale),
            new PointF(3f * scale, 8f * scale),
            new PointF(2f * scale, 9f * scale),
            new PointF(2f * scale, 15f * scale),
            new PointF(3f * scale, 16f * scale),
            new PointF(5.416f * scale, 16f * scale),
            new PointF(6.413f * scale, 16.413f * scale),
            new PointF(9.797f * scale, 19.798f * scale),
            new PointF(11f * scale, 19.298f * scale)
        };

        using var bodyPath = new GraphicsPath();
        bodyPath.AddPolygon(pts);

        using var fillBrush = new SolidBrush(color);
        g.FillPath(fillBrush, bodyPath);
        g.DrawPath(pen, bodyPath);

        // Inner Arc: M16 9 a 5 5 0 0 1 0 6
        // Center is (11, 12), radius = 5
        g.DrawArc(pen, (11f - 5f) * scale, (12f - 5f) * scale, 10f * scale, 10f * scale, -53f, 106f);

        // Outer Arc: M19.364 18.364 a 9 9 0 0 0 0 -12.728
        // Center is (11, 12), radius = 9
        g.DrawArc(pen, (11f - 9f) * scale, (12f - 9f) * scale, 18f * scale, 18f * scale, -45f, 90f);

        g.Restore(state);
    }

    public static GraphicsPath CreateRoundedRectangle(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float diameter = radius * 2;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
