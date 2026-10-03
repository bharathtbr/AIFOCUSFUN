using System.Text;
using FocusFun.Core.Svg;

namespace FocusFun.Core.Art;

using static SvgWriter;

public enum Vehicle
{
    Car,
    Bus,
    GarbageTruck,
    Bulldozer,
    SteamTrain,
    FireTruck,
    DumpTruck,
    Airplane,
    Helicopter,
    Autorickshaw,
}

public enum VehiclePart
{
    Body,
    Window,
    Wheel,
    Extra,
}

public readonly record struct PartLabel(VehiclePart Part, double X, double Y);

/// <summary>
/// Original thick-outline vehicle line art, drawn in side view on a 200 x 120 canvas (ground at y = 114)
/// and scaled into any box. White fills keep overlaps clean and leave every shape colourable.
/// </summary>
public static class Vehicles
{
    public const double CanvasWidth = 200;
    public const double CanvasHeight = 120;

    public static IReadOnlyList<Vehicle> All { get; } = Enum.GetValues<Vehicle>();

    public static string Name(Vehicle v) => v switch
    {
        Vehicle.GarbageTruck => "garbage truck",
        Vehicle.SteamTrain => "steam train",
        Vehicle.FireTruck => "fire truck",
        Vehicle.DumpTruck => "dump truck",
        _ => v.ToString().ToLowerInvariant(),
    };

    /// <summary>Colour a child would expect for each vehicle's main body (used by colour-by-number keys).</summary>
    public static string BodyColor(Vehicle v) => v switch
    {
        Vehicle.Car or Vehicle.FireTruck => "red",
        Vehicle.Bus or Vehicle.Bulldozer => "yellow",
        Vehicle.GarbageTruck or Vehicle.Autorickshaw => "green",
        Vehicle.DumpTruck or Vehicle.Helicopter => "orange",
        _ => "blue",
    };

    public static string PartColor(Vehicle v, VehiclePart part) => part switch
    {
        VehiclePart.Window => "blue",
        VehiclePart.Wheel => "black",
        VehiclePart.Extra => v switch
        {
            Vehicle.Autorickshaw or Vehicle.FireTruck => "yellow",
            Vehicle.Airplane => "red",
            _ => "black",
        },
        _ => BodyColor(v),
    };

    /// <summary>A point inside each colourable region (body, windows, wheels…) in page coordinates, matching <see cref="Draw"/>.</summary>
    public static IReadOnlyList<PartLabel> Parts(Vehicle vehicle, Box box, bool flip = false)
    {
        double scale = Math.Min(box.Width / CanvasWidth, box.Height / CanvasHeight);
        double x = box.CenterX - CanvasWidth * scale / 2;
        double y = box.CenterY - CanvasHeight * scale / 2;
        var art = new Canvas(1);
        Sketch(vehicle, art);

        var labels = PartPoints.GetValueOrDefault(vehicle, []).Select(p => new PartLabel(p.Part, p.X, p.Y))
            .Concat(art.WheelLabels.Select(w => new PartLabel(VehiclePart.Wheel, w.X, w.Y)));
        return labels.Select(l => l with
        {
            X = x + (flip ? CanvasWidth - l.X : l.X) * scale,
            Y = y + l.Y * scale,
        }).ToList();
    }

    /// <summary>Hand-placed label points (canvas units) inside each region; wheels are added automatically.</summary>
    private static readonly Dictionary<Vehicle, PartLabel[]> PartPoints = new()
    {
        [Vehicle.Car] = [new(VehiclePart.Body, 80, 82), new(VehiclePart.Window, 98, 51), new(VehiclePart.Window, 131, 52)],
        [Vehicle.Bus] = [new(VehiclePart.Body, 96, 88), new(VehiclePart.Window, 48.5, 44), new(VehiclePart.Window, 98.5, 44), new(VehiclePart.Window, 148.5, 44)],
        [Vehicle.GarbageTruck] = [new(VehiclePart.Body, 107, 60), new(VehiclePart.Body, 28, 84), new(VehiclePart.Window, 27, 57), new(VehiclePart.Extra, 186, 72)],
        [Vehicle.Bulldozer] = [new(VehiclePart.Body, 110, 72), new(VehiclePart.Window, 117, 36), new(VehiclePart.Extra, 16, 82)],
        [Vehicle.SteamTrain] = [new(VehiclePart.Body, 82, 68), new(VehiclePart.Body, 158, 74), new(VehiclePart.Window, 158, 43), new(VehiclePart.Extra, 50, 32)],
        [Vehicle.FireTruck] = [new(VehiclePart.Body, 34, 74), new(VehiclePart.Body, 78, 74), new(VehiclePart.Body, 124, 74), new(VehiclePart.Body, 172, 82), new(VehiclePart.Window, 170, 55), new(VehiclePart.Extra, 170, 36)],
        [Vehicle.DumpTruck] = [new(VehiclePart.Body, 60, 56), new(VehiclePart.Body, 170, 80), new(VehiclePart.Window, 168, 53)],
        [Vehicle.Airplane] = [new(VehiclePart.Body, 120, 77), new(VehiclePart.Body, 27, 38), new(VehiclePart.Extra, 100, 94)],
        [Vehicle.Helicopter] = [new(VehiclePart.Body, 88, 76), new(VehiclePart.Window, 140, 61)],
        [Vehicle.Autorickshaw] = [new(VehiclePart.Body, 120, 70), new(VehiclePart.Body, 90, 90), new(VehiclePart.Extra, 90, 19)],
    };

