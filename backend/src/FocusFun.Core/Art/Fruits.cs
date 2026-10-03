using FocusFun.Core.Svg;

namespace FocusFun.Core.Art;

using static SvgWriter;

public enum Fruit
{
    Apple,
    Banana,
    Mango,
    Grapes,
    Orange,
    Pear,
    Strawberry,
    Pineapple,
    Watermelon,
    Cherry,
    Lemon,
    Coconut,
    Pomegranate,
}

/// <summary>
/// Original thick-outline fruit line art in the same style as <see cref="Animals"/>, on the shared 200 x 120 canvas.
/// Every fruit has its leaf, stalk or bite on one side, so a mirrored copy looks different (odd one out relies on it).
/// </summary>
public static class Fruits
{
    public static IReadOnlyList<Fruit> All { get; } = Enum.GetValues<Fruit>();

    public static string Name(Fruit fruit) => fruit.ToString().ToLowerInvariant();

    /// <summary>Draws <paramref name="fruit"/> centred in <paramref name="box"/>, keeping its aspect ratio.</summary>
    /// <param name="lineWidth">Outline thickness in page points, independent of the drawing's size.</param>
    public static string Draw(Fruit fruit, Box box, double lineWidth = 2.5, bool flip = false) =>
        LineArt.Place(box, lineWidth, flip, art => Sketch(fruit, art));

