using System.Text;
using FocusFun.Core.Art;
using FocusFun.Core.Svg;

namespace FocusFun.Core.Generators;

using static SvgWriter;

/// <summary>
/// One letter or digit per sheet: a model with numbered strokes and arrows, then handwriting rows.
/// Easy: every copy dotted with start dots. Medium: guides fade out along the rows. Challenge: copy from the model.
/// </summary>
public sealed class EnglishTracingGenerator : ISheetGenerator
{
    private const double ModelHeight = 130;
    private const double PracticeHeight = 80;  // cap height in points (spec: 48pt+)
    private const int PracticeRows = 3;
    private const string DotDash = "0.1 9";

    private static readonly Dictionary<char, (Vehicle Vehicle, string Word)> VehicleWords = new()
    {
        ['A'] = (Vehicle.Airplane, "Airplane"),
        ['B'] = (Vehicle.Bus, "Bus"),
        ['C'] = (Vehicle.Car, "Car"),
        ['D'] = (Vehicle.DumpTruck, "Dump truck"),
        ['F'] = (Vehicle.FireTruck, "Fire truck"),
        ['G'] = (Vehicle.GarbageTruck, "Garbage truck"),
        ['H'] = (Vehicle.Helicopter, "Helicopter"),
        ['T'] = (Vehicle.SteamTrain, "Train"),
    };

    private enum Guide { Dotted, DottedWithStartDots, Blank }

    public SheetType Type => SheetType.EnglishTracing;

    public SheetContent Generate(SheetRequest request, Box box, SeededRandom random)
    {
        char letter = PickLetter(request.Letters, random);
        var glyph = StrokeFont.Get(letter);
        bool isDigit = char.IsDigit(letter);
        var w = new StringBuilder();

        // Model letter with stroke numbers and direction arrows (Easy and Medium), plain on Challenge.
        double modelScale = ModelHeight / 100;
        var modelOrigin = new GlyphPoint(box.X + 20, box.Y + 10);
        w.Append(DrawGlyph(glyph, modelOrigin, modelScale, dotted: false, strokeWidth: 6));
        if (request.Level != Level.Challenge)
            w.Append(DrawStrokeGuides(glyph, modelOrigin, modelScale, numbered: true));

        // Vehicle word to the right of the model.
        double infoX = box.X + 20 + glyph.Width * modelScale + 40;
        var info = new Box(infoX, box.Y, box.Right - infoX, ModelHeight + 20);
        if (VehicleWords.TryGetValue(letter, out var pair))
        {
            w.Append(Vehicles.Draw(pair.Vehicle, new Box(info.X, info.Y, info.Width, info.Height - 40), 2.5));
            w.Append(new SvgWriter().Text(info.CenterX, info.Bottom - 6, $"{letter} is for {pair.Word}", 22, "middle", bold: true));
        }
        else
        {
            w.Append(new SvgWriter().Text(info.CenterX, info.CenterY, $"Let's write {letter}!", 26, "middle", bold: true));
        }

        // Handwriting rows.
        double rowsTop = box.Y + ModelHeight + 50;
        double rowHeight = (box.Bottom - rowsTop) / PracticeRows;
        double scale = PracticeHeight / 100;
        const double gap = 30;
        double letterW = glyph.Width * scale;
        int perRow = Math.Clamp((int)((box.Width - 40 + gap) / (letterW + gap)), 1, 6);

        for (int row = 0; row < PracticeRows; row++)
        {
            double top = rowsTop + row * rowHeight + (rowHeight - PracticeHeight) / 2;
            w.Append(DrawGuideLines(box, top));
            for (int i = 0; i < perRow; i++)
            {
                var guide = GuideFor(request.Level, row, i);
                if (guide == Guide.Blank) continue;
                var origin = new GlyphPoint(box.X + 20 + i * (letterW + gap), top);
                w.Append(DrawGlyph(glyph, origin, scale, dotted: true, strokeWidth: 4.5));
                if (guide == Guide.DottedWithStartDots)
                    w.Append(DrawStrokeGuides(glyph, origin, scale, numbered: false, arrows: i == 0));
            }
        }

        string kind = isDigit ? "number" : "letter";
        string instruction = request.Level == Level.Challenge
            ? $"Copy the {kind} {letter} on the lines!"
            : $"Trace the {kind} {letter}!";
        return new SheetContent($"Let's Write {letter}", instruction, w.ToString(), null);
    }

    /// <summary>Every supported character with numbered strokes, for checking stroke order against the school's handwriting book.</summary>
    public static string StrokeOrderChart(Box box, int columns = 6)
    {
        var chars = StrokeFont.Characters.OrderBy(c => char.IsDigit(c)).ThenBy(c => c).ToList();
        int rows = (int)Math.Ceiling(chars.Count / (double)columns);
        double cellW = box.Width / columns, cellH = box.Height / rows;
        double scale = Math.Min(cellW / 140, (cellH - 20) / 130);
        var w = new StringBuilder();
        for (int i = 0; i < chars.Count; i++)
        {
            var glyph = StrokeFont.Get(chars[i]);
            double cx = box.X + (i % columns + 0.5) * cellW;
            var origin = new GlyphPoint(cx - glyph.Width * scale / 2, box.Y + i / columns * cellH + 20 * scale + 10);
            w.Append(DrawGlyph(glyph, origin, scale, dotted: false, strokeWidth: 4));
            w.Append(DrawStrokeGuides(glyph, origin, scale, numbered: true));
        }
        return w.ToString();
    }

