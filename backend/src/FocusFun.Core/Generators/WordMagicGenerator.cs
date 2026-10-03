using System.Text;
using FocusFun.Core.Art;
using FocusFun.Core.Svg;

namespace FocusFun.Core.Generators;

using static SvgWriter;

/// <summary>One word to complete: the answer, its picture hint (if any) and what is printed for the child.</summary>
/// <param name="Word">The answer, uppercase.</param>
/// <param name="Puzzle">Easy/Medium: the word with '_' in each blank. Challenge: the scrambled letters.</param>
/// <param name="Blanks">Positions of the empty boxes (every position on Challenge).</param>
/// <param name="Bank">Easy only: letters to choose from, including the answer.</param>
public sealed record WordMagicItem(string Word, Picture? Picture, string Puzzle, IReadOnlyList<int> Blanks, IReadOnlyList<char> Bank)
{
    /// <summary>Challenge items: every box is empty and <see cref="Puzzle"/> holds the mixed-up letters.</summary>
    public bool IsScramble => Blanks.Count == Word.Length;
}

/// <summary>Early reading and spelling: fill in the first letter, fill in missing letters, or unscramble a word, with a picture as the hint.</summary>
public sealed class WordMagicGenerator : ISheetGenerator
{
    private const double PictureWidth = 130;
    private const double LetterFont = 30;

