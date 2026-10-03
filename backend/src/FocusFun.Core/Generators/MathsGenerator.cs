using System.Globalization;
using System.Text;
using FocusFun.Core.Art;
using FocusFun.Core.Svg;

namespace FocusFun.Core.Generators;

using static SvgWriter;

public enum MathsKind
{
    Addition,
    Subtraction,
    /// <summary>Part-part-whole: A and B are the parts, C the whole.</summary>
    Bond,
}

/// <summary>Which of the three numbers the child writes.</summary>
public enum MathsBlank
{
    First,
    Second,
    Result,
}

/// <summary>One problem: A + B = C, A − B = C, or a bond with parts A, B and whole C; <see cref="Blank"/> is left empty.</summary>
public sealed record MathsProblem(MathsKind Kind, int A, int B, MathsBlank Blank)
{
    public int C => Kind == MathsKind.Subtraction ? A - B : A + B;

    public int Answer => Blank switch
    {
        MathsBlank.First => A,
        MathsBlank.Second => B,
        _ => C,
    };
}

/// <summary>Picture addition, number sentences with a number line, and missing numbers with number bonds.</summary>
public sealed class MathsGenerator : ISheetGenerator
{
    private const double NumberSize = 34;
    private const double AnswerSize = 38;
    private const double BoxSize = 60;
    private const double OpWidth = 40;
    private const double LineWidth = 3;

    public SheetType Type => SheetType.Maths;

    public static int ProblemCount(Level level) => level switch
    {
        Level.Easy => 6,
        _ => 8,
    };

    /// <summary>Biggest number that appears anywhere on the sheet.</summary>
    public static int MaxNumber(Level level) => level == Level.Easy ? 10 : 20;

    public SheetContent Generate(SheetRequest request, Box box, SeededRandom random)
    {
        var problems = Plan(request.Level, random);
        var pictures = PictureSets.For(request).All.ToList();
        random.Shuffle(pictures);

        var task = new SvgWriter();
        var answers = new List<(Box Box, int Value)>();
        switch (request.Level)
        {
            case Level.Easy: DrawEasy(problems, pictures, box, task, answers); break;
            case Level.Medium: DrawMedium(problems, pictures[0], box, task, answers); break;
            default: DrawChallenge(problems, box, task, answers); break;
        }

        string body = task.ToString();
        var key = new SvgWriter().Raw(body);
        foreach (var (b, value) in answers)
            key.Text(b.CenterX, b.CenterY + AnswerSize * 0.36, Num(value), AnswerSize, "middle", bold: true);

        var (title, instruction) = request.Level switch
        {
            Level.Easy => ("Picture Maths", "Add the pictures. Write how many!"),
            Level.Medium => ("Maths Time", "Add or take away. Write the answer!"),
            _ => ("Maths Challenge", "Find the missing numbers!"),
        };
        return new SheetContent(title, instruction, body, key.ToString());
    }

    /// <summary>The sheet's problems, in the order drawn. Every number stays within 0..<see cref="MaxNumber"/>.</summary>
    public static IReadOnlyList<MathsProblem> Plan(Level level, SeededRandom random)
    {
        var problems = new List<MathsProblem>();
        void Add(Func<MathsProblem> make)
        {
            // Retry a few times so no problem repeats on one sheet.
            for (int tries = 0; tries < 50; tries++)
            {
                var p = make();
                if (!problems.Contains(p)) { problems.Add(p); return; }
            }
            problems.Add(make());
        }

        switch (level)
        {
            case Level.Easy:
                // Each group has 1–5 pictures so it can be counted at a glance; sums stay within 10.
                for (int i = 0; i < 6; i++)
                    Add(() => new MathsProblem(MathsKind.Addition, random.Next(1, 6), random.Next(1, 6), MathsBlank.Result));
                break;

            case Level.Medium:
                // Half adding, half taking away, shuffled; sums up to 20, differences never below zero.
                for (int i = 0; i < 4; i++)
                    Add(() =>
                    {
                        int a = random.Next(2, 16);
                        return new MathsProblem(MathsKind.Addition, a, random.Next(1, Math.Min(10, 20 - a) + 1), MathsBlank.Result);
                    });
                for (int i = 0; i < 4; i++)
                    Add(() =>
                    {
                        int a = random.Next(5, 21);
                        return new MathsProblem(MathsKind.Subtraction, a, random.Next(1, Math.Min(10, a - 1) + 1), MathsBlank.Result);
                    });
                random.Shuffle(problems);
                break;

            default:
                // Four missing-number sentences (two of each sign, blank before or after the sign), then four bonds.
                var forms = new List<(MathsKind, MathsBlank)>
                {
                    (MathsKind.Addition, MathsBlank.First), (MathsKind.Addition, MathsBlank.Second),
                    (MathsKind.Subtraction, MathsBlank.First), (MathsKind.Subtraction, MathsBlank.Second),
                };
                random.Shuffle(forms);
                foreach (var (kind, blank) in forms)
                    Add(() =>
                    {
                        if (kind == MathsKind.Addition)
                        {
                            int c = random.Next(6, 21), a = random.Next(1, c);
                            return new MathsProblem(kind, a, c - a, blank);
                        }
                        int whole = random.Next(6, 21), b = random.Next(1, Math.Min(10, whole - 1) + 1);
                        return new MathsProblem(kind, whole, b, blank);
                    });

                var bondBlanks = new List<MathsBlank> { MathsBlank.Result, MathsBlank.First, MathsBlank.Second, random.Next(2) == 0 ? MathsBlank.First : MathsBlank.Second };
                random.Shuffle(bondBlanks);
                foreach (var blank in bondBlanks)
                    Add(() =>
                    {
                        int whole = random.Next(5, 21), a = random.Next(1, whole);
                        return new MathsProblem(MathsKind.Bond, a, whole - a, blank);
                    });
                break;
        }
        return problems;
    }

