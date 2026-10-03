using System.Text;
using FocusFun.Core.Svg;

namespace FocusFun.Core.Art;

using static SvgWriter;

public enum SceneKind
{
    Sun,
    Cloud,
    Bird,
    Flyer,
    Tree,
    PineTree,
    TrafficLight,
    RoadVehicle,
}

/// <summary>
/// One thing in a scene, in scene-canvas units. Sky items are placed by their centre, grounded items by their bottom centre.
/// <paramref name="Alt"/> selects a variant (sun without rays, traffic light without its lamps) and <paramref name="Hidden"/> removes the item
/// while keeping its slot and bounds; both are used by spot-the-difference.
/// </summary>
public sealed record SceneItem(SceneKind Kind, double X, double Y, double Size, Vehicle Vehicle = Vehicle.Car, bool Flip = false, bool Alt = false, bool Hidden = false);

/// <summary>A scene as data, so it can be drawn plain (colouring), labelled (colour-by-number) or changed (spot the difference).</summary>
public sealed record SceneSpec(double Height, bool HasStreet, IReadOnlyList<SceneItem> Items);

/// <summary>A colour-by-number label: which colour the region is, and a point inside it (page coordinates).</summary>
public readonly record struct SceneLabel(string Color, double X, double Y);

/// <summary>Drawn scene plus labels and each item's bounds (page coordinates, same order as <see cref="SceneSpec.Items"/>).</summary>
public sealed record RenderedScene(string Svg, IReadOnlyList<SceneLabel> Labels, IReadOnlyList<Box> ItemBounds);

/// <summary>Builds and draws simple street scenes on a canvas 600 units wide (height varies with the page area).</summary>
public static class Scenes
{
    public const double Width = 600;

    public static readonly Vehicle[] RoadVehicles =
        [Vehicle.Car, Vehicle.Bus, Vehicle.GarbageTruck, Vehicle.FireTruck, Vehicle.DumpTruck, Vehicle.Autorickshaw, Vehicle.Bulldozer];

    private static double RoadTop(double h) => h * 0.72;
    private static double RoadBottom(double h) => h * 0.96;
    private static double RoadMid(double h) => (RoadTop(h) + RoadBottom(h)) / 2;
    private static double Horizon(double h) => h * 0.56;

    /// <summary>Canvas height that fills <paramref name="box"/> at full width.</summary>
    public static double HeightFor(Box box) => Width * box.Height / box.Width;

    /// <summary>One large vehicle and nothing else: the Easy colouring page.</summary>
    public static SceneSpec SingleVehicle(Vehicle vehicle, double height, bool flip = false) =>
        new(height, HasStreet: false, [new SceneItem(SceneKind.RoadVehicle, Width / 2, height * 0.5 + 520 * 0.3, 520, vehicle, flip)]);

    /// <summary>
    /// A street: road, hills, sun and clouds. <paramref name="busy"/> adds a second vehicle, a flyer, trees, a traffic light and birds.
    /// </summary>
    public static SceneSpec Street(SeededRandom random, double height, bool busy)
    {
        var items = new List<SceneItem>();
        double sky = Horizon(height);
        double ground = RoadTop(height) - 2;
        double lane = RoadMid(height) + 4;
        // Taller (full-page) scenes get bigger things so the sky is not left empty.
        double k = Math.Clamp(height / 330, 1, 1.45);

        items.Add(new SceneItem(SceneKind.Sun, 530, sky * 0.24, 80 * k));

        var cloudSlots = new List<double> { 85, 235, 385 };
        random.Shuffle(cloudSlots);
        int clouds = busy ? 3 : 2;
        foreach (double x in cloudSlots.Take(clouds))
            items.Add(new SceneItem(SceneKind.Cloud, x + random.Next(-10, 11), sky * (0.18 + random.NextDouble() * 0.12), Math.Min(128, (110 + random.Next(0, 18)) * k)));

        var roadVehicles = RoadVehicles.ToList();
        random.Shuffle(roadVehicles);
        if (busy)
        {
            items.Add(new SceneItem(SceneKind.Flyer, 150 + random.Next(0, 300), sky * 0.62, 130 * k, random.Next(2) == 0 ? Vehicle.Airplane : Vehicle.Helicopter, random.Next(2) == 0));
            items.Add(new SceneItem(SceneKind.Bird, 60 + random.Next(0, 40), sky * 0.58, 26));
            items.Add(new SceneItem(SceneKind.Bird, 470 + random.Next(0, 40), sky * 0.66, 22));

            items.Add(new SceneItem(random.Next(2) == 0 ? SceneKind.Tree : SceneKind.PineTree, 40, ground, 58 * k));
            items.Add(new SceneItem(SceneKind.TrafficLight, 300, ground, 34 * k));
            items.Add(new SceneItem(random.Next(2) == 0 ? SceneKind.Tree : SceneKind.PineTree, 560, ground, 58 * k));

            items.Add(new SceneItem(SceneKind.RoadVehicle, 170, lane, Math.Min(240, 190 * k), roadVehicles[0], random.Next(2) == 0));
            items.Add(new SceneItem(SceneKind.RoadVehicle, 430, lane, Math.Min(240, 190 * k), roadVehicles[1], random.Next(2) == 0));
        }
        else
        {
            items.Add(new SceneItem(SceneKind.Tree, 60, ground, 70 * k));
            items.Add(new SceneItem(SceneKind.RoadVehicle, 320, lane, Math.Min(330, 250 * k), roadVehicles[0], random.Next(2) == 0));
        }

        return new SceneSpec(height, HasStreet: true, items);
    }

