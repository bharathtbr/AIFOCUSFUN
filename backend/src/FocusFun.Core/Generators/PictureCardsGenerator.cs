using FocusFun.Core.Art;
using FocusFun.Core.Svg;

namespace FocusFun.Core.Generators;

/// <summary>
/// Cards of the topic's pictures with their names: 4 / 6 / 8 per page. For a yoga topic each card is a pose card with a
/// row of breath circles to tick while holding it (3 / 5 / 8 breaths), a calm movement break for a child with ADHD;
/// for any other topic, a picture to name and colour.
/// </summary>
public sealed class PictureCardsGenerator : ISheetGenerator
{
    public SheetType Type => SheetType.PictureCards;

    public static int Cards(Level level) => level switch { Level.Easy => 4, Level.Medium => 6, _ => 8 };

    public static int Breaths(Level level) => level switch { Level.Easy => 3, Level.Medium => 5, _ => 8 };

    public static bool IsYoga(SheetRequest request, IPictureSet set) =>
        request.Theme.Contains("yoga", StringComparison.OrdinalIgnoreCase) || set.Plural.Contains("yoga", StringComparison.OrdinalIgnoreCase);

    public SheetContent Generate(SheetRequest request, Box box, SeededRandom random)
    {
        var set = ThemeText.Pictures(request, atLeast: 1);
        var pictures = set.All.ToList();
        random.Shuffle(pictures);
        pictures = pictures.Take(Cards(request.Level)).ToList();
        bool yoga = IsYoga(request, set);
        int breaths = Breaths(request.Level);

        int cols = 2, rows = (pictures.Count + 1) / 2;
        const double gap = 14;
        double cardW = (box.Width - gap) / cols, cardH = (box.Height - gap * (rows - 1)) / rows;
        var w = new SvgWriter();
        for (int i = 0; i < pictures.Count; i++)
        {
            var card = new Box(box.X + i % cols * (cardW + gap), box.Y + i / cols * (cardH + gap), cardW, cardH);
            w.Rect(card.X, card.Y, card.Width, card.Height, 2.5, radius: 14);

            double footer = yoga ? 62 : 38;
            var art = new Box(card.X + 12, card.Y + 10, card.Width - 24, card.Height - footer - 16);
            w.Raw(pictures[i].Render(art, 2.5));

            string name = ThemeText.Capitalize(pictures[i].Word);
            double nameSize = Math.Min(20, card.Width / Math.Max(6, name.Length * 0.62));
            double nameY = card.Bottom - footer + 18;
            w.Text(card.CenterX, nameY, name, nameSize, "middle", bold: true);

            if (yoga)
            {
                // One circle per breath, to tick while holding the pose.
                double r = Math.Min(9, (card.Width - 40) / (breaths * 2.6));
                double spacing = r * 2.6, startX = card.CenterX - spacing * (breaths - 1) / 2;
                for (int b = 0; b < breaths; b++) w.Circle(startX + b * spacing, nameY + 24, r, 2);
            }
        }

        string title = yoga ? "Yoga Cards" : $"{ThemeText.Capitalize(ThemeText.Singular(set))} Cards";
        string instruction = yoga ? $"Hold each pose for {breaths} breaths. Tick!" : "Say each name, then color it!";
        return new SheetContent(title, instruction, w.ToString(), null);
    }
}
