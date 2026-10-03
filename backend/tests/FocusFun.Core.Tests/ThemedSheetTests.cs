using System.Xml.Linq;
using FocusFun.Core.Art;
using FocusFun.Core.Generators;
using FocusFun.Core.Svg;

namespace FocusFun.Core.Tests;

/// <summary>Pattern, odd one out, count and word search follow the sheet's theme instead of always drawing vehicles.</summary>
public class ThemedSheetTests
{
    private static readonly Box Content = SheetPage.For(PaperSize.Letter).Content;
    private readonly SheetRenderer _renderer = SheetRenderer.CreateDefault();

    private static readonly string[] Fruits = ["apple", "mango", "banana", "grapes", "orange", "lemon", "pear", "plum"];

    public static TheoryData<SheetType, Level, string> ThemedSheets()
    {
        var data = new TheoryData<SheetType, Level, string>();
        foreach (var type in new[] { SheetType.Pattern, SheetType.OddOneOut, SheetType.CountVehicles, SheetType.WordSearch })
            foreach (var level in Enum.GetValues<Level>())
                foreach (var theme in new[] { "vehicles", "animals", "fruits" })
                    data.Add(type, level, theme);
        return data;
    }

    [Theory]
    [MemberData(nameof(ThemedSheets))]
    public void Themed_sheets_render_valid_repeatable_svg(SheetType type, Level level, string theme)
    {
        var header = new SheetHeader(null, null);
        for (int i = 0; i < 5; i++)
        {
            var request = new SheetRequest(type, level, $"t{i}", Theme: theme);
            var first = _renderer.Render(request, header);
            XDocument.Parse(first.Svg);
            XDocument.Parse(first.AnswerKeySvg!);
            Assert.InRange(first.Instruction.Length, 1, 40);

            var again = _renderer.Render(request, header);
            Assert.Equal(first.Svg, again.Svg);
            Assert.Equal(first.AnswerKeySvg, again.AnswerKeySvg);
        }
    }

    [Theory]
    [InlineData(SheetType.Pattern)]
    [InlineData(SheetType.OddOneOut)]
    [InlineData(SheetType.CountVehicles)]
    [InlineData(SheetType.WordSearch)]
    public void Picture_sets_differ_from_vehicle_sheets_and_unknown_themes_fall_back_to_vehicles(SheetType type)
    {
        var header = new SheetHeader(null, null);
        var vehicles = _renderer.Render(new SheetRequest(type, Level.Medium, "same", Theme: "vehicles"), header);
        var animals = _renderer.Render(new SheetRequest(type, Level.Medium, "same", Theme: "animals"), header);
        var fruits = _renderer.Render(new SheetRequest(type, Level.Medium, "same", Theme: "fruits"), header);
        var unknown = _renderer.Render(new SheetRequest(type, Level.Medium, "same", Theme: "planets"), header);
        Assert.NotEqual(vehicles.Svg, animals.Svg);
        Assert.NotEqual(vehicles.Svg, fruits.Svg);
        Assert.Equal(vehicles.Svg, unknown.Svg);
    }

    [Fact]
    public void Count_wording_follows_the_theme()
    {
        var content = new CountVehiclesGenerator().Generate(new SheetRequest(SheetType.CountVehicles, Level.Easy, "w", Theme: "animals"), Content, new SeededRandom("w"));
        Assert.Equal("Count the Animals", content.Title);
        Assert.Equal("Count each animal. Write how many!", content.Instruction);

        var vehicles = new CountVehiclesGenerator().Generate(new SheetRequest(SheetType.CountVehicles, Level.Easy, "w"), Content, new SeededRandom("w"));
        Assert.Equal("Count the Vehicles", vehicles.Title);
        Assert.Equal("Count each vehicle. Write how many!", vehicles.Instruction);
    }

    [Theory]
    [InlineData(Level.Easy)]
    [InlineData(Level.Medium)]
    [InlineData(Level.Challenge)]
    public void Pattern_odd_one_out_and_count_use_the_requested_set(Level level)
    {
        foreach (var theme in new[] { "vehicles", "animals", "fruits" })
        {
            var set = PictureSets.For(theme);
            var words = set.All.Select(p => p.Word).ToHashSet();
            for (int i = 0; i < 20; i++)
            {
                for (int row = 0; row < 4; row++)
                    Assert.All(PatternGenerator.MakeSequence(set, level, row, new SeededRandom($"p{i}")), p => Assert.Contains(p.Word, words));

                Assert.All(OddOneOutGenerator.Plan(set, level, new SeededRandom($"o{i}")).SelectMany(r => r.Items), item => Assert.Contains(item.Picture.Word, words));

                var plan = CountVehiclesGenerator.Plan(set, level, Content, new SeededRandom($"c{i}"));
                Assert.All(plan.Placed, p => Assert.Contains(p.Item.Word, words));
                Assert.All(plan.Tally, t => Assert.Contains(t.Item.Word, words));
            }
        }
    }