    public static RenderedScene Render(SceneSpec spec, Box box, double lineWidth = 2.5)
    {
        double s = Math.Min(box.Width / Width, box.Height / spec.Height);
        double ox = box.CenterX - Width * s / 2, oy = box.CenterY - spec.Height * s / 2;
        double lw = lineWidth / s;
        Box ToPage(Box b) => new(ox + b.X * s, oy + b.Y * s, b.Width * s, b.Height * s);

        var svg = new StringBuilder();
        var labels = new List<(string Color, double X, double Y)>();   // canvas units
        var bounds = new Box[spec.Items.Count];
        var w = new Canvas(svg, lw, s);

        // Back to front: sky items, hills, grounded items, road, road vehicles.
        int[] order = Enumerable.Range(0, spec.Items.Count).OrderBy(i => Layer(spec.Items[i].Kind)).ToArray();
        bool streetDrawn = false;
        foreach (int i in order)
        {
            var item = spec.Items[i];
            if (spec.HasStreet && !streetDrawn && Layer(item.Kind) >= 2)
            {
                DrawHills(w, spec.Height);
                streetDrawn = true;
            }
            if (spec.HasStreet && item.Kind == SceneKind.RoadVehicle && !w.RoadDrawn)
                DrawRoad(w, spec.Height);
            // Hidden items are measured on a throwaway canvas so their bounds still exist for the answer key.
            bounds[i] = ToPage(item.Hidden ? ApproxBounds(item) : DrawItem(w, item, labels));
        }
        if (spec.HasStreet && !streetDrawn) DrawHills(w, spec.Height);
        if (spec.HasStreet && !w.RoadDrawn) DrawRoad(w, spec.Height);

        if (spec.HasStreet) AddBackgroundLabels(spec, labels);
        w.Rect(0, 0, Width, spec.Height, 12, fill: "none");

        string group = $"<g transform=\"translate({N(ox)} {N(oy)}) scale({N(s)})\" fill=\"#fff\" stroke=\"#000\" stroke-linejoin=\"round\" stroke-linecap=\"round\">{svg}</g>";
        var pageLabels = labels.Select(l => new SceneLabel(l.Color, ox + l.X * s, oy + l.Y * s)).ToList();
        return new RenderedScene(group, pageLabels, bounds);
    }

    private static int Layer(SceneKind kind) => kind switch
    {
        SceneKind.Sun or SceneKind.Cloud or SceneKind.Bird or SceneKind.Flyer => 0,
        SceneKind.Tree or SceneKind.PineTree or SceneKind.TrafficLight => 2,
        _ => 3,
    };

