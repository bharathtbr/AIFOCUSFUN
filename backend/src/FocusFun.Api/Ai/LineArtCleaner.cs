using FocusFun.Core.Art;
using SkiaSharp;

namespace FocusFun.Api.Ai;

/// <param name="Shape">Outline and colourable areas, for dot-to-dot and colour-by-number.</param>
public sealed record CleanedImage(byte[] Png, int Width, int Height, PictureShape Shape);

/// <summary>
/// Turns a generated picture into printable line art: pure black lines on a transparent background (no greys, so it
/// prints on a home black-and-white printer and stays colourable), trimmed to the drawing and scaled down for sheets.
/// </summary>
public static class LineArtCleaner
{
    /// <summary>Pixels darker than this (0–255 luminance) become black lines; everything else goes.</summary>
    public const int Threshold = 150;
    public const int MaxSide = 640;

    public static CleanedImage Clean(byte[] image)
    {
        using var source = SKBitmap.Decode(image) ?? throw new InvalidOperationException("The picture couldn't be read.");

        // Trim to the drawing, with a small margin.
        var ink = InkBounds(source) ?? throw new InvalidOperationException("The picture is blank.");
        int margin = (int)(Math.Max(ink.Width, ink.Height) * 0.03) + 2;
        var crop = SKRectI.Intersect(SKRectI.Inflate(ink, margin, margin), new SKRectI(0, 0, source.Width, source.Height));

        double scale = Math.Min(1, (double)MaxSide / Math.Max(crop.Width, crop.Height));
        int width = Math.Max(1, (int)Math.Round(crop.Width * scale)), height = Math.Max(1, (int)Math.Round(crop.Height * scale));

        // Scale on white first (smooth), then threshold, so thin lines survive the resize.
        using var scaled = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using (var canvas = new SKCanvas(scaled))
        using (var sourceImage = SKImage.FromBitmap(source))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawImage(sourceImage, SKRect.Create(crop.Left, crop.Top, crop.Width, crop.Height), SKRect.Create(0, 0, width, height),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        }

        using var output = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        var mask = new bool[width, height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                mask[x, y] = IsInk(scaled.GetPixel(x, y));
                output.SetPixel(x, y, mask[x, y] ? SKColors.Black : SKColors.Transparent);
            }

        using var png = SKImage.FromBitmap(output).Encode(SKEncodedImageFormat.Png, 100);
        return new CleanedImage(png.ToArray(), width, height, ShapeAnalyzer.Analyze(mask));
    }

    /// <summary>Transparent pixels count as paper.</summary>
    private static bool IsInk(SKColor c) => c.Alpha > 127 && 0.299 * c.Red + 0.587 * c.Green + 0.114 * c.Blue < Threshold;

    private static SKRectI? InkBounds(SKBitmap bitmap)
    {
        int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (!IsInk(bitmap.GetPixel(x, y))) continue;
                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
            }
        return right < 0 ? null : new SKRectI(left, top, right + 1, bottom + 1);
    }
}
