using System.Text.RegularExpressions;
using System.Xml.Linq;
using FocusFun.Core.Art;
using FocusFun.Core.Generators;

namespace FocusFun.Core.Tests;

/// <summary>Coloring, spot the difference, mazes, picture cards, dot-to-dot and colour-by-number for topics other than vehicles.</summary>
public class TopicSheetTests
{
    private readonly SheetRenderer _renderer = SheetRenderer.CreateDefault();
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";

    /// <summary>A 200 x 200 "drawing": a ring split down the middle by a line, with a small box in the left half.</summary>
    private static bool[,] Drawing()
    {
        var ink = new bool[200, 200];
        for (int x = 0; x < 200; x++)
            for (int y = 0; y < 200; y++)
            {
                double d = Math.Sqrt((x - 100) * (x - 100) + (y - 100) * (y - 100));
                bool ring = d is >= 80 and <= 86;
                bool divider = x is >= 99 and <= 101 && d < 82;
                bool box = x is >= 50 and <= 76 && y is >= 88 and <= 114 && (x is <= 52 or >= 74 || y is <= 90 or >= 112);
                ink[x, y] = ring || divider || box;
            }
        return ink;
    }

    [Fact]
    public void Analyzer_finds_the_outline_and_the_enclosed_areas()
    {
        var shape = ShapeAnalyzer.Analyze(Drawing());

        // Outline: clockwise, round, hugging the ring's outer edge (radius ~86).
        Assert.True(ShapeAnalyzer.Area(shape.Outline) > 0);
        Assert.InRange(shape.Outline.Count, 8, 400);
        Assert.All(shape.Outline, p => Assert.InRange(Math.Sqrt((p.X - 100) * (p.X - 100) + (p.Y - 100) * (p.Y - 100)), 82, 89));

        // Areas: the two halves (right one bigger, since the box takes part of the left) and the inside of the box.
        Assert.Equal(3, shape.Regions.Count);
        Assert.True(shape.Regions[0].X > 100, "largest area is the right half");
        Assert.True(shape.Regions[1].X < 100);
        var small = shape.Regions[2];
        Assert.InRange(small.X, 53, 74);
        Assert.InRange(small.Y, 91, 112);
        Assert.All(shape.Regions, r => Assert.True(r.Room >= 5));
    }

    private static SheetPictures Shaped(int count = 5)
    {
        var shape = ShapeAnalyzer.Analyze(Drawing());
        string[] names = ["tree pose", "cat pose", "cobra pose", "mountain pose", "butterfly pose", "star pose"];
        return new SheetPictures("yoga poses", names.Take(count).Select((n, i) => new PictureAsset($"k{i}", n, Png, 200, 200, shape)).ToList());
    }

    [Theory]
    [InlineData(Level.Easy, 15)]
    [InlineData(Level.Medium, 30)]
    [InlineData(Level.Challenge, 25)]
    public void Dot_to_dot_follows_an_image_outline(Level level, int dots)
    {
        var result = _renderer.Render(new SheetRequest(SheetType.DotToDot, level, "d", Theme: "yoga-poses", Pictures: Shaped()), new SheetHeader(null, null));
        XDocument.Parse(result.Svg);
        Assert.StartsWith("Dot-to-Dot: ", result.Title);
        Assert.Contains(" Pose", result.Title);
        Assert.Equal(dots, Regex.Matches(result.Svg, "<circle[^>]*fill=\"#000\"").Count);
        Assert.DoesNotContain("<image", result.Svg);        // the child sees only dots…
        Assert.Contains("<image", result.AnswerKeySvg!);    // …the key shows the picture
    }

    [Theory]
    [InlineData(Level.Easy, 3)]
    [InlineData(Level.Medium, 3)]
    [InlineData(Level.Challenge, 3)]
    public void Colour_by_number_labels_each_enclosed_area(Level level, int areas)
    {
        var result = _renderer.Render(new SheetRequest(SheetType.ColorByNumber, level, "c", Theme: "yoga-poses", Pictures: Shaped()), new SheetHeader(null, null));
        XDocument.Parse(result.Svg);
        Assert.Contains("<image", result.Svg);
        string symbol = level == Level.Medium ? "A" : "1";
        Assert.Contains($">{symbol}</text>", result.Svg);
        // Bold text: one symbol per area plus one per key entry (as many colours as areas, up to the level's limit),
        // besides the title and the instruction.
        int keyEntries = Math.Min(ColorByNumberGenerator.MaxColors(level), areas);
        Assert.Equal(areas + keyEntries, Regex.Matches(result.Svg, "font-weight=\"700\"[^>]*>[^<]+</text>").Count - 2);
    }

