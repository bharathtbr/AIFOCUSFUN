using System.Xml.Linq;
using FocusFun.Core.Art;
using FocusFun.Core.Generators;

namespace FocusFun.Core.Tests;

public class WordMagicTests
{
    private static readonly string[] Animals = ["CAT", "DOG", "COW", "PIG", "HEN", "FISH"];

    private readonly SheetRenderer _renderer = SheetRenderer.CreateDefault();

    public static TheoryData<Level, string> Cases()
    {
        var data = new TheoryData<Level, string>();
        foreach (var level in Enum.GetValues<Level>())
            for (int i = 0; i < 40; i++)
                data.Add(level, $"seed-{i}");
        return data;
    }

    private static IReadOnlyList<WordMagicItem> Plan(Level level, string seed, IReadOnlyList<string>? words = null, string theme = "vehicles") =>
        WordMagicGenerator.Plan(new SheetRequest(SheetType.WordMagic, level, seed, theme, Words: words), new SeededRandom(seed));

    [Theory]
    [MemberData(nameof(Cases))]
    public void Every_item_is_a_fair_puzzle_for_its_word(Level level, string seed)
    {
        var items = Plan(level, seed);
        var (min, max) = WordMagicGenerator.Lengths(level);

        Assert.Equal(WordMagicGenerator.ItemCount(level), items.Count);
        Assert.Equal(items.Count, items.Select(i => i.Word).Distinct().Count());
        foreach (var item in items)
        {
            Assert.InRange(item.Word.Length, min, max);
            Assert.All(item.Word, c => Assert.InRange(c, 'A', 'Z'));
            Assert.Equal(item.Word.Length, item.Puzzle.Length);
            Assert.NotNull(item.Picture);   // the built-in words always come with a picture
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Level_decides_which_letters_are_blank(Level level, string seed)
    {
        var listed = Plan(level, seed).Select(i => i.Word).ToHashSet();
        foreach (var item in Plan(level, seed))
        {
            switch (level)
            {
                case Level.Easy:
                    Assert.Equal([0], item.Blanks);
                    Assert.Equal("_" + item.Word[1..], item.Puzzle);
                    Assert.Contains(item.Word[0], item.Bank);
                    Assert.Equal(item.Bank.Count, item.Bank.Distinct().Count());
                    break;
                case Level.Medium:
                    Assert.InRange(item.Blanks.Count, 1, 2);
                    Assert.DoesNotContain(0, item.Blanks);
                    for (int i = 0; i < item.Word.Length; i++)
                        Assert.Equal(item.Blanks.Contains(i) ? '_' : item.Word[i], item.Puzzle[i]);
                    break;
                default:
                    Assert.True(item.IsScramble);
                    Assert.NotEqual(item.Word, item.Puzzle);
                    Assert.Equal(item.Word.Order(), item.Puzzle.Order());
                    Assert.DoesNotContain(item.Puzzle, listed);
                    break;
            }
        }
    }

    [Theory]
    [InlineData(Level.Medium)]
    [InlineData(Level.Challenge)]
    public void Uses_the_approved_words_when_given(Level level)
    {
        for (int i = 0; i < 20; i++)
        {
            var items = Plan(level, $"w{i}", Animals, "animals");
            Assert.Equal(WordMagicGenerator.ItemCount(level), items.Count);
            Assert.All(items, item => Assert.Contains(item.Word, Animals));
        }
    }

    [Fact]
    public void Easy_uses_approved_words_that_have_pictures_and_tops_up_with_theme_pictures()
    {
        var themeWords = Plan(Level.Easy, "fallback", theme: "animals").Select(i => i.Word);
        for (int i = 0; i < 20; i++)
        {
            var items = Plan(Level.Easy, $"w{i}", Animals, "animals");
            Assert.Equal(WordMagicGenerator.ItemCount(Level.Easy), items.Count);
            Assert.All(items, item => Assert.NotNull(item.Picture));
            foreach (var word in Animals.Where(w => PictureSets.Find(w) is not null))
                Assert.Contains(word, items.Select(it => it.Word));
        }
        Assert.NotEmpty(themeWords);
    }

    [Fact]
    public void Skips_phrases_and_odd_entries_in_the_approved_list()
    {
        var items = Plan(Level.Medium, "odd", ["fire truck", " van ", "4x4", "", "tram", "jeep", "taxi", "ship", "boat"]);
        Assert.DoesNotContain(items, i => i.Word.Contains(' ') || i.Word.Contains('4'));
        Assert.Contains(items, i => i.Word == "VAN");
    }

    [Fact]
    public void Falls_back_to_theme_pictures_without_a_word_list()
    {
        var vehicleWords = PictureSets.For("vehicles").All.Select(p => p.Word).ToList();
        foreach (var level in Enum.GetValues<Level>())
            foreach (var item in Plan(level, "fallback"))
                Assert.Contains(item.Picture!.Word, vehicleWords);
    }

    [Theory]
    [InlineData(Level.Easy)]
    [InlineData(Level.Medium)]
    [InlineData(Level.Challenge)]
    public void Same_seed_gives_the_same_sheet(Level level)
    {
        var request = new SheetRequest(SheetType.WordMagic, level, "repeat", Words: Animals);
        var header = new SheetHeader(null, null);
        var a = _renderer.Render(request, header);
        var b = _renderer.Render(request, header);
        Assert.Equal(a.Svg, b.Svg);
        Assert.Equal(a.AnswerKeySvg, b.AnswerKeySvg);
        Assert.NotEqual(a.Svg, _renderer.Render(request with { Seed = "other" }, header).Svg);
    }

    [Theory]
    [InlineData(Level.Easy)]
    [InlineData(Level.Medium)]
    [InlineData(Level.Challenge)]
    public void Renders_valid_svg_with_short_instruction_and_answer_key(Level level)
    {
        for (int i = 0; i < 10; i++)
        {
            var result = _renderer.Render(new SheetRequest(SheetType.WordMagic, level, $"r{i}", Words: i % 2 == 0 ? Animals : null), new SheetHeader("Aarav", null));
            Assert.Equal("Word Magic", result.Title);
            Assert.InRange(result.Instruction.Length, 1, 40);
            XDocument.Parse(result.Svg);
            Assert.NotNull(result.AnswerKeySvg);
            XDocument.Parse(result.AnswerKeySvg!);
        }
    }
}
