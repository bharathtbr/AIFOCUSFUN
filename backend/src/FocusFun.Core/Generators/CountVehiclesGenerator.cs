using System.Text;
using FocusFun.Core.Art;
using FocusFun.Core.Svg;

namespace FocusFun.Core.Generators;

using static SvgWriter;

/// <summary>One picture drawn in the scene; <see cref="Box"/> is its exact 5:3 drawing area.</summary>
public readonly record struct PlacedPicture(Picture Item, Box Box, bool Flip);

/// <summary>One tally entry: a small copy of the picture being counted and the box the child writes in.</summary>
public readonly record struct TallySlot(Picture Item, int Count, Box PictureBox, Box AnswerBox);

public sealed record CountPlan(Box Scene, IReadOnlyList<PlacedPicture> Placed, IReadOnlyList<TallySlot> Tally);

/// <summary>
/// Pictures from the sheet's theme scattered in one calm frame; the child counts each kind and writes the number
/// in the tally. Named for its first theme: <see cref="SheetType.CountVehicles"/> is kept so saved sheets still load.
/// </summary>
public sealed class CountVehiclesGenerator : ISheetGenerator
{
    private const double MaxPictureWidth = 130;
    private const double TallyRowHeight = 92;
    private const double TallyGap = 18;
    private const double MinGap = 6;

    public SheetType Type => SheetType.CountVehicles;

    public static int TypeCount(Level level) => level switch
    {
        Level.Easy => 2,
        Level.Medium => 3,
        _ => 4,
    };

    /// <summary>Inclusive range of how many of each type appear.</summary>
    public static (int Min, int Max) CountRange(Level level) => level switch
    {
        Level.Easy => (1, 5),
        Level.Medium => (2, 8),
        _ => (3, 10),
    };

    /// <summary>Cap on the total so the pictures stay large enough to count comfortably.</summary>
    public static int MaxTotal(Level level) => level switch
    {
        Level.Easy => 8,
        Level.Medium => 16,
        _ => 24,
    };

    public SheetContent Generate(SheetRequest request, Box box, SeededRandom random)
    {
        var set = ThemeText.Pictures(request, TypeCount(Level.Challenge));
        var plan = Plan(set, request.Level, box, random);
        var w = new SvgWriter();

        // One plain rounded frame keeps the scene calm.
        var s = plan.Scene;
        w.Rect(s.X, s.Y, s.Width, s.Height, 2.5, radius: 14);
        foreach (var p in plan.Placed)
            w.Raw(p.Item.Render(p.Box, p.Box.Width > 90 ? 2 : 1.6, p.Flip));

        foreach (var t in plan.Tally)
        {
            w.Raw(t.Item.Render(t.PictureBox, 2));
            w.Rect(t.AnswerBox.X, t.AnswerBox.Y, t.AnswerBox.Width, t.AnswerBox.Height, 3, radius: 10);
        }

        string task = w.ToString();
        var key = new SvgWriter().Raw(task);
        foreach (var t in plan.Tally)
            key.Text(t.AnswerBox.CenterX, t.AnswerBox.CenterY + 16, t.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), 44, "middle", bold: true);

