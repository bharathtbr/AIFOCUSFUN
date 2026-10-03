using FocusFun.Core.Svg;

namespace FocusFun.Core.Art;

using static SvgWriter;

public enum Animal
{
    Cat,
    Dog,
    Cow,
    Elephant,
    Lion,
    Giraffe,
    Horse,
    Pig,
    Rabbit,
    Duck,
    Fish,
    Turtle,
    Hen,
    Snail,
    Bear,
}

/// <summary>
/// Original thick-outline animal line art in the same style as <see cref="Vehicles"/>: side view, facing right,
/// drawn on a 200 x 120 canvas (ground at y = 114) and scaled into any box. White fills keep every shape colourable.
/// </summary>
public static class Animals
{
    public const double CanvasWidth = LineArt.CanvasWidth;
    public const double CanvasHeight = LineArt.CanvasHeight;

    public static IReadOnlyList<Animal> All { get; } = Enum.GetValues<Animal>();

    public static string Name(Animal animal) => animal.ToString().ToLowerInvariant();

    /// <summary>Draws <paramref name="animal"/> centred in <paramref name="box"/>, keeping its aspect ratio.</summary>
    /// <param name="lineWidth">Outline thickness in page points, independent of the drawing's size.</param>
    public static string Draw(Animal animal, Box box, double lineWidth = 2.5, bool flip = false) =>
        LineArt.Place(box, lineWidth, flip, art => Sketch(animal, art));

