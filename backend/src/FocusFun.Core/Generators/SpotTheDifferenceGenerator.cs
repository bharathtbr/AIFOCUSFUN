using System.Text;
using FocusFun.Core.Art;
using FocusFun.Core.Svg;

namespace FocusFun.Core.Generators;

using static SvgWriter;

/// <summary>
/// Two street scenes, one above the other; the bottom one has 3 / 5 / 8 changes to find.
/// Other topics use two panels of their own pictures instead.
/// </summary>
public sealed class SpotTheDifferenceGenerator : ISheetGenerator
{
    private const double Gap = 18;

    public SheetType Type => SheetType.SpotTheDifference;

    public static int Differences(Level level) => level switch
    {
        Level.Easy => 3,
        Level.Medium => 5,
        _ => 8,
    };

    public SheetContent Generate(SheetRequest request, Box box, SeededRandom random)
    {
        double half = (box.Height - Gap) / 2;
        var top = new Box(box.X, box.Y, box.Width, half);
        var bottom = new Box(box.X, box.Y + half + Gap, box.Width, half);
        if (ThemeText.TopicPictures(request, atLeast: 2) is { } set)
            return PicturePanels(set, Differences(request.Level), request.Level, top, bottom, random);

        var original = Scenes.Street(random, Scenes.HeightFor(top), busy: true);
        var (changed, changedIndexes) = Change(original, Differences(request.Level), request.Level, random);

        var first = Scenes.Render(original, top);
        var second = Scenes.Render(changed, bottom);
        var body = new StringBuilder()
            .Append(first.Svg)
            .Append(second.Svg);

        // Answer key: ring each change on the bottom picture (using the larger of the before/after bounds).
        var key = new StringBuilder(body.ToString());
        var original2 = Scenes.Render(original, bottom);
        foreach (int i in changedIndexes)
        {
            var b = Union(original2.ItemBounds[i], second.ItemBounds[i]);
            key.Append($"<ellipse cx=\"{N(b.CenterX)}\" cy=\"{N(b.CenterY)}\" rx=\"{N(b.Width / 2 + 8)}\" ry=\"{N(b.Height / 2 + 8)}\" fill=\"none\" stroke=\"#000\" stroke-width=\"3\" stroke-dasharray=\"10 5\"/>");
        }

        int n = changedIndexes.Count;
        return new SheetContent("Spot the Difference", $"Find the {n} differences!", body.ToString(), key.ToString());
    }

    /// <summary>
    /// Changes <paramref name="count"/> different items. Removed items stay in the list as hidden, so indexes and bounds still line up.
    /// Easy only removes things (easiest to notice); harder levels also flip, swap and alter.
    /// </summary>
    public static (SceneSpec Spec, List<int> Changed) Change(SceneSpec original, int count, Level level, SeededRandom random)
    {
        var items = original.Items.ToList();
        var candidates = Enumerable.Range(0, items.Count).ToList();
        random.Shuffle(candidates);
        var changed = new List<int>();

        foreach (int i in candidates)
        {
            if (changed.Count == count) break;
            var item = items[i];
            SceneItem? next = level == Level.Easy ? Remove(item, level) : Alter(item, random) ?? Remove(item, level);
            if (next is null) continue;
            items[i] = next;
            changed.Add(i);
        }
        return (original with { Items = items }, changed);
    }

    /// <summary>Vehicles, trees and the traffic light are changed rather than removed (removing them leaves too big a hole).</summary>
    private static SceneItem? Remove(SceneItem item, Level level) => item.Kind switch
    {
        SceneKind.Bird when level == Level.Easy => null,   // too small to notice at this level
        SceneKind.Cloud or SceneKind.Bird => item with { Hidden = true },
        SceneKind.Sun => item with { Alt = true },
        SceneKind.TrafficLight => item with { Alt = true },
        SceneKind.Tree => item with { Kind = SceneKind.PineTree },
        SceneKind.PineTree => item with { Kind = SceneKind.Tree },
        SceneKind.RoadVehicle or SceneKind.Flyer => item with { Flip = !item.Flip },
        _ => null,
    };

    private static SceneItem? Alter(SceneItem item, SeededRandom random) => item.Kind switch
    {
        SceneKind.RoadVehicle when random.Next(2) == 0 =>
            item with { Vehicle = Scenes.RoadVehicles.Where(v => v != item.Vehicle).ElementAt(random.Next(Scenes.RoadVehicles.Length - 1)) },
        SceneKind.Flyer when random.Next(2) == 0 =>
            item with { Vehicle = item.Vehicle == Vehicle.Airplane ? Vehicle.Helicopter : Vehicle.Airplane },
        SceneKind.Cloud when random.Next(2) == 0 => item with { Size = item.Size * 0.6 },
        _ => null,
    };

