using System.Text;
using FocusFun.Core.Art;
using FocusFun.Core.Svg;

namespace FocusFun.Core.Generators;

using static SvgWriter;

/// <summary>Join numbered dots around a vehicle's outline; wheels are drawn in so the shape is recognisable.</summary>
public sealed class DotToDotGenerator : ISheetGenerator
{
    private readonly record struct Pt(double X, double Y);

    private sealed record Shape(Vehicle Vehicle, Pt[] Outline, (double X, double Y, double R)[] Wheels);

    /// <summary>Clockwise outlines on the 200 x 120 vehicle canvas.</summary>
    private static readonly Shape[] Shapes =
    [
        new(Vehicle.Car,
            P(10, 98, 10, 74, 20, 64, 52, 60, 70, 38, 82, 34, 128, 34, 141, 40, 160, 60, 182, 64, 192, 76, 192, 98),
            [(52, 100, 15), (152, 100, 15)]),
        new(Vehicle.Bus,
            P(6, 98, 6, 30, 14, 22, 186, 22, 194, 30, 194, 98),
            [(42, 100, 14), (150, 100, 14)]),
        new(Vehicle.FireTruck,
            P(8, 98, 8, 50, 150, 50, 150, 40, 178, 40, 190, 46, 194, 58, 196, 98),
            [(36, 100, 14), (66, 100, 14), (172, 100, 14)]),
        new(Vehicle.DumpTruck,
            P(12, 96, 12, 84, 18, 84, 8, 28, 140, 28, 136, 64, 146, 64, 146, 38, 176, 38, 188, 44, 192, 54, 194, 96),
            [(42, 100, 15), (102, 100, 15), (168, 100, 15)]),
        new(Vehicle.GarbageTruck,
            P(6, 100, 8, 50, 12, 42, 18, 40, 52, 40, 52, 24, 178, 24, 178, 28, 194, 44, 194, 100),
            [(30, 102, 13), (128, 102, 13), (158, 102, 13)]),
        new(Vehicle.SteamTrain,
            P(10, 108, 30, 88, 30, 62, 38, 50, 44, 46, 38, 18, 62, 18, 56, 46, 82, 46, 86, 34, 94, 30, 102, 34, 106, 46,
              132, 46, 132, 25, 124, 25, 124, 16, 192, 16, 192, 25, 184, 25, 184, 86, 190, 86, 190, 96, 30, 96, 30, 108),
            [(56, 100, 14), (96, 100, 14), (158, 100, 14)]),
        new(Vehicle.Airplane,
            P(8, 20, 28, 20, 58, 56, 168, 56, 190, 60, 196, 70, 190, 80, 168, 84, 122, 84, 102, 112, 84, 112, 90, 84,
              32, 84, 16, 80, 12, 72, 22, 58),
            []),
    ];

    /// <summary>Canvas y at or below which an outline edge counts as the vehicle's underside.</summary>
    private const double BottomY = 94;

    public SheetType Type => SheetType.DotToDot;

    public static (int Dots, int Step) Numbering(Level level) => level switch
    {
        Level.Easy => (15, 1),
        Level.Medium => (30, 1),
        _ => (25, 2), // 2, 4, 6 … 50
    };

    /// <summary>An image outline needs at least this many corners to make a recognisable dot-to-dot.</summary>
    private const int MinImageCorners = 8;

    public SheetContent Generate(SheetRequest request, Box box, SeededRandom random)
    {
        var (count, step) = Numbering(request.Level);
        var images = ThemeText.ShapedImages(request, s => s.Outline.Count >= MinImageCorners);
        if (images.Count > 0) return ImageDots(images[random.Next(images.Count)], count, step, box);

        var shape = Shapes[random.Next(Shapes.Length)];
        double scale = Math.Min(box.Width / Vehicles.CanvasWidth, box.Height / Vehicles.CanvasHeight) * 0.92;
        double ox = box.CenterX - Vehicles.CanvasWidth * scale / 2;
        double oy = box.CenterY - Vehicles.CanvasHeight * scale / 2;
        Pt ToPage(Pt p) => new(ox + p.X * scale, oy + p.Y * scale);

        var dots = Resample(shape.Outline, count, BottomY).Select(ToPage).ToArray();
        var wheels = shape.Wheels.Select(wh => (C: ToPage(new Pt(wh.X, wh.Y)), R: wh.R * scale)).ToArray();

        var common = new SvgWriter();
        foreach (var (c, r) in wheels)
        {
            common.Circle(c.X, c.Y, r, 2.5, "#fff");
            common.Circle(c.X, c.Y, r * 0.38, 1.6);
        }

        var (body, key) = Draw(dots, step, common.ToString(), common.ToString());
        return new SheetContent($"Dot-to-Dot: {Title(Vehicles.Name(shape.Vehicle))}", Instruction(count, step), body, key);
    }

    /// <summary>
    /// Dots around an approved image's traced outline, starting at its top. The child sees only the dots; the answer key
    /// shows the whole picture, so it doubles as a colouring page once joined.
    /// </summary>
    private static SheetContent ImageDots(PictureAsset asset, int count, int step, Box box)
    {
        var art = new Box(box.X + box.Width * 0.06, box.Y + box.Height * 0.06, box.Width * 0.88, box.Height * 0.88);
        var (ox, oy, scale) = ImagePictureSet.Placement(asset, art);
        var outline = ShapeAnalyzer.Simplify(asset.Shape!.Outline, Math.Max(asset.Width, asset.Height) * 0.012)
            .Select(p => new Pt(ox + p.X * scale, oy + p.Y * scale)).ToArray();

        var dots = Resample(outline, count, double.MaxValue);
        int top = Enumerable.Range(0, dots.Count).MinBy(i => dots[i].Y);
        dots = [.. dots.Skip(top), .. dots.Take(top)];

        var (body, key) = Draw(dots.ToArray(), step, string.Empty, ImagePictureSet.Draw(asset, art));
        return new SheetContent($"Dot-to-Dot: {Title(asset.Word)}", Instruction(count, step), body, key);
    }

