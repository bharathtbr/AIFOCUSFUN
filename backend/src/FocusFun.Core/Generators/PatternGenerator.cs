using System.Text;
using FocusFun.Core.Art;
using FocusFun.Core.Svg;

namespace FocusFun.Core.Generators;

using static SvgWriter;

/// <summary>Rows of the theme's pictures following a pattern; the child circles which picture comes next.</summary>
public sealed class PatternGenerator : ISheetGenerator
{
    private const int Rows = 4;
    private const int Choices = 3;

    public SheetType Type => SheetType.Pattern;

    public SheetContent Generate(SheetRequest request, Box box, SeededRandom random)
    {
        var body = new StringBuilder();
        var key = new StringBuilder();
        double rowHeight = box.Height / Rows;
        var set = ThemeText.Pictures(request, Choices);

        for (int row = 0; row < Rows; row++)
        {
            var sequence = MakeSequence(set, request.Level, row, random);
            var rowBox = new Box(box.X, box.Y + row * rowHeight, box.Width, rowHeight);
            var (task, answerMark) = DrawRow(sequence, set, rowBox, random);
            body.Append(task);
            key.Append(task).Append(answerMark);
        }

        string instruction = $"Circle the {ThemeText.Singular(set)} that comes next!";
        if (instruction.Length > 40) instruction = "Circle the one that comes next!";
        return new SheetContent("What Comes Next?", instruction, body.ToString(), key.ToString());
    }

    /// <summary>The full sequence; its last item is the answer and is hidden on the sheet.</summary>
    public static IReadOnlyList<Picture> MakeSequence(IPictureSet set, Level level, int row, SeededRandom random)
    {
        string rule = level switch
        {
            Level.Easy => "AB",
            Level.Medium => row % 2 == 0 ? "ABC" : "AABB",
            _ => row % 2 == 0 ? "growing" : (row == 1 ? "ABBC" : "ABC"),
        };

        var pictures = set.All.ToList();
        random.Shuffle(pictures);
        Picture A = pictures[0], B = pictures[1], C = pictures[2];

        if (rule == "growing")
            return [A, B, A, A, B, A, A, A, B];   // A B, A A B, A A A B

        var unit = rule.Select(ch => ch switch { 'A' => A, 'B' => B, _ => C }).ToList();
        // At least two full repeats before the gap, and a varied gap position so the answer is not always "A".
        int repeats = unit.Count == 2 ? 3 : 2;
        int length = Math.Min(9, unit.Count * repeats + 1 + random.Next(unit.Count));
        return Enumerable.Range(0, length).Select(i => unit[i % unit.Count]).ToList();
    }

    private static (string Task, string AnswerMark) DrawRow(IReadOnlyList<Picture> sequence, IPictureSet set, Box row, SeededRandom random)
    {
        var w = new SvgWriter();
        int cells = sequence.Count;
        double cellW = Math.Min(72, row.Width / cells);
        double seqHeight = row.Height * 0.46;
        double left = row.X + (row.Width - cellW * cells) / 2;
        double top = row.Y + 6;

        for (int i = 0; i < cells; i++)
        {
            var cell = new Box(left + i * cellW + 3, top, cellW - 6, seqHeight);
            if (i < cells - 1)
            {
                w.Raw(sequence[i].Render(Inset(cell, 4), 1.6));
            }
            else
            {
                w.Rect(cell.X, cell.Y, cell.Width, cell.Height, 2.5, radius: 8);
                w.Text(cell.CenterX, cell.CenterY + 12, "?", 34, "middle", bold: true);
            }
        }

        // Choices: the answer plus two other pictures, in a random order.
        var answer = sequence[^1];
        var options = sequence.Distinct().Where(v => v != answer).Take(Choices - 1).ToList();
        foreach (var v in set.All)
            if (options.Count < Choices - 1 && v != answer && !options.Contains(v)) options.Add(v);
        options.Add(answer);
        random.Shuffle(options);

        double choiceW = Math.Min(110, row.Width / 5);
        double choiceTop = top + seqHeight + 10;
        double choiceH = row.Height - seqHeight - 26;
        double choiceLeft = row.CenterX - choiceW * Choices / 2;
        string answerMark = string.Empty;

        for (int i = 0; i < options.Count; i++)
        {
            var choice = new Box(choiceLeft + i * choiceW + 8, choiceTop, choiceW - 16, choiceH);
            w.Raw(options[i].Render(Inset(choice, 6), 1.6));
            if (options[i] == answer)
                answerMark = new SvgWriter().Raw(
                    $"<ellipse cx=\"{N(choice.CenterX)}\" cy=\"{N(choice.CenterY)}\" rx=\"{N(choice.Width / 2 + 4)}\" ry=\"{N(choice.Height / 2 + 4)}\" fill=\"none\" stroke=\"#000\" stroke-width=\"3\"/>").ToString();
        }

        w.Line(row.X + 20, row.Bottom - 4, row.Right - 20, row.Bottom - 4, 1, dash: "2 6");
        return (w.ToString(), answerMark);
    }

    private static Box Inset(Box b, double by) => new(b.X + by, b.Y + by, b.Width - 2 * by, b.Height - 2 * by);
}