    private static Box DrawItem(Canvas w, SceneItem item, List<(string, double, double)> labels)
    {
        double x = item.X, y = item.Y, size = item.Size;
        switch (item.Kind)
        {
            case SceneKind.Sun:
            {
                double r = size * 0.32;
                w.Circle(x, y, r);
                if (!item.Alt)
                    for (int k = 0; k < 8; k++)
                    {
                        double a = k * Math.PI / 4;
                        w.Line(x + Math.Cos(a) * r * 1.3, y + Math.Sin(a) * r * 1.3, x + Math.Cos(a) * r * 1.75, y + Math.Sin(a) * r * 1.75);
                    }
                labels.Add(("yellow", x, y));
                return new Box(x - r * 1.8, y - r * 1.8, r * 3.6, r * 3.6);
            }

            case SceneKind.Cloud:
            {
                double half = size / 2, h = size * 0.42, baseY = y + h / 2;
                w.Path($"M{N(x - half)} {N(baseY)} " +
                       $"A{N(size * 0.2)} {N(size * 0.2)} 0 0 1 {N(x - half + size * 0.32)} {N(y - h * 0.1)} " +
                       $"A{N(size * 0.24)} {N(size * 0.24)} 0 0 1 {N(x + size * 0.2)} {N(y - h * 0.18)} " +
                       $"A{N(size * 0.19)} {N(size * 0.19)} 0 0 1 {N(x + half)} {N(baseY)} Z");
                return new Box(x - half, y - h * 0.95, size, h * 1.5);
            }

            case SceneKind.Bird:
            {
                double half = size / 2;
                w.Path($"M{N(x - half)} {N(y)} Q{N(x - half / 2)} {N(y - size * 0.4)} {N(x)} {N(y)} Q{N(x + half / 2)} {N(y - size * 0.4)} {N(x + half)} {N(y)}", fill: "none");
                return new Box(x - half, y - size * 0.4, size, size * 0.5);
            }

            case SceneKind.Flyer:
            {
                var box = new Box(x - size / 2, y - size * 0.3, size, size * 0.6);
                w.Vehicle(item.Vehicle, box, item.Flip, labels);
                return box;
            }

            case SceneKind.Tree:
            {
                double trunkW = size * 0.22, trunkH = size * 0.6, r = size * 0.5;
                w.Rect(x - trunkW / 2, y - trunkH, trunkW, trunkH);
                w.Circle(x, y - trunkH - r * 0.7, r);
                labels.Add(("green", x, y - trunkH - r * 0.7));
                labels.Add(("brown", x, y - trunkH * 0.35));
                return new Box(x - r, y - trunkH - r * 1.7, 2 * r, trunkH + r * 1.7);
            }

            case SceneKind.PineTree:
            {
                double trunkW = size * 0.2, trunkH = size * 0.35, half = size * 0.5, crownH = size * 1.35;
                w.Rect(x - trunkW / 2, y - trunkH, trunkW, trunkH);
                w.Path($"M{N(x - half)} {N(y - trunkH)} L{N(x)} {N(y - trunkH - crownH)} L{N(x + half)} {N(y - trunkH)} Z");
                labels.Add(("green", x, y - trunkH - crownH * 0.3));
                labels.Add(("brown", x, y - trunkH * 0.45));
                return new Box(x - half, y - trunkH - crownH, size, trunkH + crownH);
            }

            case SceneKind.TrafficLight:
            {
                double postW = size * 0.22, headW = size * 0.75, headH = size * 1.9, postH = size * 1.2;
                w.Rect(x - postW / 2, y - postH, postW, postH);
                double headTop = y - postH - headH;
                w.Rect(x - headW / 2, headTop, headW, headH, size * 0.15);
                if (!item.Alt)
                {
                    string[] colors = ["red", "yellow", "green"];
                    for (int k = 0; k < 3; k++)
                    {
                        double cy = headTop + headH * (k + 0.5) / 3;
                        w.Circle(x, cy, size * 0.24);
                        labels.Add((colors[k], x, cy));
                    }
                }
                return new Box(x - headW / 2, headTop, headW, headH + postH);
            }

            case SceneKind.RoadVehicle:
            {
                double h = size * 0.6;
                var box = new Box(x - size / 2, y - h * 0.95, size, h);
                w.Vehicle(item.Vehicle, box, item.Flip, labels);
                return box;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(item), item.Kind, null);
        }
    }

