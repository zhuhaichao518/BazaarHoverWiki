using System.Drawing.Imaging;
using System.IO;
using Forms = System.Windows.Forms;

namespace BazaarHoverWiki;

internal sealed record CaptureFrame(
    byte[] PngBytes,
    int Width,
    int Height,
    Point CursorInFrame
);

internal static class ScreenCapture
{
    public static CaptureFrame AroundCursor(int requestedWidth, int requestedHeight)
    {
        var cursor = Forms.Cursor.Position;
        var screen = Forms.Screen.FromPoint(cursor).Bounds;

        var width = Math.Min(Math.Max(240, requestedWidth), screen.Width);
        var height = Math.Min(Math.Max(120, requestedHeight), screen.Height);

        // Tooltips may appear on either horizontal side. Keep the cursor centered
        // horizontally and reserve more room above it for the title bar.
        var left = Math.Clamp(
            cursor.X - width / 2,
            screen.Left,
            screen.Right - width
        );
        var top = Math.Clamp(
            cursor.Y - (int)(height * 0.72),
            screen.Top,
            screen.Bottom - height
        );

        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(left, top, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return new CaptureFrame(
            stream.ToArray(),
            width,
            height,
            new Point(cursor.X - left, cursor.Y - top)
        );
    }
}