    /// <summary>Six rows of [pictures] + [pictures] = ☐, with every column lined up.</summary>
    private static void DrawEasy(IReadOnlyList<MathsProblem> problems, IReadOnlyList<Picture> pictures, Box box, SvgWriter w, List<(Box, int)> answers)
    {
        const double cellW = 58, cellH = 38, pad = 6;
        double frameW = 3 * cellW + 2 * pad, frameH = 2 * cellH + 2 * pad;
        double rowW = 2 * frameW + 2 * OpWidth + BoxSize + 16;
        double left = box.X + (box.Width - rowW) / 2;
        double rowH = box.Height / problems.Count;

        for (int i = 0; i < problems.Count; i++)
        {
            var p = problems[i];
            var picture = pictures[i % pictures.Count];
            double cy = box.Y + rowH * (i + 0.5);
            double x = left;

            int[] groups = [p.A, p.B];
            for (int g = 0; g < groups.Length; g++)
            {
                w.Rect(x, cy - frameH / 2, frameW, frameH, 2.5, radius: 12);
                DrawGroup(w, picture, groups[g], new Box(x + pad, cy - frameH / 2 + pad, 3 * cellW, 2 * cellH), cellW, cellH);
                x += frameW;
                DrawOp(w, g == 0 ? '+' : '=', x + OpWidth / 2, cy);
                x += OpWidth;
            }

            var answer = new Box(x + 16, cy - BoxSize / 2, BoxSize, BoxSize);
            w.Rect(answer.X, answer.Y, answer.Width, answer.Height, LineWidth, radius: 10);
            answers.Add((answer, p.Answer));

            if (i < problems.Count - 1)
                w.Line(box.X + 20, box.Y + rowH * (i + 1), box.Right - 20, box.Y + rowH * (i + 1), 1, dash: "2 6");
        }
    }

    /// <summary>Up to three pictures in one row; four as a 2 x 2 square; five as three over two — all centred in the area.</summary>
    private static void DrawGroup(SvgWriter w, Picture picture, int count, Box area, double cellW, double cellH)
    {
        int[] rows = count switch { <= 3 => [count], 4 => [2, 2], _ => [3, 2] };
        double top = area.CenterY - rows.Length * cellH / 2;
        for (int r = 0; r < rows.Length; r++)
        {
            double rowLeft = area.CenterX - rows[r] * cellW / 2;
            for (int c = 0; c < rows[r]; c++)
                w.Raw(picture.Render(new Box(rowLeft + c * cellW + 3, top + r * cellH + 3, cellW - 6, cellH - 6), 1.5));
        }
    }

    /// <summary>A 0–20 number line with a theme picture at the start, then eight number sentences in two columns.</summary>
    private static void DrawMedium(IReadOnlyList<MathsProblem> problems, Picture picture, Box box, SvgWriter w, List<(Box, int)> answers)
    {
        const double lineArea = 96;
        double lineLeft = box.X + 16, lineRight = box.Right - 16, lineY = box.Y + 50;
        double step = (lineRight - lineLeft) / 20;
        w.Raw(picture.Render(new Box(lineLeft - 12, lineY - 50, 60, 36), 1.6));
        w.Line(lineLeft - 8, lineY, lineRight + 8, lineY, LineWidth);
        for (int n = 0; n <= 20; n++)
        {
            double x = lineLeft + n * step;
            bool big = n % 5 == 0;
            w.Line(x, lineY - (big ? 10 : 6), x, lineY + (big ? 10 : 6), big ? 3 : 2);
            w.Text(x, lineY + 32, Num(n), big ? 18 : 15, "middle", bold: big);
        }
        w.Line(box.X + 20, box.Y + lineArea, box.Right - 20, box.Y + lineArea, 1, dash: "2 6");

        DrawSentences(problems, new Box(box.X, box.Y + lineArea, box.Width, box.Height - lineArea), w, answers);
    }

