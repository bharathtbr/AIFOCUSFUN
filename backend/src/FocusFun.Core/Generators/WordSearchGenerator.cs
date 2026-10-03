using System.Text;
using FocusFun.Core.Art;
using FocusFun.Core.Svg;

namespace FocusFun.Core.Generators;

using static SvgWriter;

/// <summary>A step through the grid; words are only ever read forwards along one of these.</summary>
public readonly record struct WordDirection(int DRow, int DCol)
{
    public static readonly WordDirection Across = new(0, 1);
    public static readonly WordDirection Down = new(1, 0);
    public static readonly WordDirection DownRight = new(1, 1);
    public static readonly WordDirection UpRight = new(-1, 1);
}

/// <summary>Where one listed word sits in the grid.</summary>
public sealed record WordPlacement(string Word, int Row, int Col, WordDirection Direction)
{
    public Cell CellAt(int i) => new(Row + i * Direction.DRow, Col + i * Direction.DCol);
}

/// <summary>A filled letter grid plus the words hidden in it, in the order they are listed on the sheet.</summary>
public sealed record WordSearchPuzzle(char[,] Letters, IReadOnlyList<WordPlacement> Placements)
{
    public int Size => Letters.GetLength(0);
}

/// <summary>
/// Find the topic's words hidden in a letter grid. Approved <see cref="SheetRequest.Words"/> come first, topped up from
/// the theme's picture names (or the vehicle bank). Words never run backwards; harder levels add down and diagonals.
/// </summary>
public sealed class WordSearchGenerator : ISheetGenerator
{
    private const double RowHeight = 40;
    private const double ListGap = 18;

    /// <summary>Vehicle words with a matching picture (short names too); early readers can match the drawing to the word.</summary>
    private static readonly Dictionary<string, Vehicle> Pictured = new()
    {
        ["CAR"] = Vehicle.Car,
        ["BUS"] = Vehicle.Bus,
        ["TRAIN"] = Vehicle.SteamTrain,
        ["PLANE"] = Vehicle.Airplane,
        ["TRUCK"] = Vehicle.DumpTruck,
        ["AUTO"] = Vehicle.Autorickshaw,
        ["DOZER"] = Vehicle.Bulldozer,
        ["HELICOPTER"] = Vehicle.Helicopter,
    };

    /// <summary>No word contains another, so a listed word can never hide inside a longer one.</summary>
    private static readonly string[] Bank =
    [
        .. Pictured.Keys,
        "VAN", "TAXI", "BOAT", "SHIP", "TRAM", "JEEP", "BIKE", "JET",
        "CRANE", "METRO", "ROCKET", "TRACTOR",
    ];

    private static readonly WordDirection[] AllDirections =
    [
        new(0, 1), new(1, 0), new(1, 1), new(-1, 1), new(0, -1), new(-1, 0), new(-1, -1), new(1, -1),
    ];

    public SheetType Type => SheetType.WordSearch;

    public static int GridSize(Level level) => level switch
    {
        Level.Easy => 8,
        Level.Medium => 10,
        _ => 11,
    };

    public static int WordCount(Level level) => level switch
    {
        Level.Easy => 5,
        Level.Medium => 8,
        _ => 10,
    };

    public static IReadOnlyList<WordDirection> Directions(Level level) => level switch
    {
        Level.Easy => [WordDirection.Across],
        Level.Medium => [WordDirection.Across, WordDirection.Down],
        _ => [WordDirection.Across, WordDirection.Down, WordDirection.DownRight, WordDirection.UpRight],
    };

