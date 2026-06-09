namespace CharM.Engine.Economy;

/// <summary>
/// By-the-book starting money allowance per character level,  OCB's
/// <c>AutoGold()</c> set Carried Money to a hardcoded
/// <c>level_appropriate[level]</c> value (and Stored Money to a default).
/// </summary>
/// <remarks>
/// This is based off the last set of rules for higher level character wealth which is 
/// 3 uncommon items of charlevel-1, charlevel, charlevel+1 and gold 1 common at level-1
/// </remarks>
public static class StartingGoldTable
{
    /// <summary>Lowest level with a defined allowance.</summary>
    public const int MinLevel = 1;

    /// <summary>Highest level with a defined allowance.</summary>
    public const int MaxLevel = 30;

    /// <summary>Default Stored Money for a freshly auto-golded character.</summary>
    public static readonly D20Currency DefaultStored = D20Currency.Zero;
    private static readonly long[] CarriedGp =
    [
        0,         // [0] unused
        100,       // [1]  (confirmed; matches decompiled D20Workspace.AutoGold)
        852,       // [2]
        1124,      // [3]
        1396,      // [4]
        1988,      // [5]
        2900,      // [6]
        4260,      // [7]
        5620,      // [8]
        6980,      // [9]
        9940,      // [10]
        14500,     // [11]
        21300,     // [12]
        28100,     // [13]
        34900,     // [14]
        49700,     // [15]
        72500,     // [16]
        106500,    // [17]
        140500,    // [18]
        174500,    // [19]
        248500,    // [20]
        362500,    // [21]
        532500,    // [22]
        702500,    // [23]
        872500,    // [24]
        1242500,   // [25]
        1812500,   // [26]
        2662500,   // [27]
        3512500,   // [28]
        4362500,   // [29]
        4962500,   // [30] technically short — the formula calls for one
                   //      level+1 uncommon item, but there are no level-31
                   //      magic items, so the L30 value caps below the
                   //      pattern extrapolation from L27-L29.
    ];

    /// <summary>
    /// The level-appropriate Carried Money allowance for a given character
    /// level, clamped to the 1-30 range. Returns gold-denominated currency.
    /// </summary>
    public static D20Currency CarriedGpByLevel(int level)
    {
        int clamped = Math.Clamp(level, MinLevel, MaxLevel);
        return D20Currency.FromGold(CarriedGp[clamped]);
    }

    /// <summary>
    /// True for every level whose allowance is still a placeholder (everything
    /// except the confirmed level 1). UI/callers can surface a caveat.
    /// </summary>
    public static bool IsPlaceholder(int level) => false;
}
