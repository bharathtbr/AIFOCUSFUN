namespace FocusFun.Core.Generators;

public readonly record struct Cell(int Row, int Col);

/// <summary>
/// A perfect maze on a square grid: every cell is reachable and there is exactly one path between any two cells.
/// Built with an iterative recursive-backtracker so large grids cannot overflow the stack.
/// </summary>
public sealed class Maze
{
    private readonly bool[,] _openEast;
    private readonly bool[,] _openSouth;

    public int Size { get; }

    private Maze(int size)
    {
        Size = size;
        _openEast = new bool[size, size];
        _openSouth = new bool[size, size];
    }

    public static Maze Build(int size, SeededRandom random)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 2);
        var maze = new Maze(size);
        var visited = new bool[size, size];
        var stack = new Stack<Cell>();
        var start = new Cell(0, 0);
        visited[0, 0] = true;
        stack.Push(start);

        var options = new List<Cell>(4);
        while (stack.Count > 0)
        {
            var current = stack.Peek();
            options.Clear();
            foreach (var next in maze.Neighbours(current))
                if (!visited[next.Row, next.Col]) options.Add(next);

            if (options.Count == 0)
            {
                stack.Pop();
                continue;
            }

            var chosen = options[random.Next(options.Count)];
            maze.Open(current, chosen);
            visited[chosen.Row, chosen.Col] = true;
            stack.Push(chosen);
        }

        return maze;
    }

    public bool OpenEast(int row, int col) => col < Size - 1 && _openEast[row, col];

    public bool OpenSouth(int row, int col) => row < Size - 1 && _openSouth[row, col];

    public bool IsOpen(Cell a, Cell b)
    {
        if (a.Row == b.Row && Math.Abs(a.Col - b.Col) == 1) return OpenEast(a.Row, Math.Min(a.Col, b.Col));
        if (a.Col == b.Col && Math.Abs(a.Row - b.Row) == 1) return OpenSouth(Math.Min(a.Row, b.Row), a.Col);
        return false;
    }

    public IEnumerable<Cell> Neighbours(Cell c)
    {
        if (c.Row > 0) yield return c with { Row = c.Row - 1 };
        if (c.Col < Size - 1) yield return c with { Col = c.Col + 1 };
        if (c.Row < Size - 1) yield return c with { Row = c.Row + 1 };
        if (c.Col > 0) yield return c with { Col = c.Col - 1 };
    }

    public IEnumerable<Cell> OpenNeighbours(Cell c) => Neighbours(c).Where(n => IsOpen(c, n));

    public int PassageCount()
    {
        int count = 0;
        for (int r = 0; r < Size; r++)
            for (int c = 0; c < Size; c++)
            {
                if (OpenEast(r, c)) count++;
                if (OpenSouth(r, c)) count++;
            }
        return count;
    }

    /// <summary>Shortest (and, in a perfect maze, only) path from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public IReadOnlyList<Cell> Solve(Cell from, Cell to)
    {
        var previous = new Dictionary<Cell, Cell> { [from] = from };
        var queue = new Queue<Cell>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == to) break;
            foreach (var next in OpenNeighbours(current))
                if (previous.TryAdd(next, current)) queue.Enqueue(next);
        }

        if (!previous.ContainsKey(to)) return [];

        var path = new List<Cell>();
        for (var c = to; c != from; c = previous[c]) path.Add(c);
        path.Add(from);
        path.Reverse();
        return path;
    }

    private void Open(Cell a, Cell b)
    {
        if (a.Row == b.Row) _openEast[a.Row, Math.Min(a.Col, b.Col)] = true;
        else _openSouth[Math.Min(a.Row, b.Row), a.Col] = true;
    }
}