    public SheetContent Generate(SheetRequest request, Box box, SeededRandom random)
    {
        var puzzle = Build(request, random);
        var set = PictureSets.For(request);
        int n = puzzle.Size;
        int columns = 2;
        int listRows = (puzzle.Placements.Count + columns - 1) / columns;
        double listHeight = listRows * RowHeight;

        double cell = Math.Min(56, Math.Min(box.Width / n, (box.Height - listHeight - ListGap) / n));
        double gridSize = cell * n;
        double top = box.Y + (box.Height - gridSize - ListGap - listHeight) / 2;
        var grid = new Box(box.CenterX - gridSize / 2, top, gridSize, gridSize);
        var list = new Box(box.X, grid.Bottom + ListGap, box.Width, listHeight);

        string body = DrawGrid(puzzle, grid, cell) + DrawWordList(puzzle.Placements, set, list, columns);
        string key = body + DrawAnswers(puzzle.Placements, grid, cell);
        return new SheetContent("Word Search", "Find and circle the words!", body, key);
    }

    /// <summary>A vehicle puzzle from the built-in words.</summary>
    public static WordSearchPuzzle Build(Level level, SeededRandom random) =>
        Build(new SheetRequest(SheetType.WordSearch, level, string.Empty), random);

    /// <summary>Picks and places the words, then fills the rest so that each listed word appears exactly once.</summary>
    public static WordSearchPuzzle Build(SheetRequest request, SeededRandom random)
    {
        var level = request.Level;
        int n = GridSize(level);
        var directions = Directions(level);
        for (int round = 0; ; round++)
        {
            // Long approved words can crowd a small grid; after repeated misses, allow only shorter ones.
            var words = PickWords(request, round / 40, random);
            var letters = new char[n, n];
            var placements = new List<WordPlacement>();
            // Longest words first: they have the fewest places to go.
            foreach (var word in words.OrderByDescending(w => w.Length))
            {
                var placement = TryPlace(word, letters, directions, random);
                if (placement is null) break;
                placements.Add(placement);
            }
            if (placements.Count < words.Count) continue;

            var listed = words.Select(w => placements.First(p => p.Word == w)).ToList();
            for (int attempt = 0; attempt < 50; attempt++)
            {
                var filled = Fill(letters, random);
                if (words.All(w => CountOccurrences(filled, w) == 1))
                    return new WordSearchPuzzle(filled, listed);
            }
        }
    }

    /// <summary>How many times <paramref name="word"/> can be read in any of the 8 directions (backwards too, to avoid confusing copies).</summary>
    public static int CountOccurrences(char[,] letters, string word)
    {
        int n = letters.GetLength(0), count = 0;
        for (int r = 0; r < n; r++)
            for (int c = 0; c < n; c++)
                foreach (var d in AllDirections)
                    if (Matches(letters, word, r, c, d)) count++;
        return count;
    }

    public static bool Matches(char[,] letters, string word, int row, int col, WordDirection d)
    {
        int n = letters.GetLength(0);
        for (int i = 0; i < word.Length; i++)
        {
            int r = row + i * d.DRow, c = col + i * d.DCol;
            if (r < 0 || c < 0 || r >= n || c >= n || letters[r, c] != word[i]) return false;
        }
        return true;
    }

    /// <summary>
    /// Approved words first (any length that fits the grid), then built-in words, whose length unlocks with the level.
    /// Easy prefers pictured words so there is a drawing to match, but takes unpictured ones rather than run short.
    /// No chosen word contains another (forwards or backwards), so each can be hidden exactly once.
    /// </summary>
    private static List<string> PickWords(SheetRequest request, int shorten, SeededRandom random)
    {
        var level = request.Level;
        var set = PictureSets.For(request);
        int builtInMax = level switch { Level.Easy => 5, Level.Medium => 6, _ => 10 };
        int approvedMax = Math.Max(builtInMax, GridSize(level) - shorten);
        bool Usable(string w) => w.Length >= 3 && !IsPalindrome(w);
        bool HasPicture(string w) => PictureFor(w, set) is not null;

        var approved = (request.Words ?? []).Select(Clean).OfType<string>().Distinct()
            .Where(w => Usable(w) && w.Length <= approvedMax).ToList();
        var themeBank = ThemeBank(request.Theme).Where(w => Usable(w) && w.Length <= builtInMax && !approved.Contains(w)).ToList();
        var vehicleBank = Bank.Where(w => w.Length <= builtInMax && !approved.Contains(w) && !themeBank.Contains(w)).ToList();

        List<List<string>> tiers = level == Level.Easy
            ?
            [
                approved.Where(HasPicture).ToList(), approved.Where(w => !HasPicture(w)).ToList(),
                themeBank.Where(HasPicture).ToList(), vehicleBank.Where(HasPicture).ToList(),
                themeBank.Where(w => !HasPicture(w)).ToList(), vehicleBank.Where(w => !HasPicture(w)).ToList(),
            ]
            : [approved, themeBank, vehicleBank];

        int count = WordCount(level);
        var chosen = new List<string>();
        foreach (var tier in tiers)
        {
            if (chosen.Count == count) break;
            if (tier.Count == 0) continue;
            random.Shuffle(tier);
            foreach (var w in tier)
                if (chosen.Count < count && chosen.All(c => !Overlaps(c, w))) chosen.Add(w);
        }
        return chosen;
    }

