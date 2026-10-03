using System.Xml.Linq;
using FocusFun.Core.Art;
using FocusFun.Core.Generators;

namespace FocusFun.Core.Tests;

public class GeneratorTests
{
    private readonly SheetRenderer _renderer = SheetRenderer.CreateDefault();

    public static TheoryData<SheetType, Level, string> AllSheets()
    {
        var data = new TheoryData<SheetType, Level, string>();
        foreach (var type in Enum.GetValues<SheetType>())
            foreach (var level in Enum.GetValues<Level>())
                for (int i = 0; i < 10; i++)
                    data.Add(type, level, $"seed-{i}");
        return data;
    }

    [Theory]
    [MemberData(nameof(AllSheets))]
    public void Every_sheet_renders_valid_repeatable_svg(SheetType type, Level level, string seed)
    {
        var request = new SheetRequest(type, level, seed);
        var header = new SheetHeader("Aarav", null);
        var first = _renderer.Render(request, header);

        XDocument.Parse(first.Svg);
        if (first.AnswerKeySvg is not null) XDocument.Parse(first.AnswerKeySvg);
        Assert.False(string.IsNullOrWhiteSpace(first.Instruction));
        Assert.Equal(first.Svg, _renderer.Render(request, header).Svg);
    }

    [Theory]
    [InlineData(SheetType.RoadMaze)]
    [InlineData(SheetType.DotToDot)]
    [InlineData(SheetType.Pattern)]
    public void Puzzles_come_with_an_answer_key(SheetType type)
    {
        var result = _renderer.Render(new SheetRequest(type, Level.Medium, "key"), new SheetHeader(null, null));
        Assert.NotNull(result.AnswerKeySvg);
    }

    [Theory]
    [InlineData(Level.Easy, 15)]
    [InlineData(Level.Medium, 30)]
    [InlineData(Level.Challenge, 25)]
    public void Dot_to_dot_has_the_right_number_of_dots_for_every_vehicle(Level level, int expected)
    {
        Assert.All(DotToDotGenerator.DotCounts(level), count => Assert.Equal(expected, count));
    }

    [Fact]
    public void Challenge_dot_to_dot_counts_by_twos()
    {
        var result = _renderer.Render(new SheetRequest(SheetType.DotToDot, Level.Challenge, "x"), new SheetHeader(null, null));
        Assert.Contains("Count by 2s", result.Instruction);
        Assert.Contains(">50<", result.Svg);
    }

    [Theory]
    [InlineData("vehicles")]
    [InlineData("animals")]
    public void Easy_patterns_alternate_two_different_pictures(string theme)
    {
        var set = PictureSets.For(theme);
        for (int i = 0; i < 20; i++)
        {
            var seq = PatternGenerator.MakeSequence(set, Level.Easy, 0, new SeededRandom($"p{i}"));
            Assert.NotEqual(seq[0], seq[1]);
            for (int j = 2; j < seq.Count; j++) Assert.Equal(seq[j - 2], seq[j]);
        }
    }

    [Fact]
    public void Stroke_font_covers_uppercase_and_digits_within_bounds()
    {
        foreach (char c in "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")
        {
            Assert.True(StrokeFont.Supports(c), $"missing {c}");
            var glyph = StrokeFont.Get(c);
            Assert.NotEmpty(glyph.Strokes);
            foreach (var p in glyph.Strokes.SelectMany(s => s))
            {
                Assert.InRange(p.X, -3, glyph.Width + 3);
                Assert.InRange(p.Y, -3, 103);
            }
        }
    }

    [Theory]
    [InlineData("b", "B")]
    [InlineData("7", "7")]
    public void Tracing_uses_the_requested_letter(string letters, string expected)
    {
        var result = _renderer.Render(new SheetRequest(SheetType.EnglishTracing, Level.Easy, "t", Letters: letters), new SheetHeader(null, null));
        Assert.Equal($"Let's Write {expected}", result.Title);
    }
}