    /// <summary>Four missing-number sentences on top, four part-part-whole bonds (2 x 2) below.</summary>
    private static void DrawChallenge(IReadOnlyList<MathsProblem> problems, Box box, SvgWriter w, List<(Box, int)> answers)
    {
        var sentences = problems.Where(p => p.Kind != MathsKind.Bond).ToList();
        var bonds = problems.Where(p => p.Kind == MathsKind.Bond).ToList();
        double sentenceH = box.Height * 0.42;
        DrawSentences(sentences, new Box(box.X, box.Y, box.Width, sentenceH), w, answers);
        w.Line(box.X + 20, box.Y + sentenceH, box.Right - 20, box.Y + sentenceH, 1, dash: "2 6");

        const double r = 30, spread = 58;
        var area = new Box(box.X, box.Y + sentenceH, box.Width, box.Height - sentenceH);
        double colW = area.Width / 2, rowH = area.Height / 2;
        double drop = Math.Min(90, rowH - 2 * r - 24);
        w.Line(area.CenterX, area.Y + 16, area.CenterX, area.Bottom - 8, 1, dash: "2 6");
        for (int i = 0; i < bonds.Count; i++)
        {
            var p = bonds[i];
            double cx = area.X + colW * (i % 2 + 0.5);
            double wholeY = area.Y + rowH * (i / 2 + 0.5) - drop / 2, partsY = wholeY + drop;
            var circles = new[]
            {
                (MathsBlank.Result, cx, wholeY, p.C),
                (MathsBlank.First, cx - spread, partsY, p.A),
                (MathsBlank.Second, cx + spread, partsY, p.B),
            };
            // Joining lines run between the circle edges, so they never cross a number.
            foreach (double side in new[] { -1.0, 1.0 })
            {
                double dx = side * spread, len = Math.Sqrt(dx * dx + drop * drop);
                w.Line(cx + dx / len * r, wholeY + drop / len * r, cx + dx - dx / len * r, partsY - drop / len * r, LineWidth);
            }
            foreach (var (which, x, y, value) in circles)
            {
                w.Circle(x, y, r, LineWidth, "#fff");
                if (which == p.Blank) answers.Add((new Box(x - r, y - r, 2 * r, 2 * r), value));
                else w.Text(x, y + NumberSize * 0.36, Num(value), NumberSize, "middle", bold: true);
            }
        }
    }

    /// <summary>Number sentences in two columns; each number sits in a fixed slot so the signs line up down the page.</summary>
    private static void DrawSentences(IReadOnlyList<MathsProblem> problems, Box area, SvgWriter w, List<(Box, int)> answers)
    {
        const int columns = 2;
        const double slot = 60, opW = 40, boxSize = 52;
        int rows = (problems.Count + columns - 1) / columns;
        double colW = area.Width / columns, rowH = area.Height / rows;
        double sentenceW = 3 * slot + 2 * opW;
        w.Line(area.CenterX, area.Y + 12, area.CenterX, area.Bottom - 12, 1, dash: "2 6");

        for (int i = 0; i < problems.Count; i++)
        {
            var p = problems[i];
            double cy = area.Y + rowH * (i / columns + 0.5);
            double x = area.X + colW * (i % columns) + (colW - sentenceW) / 2;
            var parts = new[] { (MathsBlank.First, p.A), (MathsBlank.Second, p.B), (MathsBlank.Result, p.C) };
            for (int s = 0; s < parts.Length; s++)
            {
                var (which, value) = parts[s];
                double cx = x + slot / 2;
                if (which == p.Blank)
                {
                    var b = new Box(cx - boxSize / 2, cy - boxSize / 2, boxSize, boxSize);
                    w.Rect(b.X, b.Y, b.Width, b.Height, LineWidth, radius: 10);
                    answers.Add((b, value));
                }
                else
                {
                    w.Text(cx, cy + NumberSize * 0.36, Num(value), NumberSize, "middle", bold: true);
                }
                x += slot;
                if (s < 2)
                {
                    DrawOp(w, s == 1 ? '=' : p.Kind == MathsKind.Subtraction ? '-' : '+', x + opW / 2, cy);
                    x += opW;
                }
            }

            if (i % columns == 0 && i / columns < rows - 1)
                w.Line(area.X + 20, cy + rowH / 2, area.Right - 20, cy + rowH / 2, 1, dash: "2 6");
        }
    }

    /// <summary>Signs are drawn as thick strokes, so they look the same whatever font the printer has.</summary>
    private static void DrawOp(SvgWriter w, char op, double cx, double cy)
    {
        const double half = 10, stroke = 4.5;
        switch (op)
        {
            case '+':
                w.Line(cx - half, cy, cx + half, cy, stroke);
                w.Line(cx, cy - half, cx, cy + half, stroke);
                break;
            case '-':
                w.Line(cx - half, cy, cx + half, cy, stroke);
                break;
            default:
                w.Line(cx - half, cy - 6, cx + half, cy - 6, stroke);
                w.Line(cx - half, cy + 6, cx + half, cy + 6, stroke);
                break;
        }
    }

    private static string Num(int n) => n.ToString(CultureInfo.InvariantCulture);
}