    private static void Sketch(Fruit fruit, LineArt a)
    {
        switch (fruit)
        {
            case Fruit.Apple:
                a.Path("M100 36 Q98 22 104 10", open: true);
                a.Leaf(104, 22, 140, 12);
                a.Path("M100 36 Q82 24 64 30 Q40 40 42 70 Q46 104 72 110 Q86 114 100 106 Q114 114 128 110 Q154 104 158 70 Q160 40 136 30 Q118 24 100 36 Z");
                a.Path("M58 58 Q60 46 70 40", detail: true, open: true);
                break;

            case Fruit.Banana:
                a.Path("M30 46 L24 30 L36 26 L44 42 Z");
                a.Path("M30 44 Q40 100 104 104 Q160 104 178 60 Q174 56 168 60 Q150 82 104 82 Q62 80 44 42 Z");
                a.Path("M38 52 Q56 94 104 94 Q148 94 170 64", detail: true, open: true);
                a.Dot(175, 60, 3, 3);
                break;

            case Fruit.Mango:
                a.Path("M70 34 Q68 24 74 16", open: true);
                a.Leaf(74, 22, 118, 12);
                // Kidney shape: round shoulder at the stalk, a flatter belly and a small beak low on the right.
                a.Path("M56 44 Q92 16 134 30 Q172 44 170 74 Q170 86 162 90 Q164 98 156 100 Q140 112 106 110 Q60 108 44 84 Q34 62 56 44 Z");
                a.Path("M132 44 Q152 54 154 72", detail: true, open: true);
                break;

            case Fruit.Grapes:
                a.Path("M104 30 Q106 16 100 6", open: true);
                a.Leaf(104, 20, 150, 14);
                foreach (var (gx, gy) in ((double, double)[])[
                    (70, 40), (92, 38), (114, 38), (136, 40),
                    (81, 60), (103, 58), (125, 60),
                    (92, 80), (114, 80),
                    (103, 99)])
                {
                    a.Circle(gx, gy, 13);
                    a.Path($"M{N(gx - 7)} {N(gy - 2)} Q{N(gx - 6)} {N(gy - 7)} {N(gx - 1)} {N(gy - 8)}", detail: true, open: true);
                }
                break;

            case Fruit.Orange:
                a.Leaf(102, 20, 142, 8);
                a.Circle(100, 66, 46);
                a.Rect(96, 18, 8, 6, 2);
                foreach (var (dx, dy) in ((double, double)[])[(80, 48), (96, 40), (118, 50), (130, 70), (84, 80), (106, 88), (68, 66), (120, 98)])
                    a.Dot(dx, dy, 1.5, 1.5);
                break;

            case Fruit.Pear:
                a.Path("M100 22 Q100 10 108 4", open: true);
                a.Leaf(104, 12, 134, 6);
                a.Path("M100 20 Q116 20 118 44 Q120 56 136 70 Q152 88 140 104 Q126 116 100 114 Q74 116 60 104 Q48 88 64 70 Q80 56 82 44 Q84 20 100 20 Z");
                a.Path("M72 82 Q70 94 78 102", detail: true, open: true);
                break;

            case Fruit.Strawberry:
                a.Path("M100 30 Q102 16 112 8", open: true);
                a.Leaf(108, 14, 140, 10);
                a.Path("M100 112 Q60 100 50 60 Q50 38 76 36 Q88 34 100 40 Q112 34 124 36 Q150 38 150 60 Q140 100 100 112 Z");
                a.Path("M68 42 L82 28 L90 38 L100 24 L110 38 L120 28 L132 42 Q100 52 68 42 Z");
                foreach (var (sx, sy) in ((double, double)[])[(74, 60), (92, 58), (110, 58), (128, 60), (82, 76), (100, 74), (118, 76), (90, 92), (110, 92), (100, 104)])
                    a.Dot(sx, sy, 1.6, 2.6);
                break;

            case Fruit.Pineapple:
                a.Path("M104 46 L150 18 L114 50 Z");
                a.Path("M86 50 L64 28 L92 42 Z");
                a.Path("M92 50 L84 14 L96 30 L100 2 L106 28 L120 10 L112 50 Z");
                a.Ellipse(100, 80, 30, 33);
                a.Path("M82 56 L122 96 M74 74 L110 110 M98 50 L126 78 M76 98 L116 58 M90 110 L126 74 M72 82 L100 54", detail: true, open: true);
                break;

            case Fruit.Watermelon:
                // A slice with a bite out of its right-hand side.
                a.Path("M30 50 L132 50 A8 8 0 0 0 148 50 A8 8 0 0 0 164 50 L170 50 A70 60 0 0 1 30 50 Z");
                a.Path("M42 56 A58 48 0 0 0 158 56", detail: true, open: true);
                foreach (var (sx, sy) in ((double, double)[])[(64, 64), (84, 74), (104, 78), (124, 72), (78, 90), (100, 94), (120, 88), (92, 62)])
                    a.Dot(sx, sy, 2, 3.2);
                break;

            case Fruit.Cherry:
                a.Path("M74 72 Q82 40 116 14", open: true);
                a.Path("M132 78 Q126 44 116 14", open: true);
                a.Leaf(116, 14, 160, 10);
                a.Circle(72, 88, 22);
                a.Circle(132, 92, 20);
                a.Path("M60 82 Q62 74 70 72", detail: true, open: true);
                a.Path("M122 86 Q124 79 131 77", detail: true, open: true);
                break;

            case Fruit.Lemon:
                a.Leaf(120, 30, 160, 12);
                a.Path("M34 64 Q40 56 50 54 Q70 26 100 26 Q130 26 150 54 Q160 56 166 64 Q160 72 150 74 Q130 102 100 102 Q70 102 50 74 Q40 72 34 64 Z");
                foreach (var (dx, dy) in ((double, double)[])[(76, 48), (98, 40), (122, 50), (130, 74), (86, 80), (108, 88), (66, 66)])
                    a.Dot(dx, dy, 1.4, 1.4);
                a.Path("M62 56 Q70 42 84 38", detail: true, open: true);
                break;

            case Fruit.Coconut:
                a.Circle(96, 66, 44);
                a.Path("M62 44 Q54 66 62 90 M74 30 Q62 62 74 104 M92 24 Q84 60 94 110", detail: true, open: true);
                a.Dot(114, 42, 4, 5);
                a.Dot(130, 50, 4, 5);
                a.Dot(120, 60, 4, 5);
                break;

            case Fruit.Pomegranate:
                a.Leaf(112, 30, 156, 22);
                a.Path("M88 36 L84 16 L94 26 L100 12 L106 26 L116 16 L112 36 Z");
                a.Circle(100, 72, 40);
                a.Path("M74 60 Q76 48 86 42", detail: true, open: true);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(fruit), fruit, null);
        }
    }
}

public sealed class FruitPictures : IPictureSet
{
    public string Theme => "fruits";
    public string Plural => "fruits";

    public IReadOnlyList<Picture> All { get; } = Fruits.All
        .Select(f => new Picture(Fruits.Name(f), (box, width, flip) => Fruits.Draw(f, box, width, flip)))
        .ToList();
}