    [Theory]
    [InlineData(Level.Easy)]
    [InlineData(Level.Medium)]
    [InlineData(Level.Challenge)]
    public void Animal_odd_one_out_is_fair(Level level)
    {
        for (int i = 0; i < 40; i++)
            foreach (var row in OddOneOutGenerator.Plan(PictureSets.For("animals"), level, new SeededRandom($"a{i}")))
            {
                var odd = row.Items[row.OddIndex];
                var others = row.Items.Where((_, j) => j != row.OddIndex).ToList();
                Assert.Single(others.Distinct());
                // Either the same animal facing the other way (Challenge only) or a different animal facing the same way.
                if (odd.Picture == others[0].Picture)
                    Assert.True(level == Level.Challenge && odd.Flip != others[0].Flip);
                else
                    Assert.Equal(others[0].Flip, odd.Flip);
            }
    }

    [Theory]
    [InlineData(Level.Easy)]
    [InlineData(Level.Medium)]
    [InlineData(Level.Challenge)]
    public void Word_search_uses_the_supplied_words(Level level)
    {
        var supplied = Fruits.Select(w => w.ToUpperInvariant()).ToHashSet();
        for (int i = 0; i < 20; i++)
        {
            var request = new SheetRequest(SheetType.WordSearch, level, $"f{i}", Theme: "fruits", Words: Fruits);
            var puzzle = WordSearchGenerator.Build(request, new SeededRandom(request.Seed));
            var listed = puzzle.Placements.Select(p => p.Word).ToList();

            Assert.Equal(WordSearchGenerator.WordCount(level), listed.Count);
            Assert.Equal(listed.Count, listed.Distinct().Count());
            // Supplied words fill the list before any built-in word is used.
            Assert.Equal(Math.Min(supplied.Count, listed.Count), listed.Count(supplied.Contains));
            foreach (var p in puzzle.Placements)
            {
                Assert.Contains(p.Direction, WordSearchGenerator.Directions(level));
                Assert.True(WordSearchGenerator.Matches(puzzle.Letters, p.Word, p.Row, p.Col, p.Direction), p.Word);
                Assert.Equal(1, WordSearchGenerator.CountOccurrences(puzzle.Letters, p.Word));
            }
        }
    }

    [Fact]
    public void Word_search_skips_unusable_words_and_tops_up()
    {
        string[] words = ["fire truck", "car", "cart", "x-ray", "level", "a", "bus", "abcdefghijklmnop"];
        var request = new SheetRequest(SheetType.WordSearch, Level.Medium, "skip", Words: words);
        var listed = WordSearchGenerator.Build(request, new SeededRandom("skip")).Placements.Select(p => p.Word).ToList();

        Assert.Equal(WordSearchGenerator.WordCount(Level.Medium), listed.Count);
        Assert.DoesNotContain("FIRE TRUCK", listed);
        Assert.DoesNotContain("LEVEL", listed);      // palindrome: it would always be found twice
        Assert.DoesNotContain("ABCDEFGHIJKLMNOP", listed);
        Assert.Contains("BUS", listed);
        Assert.False(listed.Contains("CAR") && listed.Contains("CART"), "one word hides inside the other");
        Assert.All(listed, w => Assert.Matches("^[A-Z]+$", w));
    }

    [Fact]
    public void Animal_word_search_lists_animal_words()
    {
        var animals = PictureSets.For("animals").All.Select(p => p.Word.ToUpperInvariant()).ToHashSet();
        foreach (var level in Enum.GetValues<Level>())
        {
            var request = new SheetRequest(SheetType.WordSearch, level, "zoo", Theme: "animals");
            var listed = WordSearchGenerator.Build(request, new SeededRandom("zoo")).Placements.Select(p => p.Word);
            Assert.All(listed, w => Assert.Contains(w, animals));
        }
    }
}
