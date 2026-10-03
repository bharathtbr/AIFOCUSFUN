using System.Text;
using FocusFun.Core.Art;
using FocusFun.Core.Svg;

namespace FocusFun.Core.Generators;

/// <summary>
/// One Telugu or Hindi letter (or word) per sheet: a solid model, then handwriting rows of dotted outlines to write inside.
/// Letters come from the typed letters, else the topic's words in this script (e.g. an admin's "Telugu Varnamala" list),
/// else the level: Easy vowels, Medium consonants, Challenge consonant + vowel sign (guninthalu / barakhadi).
/// Easy: every copy dotted. Medium: dots thin out along the rows. Challenge: copy from the model.
/// No start dots or arrows yet: fonts carry no stroke order, and that needs checking against the school's book first.
/// </summary>
/// <param name="practiceHeight">Consonant body height on the lines (spec: 48pt+), leaving room above and below for signs.</param>
public abstract class IndicTracingGenerator(IndicScript script, double practiceHeight) : ISheetGenerator
{
    private const double ModelHeight = 130;
    private const int PracticeRows = 3;
    private const string Dots = "0.1 7";

    private enum Guide { Dotted, Faint, Blank }

    public abstract SheetType Type { get; }

    public IndicScript Script => script;

    public SheetContent Generate(SheetRequest request, Box box, SeededRandom random)
    {
        string text = PickLetter(request, random);
        var outline = script.Font.Outline(text)!;
        string? sound = script.SoundOf(text);
        var w = new StringBuilder();

        // Solid model letter, top left.
        double modelScale = Math.Min(ModelHeight / outline.Height, box.Width * 0.45 / outline.Width);
        double modelX = box.X + 20, modelY = box.Y + 10 + (ModelHeight - outline.Height * modelScale) / 2;
        w.Append(new SvgWriter().Path(outline.ToSvgPath(modelX, modelY, modelScale), 0, fill: "#000"));

        // Its sound to the right, shrunk to fit beside a wide word.
        double infoX = modelX + outline.Width * modelScale + 40;
        var info = new Box(infoX, box.Y, box.Right - infoX, ModelHeight + 20);
        string heading = $"Let's write {text}!";
        double headingSize = Math.Min(26, info.Width / (heading.Length * 0.6));
        var label = new SvgWriter();
        if (sound is null)
            label.Text(info.CenterX, info.CenterY, heading, headingSize, "middle", bold: true);
        else
        {
            label.Text(info.CenterX, info.CenterY - 8, heading, headingSize, "middle", bold: true);
            label.Text(info.CenterX, info.CenterY + 28, $"Say “{sound}”", 22, "middle");
        }
        w.Append(label);

        // Handwriting rows. Every letter is sized so the consonant body (క / क) fills the lines, as in a handwriting book;
        // vowel signs and conjunct parts reach above and below them.
        var body = script.Font.Outline(script.Consonants[0].Text)!;
        double rowsTop = box.Y + ModelHeight + 50;
        double rowHeight = (box.Bottom - rowsTop) / PracticeRows;
        double scale = Math.Min(practiceHeight / body.Height, Math.Min((rowHeight - 12) / outline.Height, (box.Width - 40) / outline.Width));
        const double gap = 30;
        double letterW = outline.Width * scale;
        int perRow = Math.Clamp((int)((box.Width - 40 + gap) / (letterW + gap)), 1, 6);

        for (int row = 0; row < PracticeRows; row++)
        {
            double rowTop = rowsTop + row * rowHeight;
            double top = rowTop + (rowHeight - practiceHeight) / 2;
            var lines = new SvgWriter();
            lines.Line(box.X, top, box.Right, top, 1.2);
            lines.Line(box.X, top + practiceHeight, box.Right, top + practiceHeight, 2);
            w.Append(lines);

            double y = Math.Clamp(top + (outline.MinY - body.MinY) * scale, rowTop + 6, rowTop + rowHeight - 6 - outline.Height * scale);
            for (int i = 0; i < perRow; i++)
            {
                var guide = GuideFor(request.Level, row, i);
                if (guide == Guide.Blank) continue;
                string d = outline.ToSvgPath(box.X + 20 + i * (letterW + gap), y, scale);
                // Faint guides keep the spacing but use smaller dots, so detailed letters stay readable.
                w.Append(new SvgWriter().Path(d, guide == Guide.Faint ? 1.8 : 3, dash: Dots));
            }
        }

        string kind = script.Split(text).Count() > 1 && sound is null ? "word" : "letter";
        string instruction = request.Level == Level.Challenge
            ? $"Copy the {script.Name} {kind} {text} on the lines!"
            : $"Trace the {script.Name} {kind} {text}!";
        return new SheetContent($"Let's Write {text}", instruction, w.ToString(), null);
    }

    /// <summary>Typed letters first, then the topic's words written in this script, then the level's built-in set.</summary>
    public string PickLetter(SheetRequest request, SeededRandom random)
    {
        var options = script.Split(request.Letters).Where(CanTrace).Distinct().ToList();
        if (options.Count == 0)
            options = (request.Words ?? []).Select(w => w.Trim()).Where(CanTrace).Distinct().ToList();
        if (options.Count == 0)
            options = (request.Level switch
            {
                Level.Easy => script.Vowels,
                Level.Medium => script.Consonants,
                _ => script.Combinations,
            }).Select(l => l.Text).ToList();
        return options[random.Next(options.Count)];
    }

    public bool CanTrace(string text) => text.Length <= 24 && script.Covers(text) && script.Font.Outline(text) is not null;

    private static Guide GuideFor(Level level, int row, int index) => level switch
    {
        Level.Easy => Guide.Dotted,
        Level.Medium => row switch
        {
            0 => Guide.Dotted,
            1 => Guide.Faint,
            _ => index < 2 ? Guide.Faint : Guide.Blank,
        },
        _ => row == 0 && index == 0 ? Guide.Dotted : Guide.Blank,
    };
}

// Telugu letters are rounder and more detailed than Devanagari ones, so they get a taller body.
public sealed class TeluguTracingGenerator() : IndicTracingGenerator(IndicScript.Telugu, 80)
{
    public override SheetType Type => SheetType.TeluguTracing;
}

public sealed class HindiTracingGenerator() : IndicTracingGenerator(IndicScript.Hindi, 70)
{
    public override SheetType Type => SheetType.HindiTracing;
}
