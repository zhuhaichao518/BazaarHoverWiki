using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace BazaarHoverWiki;

internal sealed class FrameColorMap
{
    private readonly byte[] _pixels;
    private readonly int _stride;

    private FrameColorMap(byte[] pixels, int width, int height, int stride)
    {
        _pixels = pixels;
        Width = width;
        Height = height;
        _stride = stride;
    }

    public int Width { get; }
    public int Height { get; }

    public static FrameColorMap FromPng(byte[] pngBytes)
    {
        using var stream = new MemoryStream(pngBytes, writable: false);
        using var decoded = new Bitmap(stream);
        using var bitmap = new Bitmap(decoded.Width, decoded.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
            graphics.DrawImageUnscaled(decoded, 0, 0);

        var bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var bitmapData = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var stride = Math.Abs(bitmapData.Stride);
            var pixels = new byte[stride * bitmap.Height];
            for (var row = 0; row < bitmap.Height; row++)
            {
                var source = IntPtr.Add(bitmapData.Scan0, row * bitmapData.Stride);
                Marshal.Copy(source, pixels, row * stride, stride);
            }

            return new FrameColorMap(pixels, bitmap.Width, bitmap.Height, stride);
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }
    }

    public double GetBlueRatio(double left, double top, double right, double bottom)
    {
        var startX = Math.Clamp((int)Math.Floor(left), 0, Width - 1);
        var startY = Math.Clamp((int)Math.Floor(top), 0, Height - 1);
        var endX = Math.Clamp((int)Math.Ceiling(right), startX + 1, Width);
        var endY = Math.Clamp((int)Math.Ceiling(bottom), startY + 1, Height);
        var bluePixels = 0;
        var chromaticPixels = 0;

        for (var y = startY; y < endY; y += 2)
        {
            for (var x = startX; x < endX; x += 2)
            {
                var offset = y * _stride + x * 4;
                var blue = _pixels[offset];
                var green = _pixels[offset + 1];
                var red = _pixels[offset + 2];
                var maximum = Math.Max(red, Math.Max(green, blue));
                var minimum = Math.Min(red, Math.Min(green, blue));
                if (maximum < 120 || maximum - minimum < 30)
                    continue;

                chromaticPixels++;
                var blueDominant = blue >= 130 && blue > red * 1.18 && blue > green * 0.95;
                var cyan =
                    green >= 130
                    && blue >= 130
                    && red < Math.Min(green, blue) * 0.75;
                if (blueDominant || cyan)
                    bluePixels++;
            }
        }

        return chromaticPixels < 5 ? 0 : (double)bluePixels / chromaticPixels;
    }
}