        var (title, instruction) = Wording(set);
        return new SheetContent(title, instruction, task, key.ToString());
    }

    /// <summary>"Count the Animals" / "Count each animal. Write how many!", generic when the theme's name is too long.</summary>
    public static (string Title, string Instruction) Wording(IPictureSet set)
    {
        string title = $"Count the {ThemeText.Capitalize(set.Plural)}";
        string instruction = $"Count each {ThemeText.Singular(set)}. Write how many!";
        if (title.Length > 28) title = "Count the Pictures";
        if (instruction.Length > 40) instruction = "Count each picture. Write how many!";
        return (title, instruction);
    }

    /// <summary>Chooses the types and counts, then scatters the pictures over a jittered grid (one per cell, so none overlap).</summary>
    public static CountPlan Plan(IPictureSet set, Level level, Box box, SeededRandom random)
    {
        // Types: avoid look-alikes so the counting, not telling pictures apart, is the task.
        var pool = set.All.ToList();
        random.Shuffle(pool);
        var types = new List<Picture>();
        foreach (var v in pool)
            if (types.Count < TypeCount(level) && types.All(t => !OddOneOutGenerator.AreSimilar(t, v))) types.Add(v);

        var (min, max) = CountRange(level);
        int[] counts;
        do counts = types.Select(_ => random.Next(min, max + 1)).ToArray();
        while (counts.Sum() > MaxTotal(level));

        // Tally panel along the bottom: one row for up to 3 types, a 2 x 2 grid for 4.
        int tallyCols = types.Count <= 3 ? types.Count : 2;
        int tallyRows = (types.Count + tallyCols - 1) / tallyCols;
        double tallyH = tallyRows * TallyRowHeight;
        var scene = new Box(box.X, box.Y, box.Width, box.Height - tallyH - TallyGap);
        var tally = new List<TallySlot>();
        double colW = box.Width / tallyCols;
        for (int i = 0; i < types.Count; i++)
        {
            double cellX = box.X + (i % tallyCols) * colW;
            double cellY = scene.Bottom + TallyGap + (i / tallyCols) * TallyRowHeight;
            double answer = TallyRowHeight - 16;
            double picW = Math.Min(110, colW - answer - 26);
            double picH = picW * Vehicles.CanvasHeight / Vehicles.CanvasWidth;
            double groupX = cellX + (colW - picW - 12 - answer) / 2;
            var picture = new Box(groupX, cellY + (TallyRowHeight - picH) / 2, picW, picH);
            var answerBox = new Box(groupX + picW + 12, cellY + 8, answer, answer);
            tally.Add(new TallySlot(types[i], counts[i], picture, answerBox));
        }

        int total = counts.Sum();
        var (cols, rows) = ChooseGrid(total, scene);
        double cellW = (scene.Width - 16) / cols, cellH = (scene.Height - 16) / rows;
        double roomW = Math.Min(cellW * 0.82, cellW - 2 * MinGap);
        double roomH = Math.Min(cellH * 0.78, cellH - 2 * MinGap);
        double vw = Math.Min(MaxPictureWidth, Math.Min(roomW, roomH * Vehicles.CanvasWidth / Vehicles.CanvasHeight));
        double vh = vw * Vehicles.CanvasHeight / Vehicles.CanvasWidth;

        var cells = Enumerable.Range(0, cols * rows).ToList();
        random.Shuffle(cells);
        var kinds = types.SelectMany((t, i) => Enumerable.Repeat(t, counts[i])).ToList();
        random.Shuffle(kinds);

        var placed = new List<PlacedPicture>();
        for (int i = 0; i < kinds.Count; i++)
        {
            int c = cells[i];
            double x0 = scene.X + 8 + (c % cols) * cellW, y0 = scene.Y + 8 + (c / cols) * cellH;
            // Jitter within the cell's spare room, keeping a gap to its edges so neighbours never touch.
            double x = x0 + MinGap + random.NextDouble() * (cellW - vw - 2 * MinGap);
            double y = y0 + MinGap + random.NextDouble() * (cellH - vh - 2 * MinGap);
            placed.Add(new PlacedPicture(kinds[i], new Box(x, y, vw, vh), random.Next(2) == 1));
        }

        return new CountPlan(scene, placed, tally);
    }

    /// <summary>The grid giving the biggest pictures with roughly a third of the cells left empty, so it looks scattered.</summary>
    private static (int Cols, int Rows) ChooseGrid(int total, Box scene)
    {
        int needed = Math.Max(total + 1, (int)Math.Ceiling(total * 1.35));
        (int, int) best = (1, needed);
        double bestWidth = 0;
        for (int cols = 1; cols <= needed; cols++)
        {
            int rows = (needed + cols - 1) / cols;
            double cellW = scene.Width / cols, cellH = scene.Height / rows;
            double width = Math.Min(cellW * 0.82, cellH * 0.78 * Vehicles.CanvasWidth / Vehicles.CanvasHeight);
            if (width > bestWidth) (bestWidth, best) = (width, (cols, rows));
        }
        return best;
    }
}
