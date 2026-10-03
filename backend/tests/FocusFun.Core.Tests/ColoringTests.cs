using FocusFun.Core.Art;
using FocusFun.Core.Generators;
using FocusFun.Core.Svg;

namespace FocusFun.Core.Tests;

public class ColoringTests
{
    private static readonly Box Content = SheetPage.For(PaperSize.Letter).Content;

    public static TheoryData<Level, string> Cases()
    {
        var data = new TheoryData<Level, string>();
        foreach (var level in Enum.GetValues<Level>())
            for (int i = 0; i < 30; i++)
                data.Add(level, $"c{i}");
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Spot_the_difference_changes_exactly_the_right_number_of_distinct_items(Level level, string seed)
    {
        var random = new SeededRandom(seed);
        var original = Scenes.Street(random, 315, busy: true);
        var (changed, indexes) = SpotTheDifferenceGenerator.Change(original, SpotTheDifferenceGenerator.Differences(level), level, random);

        Assert.Equal(SpotTheDifferenceGenerator.Differences(level), indexes.Count);
        Assert.Equal(indexes.Count, indexes.Distinct().Count());
        foreach (int i in indexes) Assert.NotEqual(original.Items[i], changed.Items[i]);
        foreach (int i in Enumerable.Range(0, original.Items.Count).Except(indexes)) Assert.Equal(original.Items[i], changed.Items[i]);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Color_by_number_key_covers_every_labelled_colour_within_the_level_limit(Level level, string seed)
    {
        var scene = Scenes.Render(ColoringGenerator.Build(level, Content, new SeededRandom(seed)), Content);
        var colors = ColorByNumberGenerator.PickColors(scene.Labels, level);

        Assert.InRange(colors.Count, 2, ColorByNumberGenerator.MaxColors(level));
        Assert.Equal(colors.Count, colors.Distinct().Count());
        Assert.All(scene.Labels, l =>
        {
            Assert.InRange(l.X, Content.X, Content.Right);
            Assert.InRange(l.Y, Content.Y, Content.Bottom);
        });
    }

    [Fact]
    public void Every_vehicle_has_a_body_label()
    {
        foreach (var v in Vehicles.All)
            Assert.Contains(Vehicles.Parts(v, new Box(0, 0, 200, 120)), p => p.Part == VehiclePart.Body);
    }
}
