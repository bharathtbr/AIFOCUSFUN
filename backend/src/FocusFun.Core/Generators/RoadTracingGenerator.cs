using System.Text;
using FocusFun.Core.Art;
using FocusFun.Core.Svg;

namespace FocusFun.Core.Generators;

using static SvgWriter;

/// <summary>A roundabout on a Challenge path: the path goes once around (Cx, Cy) at radius R, joining it at the bottom or top.</summary>
public readonly record struct Roundabout(double Cx, double Cy, double R, bool Clockwise, bool RoadBelow);

/// <summary>
/// One lane of a road-tracing sheet: the vehicle, its destination and the path between them.
/// <paramref name="Track"/> is the strip between the two icons; every path point stays inside it, inset by half the road.
/// </summary>
public sealed record TracingLane(
    Box Area,
    Box Track,
    double RoadWidth,
    Box VehicleBox,
    Box GarageBox,
    int ArrowSide,
    IReadOnlyList<(double X, double Y)> Path,
    IReadOnlyList<Roundabout> Roundabouts);

/// <summary>
/// Pencil control: lanes of vehicles driving to their garages along a dotted line.
/// Easy: straight or gently sloped wide roads. Medium: waves and zigzags on narrower roads.
/// Challenge: cursive loops and roundabouts, dotted line only.
/// </summary>
public sealed class RoadTracingGenerator : ISheetGenerator
{
    private const string DotDash = "0.1 10";
    private const double DotWidth = 5;
    private const double EdgeWidth = 2.5;
    private const double VehicleWidth = 88;
    private const double VehicleHeight = 53;
    private const double GarageSize = 64;
    private const double GarageHalf = GarageSize * 0.4;  // the garage outline spans 10..90 of its 100-unit square
    private const double Gap = 8;
    private const double LeadIn = 18;

    private static readonly Vehicle[] Drivers =
        [Vehicle.Car, Vehicle.Bus, Vehicle.GarbageTruck, Vehicle.Bulldozer, Vehicle.FireTruck, Vehicle.DumpTruck, Vehicle.Autorickshaw];

    private enum Shape { Straight, Slope, Ramp, Wave, Zigzag, Loops, Roundabout, DoubleRoundabout }

    public SheetType Type => SheetType.RoadTracing;

    public static double RoadWidth(Level level) => level switch
    {
        Level.Easy => 34,
        Level.Medium => 24,
        _ => 0,
    };

    public static int LaneCount(Level level) => level == Level.Challenge ? 3 : 4;

    public SheetContent Generate(SheetRequest request, Box box, SeededRandom random)
    {
        var lanes = Lanes(request.Level, box, random);
        var vehicles = Drivers.ToList();
        random.Shuffle(vehicles);

        var body = new StringBuilder();
        for (int i = 0; i < lanes.Count; i++)
        {
            var lane = lanes[i];
            if (i > 0) body.Append(new SvgWriter().Line(box.X, lane.Area.Y, box.Right, lane.Area.Y, 1));
            body.Append(DrawLane(lane));
            body.Append(Vehicles.Draw(vehicles[i % vehicles.Count], lane.VehicleBox, 2.5));
            body.Append(Icons.Garage(lane.GarageBox.CenterX, lane.GarageBox.CenterY, GarageSize));
        }

        string instruction = request.Level == Level.Challenge
            ? "Trace the path with your pencil!"
            : "Trace the road with your pencil!";
        return new SheetContent("Road Tracing", instruction, body.ToString(), null);
    }

