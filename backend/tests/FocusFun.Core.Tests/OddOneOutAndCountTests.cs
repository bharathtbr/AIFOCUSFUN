using System.Xml.Linq;
using FocusFun.Core.Art;
using FocusFun.Core.Generators;
using FocusFun.Core.Svg;

namespace FocusFun.Core.Tests;

public class OddOneOutAndCountTests
{
    private static readonly Box Content = SheetPage.For(PaperSize.Letter).Content;
    private static readonly IPictureSet VehicleSet = PictureSets.For("vehicles");

    public static TheoryData<Level, string> LevelsAndSeeds()
    {
        var data = new TheoryData<Level, string>();
        foreach (var level in Enum.GetValues<Level>())
            for (int i = 0; i < 40; i++)
                data.Add(level, $"s{i}");
        return data;
    }

    [Theory]
    [MemberData(nameof(LevelsAndSeeds))]
    public void Odd_one_out_rows_have_exactly_one_odd_item(Level level, string seed)
    {
        var rows = OddOneOutGenerator.Plan(VehicleSet, level, new SeededRandom(seed));
        Assert.InRange(rows.Count, 4, 5);
        Assert.Equal(OddOneOutGenerator.RowCount(level), rows.Count);

        foreach (var row in rows)
        {
            Assert.Equal(OddOneOutGenerator.ItemsPerRow(level), row.Items.Count);
            var odd = row.Items[row.OddIndex];
            var others = row.Items.Where((_, i) => i != row.OddIndex).ToList();
            Assert.Single(others.Distinct());
            Assert.NotEqual(others[0], odd);

            if (level == Level.Challenge)
                // Harder: either the same vehicle mirrored, or a look-alike facing the same way.
                Assert.True(odd.Picture == others[0].Picture
                    ? odd.Flip != others[0].Flip
                    : odd.Flip == others[0].Flip && OddOneOutGenerator.AreSimilar(odd.Picture, others[0].Picture));
            else
                Assert.True(odd.Picture != others[0].Picture && !OddOneOutGenerator.AreSimilar(odd.Picture, others[0].Picture));
        }
    }

    [Fact]
    public void Odd_position_varies_across_rows()
    {
        var positions = Enumerable.Range(0, 20)
            .SelectMany(i => OddOneOutGenerator.Plan(VehicleSet, Level.Medium, new SeededRandom($"p{i}")))
            .Select(r => r.OddIndex).Distinct().Count();
        Assert.Equal(4, positions);
    }

    [Theory]
    [MemberData(nameof(LevelsAndSeeds))]
    public void Count_plan_has_right_types_counts_and_no_overlaps(Level level, string seed)
    {
        var plan = CountVehiclesGenerator.Plan(VehicleSet, level, Content, new SeededRandom(seed));
        var (min, max) = CountVehiclesGenerator.CountRange(level);

        Assert.Equal(CountVehiclesGenerator.TypeCount(level), plan.Tally.Count);
        Assert.Equal(plan.Tally.Count, plan.Tally.Select(t => t.Item).Distinct().Count());
        foreach (var t in plan.Tally)
        {
            Assert.InRange(t.Count, min, max);
            Assert.Equal(t.Count, plan.Placed.Count(p => p.Item == t.Item));
            Assert.True(t.AnswerBox.Width >= 60 && t.AnswerBox.Height >= 60, "answer box too small to write in");
            Assert.True(Inside(t.PictureBox, Content) && Inside(t.AnswerBox, Content));
            Assert.False(Overlaps(t.PictureBox, t.AnswerBox));
            Assert.False(Overlaps(t.AnswerBox, plan.Scene));
        }
        Assert.Equal(plan.Tally.Sum(t => t.Count), plan.Placed.Count);

        Assert.True(Inside(plan.Scene, Content));
        for (int i = 0; i < plan.Placed.Count; i++)
        {
            var a = plan.Placed[i].Box;
            Assert.True(Inside(a, plan.Scene), $"vehicle {i} leaves the scene");
            Assert.True(a.Width >= 60, $"vehicle {i} is only {a.Width:0}pt wide");
            for (int j = i + 1; j < plan.Placed.Count; j++)
                Assert.False(Overlaps(a, plan.Placed[j].Box), $"vehicles {i} and {j} overlap");
        }
    }

    [Theory]
    [MemberData(nameof(LevelsAndSeeds))]
    public void Count_answer_key_shows_the_correct_numbers(Level level, string seed)
    {
        var generator = new CountVehiclesGenerator();
        var request = new SheetRequest(SheetType.CountVehicles, level, seed);
        var content = generator.Generate(request, Content, new SeededRandom(seed));
        var plan = CountVehiclesGenerator.Plan(VehicleSet, level, Content, new SeededRandom(seed));

        Assert.Empty(Texts(content.BodySvg));
        Assert.Equal(plan.Tally.Select(t => t.Count.ToString()), Texts(content.AnswerKeyBodySvg!));
        Assert.True(content.Instruction.Length <= 40);
    }

    [Theory]
    [InlineData(SheetType.OddOneOut)]
    [InlineData(SheetType.CountVehicles)]
    public void Same_seed_gives_the_same_sheet(SheetType type)
    {
        var renderer = SheetRenderer.CreateDefault();
        foreach (var level in Enum.GetValues<Level>())
        {
            var request = new SheetRequest(type, level, "repeat");
            var a = renderer.Render(request, new SheetHeader(null, null));
            var b = renderer.Render(request, new SheetHeader(null, null));
            Assert.Equal(a.Svg, b.Svg);
            Assert.Equal(a.AnswerKeySvg, b.AnswerKeySvg);
            Assert.NotEqual(a.Svg, renderer.Render(request with { Seed = "other" }, new SheetHeader(null, null)).Svg);
            Assert.True(a.Instruction.Length <= 40);
        }
    }

    private static List<string> Texts(string body) =>
        XElement.Parse($"<g>{body}</g>").Descendants("text").Select(t => t.Value).ToList();

    private static bool Inside(Box inner, Box outer) =>
        inner.X >= outer.X - 0.01 && inner.Y >= outer.Y - 0.01 && inner.Right <= outer.Right + 0.01 && inner.Bottom <= outer.Bottom + 0.01;

    private static bool Overlaps(Box a, Box b) =>
        a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;
}
