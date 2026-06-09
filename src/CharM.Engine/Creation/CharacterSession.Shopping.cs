using CharM.Engine.Economy;
using CharM.Engine.Rules;

namespace CharM.Engine.Creation;

/// <summary>
/// Outcome of a buy operation: the price charged, the resulting Carried Money
/// balance, and whether the balance went negative (the purchase is still
/// applied — OCB permits overspending).
/// </summary>
public readonly record struct PurchaseResult(
    D20Currency Cost,
    D20Currency NewBalance,
    bool WentNegative);

public sealed partial class CharacterSession
{
    // --- Shopping: buy (costs gold) vs add (free) ---

    /// <summary>
    /// Add loot to inventory for free (no money deducted). Mirrors OCB's
    /// "Add" action. This is the same path the importer uses when replaying a
    /// source <c>&lt;LootTally&gt;</c>.
    /// </summary>
    public void AddLoot(LootItem loot, int quantity = 1)
        => AddInventoryItem(loot, quantity);

    /// <summary>Free-add a single rules element to inventory.</summary>
    public void AddLoot(RulesElement item, int quantity = 1)
        => AddInventoryItem(item, quantity);

    /// <summary>
    /// Buy loot: deduct its listed price (× quantity) from Carried Money and add
    /// it to inventory. Mirrors OCB's "Buy" action. Overspending is permitted —
    /// the balance may go negative; check <see cref="PurchaseResult.WentNegative"/>.
    /// </summary>
    public PurchaseResult BuyLoot(LootItem loot, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(loot);
        if (quantity < 1)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Purchase quantity must be positive.");

        var unitCost = ItemCost.Of(loot);
        var totalCost = unitCost * quantity;
        var newBalance = CarriedMoney - totalCost;

        SetCarriedMoney(newBalance);
        AddInventoryItem(loot, quantity);

        return new PurchaseResult(totalCost, newBalance, newBalance.IsNegative);
    }

    /// <summary>Buy a single rules element, deducting its listed price.</summary>
    public PurchaseResult BuyLoot(RulesElement item, int quantity = 1)
        => BuyLoot(new LootItem { Base = item }, quantity);

    /// <summary>
    /// Price preview for a composite loot item (× quantity) without mutating
    /// state. UI uses this to show the cost before committing a buy.
    /// </summary>
    public D20Currency PriceOf(LootItem loot, int quantity = 1)
        => ItemCost.Of(loot) * Math.Max(1, quantity);

    /// <summary>Price preview for a single rules element.</summary>
    public D20Currency PriceOf(RulesElement item, int quantity = 1)
        => ItemCost.Of(item) * Math.Max(1, quantity);
}
