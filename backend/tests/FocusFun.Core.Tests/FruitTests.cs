using System.Xml.Linq;
using FocusFun.Core.Art;

namespace FocusFun.Core.Tests;

public class FruitTests
{
    private static readonly Box Box = new(10, 20, 300, 180);

    public static TheoryData<Fruit> Every()
    {
        var data = new TheoryData<Fruit>();
        foreach (var fruit in Fruits.All) data.Add(fruit);
        return data;
    }

    private static XDocument Parse(string fragment) =>
        XDocument.Parse($"<svg xmlns=\"http://www.w3.org/2000/svg\">{fragment}</svg>");

    [Theory]
    [MemberData(nameof(Every))]
    public void Every_fruit_draws_valid_svg_and_mirrors(Fruit fruit)
    {
        Assert.True(Parse(Fruits.Draw(fruit, Box)).Root!.Descendants().Count() > 2);
        Assert.Contains("scale(-1 1)", Fruits.Draw(fruit, Box, flip: true));
    }

    [Fact]
    public void Picture_set_is_discovered_with_every_fruit()
    {
        var set = PictureSets.For("fruits");
        Assert.IsType<FruitPictures>(set);
        Assert.Equal("fruits", set.Plural);
        Assert.Equal(Fruits.All.Select(Fruits.Name), set.All.Select(p => p.Word));
        Assert.True(Fruits.All.Count >= 12);
        foreach (var picture in set.All) Parse(picture.Render(Box));
    }

    [Fact]
    public void Names_are_unique_lowercase_words_that_fit_word_sheets()
    {
        var names = Fruits.All.Select(Fruits.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.All(names, n => Assert.Matches("^[a-z]+$", n));
    }
}
