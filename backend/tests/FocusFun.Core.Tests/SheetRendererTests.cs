using System.Xml.Linq;
using FocusFun.Core.Generators;

namespace FocusFun.Core.Tests;

public class SheetRendererTests
{
    private readonly SheetRenderer _renderer = new([new RoadMazeGenerator()]);
    private readonly SheetHeader _header = new("Aarav", new DateOnly(2026, 9, 25));

    [Theory]
    [InlineData(Level.Easy, PaperSize.Letter)]
    [InlineData(Level.Medium, PaperSize.A4)]
    [InlineData(Level.Challenge, PaperSize.Letter)]
    public void Road_maze_renders_valid_svg_with_answer_key(Level level, PaperSize paper)
    {
        var result = _renderer.Render(new SheetRequest(SheetType.RoadMaze, level, "seed-1", PaperSize: paper), _header);

        XDocument.Parse(result.Svg);
        Assert.NotNull(result.AnswerKeySvg);
        XDocument.Parse(result.AnswerKeySvg!);
        Assert.Contains("Aarav", result.Svg);
        Assert.Contains("Parent page", result.AnswerKeySvg);
        Assert.DoesNotContain("Aarav", result.AnswerKeySvg);
    }

    [Fact]
    public void Same_request_renders_identical_svg()
    {
        var request = new SheetRequest(SheetType.RoadMaze, Level.Medium, "repeat-me");
        Assert.Equal(_renderer.Render(request, _header).Svg, _renderer.Render(request, _header).Svg);
    }

    [Fact]
    public void Challenge_maze_asks_to_collect_coins()
    {
        var result = _renderer.Render(new SheetRequest(SheetType.RoadMaze, Level.Challenge, "coins"), _header);
        Assert.Contains("coin", result.Instruction);
    }
}