    private static void DrawHills(Canvas w, double h)
    {
        double horizon = Horizon(h), top = RoadTop(h);
        var d = new StringBuilder($"M0 {N(horizon)}");
        for (int x = 0; x <= Width; x += 10)
            d.Append($" L{x} {N(horizon + Math.Sin(x / Width * Math.PI * 2.2 + 0.6) * h * 0.035)}");
        d.Append($" L{N(Width)} {N(top)} L0 {N(top)} Z");
        w.Path(d.ToString());
    }

    private static void DrawRoad(Canvas w, double h)
    {
        w.RoadDrawn = true;
        w.Rect(-2, RoadTop(h), Width + 4, RoadBottom(h) - RoadTop(h));
        w.Line(0, RoadMid(h) + 14, Width, RoadMid(h) + 14, dash: "22 18");
    }

    /// <summary>Sky, hill and road labels, placed where no item covers them.</summary>
    private static void AddBackgroundLabels(SceneSpec spec, List<(string Color, double X, double Y)> labels)
    {
        double h = spec.Height;
        var covered = spec.Items.Where(i => !i.Hidden).Select(ApproxBounds).ToList();
        bool Free(double x, double y) => !covered.Any(b => x > b.X - 12 && x < b.Right + 12 && y > b.Y - 12 && y < b.Bottom + 12);

        void AddFirstFree(string color, IEnumerable<(double X, double Y)> candidates, int max)
        {
            foreach (var (x, y) in candidates.Where(c => Free(c.X, c.Y)).Take(max))
                labels.Add((color, x, y));
        }

        double sky = Horizon(h);
        AddFirstFree("blue", [(30, sky * 0.1), (300, sky * 0.08), (560, sky * 0.7), (30, sky * 0.8), (240, sky * 0.85)], 2);
        double hills = (Horizon(h) + h * 0.045 + RoadTop(h)) / 2;
        AddFirstFree("green", [(120, hills), (480, hills), (240, hills), (380, hills)], 2);
        double lowerLane = (RoadMid(h) + 14 + RoadBottom(h)) / 2 + 4;
        AddFirstFree("grey", [(40, lowerLane), (560, lowerLane), (300, lowerLane)], 2);
    }

    /// <summary>Item bounds in canvas units without drawing (for label placement).</summary>
    private static Box ApproxBounds(SceneItem item)
    {
        var sink = new Canvas(new StringBuilder(), 1, 1);
        return DrawItem(sink, item, []);
    }

    /// <summary>Writes shapes in scene-canvas units with a constant outline width.</summary>
    private sealed class Canvas(StringBuilder sb, double lineWidth, double pageScale)
    {
        public bool RoadDrawn { get; set; }

        public void Path(string d, string fill = "#fff") =>
            sb.Append($"<path d=\"{d}\" fill=\"{fill}\" stroke-width=\"{N(lineWidth)}\"/>");

        public void Rect(double x, double y, double w, double h, double r = 0, string fill = "#fff") =>
            sb.Append($"<rect fill=\"{fill}\" x=\"{N(x)}\" y=\"{N(y)}\" width=\"{N(w)}\" height=\"{N(h)}\" rx=\"{N(r)}\" stroke-width=\"{N(lineWidth)}\"/>");

        public void Circle(double cx, double cy, double r) =>
            sb.Append($"<circle cx=\"{N(cx)}\" cy=\"{N(cy)}\" r=\"{N(r)}\" stroke-width=\"{N(lineWidth)}\"/>");

        public void Line(double x1, double y1, double x2, double y2, string? dash = null) =>
            sb.Append($"<line x1=\"{N(x1)}\" y1=\"{N(y1)}\" x2=\"{N(x2)}\" y2=\"{N(y2)}\" stroke-width=\"{N(lineWidth)}\"{(dash is null ? string.Empty : $" stroke-dasharray=\"{dash}\"")}/>");

        public void Vehicle(Vehicle v, Box box, bool flip, List<(string, double, double)> labels)
        {
            sb.Append(Vehicles.Draw(v, box, lineWidth, flip));
            // Wheels and windows on small vehicles are too small to hold a label.
            bool roomy = box.Height * pageScale >= 150;
            foreach (var part in Vehicles.Parts(v, box, flip).Where(p => roomy || p.Part is VehiclePart.Body or VehiclePart.Extra))
                labels.Add((Vehicles.PartColor(v, part.Part), part.X, part.Y));
        }
    }
}