    /// <summary>Numbered dots (with a star at the start) for the sheet; the joined outline over <paramref name="keyArt"/> for the answer key.</summary>
    private static (string Body, string Key) Draw(Pt[] dots, int step, string common, string keyArt)
    {
        int count = dots.Length;
        var body = new SvgWriter().Raw(common);
        double fontSize = count > 20 ? 11 : 14;
        var centroid = new Pt(dots.Average(d => d.X), dots.Average(d => d.Y));
        for (int i = 0; i < dots.Length; i++)
        {
            var d = dots[i];
            body.Circle(d.X, d.Y, i == 0 ? 5 : 3.5, 1, "#000");
            var label = LabelPosition(dots, i, centroid, fontSize);
            body.Text(label.X, label.Y + fontSize * 0.35, ((i + 1) * step).ToString(), fontSize, "middle", bold: i == 0);
        }
        body.Raw(Icons.Star(dots[0].X - 14, dots[0].Y - 16, 7, 1.5));

        var outline = new StringBuilder($"M{N(dots[0].X)} {N(dots[0].Y)}");
        foreach (var d in dots.Skip(1)) outline.Append($"L{N(d.X)} {N(d.Y)}");
        outline.Append('Z');
        var key = new SvgWriter().Raw(keyArt).Path(outline.ToString(), 3);
        return (body.ToString(), key.ToString());
    }

    private static string Instruction(int count, int step)
    {
        int last = count * step;
        return step == 1 ? $"Join the dots from 1 to {last}!" : $"Count by {step}s and join the dots to {last}!";
    }

    private static string Title(string name) => ThemeText.Capitalize(name);

    /// <summary>
    /// Returns exactly <paramref name="count"/> points on the closed outline, starting at its first point:
    /// drops the least important corners when there are too many, and splits the longest edges when there are too few.
    /// </summary>
    /// <param name="bottomY">Edges at or below this y are split last (a vehicle's underside hides behind its wheels).</param>
    private static List<Pt> Resample(Pt[] outline, int count, double bottomY)
    {
        var pts = outline.ToList();
        while (pts.Count > count)
        {
            int weakest = Enumerable.Range(1, pts.Count - 1).MinBy(i => TriangleArea(pts[i - 1], pts[i], pts[(i + 1) % pts.Count]));
            pts.RemoveAt(weakest);
        }
        while (pts.Count < count)
        {
            // The bottom edge sits behind the wheels, so prefer splitting any other edge.
            double Weight(int i)
            {
                var (a, b) = (pts[i], pts[(i + 1) % pts.Count]);
                return Distance(a, b) * (a.Y >= bottomY && b.Y >= bottomY ? 0.25 : 1);
            }
            int longest = Enumerable.Range(0, pts.Count).MaxBy(Weight);
            var a = pts[longest];
            var b = pts[(longest + 1) % pts.Count];
            pts.Insert(longest + 1, new Pt((a.X + b.X) / 2, (a.Y + b.Y) / 2));
        }
        return pts;
    }

    /// <summary>Places a label just outside the outline, along the corner's outward bisector.</summary>
    private static Pt LabelPosition(Pt[] dots, int i, Pt centroid, double fontSize)
    {
        var prev = dots[(i - 1 + dots.Length) % dots.Length];
        var next = dots[(i + 1) % dots.Length];
        var d = dots[i];

        // Normals of the two edges meeting at this dot (outline is clockwise in screen space, so (dy, -dx) points out).
        var (n1x, n1y) = Normal(prev, d);
        var (n2x, n2y) = Normal(d, next);
        double nx = n1x + n2x, ny = n1y + n2y;
        double len = Math.Sqrt(nx * nx + ny * ny);
        if (len < 1e-6) { nx = d.X - centroid.X; ny = d.Y - centroid.Y; len = Math.Sqrt(nx * nx + ny * ny); }

        // Labels on the underside would land on the wheels, so put those inside the outline instead.
        if (ny / len > 0.6) { nx = -nx; ny = -ny; }

        double offset = fontSize * 0.95;
        return new Pt(d.X + nx / len * offset, d.Y + ny / len * offset);
    }

    private static (double X, double Y) Normal(Pt a, Pt b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        return len < 1e-6 ? (0, 0) : (dy / len, -dx / len);
    }

    private static double TriangleArea(Pt a, Pt b, Pt c) => Math.Abs((b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y)) / 2;

    private static double Distance(Pt a, Pt b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static Pt[] P(params double[] xy) => Enumerable.Range(0, xy.Length / 2).Select(i => new Pt(xy[2 * i], xy[2 * i + 1])).ToArray();

    /// <summary>Exposed for tests: the number of dots each vehicle gets at every level.</summary>
    public static IEnumerable<int> DotCounts(Level level)
    {
        var (count, _) = Numbering(level);
        return Shapes.Select(s => Resample(s.Outline, count, BottomY).Count);
    }
}
