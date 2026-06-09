namespace CharM.Engine.Creation;

/// <summary>
/// The six 4E ability scores in standard order.
/// </summary>
public enum Ability { Strength, Constitution, Dexterity, Intelligence, Wisdom, Charisma }

/// <summary>
/// A set of 6 base ability scores (before racial modifiers).
/// </summary>
public sealed class AbilityScoreSet
{
    private readonly int[] _scores = new int[6];

    public int this[Ability ability]
    {
        get => _scores[(int)ability];
        set => _scores[(int)ability] = value;
    }

    public int[] ToArray() => (int[])_scores.Clone();

    /// <summary>Total point-buy cost of these scores. Returns -1 if any score is out of valid range (8-18).</summary>
    public int PointBuyCost()
    {
        int total = 0;
        foreach (var score in _scores)
        {
            int cost = PointBuyCostTable(score);
            if (cost <= -99) return -1; // out of valid range
            total += cost;
        }
        return total;
    }

    /// <summary>
    /// Point-buy cost for a single score value (4e Rules Compendium).
    /// Costs are relative to a base of 10. Scores below 10 give refunds.
    /// Initial array is five 10s and one 8, with 22 points to spend.
    /// </summary>
    public static int PointBuyCostTable(int score) => score switch
    {
        8 => -2,
        9 => -1,
        10 => 0,
        11 => 1,
        12 => 2,
        13 => 3,
        14 => 5,
        15 => 7,
        16 => 9,
        17 => 12,
        18 => 16,
        _ => -99
    };
}

/// <summary>
/// Generates ability score sets using various methods.
/// </summary>
public static class AbilityScoreGenerator
{
    /// <summary>Standard point-buy budget (points available from the default starting array).</summary>
    public const int StandardBudget = 22;

    /// <summary>
    /// Default starting array for point-buy: five 10s and one 8.
    /// The 8 frees up 2 points, giving an effective 22 spendable from this base.
    /// </summary>
    public static readonly int[] PointBuyDefault = [10, 10, 10, 10, 10, 8];

    /// <summary>Standard array</summary>
    public static readonly int[] StandardArray = [16, 14, 13, 12, 11, 10];

    /// <summary>Balanced array</summary>
    public static readonly int[] BalancedArray = [14, 14, 13, 12, 11, 10];

    /// <summary>Dual-focused array</summary>
    public static readonly int[] DualFocusedArray = [16, 16, 12, 11, 11, 8];

    /// <summary>Create a score set from a standard array, assigned in ability order.</summary>
    public static AbilityScoreSet FromArray(int[] array)
    {
        if (array.Length != 6)
            throw new ArgumentException("Array must contain exactly 6 scores.", nameof(array));

        var set = new AbilityScoreSet();
        for (int i = 0; i < 6; i++)
            set[(Ability)i] = array[i];
        return set;
    }

    /// <summary>Create a custom score set from 6 user-provided values.</summary>
    public static AbilityScoreSet FromCustom(int str, int con, int dex, int intel, int wis, int cha)
    {
        var set = new AbilityScoreSet();
        set[Ability.Strength] = str;
        set[Ability.Constitution] = con;
        set[Ability.Dexterity] = dex;
        set[Ability.Intelligence] = intel;
        set[Ability.Wisdom] = wis;
        set[Ability.Charisma] = cha;
        return set;
    }

    /// <summary>Roll 4d6 drop lowest for each ability.</summary>
    public static AbilityScoreSet Roll4d6DropLowest(Random? random = null)
    {
        random ??= Random.Shared;
        var set = new AbilityScoreSet();
        for (int i = 0; i < 6; i++)
        {
            int lowest = 0, total = 0;
            for (int j = 0; j < 4; j++)
            {
                int roll = random.Next(1, 7);
                if (lowest == 0 || roll < lowest)
                {
                    total += lowest;
                    lowest = roll;
                }
                else
                {
                    total += roll;
                }
            }
            set[(Ability)i] = total;
        }
        return set;
    }

    /// <summary>Roll 3d6 straight for each ability (old school).</summary>
    public static AbilityScoreSet Roll3d6(Random? random = null)
    {
        random ??= Random.Shared;
        var set = new AbilityScoreSet();
        for (int i = 0; i < 6; i++)
        {
            int total = 0;
            for (int j = 0; j < 3; j++)
                total += random.Next(1, 7);
            set[(Ability)i] = total;
        }
        return set;
    }

    /// <summary>Roll 2d6+6 for each ability (generous).</summary>
    public static AbilityScoreSet Roll2d6Plus6(Random? random = null)
    {
        random ??= Random.Shared;
        var set = new AbilityScoreSet();
        for (int i = 0; i < 6; i++)
        {
            int total = 6;
            for (int j = 0; j < 2; j++)
                total += random.Next(1, 7);
            set[(Ability)i] = total;
        }
        return set;
    }

    /// <summary>Validate that a score set meets point-buy budget.</summary>
    public static bool IsValidPointBuy(AbilityScoreSet scores, int budget = StandardBudget)
    {
        int cost = scores.PointBuyCost();
        if (cost == -1) return false; // out-of-range score
        return RemainingPoints(scores, budget) >= 0;
    }

    /// <summary>
    /// Calculate remaining point-buy points.
    /// Points are relative to the default starting array [10,10,10,10,10,8].
    /// At defaults, remaining = budget (22). Raising a score spends points;
    /// lowering a 10 to 9 or 8 frees points.
    /// </summary>
    public static int RemainingPoints(AbilityScoreSet scores, int budget = StandardBudget)
    {
        int defaultCost = PointBuyDefault.Sum(s => AbilityScoreSet.PointBuyCostTable(s));
        int currentCost = scores.PointBuyCost();
        return budget - (currentCost - defaultCost);
    }

    /// <summary>
    /// Cost to increment a score by 1 from its current value.
    /// Returns the marginal cost (difference between cost at current+1 and cost at current).
    /// </summary>
    public static int IncrementCost(int currentScore)
    {
        if (currentScore >= 18) return int.MaxValue;
        return AbilityScoreSet.PointBuyCostTable(currentScore + 1)
             - AbilityScoreSet.PointBuyCostTable(currentScore);
    }
}
