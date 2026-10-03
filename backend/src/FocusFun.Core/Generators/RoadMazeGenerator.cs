using System.Text;
using FocusFun.Core.Art;
using FocusFun.Core.Svg;

namespace FocusFun.Core.Generators;

using static SvgWriter;

/// <summary>
/// "Drive the car to the garage": a perfect maze with the car at the top-left entrance and the garage at the bottom-right exit.
/// Other topics use one of their pictures and a star ("Help the cat reach the star!").
/// </summary>
public sealed class RoadMazeGenerator : ISheetGenerator
{
    private const double WallWidth = 3;
    private const int ChallengeCoins = 3;

    /// <summary>Vehicles that drive on roads and so make sense at the maze entrance.</summary>
    private static readonly Vehicle[] Drivers =
        [Vehicle.Car, Vehicle.Bus, Vehicle.GarbageTruck, Vehicle.FireTruck, Vehicle.DumpTruck, Vehicle.Autorickshaw];

    public SheetType Type => SheetType.RoadMaze;

    public static int GridSize(Level level) => level switch
    {
        Level.Easy => 6,
        Level.Medium => 10,
        _ => 14,
    };

    public SheetContent Generate(SheetRequest request, Box box, SeededRandom random)
    {
        int n = GridSize(request.Level);
        var maze = Maze.Build(n, random);
        var start = new Cell(0, 0);
        var end = new Cell(n - 1, n - 1);
        var solution = maze.Solve(start, end);

        // Leave room either side of the grid for the car and the garage.
        double iconSpace = Math.Min(80, box.Width * 0.14);
        double cell = Math.Min((box.Width - 2 * iconSpace) / n, box.Height / n);
        double gridSize = cell * n;
        var grid = new Box(box.CenterX - gridSize / 2, box.Y + (box.Height - gridSize) / 2, gridSize, gridSize);

        var coins = request.Level == Level.Challenge ? PickCoins(solution, random) : [];
        // Other topics send one of their own pictures to a star instead of a vehicle to the garage.
        var topic = ThemeText.TopicPictures(request);
        var vehicle = Drivers[random.Next(Drivers.Length)];
        var traveller = topic?.All[random.Next(topic.All.Count)];

        double iconSize = Math.Min(iconSpace, cell * 1.6);
        var travellerBox = new Box(grid.X - iconSpace / 2 - iconSize / 2, grid.Y + cell / 2 - iconSize / 2, iconSize, iconSize);
        double goalX = grid.Right + iconSpace / 2, goalY = grid.Bottom - cell / 2;
        var body = new StringBuilder()
            .Append(DrawWalls(maze, grid, cell))
            .Append(traveller is null ? Vehicles.Draw(vehicle, travellerBox, 2) : traveller.Render(travellerBox, 2))
            .Append(traveller is null ? Icons.Garage(goalX, goalY, iconSize) : Icons.Star(goalX, goalY, iconSize * 0.4, 2.5));
        foreach (var coin in coins)
            body.Append(Icons.Coin(CenterX(grid, cell, coin), CenterY(grid, cell, coin), cell * 0.28));

        string answerKey = body + DrawSolution(solution, grid, cell, iconSpace);

        string instruction = (traveller, coins.Count > 0) switch
        {
            (null, true) => "Drive to the garage and collect every coin!",
            (null, false) => $"Drive the {Vehicles.Name(vehicle)} to the garage!",
            (_, true) => "Reach the star and collect every coin!",
            _ => $"Help the {traveller.Word} reach the star!",
        };

        return new SheetContent(traveller is null ? "Road Maze" : "Maze", instruction, body.ToString(), answerKey);
    }

    private static string DrawWalls(Maze maze, Box grid, double cell)
    {
        int n = maze.Size;
        var d = new StringBuilder();
        void Segment(double x1, double y1, double x2, double y2) =>
            d.Append($"M{N(x1)} {N(y1)}L{N(x2)} {N(y2)}");

        // Outer walls, with an entrance on the left of the first row and an exit on the right of the last row.
        Segment(grid.X, grid.Y, grid.Right, grid.Y);
        Segment(grid.X, grid.Bottom, grid.Right, grid.Bottom);
        Segment(grid.X, grid.Y + cell, grid.X, grid.Bottom);
        Segment(grid.Right, grid.Y, grid.Right, grid.Bottom - cell);

        for (int r = 0; r < n; r++)
            for (int c = 0; c < n; c++)
            {
                double x = grid.X + c * cell, y = grid.Y + r * cell;
                if (c < n - 1 && !maze.OpenEast(r, c)) Segment(x + cell, y, x + cell, y + cell);
                if (r < n - 1 && !maze.OpenSouth(r, c)) Segment(x, y + cell, x + cell, y + cell);
            }

        return new SvgWriter().Path(d.ToString(), WallWidth).ToString();
    }

    private static string DrawSolution(IReadOnlyList<Cell> solution, Box grid, double cell, double iconSpace)
    {
        var d = new StringBuilder($"M{N(grid.X - iconSpace * 0.1)} {N(grid.Y + cell / 2)}");
        foreach (var c in solution)
            d.Append($"L{N(CenterX(grid, cell, c))} {N(CenterY(grid, cell, c))}");
        d.Append($"L{N(grid.Right + iconSpace * 0.1)} {N(grid.Bottom - cell / 2)}");
        return new SvgWriter().Path(d.ToString(), Math.Max(3, cell * 0.18), dash: "8 6").ToString();
    }

    /// <summary>Coins sit on the solution path so every one of them can be collected on the way.</summary>
    private static List<Cell> PickCoins(IReadOnlyList<Cell> solution, SeededRandom random)
    {
        var candidates = solution.Skip(2).Take(Math.Max(0, solution.Count - 4)).ToList();
        random.Shuffle(candidates);
        return candidates.Take(ChallengeCoins).ToList();
    }

    private static double CenterX(Box grid, double cell, Cell c) => grid.X + (c.Col + 0.5) * cell;

    private static double CenterY(Box grid, double cell, Cell c) => grid.Y + (c.Row + 0.5) * cell;
}
