using System.Xml.Linq;
using FocusFun.Core.Art;

namespace FocusFun.Core.Tests;

public class AnimalTests
{
    private static readonly Box Box = new(10, 20, 300, 180);

    public static TheoryData<Animal> Everyone()
    {
        var data = new TheoryData<Animal>();
        foreach (var animal in Animals.All) data.Add(animal);
        return data;
    }

    private static XDocument Parse(string fragment) =>
        XDocument.Parse($"<svg xmlns=\"http://www.w3.org/2000/svg\">{fragment}</svg>");

    [Theory]
    [MemberData(nameof(Everyone))]
    public void Every_animal_draws_valid_svg(Animal animal)
    {
        var doc = Parse(Animals.Draw(animal, Box));
        Assert.True(doc.Root!.Descendants().Count() > 3);
    }

    [Theory]
    [MemberData(nameof(Everyone))]
    public void Flip_mirrors_the_drawing(Animal animal)
    {
        string plain = Animals.Draw(animal, Box), flipped = Animals.Draw(animal, Box, flip: true);
        Parse(flipped);
        Assert.DoesNotContain("scale(-1 1)", plain);
        Assert.Contains("scale(-1 1)", flipped);
    }

    [Fact]
    public void Line_width_is_in_page_points_whatever_the_size()
    {
        string small = Animals.Draw(Animal.Cat, new Box(0, 0, 60, 36), lineWidth: 2);
        string large = Animals.Draw(Animal.Cat, new Box(0, 0, 600, 360), lineWidth: 2);
        Assert.Contains("stroke-width=\"6.67\"", small);
        Assert.Contains("stroke-width=\"0.67\"", large);
    }

    [Fact]
    public void Picture_set_is_discovered_with_every_animal()
    {
        var set = PictureSets.For("animals");
        Assert.IsType<AnimalPictures>(set);
        Assert.Equal("animals", set.Plural);
        Assert.Equal(Animals.All.Select(Animals.Name), set.All.Select(p => p.Word));
        Assert.True(Animals.All.Count >= 12);
        foreach (var picture in set.All) Parse(picture.Render(Box));
    }

    [Fact]
    public void Names_are_unique_lowercase_words()
    {
        var names = Animals.All.Select(Animals.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.All(names, n => Assert.Equal(n.ToLowerInvariant(), n));
    }
}
