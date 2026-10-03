using System.Text.RegularExpressions;
using System.Xml.Linq;
using FocusFun.Core.Art;
using FocusFun.Core.Generators;

namespace FocusFun.Core.Tests;

/// <summary>Approved image pictures (e.g. AI-drawn) stand in for a theme's drawn pictures on picture sheets.</summary>
public class ImagePictureTests
{
    private readonly SheetRenderer _renderer = SheetRenderer.CreateDefault();

    // A 1x1 PNG is enough: sheets only reference and size the image.
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";

    private static readonly SheetPictures Yoga = new("yoga poses",
    [
        new("a1", "tree pose", Png, 300, 400),
        new("b2", "cat pose", Png, 400, 300),
        new("c3", "cobra pose", Png, 420, 260),
        new("d4", "mountain pose", Png, 200, 420),
        new("e5", "butterfly pose", Png, 380, 300),
        new("f6", "star pose", Png, 360, 360),
    ]);

    [Theory]
    [InlineData(SheetType.Pattern)]
    [InlineData(SheetType.OddOneOut)]
    [InlineData(SheetType.CountVehicles)]
    [InlineData(SheetType.Maths)]
    public void Picture_sheets_use_the_approved_images_and_embed_each_once(SheetType type)
    {
        foreach (var level in Enum.GetValues<Level>())
            for (int i = 0; i < 5; i++)
            {
                var request = new SheetRequest(type, level, $"y{i}", Theme: "yoga-poses", Pictures: Yoga);
                var result = _renderer.Render(request, new SheetHeader(null, null));
                foreach (string svg in new[] { result.Svg, result.AnswerKeySvg }.OfType<string>())
                {
                    XDocument.Parse(svg);
                    var used = Regex.Matches(svg, "href=\"#(pic-[a-z0-9]+)\"").Select(m => m.Groups[1].Value).Distinct().ToList();
                    if (type != SheetType.Maths || level != Level.Challenge) // Challenge maths is numbers only
                        Assert.NotEmpty(used);
                    foreach (string id in used)
                        Assert.Single(Regex.Matches(svg, $"<image id=\"{id}\""));
                    Assert.DoesNotContain("<g transform", svg.Split("<defs>")[0]); // no drawn vehicle art before the defs
                }
                Assert.Equal(result.Svg, _renderer.Render(request, new SheetHeader(null, null)).Svg);
            }
    }

    [Fact]
    public void Wording_follows_the_topic()
    {
        var result = _renderer.Render(new SheetRequest(SheetType.CountVehicles, Level.Easy, "w", Theme: "yoga-poses", Pictures: Yoga), new SheetHeader(null, null));
        Assert.Equal("Count the Yoga Poses", result.Title);
        Assert.Equal("Count each yoga pose. Write how many!", result.Instruction);
    }

    [Fact]
    public void Odd_one_out_never_uses_a_mirrored_image_as_the_odd_one()
    {
        var set = PictureSets.For(new SheetRequest(SheetType.OddOneOut, Level.Challenge, "m", Pictures: Yoga));
        Assert.All(set.All, p => Assert.False(p.Mirrorable));
        for (int i = 0; i < 30; i++)
            foreach (var row in OddOneOutGenerator.Plan(set, Level.Challenge, new SeededRandom($"m{i}")))
                Assert.NotEqual(row.Items[row.OddIndex].Picture, row.Items[(row.OddIndex + 1) % row.Items.Count].Picture);
    }

    [Fact]
    public void Too_few_images_fall_back_to_the_drawn_set()
    {
        var two = new SheetPictures("yoga poses", Yoga.Items.Take(2).ToList());
        var result = _renderer.Render(new SheetRequest(SheetType.Pattern, Level.Challenge, "f", Theme: "fruits", Pictures: two), new SheetHeader(null, null));
        Assert.DoesNotContain("<image", result.Svg);
    }

    [Fact]
    public void Requests_without_pictures_are_unchanged()
    {
        var plain = new SheetRequest(SheetType.Pattern, Level.Medium, "same", Theme: "fruits");
        Assert.DoesNotContain("<defs>", _renderer.Render(plain, new SheetHeader(null, null)).Svg);
        Assert.Same(PictureSets.For("fruits"), PictureSets.For(plain));
    }
}
