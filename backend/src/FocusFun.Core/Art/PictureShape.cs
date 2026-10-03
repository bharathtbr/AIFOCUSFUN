namespace FocusFun.Core.Art;

public readonly record struct ShapePoint(double X, double Y);

/// <summary>An area enclosed by the drawing's lines (a window, a leaf, a shirt) that can be coloured in.</summary>
/// <param name="X">Label point: the spot deepest inside the area, where a number fits best.</param>
/// <param name="Area">Size in square pixels.</param>
/// <param name="Room">Distance in pixels from the label point to the nearest line, i.e. how big a label fits.</param>
public sealed record ShapeRegion(double X, double Y, double Area, double Room);

/// <summary>What sheets need to know about a picture's shape, in its pixel coordinates.</summary>
/// <param name="Outline">The outer edge of the drawing, clockwise on screen (for dot-to-dot).</param>
/// <param name="Regions">Enclosed areas, largest first (for colour-by-number).</param>
public sealed record PictureShape(IReadOnlyList<ShapePoint> Outline, IReadOnlyList<ShapeRegion> Regions);

/// <summary>Finds a line drawing's outline and colourable areas from its ink mask (true = black line).</summary>
public static class ShapeAnalyzer
{
    /// <summary>Areas smaller than this share of the picture are gaps between lines, not something to colour.</summary>
    private const double MinRegionShare = 0.004;

    public static PictureShape Analyze(bool[,] ink)
    {
        int w = ink.GetLength(0), h = ink.GetLength(1);
        var outside = OutsideFill(ink, w, h);
        return new PictureShape(Outline(ink, outside, w, h), Regions(ink, outside, w, h));
    }