    /// <summary>One picture in a panel: which picture, where in its cell (0–1), how big (share of the cell) and which way it faces.</summary>
    private sealed record Placed(Picture Picture, double Dx, double Dy, double Size, bool Flip, bool Hidden = false);

    /// <summary>
    /// Topic pictures (animals, fruits, approved AI pictures) scattered over a grid in two framed panels; the bottom panel
    /// has the changes. Easy only removes pictures; harder levels also swap, shrink, move, and mirror mirror-safe ones.
    /// </summary>
    private static SheetContent PicturePanels(IPictureSet set, int count, Level level, Box top, Box bottom, SeededRandom random)
    {
        int cols = 4, rows = level == Level.Challenge ? 3 : 2;
        var original = new List<Placed>();
        for (int i = 0; i < cols * rows; i++)
            original.Add(new Placed(set.All[random.Next(set.All.Count)], random.NextDouble(), random.NextDouble(), 0.7 + random.NextDouble() * 0.2, random.Next(2) == 1));

        var changed = original.ToList();
        var cells = Enumerable.Range(0, original.Count).ToList();
        random.Shuffle(cells);
        var changedCells = cells.Take(count).ToList();
        foreach (int i in changedCells)
        {
            var p = original[i];
            List<string> kinds = level == Level.Easy ? ["remove"] : ["remove", "swap", "shrink", "move"];
            if (level != Level.Easy && p.Picture.Mirrorable) kinds.Add("mirror");
            changed[i] = kinds[random.Next(kinds.Count)] switch
            {
                "swap" => p with { Picture = set.All.Where(o => o != p.Picture).ElementAt(random.Next(set.All.Count - 1)) },
                "shrink" => p with { Size = p.Size * 0.55 },
                // To the opposite corner of its cell, shrunk a little so there's room to move visibly.
                "move" => p with { Dx = p.Dx < 0.5 ? 1 : 0, Dy = p.Dy < 0.5 ? 1 : 0, Size = Math.Min(p.Size, 0.62) },
                "mirror" => p with { Flip = !p.Flip },
                _ => p with { Hidden = true },
            };
        }

        var body = new StringBuilder().Append(Panel(original, top, cols, rows)).Append(Panel(changed, bottom, cols, rows));
        var key = new StringBuilder(body.ToString());
        foreach (int i in changedCells)
        {
            var b = Union(Bounds(original[i], bottom, cols, rows, i), Bounds(changed[i], bottom, cols, rows, i));
            key.Append($"<ellipse cx=\"{N(b.CenterX)}\" cy=\"{N(b.CenterY)}\" rx=\"{N(b.Width / 2 + 8)}\" ry=\"{N(b.Height / 2 + 8)}\" fill=\"none\" stroke=\"#000\" stroke-width=\"3\" stroke-dasharray=\"10 5\"/>");
        }
        return new SheetContent("Spot the Difference", $"Find the {changedCells.Count} differences!", body.ToString(), key.ToString());
    }

    private static string Panel(List<Placed> items, Box panel, int cols, int rows)
    {
        var w = new SvgWriter().Rect(panel.X, panel.Y, panel.Width, panel.Height, 2.5, radius: 12);
        for (int i = 0; i < items.Count; i++)
            if (!items[i].Hidden) w.Raw(items[i].Picture.Render(Bounds(items[i], panel, cols, rows, i), 2, items[i].Flip));
        return w.ToString();
    }

    /// <summary>The picture's box: its share of the cell, slid within the spare room by (Dx, Dy).</summary>
    private static Box Bounds(Placed p, Box panel, int cols, int rows, int index)
    {
        double cellW = (panel.Width - 16) / cols, cellH = (panel.Height - 16) / rows;
        double w = cellW * p.Size, h = cellH * p.Size;
        double x = panel.X + 8 + index % cols * cellW + (cellW - w) * p.Dx;
        double y = panel.Y + 8 + index / cols * cellH + (cellH - h) * p.Dy;
        return new Box(x, y, w, h);
    }

    private static Box Union(Box a, Box b)
    {
        double x = Math.Min(a.X, b.X), y = Math.Min(a.Y, b.Y);
        return new Box(x, y, Math.Max(a.Right, b.Right) - x, Math.Max(a.Bottom, b.Bottom) - y);
    }
}
