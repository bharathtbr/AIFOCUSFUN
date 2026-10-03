using FocusFun.Core.Generators;

namespace FocusFun.Core.Tests;

public class MazeTests
{
    public static TheoryData<int, string> Cases()
    {
        var data = new TheoryData<int, string>();
        foreach (int size in new[] { 6, 10, 14 })
            for (int i = 0; i < 25; i++)
                data.Add(size, $"seed-{i}");
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Maze_is_perfect_so_there_is_exactly_one_solution(int size, string seed)
    {
        var maze = Maze.Build(size, new SeededRandom(seed));

        // A connected graph with V vertices and V-1 edges is a tree: exactly one path between any two cells.
        Assert.Equal(size * size - 1, maze.PassageCount());
        Assert.Equal(size * size, CountReachable(maze));

        var path = maze.Solve(new Cell(0, 0), new Cell(size - 1, size - 1));
        Assert.Equal(new Cell(0, 0), path[0]);
        Assert.Equal(new Cell(size - 1, size - 1), path[^1]);
        for (int i = 1; i < path.Count; i++)
            Assert.True(maze.IsOpen(path[i - 1], path[i]));
    }

    [Fact]
    public void Same_seed_builds_the_same_maze()
    {
        var a = Maze.Build(10, new SeededRandom("abc"));
        var b = Maze.Build(10, new SeededRandom("abc"));
        Assert.Equal(Layout(a), Layout(b));
    }

    [Fact]
    public void Different_seeds_build_different_mazes()
    {
        var a = Maze.Build(10, new SeededRandom("abc"));
        var b = Maze.Build(10, new SeededRandom("xyz"));
        Assert.NotEqual(Layout(a), Layout(b));
    }

    private static int CountReachable(Maze maze)
    {
        var seen = new HashSet<Cell> { new(0, 0) };
        var queue = new Queue<Cell>(seen);
        while (queue.Count > 0)
            foreach (var next in maze.OpenNeighbours(queue.Dequeue()))
                if (seen.Add(next)) queue.Enqueue(next);
        return seen.Count;
    }

    private static string Layout(Maze maze)
    {
        var chars = new List<char>();
        for (int r = 0; r < maze.Size; r++)
            for (int c = 0; c < maze.Size; c++)
            {
                chars.Add(maze.OpenEast(r, c) ? 'E' : '.');
                chars.Add(maze.OpenSouth(r, c) ? 'S' : '.');
            }
        return new string(chars.ToArray());
    }
}