    private static void Sketch(Animal animal, LineArt a)
    {
        switch (animal)
        {
            case Animal.Cat:
                a.Path("M50 72 Q24 70 22 44 Q21 30 30 30 Q37 30 36 42 Q37 58 54 62 Z");
                foreach (double lx in (double[])[54, 70, 104, 120]) a.Leg(lx, 84, 13);
                a.Ellipse(94, 78, 50, 20);
                a.Path("M146 28 L164 10 L169 40 Z");
                a.Path("M127 42 L131 10 L150 28 Z");
                a.Circle(148, 50, 24);
                a.Eye(158, 44);
                a.Path("M165 52 L172 52 L168.5 56 Z", detail: true);
                a.Path("M168.5 56 Q167 62 161 60", detail: true, open: true);
                a.Line(166, 58, 190, 54, detail: true);
                a.Line(166, 61, 190, 65, detail: true);
                break;

            case Animal.Dog:
                a.Path("M48 66 Q30 58 28 36 Q28 28 34 30 Q38 32 38 40 Q40 54 52 58 Z");
                foreach (double lx in (double[])[54, 70, 102, 118]) a.Leg(lx, 84, 13);
                a.Ellipse(92, 76, 48, 21);
                a.Rect(150, 46, 38, 22, 11);
                a.Circle(144, 48, 22);
                a.Dot(185, 51, 5, 4);
                a.Path("M187 62 Q178 66 166 60", detail: true, open: true);
                a.Eye(154, 40);
                a.Path("M130 30 Q118 32 120 58 Q122 70 132 64 Q140 48 140 32 Z");
                break;

            case Animal.Cow:
                a.Path("M32 56 Q20 64 22 90", open: true);
                a.Path("M22 88 Q14 100 20 106 Q28 102 26 88 Z");
                foreach (double lx in (double[])[38, 56, 110, 128])
                {
                    a.Leg(lx, 80, 16, rounded: false);
                    a.Line(lx, 106, lx + 16, 106, detail: true);
                }
                a.Ellipse(80, 92, 12, 8);
                a.Rect(28, 42, 126, 48, 20);
                a.Path("M48 50 Q62 44 70 54 Q74 68 60 70 Q44 68 48 50 Z", detail: true);
                a.Path("M98 62 Q112 54 122 62 Q126 76 112 78 Q96 76 98 62 Z", detail: true);
                a.Path("M156 24 Q148 8 160 6 Q158 14 164 22 Z");
                a.Path("M176 22 Q180 8 192 8 Q184 14 184 24 Z");
                a.Ellipse(144, 34, 12, 5.5, -18);
                a.Ellipse(192, 34, 12, 5.5, 18);
                a.Rect(150, 18, 36, 46, 16);
                a.Ellipse(168, 64, 20, 14);
                a.Dot(161, 64, 2.4, 3);
                a.Dot(175, 64, 2.4, 3);
                a.Eye(160, 36);
                a.Eye(176, 36);
                break;

            case Animal.Elephant:
                a.Path("M34 60 Q24 68 26 86", open: true);
                a.Path("M26 84 Q20 94 24 98 Q30 94 28 84 Z");
                foreach (double lx in (double[])[42, 66, 110, 134])
                {
                    a.Leg(lx, 78, 24, rounded: false);
                    a.Path($"M{N(lx + 5)} 113 Q{N(lx + 12)} 106 {N(lx + 19)} 113", detail: true, open: true);
                }
                a.Ellipse(92, 64, 60, 34);
                a.Path("M166 48 Q190 56 190 84 L190 100 Q190 106 196 104 L198 110 Q182 118 180 100 L180 86 Q178 72 160 68 Z");
                a.Circle(150, 50, 28);
                a.Path("M130 28 Q108 32 108 60 Q110 88 134 84 Q148 80 146 56 Q146 32 130 28 Z");
                a.Eye(162, 42);
                a.Path("M158 60 Q164 66 170 62", detail: true, open: true);
                break;

            case Animal.Lion:
                a.Path("M42 74 Q18 76 20 50", open: true);
                a.Ellipse(20, 46, 6, 8);
                foreach (double lx in (double[])[52, 68, 102, 118]) a.Leg(lx, 84, 14);
                a.Ellipse(90, 78, 50, 20);
                a.Scallop(146, 50, 30, 12, 7);
                a.Circle(137, 34, 6.5);
                a.Circle(163, 34, 6.5);
                a.Circle(150, 52, 20);
                a.Eye(143, 47, 3.5);
                a.Eye(157, 47, 3.5);
                a.Path("M146 55 L154 55 L150 59 Z", detail: true);
                a.Path("M150 59 Q148 64 143 63 M150 59 Q152 64 157 63", detail: true, open: true);
                break;

            case Animal.Giraffe:
                a.Path("M44 54 Q34 62 34 82", open: true);
                a.Path("M34 80 Q28 90 32 94 Q38 90 36 80 Z");
                foreach (double lx in (double[])[50, 64, 96, 110]) a.Leg(lx, 64, 10, rounded: false);
                a.Path("M100 58 L134 26 L150 34 L124 70 Z");
                a.Ellipse(80, 62, 40, 16);
                a.Line(144, 20, 142, 8);
                a.Line(152, 18, 153, 6);
                a.Circle(142, 6, 3.5);
                a.Circle(153, 5, 3.5);
                a.Ellipse(138, 22, 8, 3.5, -30);
                a.Ellipse(156, 28, 18, 10, 20);
                a.Eye(154, 24, 3);
                a.Dot(170, 34, 1.6, 1.6);
                foreach (var (sx, sy) in ((double, double)[])[(52, 56), (68, 52), (84, 54), (100, 58), (62, 66), (80, 66)])
                    a.Rect(sx, sy, 10, 8, 3, detail: true);
                foreach (var (sx, sy) in ((double, double)[])[(122, 44), (132, 34), (114, 56)])
                    a.Rect(sx, sy, 8, 7, 2.5, detail: true);
                break;

            case Animal.Horse:
                a.Path("M42 62 Q22 64 20 86 Q18 100 26 106 Q30 90 38 80 Q44 72 48 70 Z");
                foreach (double lx in (double[])[50, 64, 108, 122])
                {
                    a.Leg(lx, 76, 11, rounded: false);
                    a.Line(lx, 106, lx + 11, 106, detail: true);
                }
                a.Path("M141 10 L145 0 L154 8 Z");
                a.Path("M142 8 Q128 8 130 18 Q118 20 121 30 Q108 34 114 43 Q102 48 110 58 L128 40 Z");
                a.Path("M110 58 Q122 30 136 14 Q144 4 156 8 L184 30 Q194 38 189 48 Q183 56 172 52 L158 44 Q146 58 132 72 Z");
                a.Ellipse(88, 68, 50, 20);
                a.Eye(156, 20);
                a.Dot(184, 40, 2, 2);
                a.Path("M176 50 Q181 53 186 50", detail: true, open: true);
                break;

            case Animal.Pig:
                a.Path("M42 66 Q30 62 32 54 Q36 48 40 54 Q42 60 34 60 Q26 60 26 52", open: true);
                foreach (double lx in (double[])[60, 76, 110, 126]) a.Leg(lx, 86, 14);
                a.Ellipse(96, 72, 56, 30);
                a.Circle(152, 58, 24);
                a.Path("M144 38 Q152 20 170 26 Q166 40 154 46 Z");
                a.Ellipse(178, 62, 8, 12);
                a.Dot(178, 57, 1.8, 2.8);
                a.Dot(178, 67, 1.8, 2.8);
                a.Eye(160, 50);
                a.Path("M158 72 Q164 77 170 73", detail: true, open: true);
                break;

            case Animal.Rabbit:
                a.Circle(58, 88, 12);
                a.Ellipse(94, 84, 40, 28);
                a.Path("M62 90 Q64 70 84 72 Q100 76 100 96", detail: true, open: true);
                a.Ellipse(98, 108, 26, 6);
                a.Ellipse(134, 108, 10, 6);
                a.Ellipse(128, 20, 8, 22, -14);
                a.Ellipse(128, 21, 3.5, 15, -14, detail: true);
                a.Ellipse(148, 20, 8, 22, 12);
                a.Ellipse(148, 21, 3.5, 15, 12, detail: true);
                a.Circle(142, 58, 22);
                a.Eye(150, 52);
                a.Dot(164, 60, 2.5, 2.2);
                a.Path("M163 62 Q162 68 156 67", detail: true, open: true);
                a.Line(162, 63, 182, 58, detail: true);
                a.Line(162, 65, 182, 70, detail: true);
                break;

            case Animal.Duck:
                a.Line(94, 100, 94, 112);
                a.Line(118, 100, 118, 112);
                a.Path("M82 114 L94 108 L106 114 Z");
                a.Path("M106 114 L118 108 L130 114 Z");
                a.Rect(134, 44, 22, 36, 8);
                a.Path("M28 48 Q44 66 62 64 L128 64 Q164 64 164 86 Q162 106 128 106 L72 106 Q34 106 28 48 Z");
                a.Path("M160 36 Q180 34 192 42 Q192 50 178 50 L160 50 Z");
                a.Line(166, 45, 186, 45, detail: true);
                a.Circle(146, 38, 20);
                a.Eye(150, 32);
                a.Path("M70 72 Q100 64 128 76 Q118 98 88 96 Q74 90 70 72 Z", detail: true);
                a.Path("M84 78 Q100 74 116 82 M88 86 Q102 84 112 90", detail: true, open: true);
                break;

            case Animal.Fish:
                a.Path("M64 62 L20 30 Q30 62 20 94 Z");
                a.Path("M84 36 Q104 10 138 32 Z");
                a.Path("M100 88 Q110 106 126 90 Z");
                a.Ellipse(112, 62, 58, 32);
                a.Path("M146 38 Q134 62 146 86", detail: true, open: true);
                a.Path("M122 60 Q108 52 102 68 Q112 72 122 60 Z", detail: true);
                foreach (var (sx, sy) in ((double, double)[])[(76, 50), (90, 44), (90, 62), (76, 70), (104, 80)])
                    a.Path($"M{N(sx)} {N(sy)} Q{N(sx + 7)} {N(sy + 6)} {N(sx)} {N(sy + 12)}", detail: true, open: true);
                a.Eye(156, 52, 5);
                a.Path("M170 66 Q165 70 161 66", detail: true, open: true);
                a.Circle(184, 34, 4.5, detail: true);
                a.Circle(192, 18, 3, detail: true);
                break;

            case Animal.Turtle:
                a.Path("M34 91 L14 95 L34 98 Z");
                a.Rect(46, 90, 22, 23, 9);
                a.Rect(132, 90, 22, 23, 9);
                a.Rect(150, 76, 30, 14, 7);
                a.Circle(182, 74, 14);
                a.Path("M34 92 Q36 30 100 30 Q164 30 166 92 Z");
                a.Rect(28, 88, 144, 11, 5.5);
                a.Path("M86 48 L114 48 L124 64 L114 80 L86 80 L76 64 Z", detail: true);
                a.Path("M86 48 L78 34 M114 48 L122 34 M76 64 L38 70 M124 64 L162 70 M86 80 L80 88 M114 80 L120 88", detail: true, open: true);
                a.Eye(186, 70, 3.5);
                a.Path("M188 80 Q192 81 195 78", detail: true, open: true);
                break;

            case Animal.Hen:
                foreach (double lx in (double[])[88, 108])
                {
                    a.Line(lx, 96, lx, 110);
                    a.Path($"M{N(lx - 8)} 113 L{N(lx)} 110 L{N(lx + 8)} 113 M{N(lx)} 110 L{N(lx + 2)} 113", open: true);
                }
                a.Path("M68 58 Q36 58 28 30 Q46 40 50 34 Q42 22 48 12 Q60 30 64 30 Q64 20 72 14 Q78 40 82 52 Z");
                a.Ellipse(98, 72, 44, 30);
                a.Path("M124 28 Q122 14 132 16 Q134 6 142 9 Q148 2 153 13 Q160 17 154 28 Z");
                a.Path("M154 36 L172 42 L154 48 Z");
                a.Ellipse(157, 56, 5, 8);
                a.Circle(140, 40, 18);
                a.Eye(146, 36);
                a.Path("M76 68 Q100 60 122 72 Q112 92 90 92 Q78 86 76 68 Z", detail: true);
                a.Path("M88 74 Q102 70 114 78 M92 82 Q104 80 112 86", detail: true, open: true);
                break;

            case Animal.Snail:
                a.Line(170, 52, 164, 22);
                a.Line(184, 50, 188, 20);
                a.Circle(164, 20, 5);
                a.Circle(188, 18, 5);
                a.Dot(165, 20, 2, 2);
                a.Dot(189, 18, 2, 2);
                a.Path("M14 112 Q20 98 44 98 L156 98 Q162 98 164 88 L166 60 Q168 48 180 48 Q192 48 192 60 L190 100 Q188 112 176 112 Z");
                a.Path("M178 70 Q183 75 188 70", detail: true, open: true);
                a.Circle(96, 60, 40);
                a.Path("M96 60 A6 6 0 0 1 108 60 A12 12 0 0 1 84 60 A18 18 0 0 1 120 60 A24 24 0 0 1 72 60 A30 30 0 0 1 132 60", detail: true, open: true);
                break;

            case Animal.Bear:
                a.Circle(34, 68, 7);
                foreach (double lx in (double[])[44, 64, 104, 124]) a.Leg(lx, 80, 22);
                a.Path("M34 74 Q30 46 64 42 Q92 40 108 32 Q138 22 150 48 Q156 68 150 82 Q146 90 134 90 L50 90 Q34 90 34 74 Z");
                a.Circle(144, 38, 9);
                a.Ellipse(176, 64, 16, 11);
                a.Circle(156, 58, 23);
                a.Path("M140 42 Q143 35 149 37", detail: true, open: true);
                a.Dot(190, 59, 4.5, 3.5);
                a.Path("M190 68 Q184 72 176 69", detail: true, open: true);
                a.Eye(162, 50);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(animal), animal, null);
        }
    }
}

public sealed class AnimalPictures : IPictureSet
{
    public string Theme => "animals";
    public string Plural => "animals";

    public IReadOnlyList<Picture> All { get; } = Animals.All
        .Select(a => new Picture(Animals.Name(a), (box, width, flip) => Animals.Draw(a, box, width, flip)))
        .ToList();
}