    /// <summary>Built-in words for the theme: its pictures' single-word names, or the vehicle bank for vehicles and unknown themes.</summary>
    private static IReadOnlyList<string> ThemeBank(string? theme)
    {
        var set = PictureSets.For(theme);
        if (ThemeText.IsVehicles(set) || !string.Equals(set.Theme, theme?.Trim(), StringComparison.OrdinalIgnoreCase)) return Bank;
        return set.All.Select(p => Clean(p.Word)).OfType<string>().Distinct().ToList();
    }

    /// <summary>The drawing shown beside a listed word: the vehicle short names first, then the sheet's own theme, then any theme.</summary>
    private static Picture? PictureFor(string word, IPictureSet own)
    {
        if (Pictured.TryGetValue(word, out var vehicle))
            return PictureSets.For("vehicles").All.First(p => ThemeText.AsVehicle(p) == vehicle);
        return own.All.FirstOrDefault(p => string.Equals(p.Word, word, StringComparison.OrdinalIgnoreCase)) ?? PictureSets.Find(word);
    }

    /// <summary>Uppercase single word of plain letters, or null for phrases, digits and blanks.</summary>
    private static string? Clean(string? word)
    {
        string w = (word ?? string.Empty).Trim().ToUpperInvariant();
        return w.Length > 0 && w.All(c => c is >= 'A' and <= 'Z') ? w : null;
    }

    /// <summary>A palindrome reads the same backwards, so it would always be found twice.</summary>
    private static bool IsPalindrome(string w) => w.SequenceEqual(w.Reverse());

    /// <summary>True when one word, read either way, hides inside the other.</summary>
    private static bool Overlaps(string a, string b)
    {
        string ra = new(a.Reverse().ToArray());
        return a.Contains(b) || b.Contains(a) || ra.Contains(b) || b.Contains(ra);
    }

    /// <summary>Words never share cells, which keeps the answer key clean and the search fair for young children.</summary>
    private static WordPlacement? TryPlace(string word, char[,] letters, IReadOnlyList<WordDirection> directions, SeededRandom random)
    {
        int n = letters.GetLength(0);
        var options = new List<WordPlacement>();
        foreach (var d in directions)
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++)
                {
                    var p = new WordPlacement(word, r, c, d);
                    if (Enumerable.Range(0, word.Length).All(i => Free(letters, p.CellAt(i)))) options.Add(p);
                }
        if (options.Count == 0) return null;

