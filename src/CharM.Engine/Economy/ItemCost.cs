using CharM.Engine.Creation;
using CharM.Engine.Rules;

namespace CharM.Engine.Economy;

/// <summary>
/// Resolves the purchase price of rules items from their cost fields and
/// mirrors the OCB <c>LootCost(base, enchant)</c> composition rule.
/// </summary>
public static class ItemCost
{
    // Cost-bearing field names on a RulesElement, aligned to the five
    // currency denominations. Items in rules.db carry the price as separate
    // Gold/Silver/Copper/Platinum/Astral fields (e.g. Longsword Gold=15).
    private static readonly (string Field, int DenomIndex)[] CostFields =
    [
        ("Copper", 0),
        ("Silver", 1),
        ("Gold", 2),
        ("Platinum", 3),
        ("Astral", 4),
    ];

    /// <summary>
    /// Read the listed price of a single rules element from its cost fields.
    /// Missing or blank fields contribute zero.
    /// </summary>
    public static D20Currency Of(RulesElement element)
    {
        double cp = 0, sp = 0, gp = 0, pp = 0, ad = 0;
        foreach (var (field, denom) in CostFields)
        {
            if (!element.Fields.TryGetValue(field, out var raw) || string.IsNullOrWhiteSpace(raw))
                continue;

            var cleaned = raw.Replace(",", string.Empty).Trim();
            if (!double.TryParse(cleaned, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var value))
                continue;

            switch (denom)
            {
                case 0: cp += value; break;
                case 1: sp += value; break;
                case 2: gp += value; break;
                case 3: pp += value; break;
                case 4: ad += value; break;
            }
        }

        return new D20Currency(cp, sp, gp, pp, ad);
    }

    /// <summary>
    /// Cost of a base + enchantment pairing, mirroring OCB's
    /// <c>LootCost</c>: when an enchantment is present its price already
    /// represents the finished item, so the enchantment's cost wins;
    /// otherwise the base item's cost is used.
    /// </summary>
    /// <remarks>
    /// The OCB <c>LootCost</c> also applies a per-character percentage discount
    /// for one special item family (cost scaled by <c>100 / (charStat + 1)</c>).
    /// That branch is deferred — it is not needed for by-the-book mundane/level-1
    /// shopping. See <c>D20Workspace.LootCost</c> in the decompile.
    /// </remarks>
    public static D20Currency Of(RulesElement @base, RulesElement? enchantment)
        => Of(enchantment ?? @base);

    /// <summary>
    /// Total cost of a composite <see cref="LootItem"/>: the base+enchantment
    /// price plus any attached augment's own listed price.
    /// </summary>
    public static D20Currency Of(LootItem loot)
    {
        var cost = Of(loot.Base, loot.Enchantment);
        if (loot.Augment is not null)
            cost += Of(loot.Augment);
        return cost;
    }
}