    /// <summary>Paper reachable from the edge without crossing a line.</summary>
    private static bool[,] OutsideFill(bool[,] ink, int w, int h)
    {
        var outside = new bool[w, h];
        var queue = new Queue<(int X, int Y)>();
        void Seed(int x, int y)
        {
            if (!ink[x, y] && !outside[x, y]) { outside[x, y] = true; queue.Enqueue((x, y)); }
        }
        for (int x = 0; x < w; x++) { Seed(x, 0); Seed(x, h - 1); }
        for (int y = 0; y < h; y++) { Seed(0, y); Seed(w - 1, y); }
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            if (x > 0) Seed(x - 1, y);
            if (x < w - 1) Seed(x + 1, y);
            if (y > 0) Seed(x, y - 1);
            if (y < h - 1) Seed(x, y + 1);
        }
        return outside;
    }

    // Moore neighbourhood, clockwise on screen starting west: W, NW, N, NE, E, SE, S, SW.
    private static readonly (int X, int Y)[] Around = [(-1, 0), (-1, -1), (0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1)];

    /// <summary>Traces the edge of the largest solid part (lines plus what they enclose), then simplifies it.</summary>
    private static IReadOnlyList<ShapePoint> Outline(bool[,] ink, bool[,] outside, int w, int h)
    {
        // The solid body: everything that isn't outside paper. Keep only its largest connected piece.
        var label = new int[w, h];
        int best = 0, bestSize = 0, next = 0;
        (int X, int Y) bestStart = default;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (outside[x, y] || label[x, y] != 0) continue;
                int id = ++next, size = 0;
                var stack = new Stack<(int X, int Y)>();
                stack.Push((x, y));
                label[x, y] = id;
                while (stack.Count > 0)
                {
                    var (cx, cy) = stack.Pop();
                    size++;
                    foreach (var (dx, dy) in Around)
                    {
                        int nx = cx + dx, ny = cy + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h || outside[nx, ny] || label[nx, ny] != 0) continue;
                        label[nx, ny] = id;
                        stack.Push((nx, ny));
                    }
                }
                // Raster order means (x, y) is this piece's top-left pixel: a safe place to start tracing.
                if (size > bestSize) { best = id; bestSize = size; bestStart = (x, y); }
            }
        if (best == 0) return [];

        bool Solid(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && label[x, y] == best;

        // Moore-neighbour tracing, entering from the west (known to be outside the piece).
        var contour = new List<ShapePoint>();
        var start = bestStart;
        var current = start;
        int backtrack = 0; // direction from current to the last outside pixel checked
        int limit = 4 * (w + h) * 8 + bestSize;
        do
        {
            contour.Add(new ShapePoint(current.X + 0.5, current.Y + 0.5));
            int found = -1;
            for (int k = 1; k <= 8; k++)
            {
                int d = (backtrack + k) % 8;
                if (Solid(current.X + Around[d].X, current.Y + Around[d].Y)) { found = d; break; }
            }
            if (found < 0) break; // a single pixel
            var prevOutside = (X: current.X + Around[(found + 7) % 8].X, Y: current.Y + Around[(found + 7) % 8].Y);
            current = (current.X + Around[found].X, current.Y + Around[found].Y);
            // Point the backtrack at the outside pixel we checked just before finding this one, as seen from the new pixel.
            backtrack = Array.IndexOf(Around, (prevOutside.X - current.X, prevOutside.Y - current.Y));
            if (backtrack < 0) backtrack = (found + 4) % 8;
        } while (current != start && contour.Count < limit);

        var simplified = Simplify(contour, 0.8);
        return Area(simplified) < 0 ? simplified.AsEnumerable().Reverse().ToList() : simplified;
    }

    /// <summary>Enclosed paper areas, each with the point deepest inside it.</summary>
    private static IReadOnlyList<ShapeRegion> Regions(bool[,] ink, bool[,] outside, int w, int h)
    {
        var seen = new bool[w, h];
        var regions = new List<ShapeRegion>();
        double minArea = MinRegionShare * w * h;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (ink[x, y] || outside[x, y] || seen[x, y]) continue;
                var pixels = new List<(int X, int Y)>();
                var queue = new Queue<(int X, int Y)>();
                queue.Enqueue((x, y));
                seen[x, y] = true;
                while (queue.Count > 0)
                {
                    var p = queue.Dequeue();
                    pixels.Add(p);
                    foreach (var (dx, dy) in ((int, int)[])[(1, 0), (-1, 0), (0, 1), (0, -1)])
                    {
                        int nx = p.X + dx, ny = p.Y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h || ink[nx, ny] || outside[nx, ny] || seen[nx, ny]) continue;
                        seen[nx, ny] = true;
                        queue.Enqueue((nx, ny));
                    }
                }
                if (pixels.Count < minArea) continue;
                var (lx, ly, room) = DeepestPoint(pixels);
                regions.Add(new ShapeRegion(lx + 0.5, ly + 0.5, pixels.Count, room));
            }
        return regions.OrderByDescending(r => r.Area).ToList();
    }

    /// <summary>The pixel farthest from the region's edge (breadth-first distance inward from its border pixels).</summary>
    private static (int X, int Y, double Room) DeepestPoint(List<(int X, int Y)> pixels)
    {
        var inside = pixels.ToHashSet();
        var distance = new Dictionary<(int, int), int>();
        var queue = new Queue<(int X, int Y)>();
        foreach (var p in pixels)
            if (!inside.Contains((p.X + 1, p.Y)) || !inside.Contains((p.X - 1, p.Y)) || !inside.Contains((p.X, p.Y + 1)) || !inside.Contains((p.X, p.Y - 1)))
            {
                distance[p] = 1;
                queue.Enqueue(p);
            }
        var deepest = queue.Count > 0 ? queue.Peek() : pixels[0];
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            int d = distance[p];
            if (d > distance.GetValueOrDefault(deepest)) deepest = p;
            foreach (var n in new[] { (p.X + 1, p.Y), (p.X - 1, p.Y), (p.X, p.Y + 1), (p.X, p.Y - 1) })
                if (inside.Contains(n) && !distance.ContainsKey(n)) { distance[n] = d + 1; queue.Enqueue(n); }
        }
        return (deepest.X, deepest.Y, distance.GetValueOrDefault(deepest));
    }

    /// <summary>Douglas–Peucker on a closed outline: keeps corners, drops points within <paramref name="tolerance"/> px of a straight run.</summary>
    public static List<ShapePoint> Simplify(IReadOnlyList<ShapePoint> points, double tolerance)
    {
        if (points.Count < 4) return points.ToList();
        var keep = new bool[points.Count];
        keep[0] = true;
        int far = Enumerable.Range(1, points.Count - 1).MaxBy(i => Sq(points[i].X - points[0].X) + Sq(points[i].Y - points[0].Y));
        keep[far] = true;
        Mark(0, far);
        Mark(far, points.Count); // index Count wraps to 0
        return points.Where((_, i) => keep[i]).ToList();

        void Mark(int from, int to)
        {
            var a = points[from];
            var b = points[to % points.Count];
            double worst = -1;
            int index = -1;
            for (int i = from + 1; i < to; i++)
            {
                double d = SegmentDistance(points[i], a, b);
                if (d > worst) { worst = d; index = i; }
            }
            if (index < 0 || worst <= tolerance) return;
            keep[index] = true;
            Mark(from, index);
            Mark(index, to);
        }
    }

    /// <summary>Shoelace area; positive when the points run clockwise on screen (y down).</summary>
    public static double Area(IReadOnlyList<ShapePoint> p)
    {
        double sum = 0;
        for (int i = 0; i < p.Count; i++)
        {
            var a = p[i];
            var b = p[(i + 1) % p.Count];
            sum += a.X * b.Y - b.X * a.Y;
        }
        return sum / 2;
    }

    private static double SegmentDistance(ShapePoint p, ShapePoint a, ShapePoint b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, len2 = dx * dx + dy * dy;
        double t = len2 == 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2, 0, 1);
        return Math.Sqrt(Sq(p.X - (a.X + t * dx)) + Sq(p.Y - (a.Y + t * dy)));
    }

    private static double Sq(double v) => v * v;
}