    /// <summary>Lays out the lanes and their paths; exposed so tests can check every point stays in its lane.</summary>
    public static IReadOnlyList<TracingLane> Lanes(Level level, Box box, SeededRandom random)
    {
        int count = LaneCount(level);
        double laneH = box.Height / count;
        double road = RoadWidth(level);
        double pad = (road > 0 ? road / 2 + EdgeWidth : DotWidth / 2) + 4;
        var shapes = PickShapes(level, random);
        var used = new HashSet<double>();  // wave frequencies and zigzag counts already on the sheet, so repeated shapes differ

        var lanes = new List<TracingLane>();
        for (int i = 0; i < count; i++)
        {
            var area = new Box(box.X, box.Y + i * laneH, box.Width, laneH);
            double left = area.X + 2 + VehicleWidth + Gap;
            double right = area.Right - 2 - 2 * GarageHalf - Gap;
            var track = new Box(left, area.Y, right - left, area.Height);

            // How far from the lane centre the path may go, and how far its ends may go (the icons sit on them).
            double reach = laneH / 2 - pad;
            double endReach = laneH / 2 - VehicleHeight / 2 - 4;
            var (path, side, roundabouts) = Build(shapes[i], track, area.CenterY, reach, endReach, road, used, random);

            var start = path[0];
            var end = path[^1];
            var vehicleBox = new Box(area.X + 2, start.Y - VehicleHeight / 2, VehicleWidth, VehicleHeight);
            var garageBox = new Box(area.Right - 2 - 2 * GarageHalf, end.Y - GarageHalf, 2 * GarageHalf, 2 * GarageHalf);
            lanes.Add(new TracingLane(area, track, road, vehicleBox, garageBox, side, path, roundabouts));
        }
        return lanes;
    }

    private static List<Shape> PickShapes(Level level, SeededRandom random)
    {
        List<Shape> shapes;
        switch (level)
        {
            case Level.Easy:
                // A plain straight road first as a warm-up, then gentle slopes.
                var rest = new List<Shape> { Shape.Slope, Shape.Ramp, random.Next(2) == 0 ? Shape.Slope : Shape.Ramp };
                random.Shuffle(rest);
                return [Shape.Straight, .. rest];
            case Level.Medium:
                shapes = [Shape.Wave, Shape.Zigzag, Shape.Wave, Shape.Zigzag];
                break;
            default:
                shapes = [Shape.Loops, random.Next(2) == 0 ? Shape.Roundabout : Shape.DoubleRoundabout,
                    random.Next(2) == 0 ? Shape.Loops : Shape.Roundabout];
                break;
        }
        random.Shuffle(shapes);
        return shapes;
    }