    private static char PickLetter(string? letters, SeededRandom random)
    {
        var options = (letters ?? string.Empty).Where(StrokeFont.Supports).Select(char.ToUpperInvariant).Distinct().ToList();
        if (options.Count == 0) options = StrokeFont.Characters.Where(char.IsLetter).ToList();
        return options[random.Next(options.Count)];
    }

    private static Guide GuideFor(Level level, int row, int index) => level switch
    {
        Level.Easy => Guide.DottedWithStartDots,
        Level.Medium => row switch
        {
            0 => Guide.DottedWithStartDots,
            1 => Guide.Dotted,
            _ => index < 2 ? Guide.Dotted : Guide.Blank,
        },
        _ => row == 0 && index == 0 ? Guide.Dotted : Guide.Blank,
    };

    private static string DrawGuideLines(Box box, double top)
    {
        var w = new SvgWriter();
        w.Line(box.X, top, box.Right, top, 1.2);
        w.Line(box.X, top + PracticeHeight / 2, box.Right, top + PracticeHeight / 2, 1, dash: "6 6");
        w.Line(box.X, top + PracticeHeight, box.Right, top + PracticeHeight, 2);
        return w.ToString();
    }

    private static string DrawGlyph(Glyph glyph, GlyphPoint origin, double scale, bool dotted, double strokeWidth)
    {
        var w = new SvgWriter();
        foreach (var stroke in glyph.Strokes)
        {
            var d = new StringBuilder();
            for (int i = 0; i < stroke.Count; i++)
            {
                var p = ToPage(stroke[i], origin, scale);
                d.Append(i == 0 ? 'M' : 'L').Append(N(p.X)).Append(' ').Append(N(p.Y));
            }
            w.Path(d.ToString(), strokeWidth, dash: dotted ? DotDash : null);
        }
        return w.ToString();
    }

    /// <summary>Start dot (or numbered badge) at the beginning of each stroke, with an arrow showing which way to go.</summary>
    private static string DrawStrokeGuides(Glyph glyph, GlyphPoint origin, double scale, bool numbered, bool arrows = true)
    {
        var w = new SvgWriter();
        var centre = ToPage(new GlyphPoint(glyph.Width / 2, 50), origin, scale);
        var badges = new List<GlyphPoint>();

        for (int s = 0; s < glyph.Strokes.Count; s++)
        {
            var stroke = glyph.Strokes[s].Select(p => ToPage(p, origin, scale)).ToList();
            var start = stroke[0];

            // Strokes that begin where an earlier one did (B, D, P…) get their badge moved along the stroke so both stay visible.
            var badge = start;
            if (badges.Any(b => Math.Abs(b.X - badge.X) < 18 && Math.Abs(b.Y - badge.Y) < 18))
                badge = PointAlong(stroke, 24);
            badges.Add(badge);

            if (arrows)
            {
                var ahead = PointAlong(stroke, 18 * scale + 8);
                double dx = ahead.X - start.X, dy = ahead.Y - start.Y;
                double len = Math.Max(1e-6, Math.Sqrt(dx * dx + dy * dy));
                dx /= len; dy /= len;

                // Offset the arrow to the side of the stroke facing away from the letter's centre.
                double nx = -dy, ny = dx;
                if ((start.X - centre.X) * nx + (start.Y - centre.Y) * ny < 0) { nx = -nx; ny = -ny; }
                double off = numbered ? 16 : 11, arrowLen = numbered ? 26 : 18;
                double ax = start.X + nx * off + dx * 6, ay = start.Y + ny * off + dy * 6;
                double bx = ax + dx * arrowLen, by = ay + dy * arrowLen;
                w.Line(ax, ay, bx, by, 2);
                w.Path($"M{N(bx - dx * 7 + nx * 5)} {N(by - dy * 7 + ny * 5)}L{N(bx)} {N(by)}L{N(bx - dx * 7 - nx * 5)} {N(by - dy * 7 - ny * 5)}", 2);
            }

            if (numbered)
            {
                w.Circle(badge.X, badge.Y, 10, 2, "#fff");
                w.Text(badge.X, badge.Y + 4.5, (s + 1).ToString(), 13, "middle", bold: true);
            }
            else
            {
                w.Circle(start.X, start.Y, 4.5, 1, "#000");
            }
        }
        return w.ToString();
    }

    private static GlyphPoint PointAlong(IReadOnlyList<GlyphPoint> stroke, double distance)
    {
        double travelled = 0;
        for (int i = 1; i < stroke.Count; i++)
        {
            double dx = stroke[i].X - stroke[i - 1].X, dy = stroke[i].Y - stroke[i - 1].Y;
            double seg = Math.Sqrt(dx * dx + dy * dy);
            if (travelled + seg >= distance && seg > 0)
            {
                double t = (distance - travelled) / seg;
                return new GlyphPoint(stroke[i - 1].X + dx * t, stroke[i - 1].Y + dy * t);
            }
            travelled += seg;
        }
        return stroke[^1];
    }

    private static GlyphPoint ToPage(GlyphPoint p, GlyphPoint origin, double scale) =>
        new(origin.X + p.X * scale, origin.Y + p.Y * scale);
}
