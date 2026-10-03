using FocusFun.Core.Art;
using FocusFun.Core.Svg;

namespace FocusFun.Core.Generators;

/// <summary>
/// A page to colour in. Easy: one big vehicle. Medium: a vehicle on a road with sun, clouds and a tree.
/// Challenge: a busy street with two vehicles, something flying, trees and a traffic light.
/// Other topics (animals, fruits, approved AI pictures) use their own pictures: one big, two, or four.
/// </summary>
public sealed class ColoringGenerator : ISheetGenerator
{
    public SheetType Type => SheetType.Coloring;

    public SheetContent Generate(SheetRequest request, Box box, SeededRandom random)
    {
        if (ThemeText.TopicPictures(request) is { } set) return TopicPage(set, request.Level, box, random);

        var scene = Build(request.Level, box, random);
        string svg = Scenes.Render(scene, box, lineWidth: 3).Svg;

        var vehicles = scene.Items.Where(i => i.Kind == SceneKind.RoadVehicle).ToList();
        string instruction = request.Level == Level.Easy
            ? $"Color the {Vehicles.Name(vehicles[0].Vehicle)}!"
            : "Color the picture!";
        return new SheetContent("Coloring Time", instruction, svg, null);
    }

    /// <summary>Shared with colour-by-number so both use the same scenes per level.</summary>
    public static SceneSpec Build(Level level, Box box, SeededRandom random)
    {
        double height = Scenes.HeightFor(box);
        return level switch
        {
            Level.Easy => Scenes.SingleVehicle(Vehicles.All[random.Next(Vehicles.All.Count)], height, random.Next(2) == 0),
            Level.Medium => Scenes.Street(random, height, busy: false),
            _ => Scenes.Street(random, height, busy: true),
        };
    }

    /// <summary>Easy: one picture filling the page. Medium: two, one above the other. Challenge: four in a 2 x 2 grid.</summary>
    private static SheetContent TopicPage(IPictureSet set, Level level, Box box, SeededRandom random)
    {
        var pictures = set.All.ToList();
        random.Shuffle(pictures);
        int count = level switch { Level.Easy => 1, Level.Medium => 2, _ => 4 };
        var chosen = Enumerable.Range(0, count).Select(i => pictures[i % pictures.Count]).ToList();

        int cols = count == 4 ? 2 : 1, rows = count / cols;
        double cellW = box.Width / cols, cellH = box.Height / rows;
        var w = new SvgWriter();
        for (int i = 0; i < chosen.Count; i++)
        {
            var cell = new Box(box.X + i % cols * cellW + 12, box.Y + i / cols * cellH + 12, cellW - 24, cellH - 24);
            w.Raw(chosen[i].Render(cell, 3));
        }

        string instruction = count == 1 ? $"Color the {chosen[0].Word}!" : $"Color the {set.Plural}!";
        return new SheetContent("Coloring Time", instruction, w.ToString(), null);
    }
}
