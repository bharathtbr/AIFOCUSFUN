using System.Xml.Linq;
using FocusFun.Core.Generators;

namespace FocusFun.Core.Tests;

public class MathsTests
{
    private readonly SheetRenderer _renderer = SheetRenderer.CreateDefault();

    public static TheoryData<Level, string> LevelsAndSeeds()
    {
        var data = new TheoryData<Level, string>();
        foreach (var level in Enum.GetValues<Level>())
            for (int i = 0; i < 50; i++)
                data.Add(level, $"m{i}");
        return data;
    }

    [Theory]
    [MemberData(nameof(LevelsAndSeeds))]
    public void Plan_has_the_right_number_of_correct_problems_in_range(Level level, string seed)
    {
        var problems = MathsGenerator.Plan(level, new SeededRandom(seed));
        Assert.Equal(MathsGenerator.ProblemCount(level), problems.Count);
        Assert.InRange(problems.Count, 6, 8);
        Assert.Equal(problems.Count, problems.Distinct().Count());

        int max = MathsGenerator.MaxNumber(level);
        foreach (var p in problems)
        {
            Assert.Equal(p.Kind == MathsKind.Subtraction ? p.A - p.B : p.A + p.B, p.C);
            Assert.All(new[] { p.A, p.B, p.C }, n => Assert.InRange(n, 0, max));
            Assert.True(p.A >= 1 && p.B >= 1, $"{p} uses a zero");
            Assert.True(p.Answer >= 0, $"{p} has a negative answer");
            Assert.Equal(p.Blank switch { MathsBlank.First => p.A, MathsBlank.Second => p.B, _ => p.C }, p.Answer);
        }

        switch (level)
        {
            case Level.Easy:
                Assert.All(problems, p =>
                {
                    Assert.Equal(MathsKind.Addition, p.Kind);
                    Assert.Equal(MathsBlank.Result, p.Blank);
                    Assert.InRange(p.A, 1, 5);
                    Assert.InRange(p.B, 1, 5);
                    Assert.True(p.C <= 10);
                });
                break;
            case Level.Medium:
                Assert.All(problems, p => Assert.Equal(MathsBlank.Result, p.Blank));
                Assert.Equal(4, problems.Count(p => p.Kind == MathsKind.Addition));
                Assert.Equal(4, problems.Count(p => p.Kind == MathsKind.Subtraction));
                break;
            default:
                var sentences = problems.Where(p => p.Kind != MathsKind.Bond).ToList();
                Assert.Equal(4, sentences.Count);
                Assert.All(sentences, p => Assert.NotEqual(MathsBlank.Result, p.Blank));
                Assert.Contains(sentences, p => p.Kind == MathsKind.Addition);
                Assert.Contains(sentences, p => p.Kind == MathsKind.Subtraction);
                var bonds = problems.Where(p => p.Kind == MathsKind.Bond).ToList();
                Assert.Equal(4, bonds.Count);
                Assert.Contains(bonds, p => p.Blank == MathsBlank.Result);
                Assert.Contains(bonds, p => p.Blank != MathsBlank.Result);
                break;
        }
    }

    [Theory]
    [MemberData(nameof(LevelsAndSeeds))]
    public void Sheet_is_valid_repeatable_and_has_an_answer_key(Level level, string seed)
    {
        var request = new SheetRequest(SheetType.Maths, level, seed);
        var first = _renderer.Render(request, new SheetHeader("Aarav", null));

        XDocument.Parse(first.Svg);
        Assert.NotNull(first.AnswerKeySvg);
        XDocument.Parse(first.AnswerKeySvg);
        Assert.InRange(first.Instruction.Length, 1, 40);
        Assert.Equal(first.Svg, _renderer.Render(request, new SheetHeader("Aarav", null)).Svg);
        Assert.DoesNotContain("#ccc", first.Svg, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(Level.Easy)]
    [InlineData(Level.Medium)]
    [InlineData(Level.Challenge)]
    public void Answer_key_writes_every_answer(Level level)
    {
        var problems = MathsGenerator.Plan(level, new SeededRandom("key"));
        var content = _renderer.Generate(new SheetRequest(SheetType.Maths, level, "key"));
        Assert.NotNull(content.AnswerKeyBodySvg);

        // The key is the task plus one big number per blank.
        var added = XElement.Parse($"<g>{content.AnswerKeyBodySvg[content.BodySvg.Length..]}</g>").Elements("text")
            .Select(t => int.Parse(t.Value)).ToList();
        Assert.StartsWith(content.BodySvg, content.AnswerKeyBodySvg);
        Assert.Equal(problems.Select(p => p.Answer).Order(), added.Order());
    }

    [Theory]
    [InlineData("vehicles")]
    [InlineData("animals")]
    [InlineData("no-such-theme")]
    public void Renders_for_every_theme(string theme)
    {
        foreach (var level in Enum.GetValues<Level>())
        {
            var request = new SheetRequest(SheetType.Maths, level, "theme", Theme: theme);
            var result = _renderer.Render(request, new SheetHeader(null, null));
            XDocument.Parse(result.Svg);
            Assert.Equal(result.Svg, _renderer.Render(request, new SheetHeader(null, null)).Svg);
        }

        // An unknown theme draws exactly what vehicles draws.
        if (theme == "no-such-theme")
            Assert.Equal(
                _renderer.Generate(new SheetRequest(SheetType.Maths, Level.Easy, "theme", Theme: "vehicles")).BodySvg,
                _renderer.Generate(new SheetRequest(SheetType.Maths, Level.Easy, "theme", Theme: theme)).BodySvg);
    }

    [Fact]
    public void Different_seeds_give_different_sheets()
    {
        var sheets = Enumerable.Range(0, 10)
            .Select(i => _renderer.Generate(new SheetRequest(SheetType.Maths, Level.Medium, $"d{i}")).BodySvg)
            .Distinct().Count();
        Assert.True(sheets >= 9);
    }
}