        // Pick the direction first so rare directions (diagonals) are not swamped by the many across/down spots.
        var usable = directions.Where(d => options.Any(o => o.Direction == d)).ToList();
        var dir = usable[random.Next(usable.Count)];
        var inDir = options.Where(o => o.Direction == dir).ToList();
        var chosen = inDir[random.Next(inDir.Count)];
        for (int i = 0; i < word.Length; i++)
        {
            var cell = chosen.CellAt(i);
            letters[cell.Row, cell.Col] = word[i];
        }
        return chosen;
    }

    private static bool Free(char[,] letters, Cell c)
    {
        int n = letters.GetLength(0);
        return c.Row >= 0 && c.Col >= 0 && c.Row < n && c.Col < n && letters[c.Row, c.Col] == '\0';
    }

    private static char[,] Fill(char[,] letters, SeededRandom random)
    {
        var filled = (char[,])letters.Clone();
        int n = filled.GetLength(0);
        for (int r = 0; r < n; r++)
            for (int c = 0; c < n; c++)
                if (filled[r, c] == '\0') filled[r, c] = (char)('A' + random.Next(26));
        return filled;
    }

    private static string DrawGrid(WordSearchPuzzle puzzle, Box grid, double cell)
    {
        int n = puzzle.Size;
        var w = new SvgWriter();
        var d = new StringBuilder();
        for (int i = 1; i < n; i++)
        {
            d.Append($"M{N(grid.X + i * cell)} {N(grid.Y)}V{N(grid.Bottom)}");
            d.Append($"M{N(grid.X)} {N(grid.Y + i * cell)}H{N(grid.Right)}");
        }
        w.Path(d.ToString(), 0.75);
        w.Rect(grid.X, grid.Y, grid.Width, grid.Height, 3, radius: 10);

        double fontSize = cell * 0.6;
        for (int r = 0; r < n; r++)
            for (int c = 0; c < n; c++)
                w.Text(grid.X + (c + 0.5) * cell, grid.Y + (r + 0.5) * cell + fontSize * 0.35, puzzle.Letters[r, c].ToString(), fontSize, "middle", bold: true);
        return w.ToString();
    }

    private static string DrawWordList(IReadOnlyList<WordPlacement> placements, IPictureSet set, Box list, int columns)
    {
        var w = new SvgWriter();
        double colWidth = Math.Min(230, list.Width / columns);
        double left = list.CenterX - colWidth * columns / 2;
        const double tick = 22, fontSize = 22;

        for (int i = 0; i < placements.Count; i++)
        {
            string word = placements[i].Word;
            double x = left + (i % columns) * colWidth + 12;
            double cy = list.Y + (i / columns + 0.5) * RowHeight;
            w.Rect(x, cy - tick / 2, tick, tick, 2.5, radius: 4);
            w.Text(x + tick + 12, cy + fontSize * 0.35, word, fontSize, bold: true);
            if (PictureFor(word, set) is { } picture)
                w.Raw(picture.Render(new Box(x + colWidth - 24 - 60, cy - 17, 60, 34), 1.5));
        }
        return w.ToString();
    }

    /// <summary>A stroke-only capsule around each word, rotated to follow diagonal words.</summary>
    private static string DrawAnswers(IReadOnlyList<WordPlacement> placements, Box grid, double cell)
    {
        var sb = new StringBuilder();
        foreach (var p in placements)
        {
            var first = p.CellAt(0);
            var last = p.CellAt(p.Word.Length - 1);
            double x1 = grid.X + (first.Col + 0.5) * cell, y1 = grid.Y + (first.Row + 0.5) * cell;
            double x2 = grid.X + (last.Col + 0.5) * cell, y2 = grid.Y + (last.Row + 0.5) * cell;
            double length = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
            double angle = Math.Atan2(y2 - y1, x2 - x1) * 180 / Math.PI;
            double h = cell * 0.78, w = length + h;
            double cx = (x1 + x2) / 2, cy = (y1 + y2) / 2;
            sb.Append($"<rect x=\"{N(cx - w / 2)}\" y=\"{N(cy - h / 2)}\" width=\"{N(w)}\" height=\"{N(h)}\" rx=\"{N(h / 2)}\" fill=\"none\" stroke=\"#000\" stroke-width=\"3\" transform=\"rotate({N(angle)} {N(cx)} {N(cy)})\"/>");
        }
        return sb.ToString();
    }
}
