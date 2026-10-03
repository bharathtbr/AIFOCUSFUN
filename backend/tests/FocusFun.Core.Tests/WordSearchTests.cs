using FocusFun.Core.Generators;

namespace FocusFun.Core.Tests;

public class WordSearchTests
{
    private readonly SheetRenderer _renderer = SheetRenderer.CreateDefault();

    public static TheoryData<Level, string> Cases()
    {
        var data = new TheoryData<Level, string>();
        foreach (var level in Enum.GetValues<Level>())
            for (int i = 0; i < 40; i++)
                data.Add(level, $"seed-{i}");
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Every_listed_word_is_hidden_exactly_once_in_an_allowed_direction(Level level, string seed)
    {
        var puzzle = WordSearchGenerator.Build(level, new SeededRandom(seed));
        var allowed = WordSearchGenerator.Directions(level);

        Assert.Equal(WordSearchGenerator.GridSize(level), puzzle.Size);
        Assert.Equal(WordSearchGenerator.WordCount(level), puzzle.Placements.Count);
        Assert.Equal(puzzle.Placements.Count, puzzle.Placements.Select(p => p.Word).Distinct().Count());

        foreach (var p in puzzle.Placements)
        {
            Assert.Contains(p.Direction, allowed);
            Assert.True(WordSearchGenerator.Matches(puzzle.Letters, p.Word, p.Row, p.Col, p.Direction), p.Word);
            Assert.Equal(1, WordSearchGenerator.CountOccurrences(puzzle.Letters, p.Word));
        }

        for (int r = 0; r < puzzle.Size; r++)
            for (int c = 0; c < puzzle.Size; c++)
                Assert.InRange(puzzle.Letters[r, c], 'A', 'Z');
    }

    [Theory]
    [InlineData(Level.Easy)]
    [InlineData(Level.Medium)]
    [InlineData(Level.Challenge)]
    public void Same_seed_gives_the_same_sheet(Level level)
    {
        var request = new SheetRequest(SheetType.WordSearch, level, "repeat");
        var header = new SheetHeader(null, null);
        var a = _renderer.Render(request, header);
        var b = _renderer.Render(request, header);
        Assert.Equal(a.Svg, b.Svg);
        Assert.Equal(a.AnswerKeySvg, b.AnswerKeySvg);
        Assert.NotEqual(a.Svg, _renderer.Render(request with { Seed = "other" }, header).Svg);
    }

    [Fact]
    public void Challenge_uses_diagonals_across_seeds()
    {
        var used = Enumerable.Range(0, 20)
            .SelectMany(i => WordSearchGenerator.Build(Level.Challenge, new SeededRandom($"d{i}")).Placements)
            .Select(p => p.Direction)
            .ToHashSet();
        Assert.Contains(WordDirection.DownRight, used);
        Assert.Contains(WordDirection.UpRight, used);
    }

    [Fact]
    public void Instruction_fits_on_one_line()
    {
        var result = _renderer.Render(new SheetRequest(SheetType.WordSearch, Level.Medium, "x"), new SheetHeader(null, null));
        Assert.True(result.Instruction.Length <= 40);
        Assert.NotNull(result.AnswerKeySvg);
    }
}