    /// <summary>Everyday short names for pictures whose full name is long, so early readers still get the word.</summary>
    private static readonly Dictionary<string, string> ShortNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["airplane"] = "PLANE",
        ["autorickshaw"] = "AUTO",
    };

    private const string Consonants = "BCDFGHJKLMNPRSTVWZ";

    public SheetType Type => SheetType.WordMagic;

    public static int ItemCount(Level level) => level == Level.Challenge ? 5 : 6;

    public static (int Min, int Max) Lengths(Level level) => level switch
    {
        Level.Easy => (3, 5),
        Level.Medium => (3, 6),
        _ => (3, 8),
    };

    public static string Instruction(Level level) => level switch
    {
        Level.Easy => "Write the missing letter!",
        Level.Medium => "Fill in the missing letters!",
        _ => "Fix the mixed-up words!",
    };

    public SheetContent Generate(SheetRequest request, Box box, SeededRandom random)
    {
        var items = Plan(request, random);
        var body = new StringBuilder();
        var key = new StringBuilder();
        double rowHeight = Math.Min(box.Height / items.Count, 130);
        double top = box.Y + (box.Height - rowHeight * items.Count) / 2;

        for (int i = 0; i < items.Count; i++)
        {
            var row = new Box(box.X, top + i * rowHeight, box.Width, rowHeight);
            var (task, answers) = items[i].IsScramble ? DrawScramble(items[i], row) : DrawBlanks(items[i], row);
            body.Append(task);
            key.Append(task).Append(answers);
        }
        return new SheetContent("Word Magic", Instruction(request.Level), body.ToString(), key.ToString());
    }

    /// <summary>Picks the words and makes each one's puzzle. Approved <see cref="SheetRequest.Words"/> come first; theme pictures top up the rest.</summary>
    public static IReadOnlyList<WordMagicItem> Plan(SheetRequest request, SeededRandom random)
    {
        var level = request.Level;
        var (min, max) = Lengths(level);
        var pictured = PicturedWords(request.Theme);
        bool Fits(string w) => w.Length >= min && w.Length <= max && (level != Level.Easy || pictured.ContainsKey(w))
            && (level != Level.Challenge || w.Distinct().Count() > 1);

        var approved = (request.Words ?? []).Select(Clean).OfType<string>().Distinct().Where(Fits).ToList();
        // Then the theme's own pictures, and other themes' pictures only if the theme runs short.
        var themeWords = PictureSets.For(request.Theme).All.SelectMany(p => ReadingWords(p.Word)).ToHashSet();
        var fallback = pictured.Keys.Where(w => Fits(w) && !approved.Contains(w)).ToList();
        var ordered = Prefer(approved, level, random)
            .Concat(Prefer(fallback.Where(themeWords.Contains).ToList(), level, random))
            .Concat(Prefer(fallback.Where(w => !themeWords.Contains(w)).ToList(), level, random));

        // Each picture at most once, so PLANE and AIRPLANE never share a sheet.
        var words = new List<(string Word, Picture? Picture)>();
        var usedPictures = new HashSet<Picture>();
        foreach (var word in ordered)
        {
            if (words.Count == ItemCount(level)) break;
            Picture? picture = null;
            if (pictured.TryGetValue(word, out var options))
            {
                var fresh = options.Where(p => !usedPictures.Contains(p)).ToList();
                if (fresh.Count == 0) continue;
                picture = fresh[random.Next(fresh.Count)];
                usedPictures.Add(picture);
            }
            words.Add((word, picture));
        }

        var listed = words.Select(w => w.Word).Concat(pictured.Keys).Concat(approved).ToHashSet();
        return words.Select(w => level switch
        {
            Level.Easy => FirstLetter(w.Word, w.Picture, random),
            Level.Medium => MiddleLetters(w.Word, w.Picture, random),
            _ => Scramble(w.Word, w.Picture, listed, random),
        }).ToList();
    }

    /// <summary>Uppercase single word of plain letters, or null for phrases, digits and blanks.</summary>
    private static string? Clean(string? word)
    {
        string w = (word ?? string.Empty).Trim().ToUpperInvariant();
        return w.Length > 0 && w.All(c => c is >= 'A' and <= 'Z') ? w : null;
    }

    /// <summary>Shuffled, with 4+ letter words first on the harder levels so they feel harder.</summary>
    private static IEnumerable<string> Prefer(List<string> words, Level level, SeededRandom random)
    {
        var list = words.ToList();
        random.Shuffle(list);
        return level == Level.Easy ? list : list.OrderBy(w => w.Length < 4 ? 1 : 0);
    }

    /// <summary>Readable single words for every picture; a word shared across themes keeps the sheet's own theme's pictures.</summary>
    private static Dictionary<string, List<Picture>> PicturedWords(string? theme)
    {
        var map = new Dictionary<string, List<Picture>>();
        var own = PictureSets.For(theme);
        var sets = new[] { own }.Concat(PictureSets.Themes.Order(StringComparer.Ordinal).Select(PictureSets.For).Where(s => s != own));
        foreach (var set in sets)
        {
            bool top = set == own;
            foreach (var picture in set.All)
                foreach (var word in ReadingWords(picture.Word))
                {
                    // Other themes only fill words the sheet's own theme doesn't have.
                    if (!map.TryGetValue(word, out var list)) map[word] = list = [];
                    else if (!top && list.Count > 0) continue;
                    list.Add(picture);
                }
        }
        return map;
    }

    /// <summary>A picture's word, its head noun for phrases ("fire truck" reads TRUCK), and any short name.</summary>
    private static IEnumerable<string> ReadingWords(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0 && Clean(parts[^1]) is { } head) yield return head;
        if (ShortNames.TryGetValue(name, out var shortName)) yield return shortName;
    }

    private static WordMagicItem FirstLetter(string word, Picture? picture, SeededRandom random)
    {
        var bank = Consonants.Where(c => c != word[0]).ToList();
        random.Shuffle(bank);
        var choices = new List<char> { word[0], bank[0], bank[1] };
        random.Shuffle(choices);
        return new WordMagicItem(word, picture, "_" + word[1..], [0], choices);
    }

    /// <summary>One blank, or two apart from each other on words of 5+ letters; never the first letter.</summary>
    private static WordMagicItem MiddleLetters(string word, Picture? picture, SeededRandom random)
    {
        int count = word.Length >= 5 ? random.Next(1, 3) : 1;
        var positions = new List<int> { random.Next(1, word.Length) };
        if (count == 2)
        {
            var apart = Enumerable.Range(1, word.Length - 1).Where(p => Math.Abs(p - positions[0]) > 1).ToList();
            if (apart.Count > 0) positions.Add(apart[random.Next(apart.Count)]);
        }
        positions.Sort();
        var puzzle = word.ToCharArray();
        foreach (int p in positions) puzzle[p] = '_';
        return new WordMagicItem(word, picture, new string(puzzle), positions, []);
    }

    /// <summary>A shuffle that is neither the word itself nor another word on the list.</summary>
    private static WordMagicItem Scramble(string word, Picture? picture, IReadOnlySet<string> listed, SeededRandom random)
    {
        var letters = word.ToCharArray();
        string scrambled = word;
        for (int attempt = 0; attempt < 100 && (scrambled == word || listed.Contains(scrambled)); attempt++)
        {
            random.Shuffle(letters);
            scrambled = new string(letters);
        }
        // Rotating by one always differs for words with 2+ different letters.
        if (scrambled == word) scrambled = word[1..] + word[0];
        return new WordMagicItem(word, picture, scrambled, Enumerable.Range(0, word.Length).ToList(), []);
    }

    private static (string Task, string Answers) DrawBlanks(WordMagicItem item, Box row)
    {
        var w = new SvgWriter();
        var a = new SvgWriter();
        const double size = 44, step = 50;
        const double bankWidth = 132;
        DrawPicture(w, item, row);

        // Left-aligned at one column so every word starts in the same place.
        double x0 = row.X + PictureWidth + 18;
        double y = row.CenterY - size / 2;
        for (int i = 0; i < item.Word.Length; i++)
        {
            double x = x0 + i * step;
            bool blank = item.Puzzle[i] == '_';
            w.Rect(x, y, size, size, blank ? 3.5 : 2, radius: 6);
            var target = blank ? a : w;
            target.Text(x + size / 2, y + size / 2 + LetterFont * 0.36, item.Word[i].ToString(), LetterFont, "middle", bold: true);
        }

        if (item.Bank.Count > 0)
        {
            // A small choice of letters, calm and set apart on the right.
            double bx = row.Right - bankWidth, bh = 44;
            w.Rect(bx, row.CenterY - bh / 2, bankWidth, bh, 2, radius: bh / 2);
            for (int i = 0; i < item.Bank.Count; i++)
            {
                double cx = bx + bankWidth / 2 + (i - (item.Bank.Count - 1) / 2.0) * 38;
                w.Text(cx, row.CenterY + 28 * 0.36, item.Bank[i].ToString(), 28, "middle", bold: true);
                if (item.Bank[i] == item.Word[0]) a.Circle(cx, row.CenterY, 16, 2.5);
            }
        }
        return (w.ToString(), a.ToString());
    }

    /// <summary>Scrambled letters on round tiles, with one write-in box under each.</summary>
    private static (string Task, string Answers) DrawScramble(WordMagicItem item, Box row)
    {
        var w = new SvgWriter();
        var a = new SvgWriter();
        const double size = 42, step = 50, tile = 40, gap = 8;
        DrawPicture(w, item, row);

        double x0 = row.X + PictureWidth + 12;
        double tileY = row.CenterY - (tile + gap + size) / 2;
        double boxY = tileY + tile + gap;
        for (int i = 0; i < item.Word.Length; i++)
        {
            double cx = x0 + i * step + size / 2;
            w.Circle(cx, tileY + tile / 2, tile / 2, 2);
            w.Text(cx, tileY + tile / 2 + 28 * 0.36, item.Puzzle[i].ToString(), 28, "middle", bold: true);
            w.Rect(cx - size / 2, boxY, size, size, 3, radius: 6);
            a.Text(cx, boxY + size / 2 + LetterFont * 0.36, item.Word[i].ToString(), LetterFont, "middle", bold: true);
        }
        return (w.ToString(), a.ToString());
    }

    private static void DrawPicture(SvgWriter w, WordMagicItem item, Box row)
    {
        if (item.Picture is null) return;
        double h = Math.Min(80, row.Height - 16);
        w.Raw(item.Picture.Render(new Box(row.X + 4, row.CenterY - h / 2, PictureWidth - 8, h), 2));
    }
}
