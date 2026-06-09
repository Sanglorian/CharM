namespace CharM.Engine.Evaluation;

/// <summary>
/// Handles weapon damage die progression for the die-increase mechanic.
/// Die progression: d4 → d6 → d8 → d10 → d12 → 2d6 → 2d8 → 2d10 → 2d12
/// </summary>
public static class DieProgression
{
    // Canonical die progression as (count, sides) tuples.
    private static readonly (int Count, int Sides)[] Progression =
    [
        (1, 4), (1, 6), (1, 8), (1, 10), (1, 12),
        (2, 6), (2, 8), (2, 10), (2, 12),
    ];

    /// <summary>
    /// Increase a damage die expression by N steps.
    /// Input examples: "1d6", "1d8", "2d6"
    /// </summary>
    public static string Increase(string damage, int steps)
    {
        if (steps <= 0)
            return damage;

        var (count, sides) = Parse(damage);
        int index = FindIndex(count, sides);
        int newIndex = Math.Min(index + steps, Progression.Length - 1);
        var result = Progression[newIndex];
        return $"{result.Count}d{result.Sides}";
    }

    private static (int Count, int Sides) Parse(string damage)
    {
        var span = damage.AsSpan().Trim();
        int dIndex = span.IndexOf('d');
        if (dIndex < 0)
            throw new ArgumentException($"Invalid damage die expression: '{damage}'");

        int count = dIndex == 0 ? 1 : int.Parse(span[..dIndex]);
        int sides = int.Parse(span[(dIndex + 1)..]);
        return (count, sides);
    }

    private static int FindIndex(int count, int sides)
    {
        for (int i = 0; i < Progression.Length; i++)
        {
            if (Progression[i].Count == count && Progression[i].Sides == sides)
                return i;
        }

        throw new ArgumentException($"Die {count}d{sides} is not in the standard progression.");
    }
}
