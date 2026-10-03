namespace FocusFun.Core;

/// <summary>
/// Deterministic PRNG (SplitMix64) so the same seed always reproduces the same sheet.
/// We avoid System.Random because its seeded algorithm is not guaranteed stable across .NET versions.
/// </summary>
public sealed class SeededRandom
{
    private ulong _state;

    public SeededRandom(string seed)
    {
        _state = Fnv1a64(seed);
    }

    public ulong NextULong()
    {
        ulong z = _state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Returns an integer in [0, maxExclusive).</summary>
    public int Next(int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExclusive);
        return (int)(NextULong() % (ulong)maxExclusive);
    }

    /// <summary>Returns an integer in [minInclusive, maxExclusive).</summary>
    public int Next(int minInclusive, int maxExclusive) => minInclusive + Next(maxExclusive - minInclusive);

    public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

    public void Shuffle<T>(IList<T> items)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    public static string NewSeed() => Guid.NewGuid().ToString("N")[..10];

    private static ulong Fnv1a64(string text)
    {
        ulong hash = 0xCBF29CE484222325UL;
        foreach (char c in text)
        {
            hash ^= c;
            hash *= 0x100000001B3UL;
        }
        return hash;
    }
}
