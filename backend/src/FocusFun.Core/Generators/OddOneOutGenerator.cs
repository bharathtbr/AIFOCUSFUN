using System.Text;
using FocusFun.Core.Art;
using FocusFun.Core.Svg;

namespace FocusFun.Core.Generators;

using static SvgWriter;

/// <summary>One picture in an odd-one-out row; <see cref="Flip"/> means it faces the other way.</summary>
public readonly record struct OddItem(Picture Picture, bool Flip);

/// <summary>A row of identical pictures except the one at <see cref="OddIndex"/>.</summary>
public sealed record OddRow(IReadOnlyList<OddItem> Items, int OddIndex);

/// <summary>Rows of matching pictures from the sheet's theme with one different; the child circles the odd one out.</summary>
public sealed class OddOneOutGenerator : ISheetGenerator
{
    /// <summary>Pairs that look alike (same overall shape), used only where a harder puzzle is wanted.</summary>
    public static IReadOnlyList<(Vehicle A, Vehicle B)> SimilarPairs { get; } =
    [
        (Vehicle.FireTruck, Vehicle.DumpTruck),
        (Vehicle.GarbageTruck, Vehicle.DumpTruck),
        (Vehicle.GarbageTruck, Vehicle.FireTruck),
        (Vehicle.Car, Vehicle.Autorickshaw),
        (Vehicle.Bus, Vehicle.FireTruck),
        (Vehicle.Airplane, Vehicle.Helicopter),
    ];

    /// <summary>Vehicles whose front and back differ enough that a mirrored copy is easy to spot.</summary>
    private static readonly Vehicle[] Asymmetric =
        [Vehicle.GarbageTruck, Vehicle.Bulldozer, Vehicle.SteamTrain, Vehicle.FireTruck,
         Vehicle.DumpTruck, Vehicle.Airplane, Vehicle.Helicopter, Vehicle.Autorickshaw];

    public SheetType Type => SheetType.OddOneOut;

    public static int RowCount(Level level) => level == Level.Easy ? 4 : 5;

    public static int ItemsPerRow(Level level) => level switch
    {
        Level.Easy => 3,
        Level.Medium => 4,
        _ => 6,
    };

    public static bool AreSimilar(Vehicle a, Vehicle b) =>
        SimilarPairs.Any(p => (p.A == a && p.B == b) || (p.A == b && p.B == a));

    /// <summary>True only for two vehicle pictures listed in <see cref="SimilarPairs"/>; other themes have no known look-alikes.</summary>
    public static bool AreSimilar(Picture a, Picture b) =>
        ThemeText.AsVehicle(a) is { } va && ThemeText.AsVehicle(b) is { } vb && AreSimilar(va, vb);

    public SheetContent Generate(SheetRequest request, Box box, SeededRandom random)
    {
        var rows = Plan(ThemeText.Pictures(request), request.Level, random);
        var body = new StringBuilder();
        var key = new StringBuilder();
        double rowHeight = box.Height / rows.Count;

        for (int r = 0; r < rows.Count; r++)
        {
            var rowBox = new Box(box.X, box.Y + r * rowHeight, box.Width, rowHeight);
            var (task, answerMark) = DrawRow(rows[r], rowBox, last: r == rows.Count - 1);
            body.Append(task);
            key.Append(task).Append(answerMark);
        }

        return new SheetContent("Odd One Out", "Circle the one that is different!", body.ToString(), key.ToString());
    }

    /// <summary>
    /// Easy/Medium: the odd one is a clearly different picture. Challenge: the odd one is either the same
    /// picture facing the other way or (vehicles only) a look-alike vehicle, mixed across the rows.
    /// Other themes have no look-alike list, so their Challenge "different" rows use any other picture.
    /// </summary>
    public static IReadOnlyList<OddRow> Plan(IPictureSet set, Level level, SeededRandom random)
    {
        bool vehicles = ThemeText.IsVehicles(set);
        bool Asym(Picture p) => p.Mirrorable && (!vehicles || Asymmetric.Contains(ThemeText.AsVehicle(p)!.Value));
        bool canMirror = set.All.Any(Asym);
        bool HasLookAlike(Picture p) => !vehicles || SimilarPairs.Any(s => s.A == ThemeText.AsVehicle(p) || s.B == ThemeText.AsVehicle(p));

        int rowCount = RowCount(level), perRow = ItemsPerRow(level);
        var challengeKinds = new List<bool> { true, true, false, false, false }; // true = mirrored odd one
        random.Shuffle(challengeKinds);

        var mains = set.All.ToList();
        random.Shuffle(mains);
        // Deal odd positions from shuffled blocks so they spread across the row rather than clustering.
        var positions = new List<int>();
        while (positions.Count < rowCount)
        {
            var block = Enumerable.Range(0, perRow).ToList();
            random.Shuffle(block);
            positions.AddRange(block);
        }
        var rows = new List<OddRow>();

        for (int r = 0; r < rowCount; r++)
        {
            // Sets without a mirror-safe picture (e.g. generated images) use a different picture on every row.
            bool mirrored = level == Level.Challenge && challengeKinds[r] && canMirror;
            // Pick a main picture that suits the row kind, preferring ones not used yet.
            var main = mains.First(v => mirrored ? Asym(v) : level != Level.Challenge || HasLookAlike(v));
            mains.Remove(main);
            mains.Add(main);

            bool mainFlip = random.Next(2) == 1;
            OddItem odd;
            if (mirrored)
            {
                odd = new OddItem(main, !mainFlip);
            }
            else
            {
                var pool = set.All.Where(v => v != main && (level == Level.Challenge && vehicles ? AreSimilar(v, main) : !AreSimilar(v, main))).ToList();
                odd = new OddItem(pool[random.Next(pool.Count)], mainFlip);
            }

            int oddIndex = positions[r];
            var items = Enumerable.Range(0, perRow).Select(i => i == oddIndex ? odd : new OddItem(main, mainFlip)).ToList();
            rows.Add(new OddRow(items, oddIndex));
        }
        return rows;
    }

    private static (string Task, string AnswerMark) DrawRow(OddRow row, Box rowBox, bool last)
    {
        var w = new SvgWriter();
        int n = row.Items.Count;
        double cellW = rowBox.Width / n;
        // Keep the 5:3 picture shape, never taller than the row allows.
        double artW = Math.Min(cellW - 18, (rowBox.Height - 30) * Vehicles.CanvasWidth / Vehicles.CanvasHeight);
        artW = Math.Min(artW, 150);
        double artH = artW * Vehicles.CanvasHeight / Vehicles.CanvasWidth;
        double lineWidth = n > 4 ? 1.8 : 2.4;
        string answerMark = string.Empty;

        for (int i = 0; i < n; i++)
        {
            double cx = rowBox.X + (i + 0.5) * cellW, cy = rowBox.CenterY;
            var art = new Box(cx - artW / 2, cy - artH / 2, artW, artH);
            w.Raw(row.Items[i].Picture.Render(art, lineWidth, row.Items[i].Flip));
            if (i == row.OddIndex)
                answerMark = $"<ellipse cx=\"{N(cx)}\" cy=\"{N(cy)}\" rx=\"{N(Math.Min(artW / 2 + 8, cellW / 2 - 1))}\" ry=\"{N(artH / 2 + 10)}\" fill=\"none\" stroke=\"#000\" stroke-width=\"3\"/>";
        }

        if (!last) w.Line(rowBox.X + 20, rowBox.Bottom, rowBox.Right - 20, rowBox.Bottom, 1, dash: "2 6");
        return (w.ToString(), answerMark);
    }
}
