using System.Text;
using FocusFun.Core.Art;
using FocusFun.Core.Svg;

namespace FocusFun.Core.Generators;

using static SvgWriter;

/// <summary>
/// The colouring scenes with a symbol in each region and a key at the bottom (symbol = colour name).
/// Easy uses numbers, Medium English letters, Challenge numbers with more colours.
/// Topics with approved AI pictures use one of those, with a symbol in each area the picture's lines enclose.
/// </summary>
/// <remarks>The spec's Challenge key uses Hindi or Telugu letters; that arrives with the Phase 2 script support.</remarks>
public sealed class ColorByNumberGenerator : ISheetGenerator
{
    private const double KeyHeight = 92;
    private const string Letters = "ABCDEFGHIJ";

    /// <summary>Colours in the order they get symbols, so the key reads naturally.</summary>
    private static readonly string[] ColorOrder = ["red", "blue", "yellow", "green", "orange", "black", "brown", "grey"];

    public SheetType Type => SheetType.ColorByNumber;

    public static int MaxColors(Level level) => level switch
    {
        Level.Easy => 3,
        Level.Medium => 5,
        _ => 8,
    };

    /// <summary>An image picture needs this many enclosed areas big enough for a symbol to make a colour-by-number.</summary>
    private const int MinImageRegions = 3;

    /// <summary>Smallest symbol, in points, that still reads clearly inside an area.</summary>
    private const double MinSymbolSize = 11;
    private const double MaxSymbolSize = 22;

    public SheetContent Generate(SheetRequest request, Box box, SeededRandom random)
    {
        var sceneBox = new Box(box.X, box.Y, box.Width, box.Height - KeyHeight - 10);
        var images = ThemeText.ShapedImages(request, s => s.Regions.Count >= MinImageRegions);
        if (images.Count > 0) return ImagePage(images[random.Next(images.Count)], request.Level, box, sceneBox);
        var scene = Scenes.Render(ColoringGenerator.Build(request.Level, sceneBox, random), sceneBox, lineWidth: 3);

        var colors = PickColors(scene.Labels, request.Level);
        var symbols = colors.Select((c, i) => (Color: c, Symbol: request.Level == Level.Medium ? Letters[i].ToString() : (i + 1).ToString()))
            .ToDictionary(p => p.Color, p => p.Symbol);

        var body = new StringBuilder(scene.Svg);
        var w = new SvgWriter();
        foreach (var label in scene.Labels.Where(l => symbols.ContainsKey(l.Color)))
            w.Text(label.X, label.Y + 5, symbols[label.Color], 14, "middle", bold: true);
        body.Append(w).Append(DrawKey(colors, symbols, new Box(box.X, box.Bottom - KeyHeight, box.Width, KeyHeight)));

        string kind = request.Level == Level.Medium ? "letter" : "number";
        return new SheetContent("Color by Number", $"Color each {kind} in its color!", body.ToString(), null);
    }

    /// <summary>
    /// An approved image picture with a symbol in each enclosed area big enough to hold one. Colours go round in key
    /// order from the largest area down, so every colour in the key is used.
    /// </summary>
    private static SheetContent ImagePage(PictureAsset asset, Level level, Box box, Box sceneBox)
    {
        var art = new Box(sceneBox.X + 10, sceneBox.Y + 6, sceneBox.Width - 20, sceneBox.Height - 12);
        var (ox, oy, scale) = ImagePictureSet.Placement(asset, art);
        var regions = asset.Shape!.Regions.Where(r => r.Room * scale * 1.6 >= MinSymbolSize).Take(16).ToList();
        if (regions.Count == 0) regions = asset.Shape.Regions.Take(1).ToList();

        var colors = ColorOrder.Take(Math.Min(MaxColors(level), regions.Count)).ToList();
        var symbols = colors.Select((c, i) => (Color: c, Symbol: level == Level.Medium ? Letters[i].ToString() : (i + 1).ToString()))
            .ToDictionary(p => p.Color, p => p.Symbol);

        var w = new SvgWriter().Raw(ImagePictureSet.Draw(asset, art));
        for (int i = 0; i < regions.Count; i++)
        {
            var r = regions[i];
            double size = Math.Clamp(r.Room * scale * 1.6, MinSymbolSize, MaxSymbolSize);
            w.Text(ox + r.X * scale, oy + r.Y * scale + size * 0.35, symbols[colors[i % colors.Count]], size, "middle", bold: true);
        }
        w.Raw(DrawKey(colors, symbols, new Box(box.X, box.Bottom - KeyHeight, box.Width, KeyHeight)));

        string kind = level == Level.Medium ? "letter" : "number";
        return new SheetContent("Color by Number", $"Color each {kind} in its color!", w.ToString(), null);
    }

    /// <summary>The most-used colours in the scene, up to the level's limit, in key order.</summary>
    public static List<string> PickColors(IReadOnlyList<SceneLabel> labels, Level level) =>
        labels.GroupBy(l => l.Color)
            .OrderByDescending(g => g.Key is "black" or "blue" ? int.MaxValue : g.Count())   // wheels and windows are on every vehicle
            .Take(MaxColors(level))
            .Select(g => g.Key)
            .OrderBy(c => Array.IndexOf(ColorOrder, c))
            .ToList();

    /// <summary>Key: symbol, an empty circle to colour in first (the page is black and white), and the colour's name.</summary>
    private static string DrawKey(List<string> colors, Dictionary<string, string> symbols, Box box)
    {
        var w = new SvgWriter();
        w.Rect(box.X, box.Y, box.Width, box.Height, 2, radius: 10);
        int perRow = colors.Count <= 4 ? colors.Count : (int)Math.Ceiling(colors.Count / 2.0);
        int rows = (int)Math.Ceiling(colors.Count / (double)perRow);
        double cellW = box.Width / perRow, cellH = box.Height / rows;

        for (int i = 0; i < colors.Count; i++)
        {
            double cx = box.X + (i % perRow) * cellW + 16;
            double cy = box.Y + (i / perRow + 0.5) * cellH;
            w.Text(cx + 8, cy + 7, symbols[colors[i]], 20, "middle", bold: true);
            w.Text(cx + 22, cy + 7, "=", 18, "middle");
            w.Circle(cx + 44, cy, 11, 2);
            w.Text(cx + 62, cy + 6, colors[i], 17);
        }
        return w.ToString();
    }
}
