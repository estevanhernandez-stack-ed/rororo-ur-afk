using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Labs626.UrAfk.UI;

/// <summary>
/// Runtime-generated brand cursor for the pill's move affordance — the system
/// SizeAll cursor is huge and reads as "window management", not "nudge the
/// pill". Draws a small cyan four-way move glyph with a navy outline into a
/// PNG-compressed .cur stream (supported since Vista), hotspot centered. Built
/// once, cached for the app lifetime; falls back to the plain arrow if cursor
/// construction ever fails.
/// </summary>
internal static class PillCursor
{
    private static Cursor? _move;

    public static Cursor Move => _move ??= Build() ?? Cursors.Arrow;

    private static Cursor? Build()
    {
        try
        {
            const int size = 20;
            const double c = size / 2.0;

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var cyan = new SolidColorBrush(Color.FromRgb(0x17, 0xD4, 0xFA));
                var navy = new Pen(new SolidColorBrush(Color.FromRgb(0x0F, 0x1F, 0x31)), 2.5)
                {
                    LineJoin = PenLineJoin.Round,
                };

                // Four-way move glyph: axis lines + arrowheads, outlined for
                // visibility on any background.
                var g = new GeometryGroup();
                g.Children.Add(new LineGeometry(new Point(c, 3), new Point(c, size - 3)));
                g.Children.Add(new LineGeometry(new Point(3, c), new Point(size - 3, c)));
                foreach (var (tip, a, b) in new[]
                {
                    (new Point(c, 1), new Point(c - 3, 5), new Point(c + 3, 5)),                 // up
                    (new Point(c, size - 1), new Point(c - 3, size - 5), new Point(c + 3, size - 5)), // down
                    (new Point(1, c), new Point(5, c - 3), new Point(5, c + 3)),                 // left
                    (new Point(size - 1, c), new Point(size - 5, c - 3), new Point(size - 5, c + 3)), // right
                })
                {
                    var head = new StreamGeometry();
                    using (var sgc = head.Open())
                    {
                        sgc.BeginFigure(tip, isFilled: true, isClosed: true);
                        sgc.LineTo(a, true, false);
                        sgc.LineTo(b, true, false);
                    }
                    g.Children.Add(head);
                }

                // Outline pass then fill pass, so the cyan sits on a navy edge.
                dc.DrawGeometry(null, navy, g);
                dc.DrawGeometry(cyan, new Pen(cyan, 1.4), g);
            }

            var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));
            using var png = new MemoryStream();
            encoder.Save(png);
            var pngBytes = png.ToArray();

            // .cur container: ICONDIR (type 2) + one ICONDIRENTRY with the
            // hotspot at the glyph center, then the PNG payload.
            using var cur = new MemoryStream();
            using var w = new BinaryWriter(cur);
            w.Write((ushort)0);            // reserved
            w.Write((ushort)2);            // type: cursor
            w.Write((ushort)1);            // one image
            w.Write((byte)size);           // width
            w.Write((byte)size);           // height
            w.Write((byte)0);              // palette
            w.Write((byte)0);              // reserved
            w.Write((ushort)(size / 2));   // hotspot x
            w.Write((ushort)(size / 2));   // hotspot y
            w.Write(pngBytes.Length);      // payload size
            w.Write(22);                   // payload offset
            w.Write(pngBytes);
            w.Flush();
            cur.Position = 0;

            return new Cursor(cur);
        }
        catch
        {
            return null; // caller falls back to the stock arrow
        }
    }
}