    [Fact]
    public void Images_without_shapes_keep_the_vehicle_dot_to_dot()
    {
        var plain = new SheetPictures("yoga poses", Shaped().Items.Select(a => a with { Shape = null }).ToList());
        var result = _renderer.Render(new SheetRequest(SheetType.DotToDot, Level.Easy, "v", Theme: "yoga-poses", Pictures: plain), new SheetHeader(null, null));
        Assert.DoesNotContain("<image", result.AnswerKeySvg!);
    }

    [Theory]
    [InlineData("animals")]
    [InlineData("fruits")]
    public void Coloring_spot_the_difference_maze_and_cards_use_the_topics_pictures(string theme)
    {
        var words = PictureSets.For(theme).All.Select(p => p.Word).ToHashSet();
        foreach (var level in Enum.GetValues<Level>())
        {
            var coloring = _renderer.Render(new SheetRequest(SheetType.Coloring, level, "t", Theme: theme), new SheetHeader(null, null));
            Assert.Equal("Coloring Time", coloring.Title);
            Assert.DoesNotContain("road", coloring.Instruction);

            var maze = _renderer.Render(new SheetRequest(SheetType.RoadMaze, level, "t", Theme: theme), new SheetHeader(null, null));
            Assert.Contains("star", maze.Instruction);
            Assert.InRange(maze.Instruction.Length, 1, 45);

            var cards = _renderer.Render(new SheetRequest(SheetType.PictureCards, level, "t", Theme: theme), new SheetHeader(null, null));
            Assert.Equal(PictureCardsGenerator.Cards(level), Regex.Matches(cards.Svg, "rx=\"14\"").Count);
            Assert.Contains(Regex.Matches(cards.Svg, "font-weight=\"700\"[^>]*>([^<]+)</text>").Select(m => m.Groups[1].Value.ToLowerInvariant()), words.Contains);

            for (int i = 0; i < 10; i++)
            {
                var spot = _renderer.Render(new SheetRequest(SheetType.SpotTheDifference, level, $"s{i}", Theme: theme), new SheetHeader(null, null));
                XDocument.Parse(spot.AnswerKeySvg!);
                Assert.Equal($"Find the {SpotTheDifferenceGenerator.Differences(level)} differences!", spot.Instruction);
                Assert.Equal(SpotTheDifferenceGenerator.Differences(level), Regex.Matches(spot.AnswerKeySvg!, "<ellipse[^>]*stroke-dasharray=\"10 5\"").Count);
            }
        }
    }

    [Fact]
    public void Vehicle_sheets_are_unchanged_for_the_vehicle_topic()
    {
        foreach (var type in new[] { SheetType.Coloring, SheetType.SpotTheDifference, SheetType.RoadMaze })
        {
            var vehicles = _renderer.Render(new SheetRequest(type, Level.Medium, "same"), new SheetHeader(null, null));
            var unknown = _renderer.Render(new SheetRequest(type, Level.Medium, "same", Theme: "planets"), new SheetHeader(null, null));
            Assert.Equal(vehicles.Svg, unknown.Svg);
        }
        var maze = _renderer.Render(new SheetRequest(SheetType.RoadMaze, Level.Easy, "g"), new SheetHeader(null, null));
        Assert.Contains("garage", maze.Instruction);
    }

    [Theory]
    [InlineData(Level.Easy, 3)]
    [InlineData(Level.Medium, 5)]
    [InlineData(Level.Challenge, 8)]
    public void Yoga_cards_have_breath_circles(Level level, int breaths)
    {
        var result = _renderer.Render(new SheetRequest(SheetType.PictureCards, level, "y", Theme: "yoga-poses", Pictures: Shaped(6)), new SheetHeader(null, null));
        Assert.Equal("Yoga Cards", result.Title);
        Assert.Equal($"Hold each pose for {breaths} breaths. Tick!", result.Instruction);
        int cards = Math.Min(PictureCardsGenerator.Cards(level), 6);
        Assert.Equal(cards * breaths, Regex.Matches(result.Svg, "<circle[^>]*fill=\"none\" stroke=\"#000\" stroke-width=\"2\"/>").Count);
    }

    [Fact]
    public void Odd_one_out_mirrors_images_only_when_marked_mirror_safe()
    {
        var safe = new SheetPictures("yoga poses", Shaped().Items.Select(a => a with { MirrorSafe = true }).ToList());
        var set = PictureSets.For(new SheetRequest(SheetType.OddOneOut, Level.Challenge, "m", Pictures: safe));
        bool mirrored = false;
        for (int i = 0; i < 20; i++)
            foreach (var row in OddOneOutGenerator.Plan(set, Level.Challenge, new SeededRandom($"m{i}")))
                mirrored |= row.Items[row.OddIndex].Picture == row.Items[(row.OddIndex + 1) % row.Items.Count].Picture;
        Assert.True(mirrored);
    }
}
