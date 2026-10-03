namespace FocusFun.Core.Art;

public readonly record struct GlyphPoint(double X, double Y);

/// <summary>A letter as the strokes a child writes, in teaching order. Coordinates: cap height 0 (top) to 100 (baseline).</summary>
public sealed record Glyph(char Char, double Width, IReadOnlyList<IReadOnlyList<GlyphPoint>> Strokes);

/// <summary>
/// Hand-authored single-line letters (uppercase A–Z and digits 0–9) with stroke order and direction.
/// Curves are sampled into short segments; check stroke order against the school's handwriting book.
/// </summary>
public static class StrokeFont
{
    private static readonly Dictionary<char, Glyph> Glyphs = Build();

    public static IReadOnlyCollection<char> Characters => Glyphs.Keys;

    public static bool Supports(char c) => Glyphs.ContainsKey(char.ToUpperInvariant(c));

    public static Glyph Get(char c) => Glyphs[char.ToUpperInvariant(c)];

    private static Dictionary<char, Glyph> Build()
    {
        var g = new Dictionary<char, Glyph>();
        // Width comes from the drawn points, so spacing always matches the letter.
        void Add(char c, params GlyphPoint[][] strokes) => g[c] = new Glyph(c, strokes.SelectMany(p => p).Max(p => p.X), strokes);

        Add('A', L(40, 0, 0, 100), L(40, 0, 80, 100), L(16, 62, 64, 62));
        Add('B', L(0, 0, 0, 100), [.. L(0, 0, 36, 0), .. Arc(36, 25, 27, 25, -90, 90), .. L(0, 50)], [.. L(0, 50, 38, 50), .. Arc(38, 75, 30, 25, -90, 90), .. L(0, 100)]);
        Add('C', Arc(50, 50, 48, 50, -45, -315));
        Add('D', L(0, 0, 0, 100), [.. L(0, 0, 25, 0), .. Arc(25, 50, 50, 50, -90, 90), .. L(0, 100)]);
        Add('E', L(0, 0, 0, 100), L(0, 0, 65, 0), L(0, 50, 55, 50), L(0, 100, 65, 100));
        Add('F', L(0, 0, 0, 100), L(0, 0, 65, 0), L(0, 50, 55, 50));
        Add('G', Arc(50, 50, 48, 50, -40, -360), L(60, 50, 98, 50));
        Add('H', L(0, 0, 0, 100), L(70, 0, 70, 100), L(0, 50, 70, 50));
        Add('I', L(20, 0, 20, 100), L(0, 0, 40, 0), L(0, 100, 40, 100));
        Add('J', [.. L(60, 0, 60, 70), .. Arc(30, 70, 30, 30, 0, 180)]);
        Add('K', L(0, 0, 0, 100), L(70, 0, 0, 58), L(24, 42, 70, 100));
        Add('L', L(0, 0, 0, 100, 60, 100));
        Add('M', L(0, 100, 0, 0), L(0, 0, 45, 65, 90, 0, 90, 100));
        Add('N', L(0, 100, 0, 0), L(0, 0, 75, 100), L(75, 100, 75, 0));
        Add('O', Arc(45, 50, 45, 50, -90, -450));
        Add('P', L(0, 0, 0, 100), [.. L(0, 0, 36, 0), .. Arc(36, 26, 28, 26, -90, 90), .. L(0, 52)]);
        Add('Q', Arc(45, 50, 45, 50, -90, -450), L(56, 68, 90, 102));
        Add('R', L(0, 0, 0, 100), [.. L(0, 0, 36, 0), .. Arc(36, 26, 28, 26, -90, 90), .. L(0, 52)], L(28, 52, 70, 100));
        Add('S', [.. Arc(35, 25, 32, 25, -20, -270), .. Arc(35, 75, 34, 25, -90, 160)]);
        Add('T', L(0, 0, 70, 0), L(35, 0, 35, 100));
        Add('U', [.. L(0, 0, 0, 65), .. Arc(35, 65, 35, 35, 180, 0), .. L(70, 0)]);
        Add('V', L(0, 0, 40, 100, 80, 0));
        Add('W', L(0, 0, 25, 100, 55, 25, 85, 100, 110, 0));
        Add('X', L(0, 0, 75, 100), L(75, 0, 0, 100));
        Add('Y', L(0, 0, 40, 50), L(80, 0, 40, 50, 40, 100));
        Add('Z', L(0, 0, 70, 0, 0, 100, 70, 100));

        Add('0', Arc(30, 50, 30, 50, -90, -450));
        Add('1', L(8, 22, 30, 0, 30, 100));
        Add('2', [.. Arc(30, 30, 30, 30, 190, 380), .. L(0, 100, 62, 100)]);
        Add('3', [.. Arc(28, 25, 28, 25, 200, 450), .. Arc(28, 75, 31, 25, -90, 160)]);
        Add('4', L(45, 0, 0, 70, 65, 70), L(45, 0, 45, 100));
        Add('5', [.. L(8, 0, 6, 46), .. Arc(30, 70, 30, 30, 222, 500)], L(8, 0, 58, 0));
        Add('6', [.. Arc(48, 50, 44, 50, -100, -180), .. Arc(31, 72, 30, 28, 180, -180)]);
        Add('7', L(0, 0, 60, 0, 18, 100));
        Add('8', Arc(30, 25, 24, 25, -90, 270), Arc(30, 75, 30, 25, -90, 270));
        Add('9', [.. Arc(30, 28, 28, 28, 0, -360), .. L(58, 100)]);

        return g;
    }

    /// <summary>Straight segments through the given x,y pairs.</summary>
    private static GlyphPoint[] L(params double[] xy) =>
        Enumerable.Range(0, xy.Length / 2).Select(i => new GlyphPoint(xy[2 * i], xy[2 * i + 1])).ToArray();

    /// <summary>Elliptical arc sampled every ~10°. Angles in degrees, screen space (0 = right, 90 = down).</summary>
    private static GlyphPoint[] Arc(double cx, double cy, double rx, double ry, double fromDeg, double toDeg)
    {
        int steps = Math.Max(4, (int)Math.Ceiling(Math.Abs(toDeg - fromDeg) / 10));
        return Enumerable.Range(0, steps + 1).Select(i =>
        {
            double a = (fromDeg + (toDeg - fromDeg) * i / steps) * Math.PI / 180;
            return new GlyphPoint(cx + rx * Math.Cos(a), cy + ry * Math.Sin(a));
        }).ToArray();
    }
}