    /// <summary>Draws <paramref name="vehicle"/> centred in <paramref name="box"/>, keeping its aspect ratio.</summary>
    /// <param name="lineWidth">Outline thickness in page points, independent of the drawing's size.</param>
    public static string Draw(Vehicle vehicle, Box box, double lineWidth = 2.5, bool flip = false)
    {
        double scale = Math.Min(box.Width / CanvasWidth, box.Height / CanvasHeight);
        double x = box.CenterX - CanvasWidth * scale / 2;
        double y = box.CenterY - CanvasHeight * scale / 2;
        var art = new Canvas(lineWidth / scale);
        Sketch(vehicle, art);

        string mirror = flip ? $" translate({N(CanvasWidth)} 0) scale(-1 1)" : string.Empty;
        return $"<g transform=\"translate({N(x)} {N(y)}) scale({N(scale)}){mirror}\" fill=\"#fff\" stroke=\"#000\" stroke-linejoin=\"round\" stroke-linecap=\"round\">{art}</g>";
    }

    private static void Sketch(Vehicle vehicle, Canvas a)
    {
        switch (vehicle)
        {
            case Vehicle.Car:
                a.Path("M10 98 L10 74 Q10 65 20 64 L52 60 L70 38 Q74 34 82 34 L128 34 Q136 34 141 40 L160 60 L182 64 Q192 66 192 76 L192 98 Z");
                a.Path("M78 58 L88 42 L110 42 L110 58 Z", detail: true);
                a.Path("M117 58 L117 42 L131 42 L146 58 Z", detail: true);
                a.Line(113, 62, 113, 94);
                a.Line(122, 70, 132, 70);
                a.Circle(186, 74, 3, detail: true);
                a.Wheel(52, 100, 15);
                a.Wheel(152, 100, 15);
                break;

            case Vehicle.Bus:
                a.Rect(6, 22, 188, 76, 10);
                for (int i = 0; i < 6; i++) a.Rect(14 + i * 25, 32, 19, 24, 3, detail: true);
                a.Rect(166, 32, 20, 58, 3, detail: true);
                a.Line(176, 32, 176, 90);
                a.Line(6, 68, 162, 68);
                a.Line(6, 76, 162, 76);
                a.Wheel(42, 100, 14);
                a.Wheel(150, 100, 14);
                break;

            case Vehicle.GarbageTruck:
                a.Rect(52, 24, 126, 72, 6);
                for (int x = 70; x <= 160; x += 15) a.Line(x, 32, x, 88);
                a.Path("M178 28 L194 44 L194 96 L178 96 Z");
                a.Path("M8 98 L8 50 Q8 40 18 40 L48 40 L48 98 Z");
                a.Rect(14, 46, 26, 22, 4, detail: true);
                a.Rect(6, 92, 190, 8, 3);
                a.Wheel(30, 102, 13);
                a.Wheel(128, 102, 13);
                a.Wheel(158, 102, 13);
                break;

            case Vehicle.Bulldozer:
                a.Rect(88, 18, 58, 40, 4);
                a.Rect(96, 24, 42, 24, 3, detail: true);
                a.Rect(60, 34, 8, 24, 2);
                a.Rect(48, 56, 122, 30, 4);
                a.Line(24, 66, 48, 66);
                a.Line(24, 94, 36, 94);
                a.Path("M6 110 Q0 80 12 50 L26 50 L26 110 Z");
                a.Rect(30, 86, 150, 28, 14);
                for (int x = 44; x <= 166; x += 24) a.Circle(x, 100, 7, detail: true);
                break;

            case Vehicle.SteamTrain:
                a.Rect(30, 44, 104, 44, 20);
                a.Path("M44 46 L38 18 L62 18 L56 46 Z");
                a.Path("M82 46 Q82 30 94 30 Q106 30 106 46 Z");
                a.Rect(132, 24, 52, 64, 3);
                a.Rect(124, 16, 68, 9, 3);
                a.Rect(142, 32, 32, 22, 3, detail: true);
                a.Path("M30 88 L10 108 L30 108 Z");
                a.Rect(20, 86, 170, 10, 3);
                a.Wheel(56, 100, 14);
                a.Wheel(96, 100, 14);
                a.Wheel(158, 100, 14);
                a.Line(56, 100, 158, 100, detail: true);
                break;

            case Vehicle.FireTruck:
                a.Rect(8, 50, 144, 46, 4);
                a.Rect(16, 58, 36, 30, 3, detail: true);
                a.Rect(60, 58, 36, 30, 3, detail: true);
                a.Rect(104, 58, 40, 30, 3, detail: true);
                a.Line(14, 34, 146, 34);
                a.Line(14, 44, 146, 44);
                for (int x = 24; x <= 136; x += 14) a.Line(x, 34, x, 44, detail: true);
                a.Path("M150 98 L150 40 L178 40 Q192 40 194 58 L196 98 Z");
                a.Path("M158 48 L177 48 Q185 48 187 62 L158 62 Z", detail: true);
                a.Rect(163, 31, 14, 9, 2);
                a.Wheel(36, 100, 14);
                a.Wheel(66, 100, 14);
                a.Wheel(172, 100, 14);
                break;

            case Vehicle.DumpTruck:
                a.Path("M8 28 L140 28 L134 84 L18 84 Z");
                for (int x = 44; x <= 108; x += 32) a.Line(x, 34, x, 78);
                a.Path("M146 96 L146 38 L176 38 Q190 38 192 54 L194 96 Z");
                a.Path("M154 46 L173 46 Q181 46 183 60 L154 60 Z", detail: true);
                a.Rect(12, 84, 184, 12, 3);
                a.Wheel(42, 100, 15);
                a.Wheel(102, 100, 15);
                a.Wheel(168, 100, 15);
                break;

            case Vehicle.Airplane:
                a.Path("M24 58 L8 20 L28 20 L58 58 Z");
                a.Path("M12 72 Q12 58 32 56 L168 56 Q190 58 196 70 Q190 82 168 84 L32 84 Q14 82 12 72 Z");
                a.Path("M20 72 L50 72 L42 80 L12 80 Z");
                a.Path("M176 62 L186 62 L191 68 L178 68 Z", detail: true);
                for (int x = 62; x <= 150; x += 16) a.Circle(x, 66, 3.5, detail: true);
                a.Path("M86 74 L128 74 L102 112 L84 112 Z");
                break;

            case Vehicle.Helicopter:
                a.Path("M64 62 L12 56 L12 66 L64 78 Z");
                a.Circle(14, 52, 12, detail: true);
                a.Line(14, 40, 14, 64, detail: true);
                a.Path("M60 70 Q60 40 100 40 L128 40 Q162 40 162 70 Q162 94 130 94 L92 94 Q60 94 60 70 Z");
                a.Path("M126 48 Q152 50 155 70 L126 70 Z", detail: true);
                a.Line(112, 44, 112, 90, detail: true);
                a.Rect(104, 28, 10, 12, 2);
                a.Rect(26, 22, 162, 7, 3.5);
                a.Line(90, 94, 84, 110);
                a.Line(132, 94, 138, 110);
                a.Line(64, 110, 156, 110);
                break;

            case Vehicle.Autorickshaw:
                // Open-sided passenger cab under a rounded canopy, narrow nose, single front wheel.
                a.Path("M20 98 L20 64 Q20 56 30 56 L150 56 L176 62 Q186 65 186 78 L186 98 Z");
                a.Path("M26 58 L26 32 Q26 14 48 14 L132 14 Q156 14 166 36 L178 62 L166 62 L155 39 Q148 25 132 25 L50 25 Q37 25 37 38 L37 58 Z");
                a.Line(104, 25, 104, 56);
                a.Path("M46 56 L46 40 L94 40 L94 56", detail: true);
                a.Line(146, 42, 156, 56, detail: true);
                a.Line(20, 80, 150, 80, detail: true);
                a.Circle(180, 74, 4, detail: true);
                a.Wheel(58, 100, 14);
                a.Wheel(166, 100, 14);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(vehicle), vehicle, null);
        }
    }

    /// <summary>Collects shapes in canvas units; outlines use the main width, inner details a thinner one.</summary>
    private sealed class Canvas(double mainWidth)
    {
        private readonly StringBuilder _sb = new();
        private string Width(bool detail) => N(detail ? mainWidth * 0.65 : mainWidth);

        public void Path(string d, bool detail = false) =>
            _sb.Append($"<path d=\"{d}\" stroke-width=\"{Width(detail)}\"/>");

        public void Rect(double x, double y, double w, double h, double r = 0, bool detail = false) =>
            _sb.Append($"<rect x=\"{N(x)}\" y=\"{N(y)}\" width=\"{N(w)}\" height=\"{N(h)}\" rx=\"{N(r)}\" stroke-width=\"{Width(detail)}\"/>");

        public void Circle(double cx, double cy, double r, bool detail = false) =>
            _sb.Append($"<circle cx=\"{N(cx)}\" cy=\"{N(cy)}\" r=\"{N(r)}\" stroke-width=\"{Width(detail)}\"/>");

        public void Line(double x1, double y1, double x2, double y2, bool detail = false) =>
            _sb.Append($"<line x1=\"{N(x1)}\" y1=\"{N(y1)}\" x2=\"{N(x2)}\" y2=\"{N(y2)}\" stroke-width=\"{Width(detail)}\"/>");

        /// <summary>A point in each tyre ring, above the hub.</summary>
        public List<(double X, double Y)> WheelLabels { get; } = [];

        public void Wheel(double cx, double cy, double r)
        {
            Circle(cx, cy, r);
            Circle(cx, cy, r * 0.38, detail: true);
            WheelLabels.Add((cx, cy - r * 0.69));
        }

        public override string ToString() => _sb.ToString();
    }
}
