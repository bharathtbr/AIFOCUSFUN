using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FocusFun.Core.Svg;

namespace FocusFun.Core.Art;

using static SvgWriter;

/// <summary>
/// An approved picture stored as an image (e.g. drawn by an image model and checked by an admin), as a black line-art PNG.
/// </summary>
/// <param name="Key">Stable id, used to embed the image once per page and reference it wherever it's drawn.</param>
/// <param name="Word">The item's name, lowercase (e.g. "tree pose").</param>
/// <param name="PngBase64">The PNG, base64-encoded.</param>
/// <param name="Width">Pixel width.</param>
/// <param name="Height">Pixel height.</param>
/// <param name="Shape">Its outline and enclosed areas (in pixels), for dot-to-dot and colour-by-number; null if not analysed.</param>
/// <param name="MirrorSafe">An admin checked that a mirrored copy looks clearly different, so odd one out may use it mirrored.</param>
public sealed record PictureAsset(string Key, string Word, string PngBase64, int Width, int Height, PictureShape? Shape = null, bool MirrorSafe = false);

/// <summary>A topic's approved image pictures for one sheet, with the plural label used in instructions ("yoga poses").</summary>
public sealed record SheetPictures(string Plural, IReadOnlyList<PictureAsset> Items);

/// <summary>Image pictures behave like drawn ones: each draw is a reference to the image, embedded once per page by <see cref="Embed"/>.</summary>
public sealed partial class ImagePictureSet(string theme, SheetPictures pictures) : IPictureSet
{
    public string Theme => theme;
    public string Plural => pictures.Plural;

    public IReadOnlyList<Picture> All { get; } = pictures.Items
        .Select(a => new Picture(a.Word, (box, _, flip) => Use(a, box, flip), Mirrorable: a.MirrorSafe))
        .ToList();

    /// <summary>The images behind <see cref="All"/>, in the same order.</summary>
    public IReadOnlyList<PictureAsset> Assets => pictures.Items;

    /// <summary>Draws <paramref name="asset"/> into <paramref name="box"/>; the same transform maps its <see cref="PictureShape"/> points.</summary>
    public static string Draw(PictureAsset asset, Box box) => Use(asset, box, flip: false);

    /// <summary>Where a pixel of <paramref name="asset"/> lands when drawn into <paramref name="box"/> (centred, aspect kept).</summary>
    public static (double X, double Y, double Scale) Placement(PictureAsset asset, Box box)
    {
        double scale = Math.Min(box.Width / asset.Width, box.Height / asset.Height);
        return (box.CenterX - asset.Width * scale / 2, box.CenterY - asset.Height * scale / 2, scale);
    }

    private static string Id(PictureAsset a) => "pic-" + a.Key;

    private static string Use(PictureAsset a, Box box, bool flip)
    {
        var (x, y, scale) = Placement(a, box);
        string mirror = flip ? $" translate({a.Width} 0) scale(-1 1)" : string.Empty;
        // Image pixels are much smaller than page points, so the scale needs more precision than N's 2 decimals.
        string s = scale.ToString("0.#####", CultureInfo.InvariantCulture);
        return $"<use href=\"#{Id(a)}\" transform=\"translate({N(x)} {N(y)}) scale({s}){mirror}\"/>";
    }

    /// <summary>Adds each picture the page uses to its &lt;defs&gt;, once, so a sheet repeating a picture stays small.</summary>
    public static string Embed(string svg, SheetPictures? pictures)
    {
        if (pictures is null) return svg;
        var used = pictures.Items.Where(a => svg.Contains($"href=\"#{Id(a)}\"", StringComparison.Ordinal)).ToList();
        if (used.Count == 0) return svg;

        var defs = new StringBuilder("<defs>");
        foreach (var a in used)
            defs.Append($"<image id=\"{Id(a)}\" width=\"{a.Width}\" height=\"{a.Height}\" href=\"data:image/png;base64,{a.PngBase64}\"/>");
        defs.Append("</defs>");

        var open = SvgOpenTag().Match(svg);
        return open.Success ? svg.Insert(open.Index + open.Length, defs.ToString()) : svg;
    }

    [GeneratedRegex("<svg[^>]*>")]
    private static partial Regex SvgOpenTag();
}
