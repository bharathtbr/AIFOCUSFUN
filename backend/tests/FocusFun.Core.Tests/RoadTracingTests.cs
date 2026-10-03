using System.Xml.Linq;
using FocusFun.Core.Generators;
using FocusFun.Core.Svg;

namespace FocusFun.Core.Tests;

public class RoadTracingTests
{
    private readonly SheetRenderer _renderer = SheetRenderer.CreateDefault();

    public static TheoryData<Level, PaperSize, string> Cases()
    {
        var data = new TheoryData<Level, PaperSize, string>();
        foreach (var level in Enum.GetValues<Level>())
            foreach (var paper in Enum.GetValues<PaperSize>())
                for (int i = 0; i < 40; i++)
                    data.Add(level, paper, $"road-{i}");
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Sheet_is_valid_repeatable_svg_without_answer_key(Level level, PaperSize paper, string seed)
    {
        var request = new SheetRequest(SheetType.RoadTracing, level, seed, PaperSize: paper);
        var header = new SheetHeader(null, null);
        var first = _renderer.Render(request, header);

        XDocument.Parse(first.Svg);
        Assert.Null(first.AnswerKeySvg);
        Assert.InRange(first.Instruction.Length, 1, 40);
        Assert.Equal(first.Svg, _renderer.Render(request, header).Svg);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Every_path_stays_inside_its_own_lane_clear_of_the_icons(Level level, PaperSize paper, string seed)
    {
        var box = SheetPage.For(paper).Content;
        var lanes = RoadTracingGenerator.Lanes(level, box, new SeededRandom(seed));
        Assert.Equal(RoadTracingGenerator.LaneCount(level), lanes.Count);

        for (int i = 0; i < lanes.Count; i++)
        {
            var lane = lanes[i];
            Assert.True(Inside(lane.Area, box), "lane outside content box");
            if (i > 0) Assert.True(lanes[i - 1].Area.Bottom <= lane.Area.Y + 1e-6, "lanes overlap");
            Assert.True(Inside(lane.Track, lane.Area));
            Assert.Equal(RoadTracingGenerator.RoadWidth(level), lane.RoadWidth);

            // The whole road (or dotted line) stays inside the lane, between the vehicle and the garage.
            double half = Math.Max(lane.RoadWidth / 2 + 2.5, 2.5);
            Assert.True(lane.Path.Count >= 2);
            foreach (var (x, y) in lane.Path)
            {
                Assert.InRange(x, lane.Track.X - 1e-6, lane.Track.Right + 1e-6);
                Assert.InRange(y, lane.Area.Y + half, lane.Area.Bottom - half);
                Assert.InRange(y, box.Y, box.Bottom);
            }

            Assert.True(Inside(lane.VehicleBox, lane.Area), "vehicle outside lane");
            Assert.True(Inside(lane.GarageBox, lane.Area), "garage outside lane");
            Assert.True(lane.VehicleBox.Right < lane.Track.X, "vehicle overlaps the path");
            Assert.True(lane.GarageBox.X > lane.Track.Right, "garage overlaps the path");

            // Icons sit on the path ends.
            Assert.Equal(lane.Path[0].Y, lane.VehicleBox.CenterY, 6);
            Assert.Equal(lane.Path[^1].Y, lane.GarageBox.CenterY, 6);
        }
    }

    [Theory]
    [InlineData(Level.Easy)]
    [InlineData(Level.Medium)]
    public void Roads_have_no_roundabouts_below_challenge(Level level)
    {
        for (int i = 0; i < 30; i++)
        {
            var lanes = RoadTracingGenerator.Lanes(level, SheetPage.For(PaperSize.Letter).Content, new SeededRandom($"r{i}"));
            Assert.All(lanes, lane => Assert.Empty(lane.Roundabouts));
            Assert.True(lanes[0].RoadWidth > 0);
        }
    }

    [Fact]
    public void Challenge_always_has_loops_and_a_roundabout_that_fit_their_lane()
    {
        for (int i = 0; i < 30; i++)
        {
            var lanes = RoadTracingGenerator.Lanes(Level.Challenge, SheetPage.For(PaperSize.Letter).Content, new SeededRandom($"c{i}"));
            Assert.Contains(lanes, lane => lane.Roundabouts.Count > 0);
            Assert.Contains(lanes, lane => lane.Roundabouts.Count == 0);  // the loops lane
            foreach (var ring in lanes.SelectMany(l => l.Roundabouts))
                Assert.True(ring.R >= 36, "roundabout too cramped to trace");
        }
    }

    [Fact]
    public void Easy_starts_with_a_straight_road()
    {
        var lanes = RoadTracingGenerator.Lanes(Level.Easy, SheetPage.For(PaperSize.Letter).Content, new SeededRandom("easy"));
        Assert.All(lanes[0].Path, p => Assert.Equal(lanes[0].Area.CenterY, p.Y, 6));
    }

    private static bool Inside(Box inner, Box outer) =>
        inner.X >= outer.X - 1e-6 && inner.Y >= outer.Y - 1e-6 && inner.Right <= outer.Right + 1e-6 && inner.Bottom <= outer.Bottom + 1e-6;
}