    private static (List<(double X, double Y)> Path, int Side, List<Roundabout> Roundabouts) Build(
        Shape shape, Box track, double cy, double reach, double endReach, double road, HashSet<double> used, SeededRandom random)
    {
        double x0 = track.X, x1 = track.Right, w = track.Width;
        var pts = new List<(double X, double Y)>();
        var roundabouts = new List<Roundabout>();
        int side = -1;

        switch (shape)
        {
            case Shape.Straight:
                pts.Add((x0, cy));
                pts.Add((x1, cy));
                break;

            case Shape.Slope:
            case Shape.Ramp:
            {
                double s = Range(random, 14, Math.Min(36, endReach)) * Sign(random);
                if (shape == Shape.Slope)
                {
                    pts.Add((x0, cy - s));
                    pts.Add((x1, cy + s));
                }
                else
                {
                    // Flat, a smooth S down (or up), flat.
                    for (double x = x0; x <= x1 + 0.01; x += 3)
                    {
                        double u = Math.Clamp((x - x0 - w * 0.2) / (w * 0.6), 0, 1);
                        pts.Add((Math.Min(x, x1), cy - s + 2 * s * (1 - Math.Cos(Math.PI * u)) / 2));
                    }
                }
                side = s > 0 ? -1 : 1;
                break;
            }

            case Shape.Wave:
            {
                // Waves that may grow or shrink along the road; amplitude capped so crests stay wider than the road.
                var freqs = new List<double> { 1, 1.5, 2, 2.5 }.Where(f => !used.Contains(f)).ToList();
                double f = freqs[random.Next(freqs.Count)];
                used.Add(f);
                double span = w - 2 * LeadIn;
                double halfPeriod = span / (2 * f);
                double maxA = Math.Min(reach, Math.Pow(halfPeriod / Math.PI, 2) / (road / 2 + 16));
                double a0 = Range(random, Math.Min(18, maxA), maxA), a1 = Range(random, Math.Min(18, maxA), maxA);
                double sign = Sign(random);
                pts.Add((x0, cy));
                for (double x = x0 + LeadIn; x <= x1 - LeadIn + 0.01; x += 3)
                {
                    double t = Math.Clamp((x - x0 - LeadIn) / span, 0, 1);
                    double env = SmoothStep(t * 2 * f) * SmoothStep((1 - t) * 2 * f);  // fade in and out over half a wave
                    double amp = (a0 + (a1 - a0) * t) * env;
                    pts.Add((x, cy + sign * amp * Math.Sin(2 * Math.PI * f * t)));
                }
                pts.Add((x1, cy));
                side = sign > 0 ? -1 : 1;
                break;
            }

            case Shape.Zigzag:
            {
                // Sharp corners whose height grows or shrinks along the road.
                var counts = Enumerable.Range(4, 5).Where(c => !used.Contains(c)).ToList();
                int n = counts[random.Next(counts.Count)];
                used.Add(n);
                double xa = x0 + LeadIn, xb = x1 - LeadIn, dx = (xb - xa) / n;
                double maxA = Math.Min(reach, dx * 1.15);
                double a0 = Range(random, 20, maxA), a1 = Range(random, 20, maxA);
                double sign = Sign(random);
                pts.Add((x0, cy));
                pts.Add((xa, cy));
                for (int k = 1; k < n; k++)
                {
                    double a = a0 + (a1 - a0) * (k - 1) / Math.Max(1, n - 2);
                    pts.Add((xa + k * dx, cy + sign * (k % 2 == 1 ? -a : a)));
                }
                pts.Add((xb, cy));
                pts.Add((x1, cy));
                side = sign > 0 ? 1 : -1;
                break;
            }

            case Shape.Loops:
            {
                // Prolate cycloid: x = a·θ − bx·sinθ, y = −by·cosθ loops (like a joined-up "eee") wherever bx·cosθ > a.
                double bx = Range(random, 36, 48), a = bx * Range(random, 0.34, 0.46);
                double by = Range(random, 40, Math.Min(58, Math.Min(reach, endReach - 6)));
                double s = Sign(random);  // +1: loops point up, −1: loops point down
                int n = Math.Clamp((int)((w - 2 * LeadIn) / (2 * Math.PI * a)), 2, 4);
                double xs = x0 + (w - 2 * Math.PI * a * n) / 2;
                double baseY = cy + s * by;
                pts.Add((x0, baseY));
                for (double th = Math.PI; th <= Math.PI + 2 * Math.PI * n + 1e-9; th += 0.04)
                    pts.Add((xs + a * (th - Math.PI) - bx * Math.Sin(th), cy - s * by * Math.Cos(th)));
                pts.Add((xs + 2 * Math.PI * a * n, baseY));
                pts.Add((x1, baseY));
                side = s > 0 ? 1 : -1;
                break;
            }

            case Shape.Roundabout:
            {
                // A straight road that goes once round a circle; icons and circle are balanced about the lane centre.
                double r = Range(random, 52, 64);
                double s = Sign(random);  // +1: circle above the road, −1: below
                double baseY = cy + s * (r - VehicleHeight / 4);
                double xc = Range(random, x0 + r + 40, x1 - r - 40);
                pts.Add((x0, baseY));
                AddCircle(pts, roundabouts, xc, baseY - s * r, r, s);
                pts.Add((x1, baseY));
                side = s > 0 ? 1 : -1;
                break;
            }

            case Shape.DoubleRoundabout:
            {
                // One circle above the road, then one below.
                double r = Range(random, 38, Math.Min(44, reach / 2));
                double s = Sign(random);
                double gap = Range(random, 30, Math.Min(90, w - 4 * r - 60));
                double first = x0 + 30 + r + random.NextDouble() * (w - 4 * r - 60 - gap);
                pts.Add((x0, cy));
                AddCircle(pts, roundabouts, first, cy - s * r, r, s);
                AddCircle(pts, roundabouts, first + 2 * r + gap, cy + s * r, r, -s);
                pts.Add((x1, cy));
                side = s > 0 ? 1 : -1;
                break;
            }
        }
        return (pts, side, roundabouts);
    }

    /// <summary>Goes once round a circle, starting and ending at the point where the road touches it, heading right.</summary>
    private static void AddCircle(List<(double X, double Y)> pts, List<Roundabout> roundabouts, double xc, double yc, double r, double s)
    {
        // s = +1: road touches the bottom (φ = π/2); s = −1: the top. Heading right means φ runs against s.
        double phi0 = s * Math.PI / 2, dir = -s;
        int steps = (int)Math.Ceiling(2 * Math.PI * r / 3);
        for (int k = 0; k <= steps; k++)
        {
            double phi = phi0 + dir * 2 * Math.PI * k / steps;
            pts.Add((xc + r * Math.Cos(phi), yc + r * Math.Sin(phi)));
        }
        roundabouts.Add(new Roundabout(xc, yc, r, Clockwise: dir > 0, RoadBelow: s > 0));  // SVG y points down, so increasing φ is clockwise
    }

    private static string DrawLane(TracingLane lane)
    {
        var d = new StringBuilder();
        for (int i = 0; i < lane.Path.Count; i++)
            d.Append(i == 0 ? 'M' : 'L').Append(N(lane.Path[i].X)).Append(' ').Append(N(lane.Path[i].Y));
        string path = d.ToString();
        var w = new SvgWriter();

        // Road: a wide black stroke under a slightly narrower white one leaves two clean parallel edges on any curve.
        if (lane.RoadWidth > 0)
        {
            // Square white caps hide the hairline anti-aliasing leaves across the open road ends.
            w.Raw(Stroke(path, lane.RoadWidth + 2 * EdgeWidth, "#000", "butt"));
            w.Raw(Stroke(path, lane.RoadWidth, "#fff", "square"));
        }
        w.Path(path, DotWidth, dash: DotDash);

        foreach (var ring in lane.Roundabouts)
        {
            // Centre island, and an arrow on the far side showing which way round to go.
            w.Circle(ring.Cx, ring.Cy, ring.R * 0.28, 2);
            double dir = ring.Clockwise ? 1 : -1;
            double phi = ring.RoadBelow ? -Math.PI / 2 : Math.PI / 2;  // far side from the road
            double ar = ring.R - 13;
            double px = ring.Cx + ar * Math.Cos(phi), py = ring.Cy + ar * Math.Sin(phi);
            double tx = -Math.Sin(phi) * dir, ty = Math.Cos(phi) * dir;
            w.Raw(Arrow(px - tx * 10, py - ty * 10, tx, ty, 20));
        }

        // Big start dot with a small arrow beside it pointing the way to go.
        var (sx, sy) = lane.Path[0];
        var ahead = PointAlong(lane.Path, 20);
        double ux = ahead.X - sx, uy = ahead.Y - sy, len = Math.Max(1e-6, Math.Sqrt(ux * ux + uy * uy));
        ux /= len; uy /= len;
        double off = (lane.RoadWidth > 0 ? lane.RoadWidth / 2 + EdgeWidth : 0) + 11;
        w.Raw(Arrow(sx, sy + lane.ArrowSide * off, ux, uy, 22));
        w.Circle(sx, sy, 6.5, 1, "#000");
        return w.ToString();
    }

    private static string Arrow(double x, double y, double ux, double uy, double length)
    {
        double bx = x + ux * length, by = y + uy * length;
        double nx = -uy, ny = ux;
        var w = new SvgWriter();
        w.Line(x, y, bx, by, 2.2);
        w.Path($"M{N(bx - ux * 7 + nx * 5)} {N(by - uy * 7 + ny * 5)}L{N(bx)} {N(by)}L{N(bx - ux * 7 - nx * 5)} {N(by - uy * 7 - ny * 5)}", 2.2);
        return w.ToString();
    }

    private static string Stroke(string d, double width, string colour, string cap) =>
        $"<path d=\"{d}\" fill=\"none\" stroke=\"{colour}\" stroke-width=\"{N(width)}\" stroke-linecap=\"{cap}\" stroke-linejoin=\"round\"/>";

    private static (double X, double Y) PointAlong(IReadOnlyList<(double X, double Y)> path, double distance)
    {
        double travelled = 0;
        for (int i = 1; i < path.Count; i++)
        {
            double dx = path[i].X - path[i - 1].X, dy = path[i].Y - path[i - 1].Y;
            double seg = Math.Sqrt(dx * dx + dy * dy);
            if (travelled + seg >= distance && seg > 0)
            {
                double t = (distance - travelled) / seg;
                return (path[i - 1].X + dx * t, path[i - 1].Y + dy * t);
            }
            travelled += seg;
        }
        return path[^1];
    }

    private static double SmoothStep(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static double Range(SeededRandom random, double min, double max) => min + random.NextDouble() * Math.Max(0, max - min);

    private static double Sign(SeededRandom random) => random.Next(2) == 0 ? 1 : -1;
}
