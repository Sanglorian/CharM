using CharM.Engine.CharacterModel;
using CharM.Engine.Orchestration;
using CharM.Engine.Rules;
using System.Xml.Linq;

namespace CharM.Engine.Creation;

public sealed partial class CharacterSession
{
    // --- Equipment ---

    /// <summary>
    /// Get all currently equipped composite loot items, keyed by slot name.
    /// </summary>
    public IReadOnlyDictionary<string, LootItem> GetEquippedLoot() => _equippedItems;

    /// <summary>Get the composite loot equipped in a specific slot, or null.</summary>
    public LootItem? GetEquippedLoot(string slot) => _equippedItems.GetValueOrDefault(slot);

    /// <summary>
    /// Back-compat: get all equipped slots' base RulesElements. Callers that
    /// need composite info should use <see cref="GetEquippedLoot()"/>.
    /// </summary>
    public IReadOnlyDictionary<string, RulesElement> GetEquippedItems()
        => _equippedItems.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Base,
            StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Back-compat: get the base RulesElement of whatever's in the slot.
    /// </summary>
    public RulesElement? GetEquippedItem(string slot)
        => _equippedItems.TryGetValue(slot, out var loot) ? loot.Base : null;

    /// <summary>Equip a composite loot item in the given slot. Replaces any existing item in that slot.</summary>
    public void EquipItem(string slot, LootItem loot)
    {
        _equippedItems[slot] = loot;
        InvalidateSnapshot();
        NotifyChanged();
    }

    /// <summary>Equip a single (non-composite) RulesElement — convenience overload.</summary>
    public void EquipItem(string slot, RulesElement item)
        => EquipItem(slot, new LootItem { Base = item });

    /// <summary>Remove the item from the given slot. Returns false if slot was empty.</summary>
    public bool UnequipItem(string slot)
    {
        if (!_equippedItems.Remove(slot))
            return false;

        InvalidateSnapshot();
        NotifyChanged();
        return true;
    }

    // --- Inventory (non-slot gear: gear, consumables, mundane items) ---

    /// <summary>Get all inventory items (non-equipped gear).</summary>
    public IReadOnlyList<InventoryItem> GetInventory() => _inventory;

    public IReadOnlyList<RitualPracticeAlchemyEntry> GetRitualPracticeAlchemyEntries()
    {
        var rows = new Dictionary<string, RitualPracticeAlchemyEntry>(StringComparer.OrdinalIgnoreCase);

        void Add(RulesElement element, RitualPracticeAlchemyKind kind, int quantity, bool isInventoryItem)
        {
            var key = element.InternalId ?? $"{element.Type}:{element.Name}";
            if (rows.TryGetValue(key, out var existing))
            {
                rows[key] = existing with
                {
                    Quantity = existing.Quantity + quantity,
                    IsInventoryItem = existing.IsInventoryItem || isInventoryItem,
                };
            }
            else
            {
                rows[key] = new RitualPracticeAlchemyEntry(element, kind, quantity, isInventoryItem);
            }
        }

        foreach (var element in _wizard.ElementTree.GetActiveElements())
        {
            var kind = RitualPracticeAlchemyClassifier.GetKind(element);
            if (kind is RitualPracticeAlchemyKind.Ritual
                or RitualPracticeAlchemyKind.RitualScroll
                or RitualPracticeAlchemyKind.AlchemicalFormula
                or RitualPracticeAlchemyKind.PoisonRecipe
                or RitualPracticeAlchemyKind.MartialPractice)
            {
                Add(element, kind.Value, 0, isInventoryItem: false);
            }
        }

        foreach (var inventory in _inventory)
        {
            foreach (var component in inventory.Item.Components())
            {
                var kind = RitualPracticeAlchemyClassifier.GetKind(component);
                if (kind is null)
                    continue;

                Add(component, kind.Value, inventory.Quantity, isInventoryItem: true);
            }
        }

        return rows.Values
            .OrderBy(r => RitualPracticeAlchemyClassifier.KindSortKey(r.Kind))
            .ThenBy(r => LevelSortKey(r.Element))
            .ThenBy(r => r.Element.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Add a composite loot item to inventory; if the same owned composite is already present, increases quantity. Quantity 0 is allowed as a "placeholder" — OCB users sometimes record items they intend to acquire later (count="0" in source). Zero-count placeholders are intentionally not merged: duplicate placeholder rows are source-visible LootTally entries and must round-trip as separate rows. Items that share a CompositeKey but differ on <see cref="LootItem.AugmentXml"/> (e.g. two Longsword + Frost Weapon +3 placeholders, one bare and one with an attached Siberys Shard) are kept as separate entries so source rows round-trip distinctly.</summary>
    public void AddInventoryItem(LootItem loot, int quantity = 1)
    {
        if (quantity < 0) return;
        var key = loot.CompositeKey;
        var existing = quantity > 0
            ? _inventory.FirstOrDefault(i =>
                i.Quantity > 0
                && string.Equals(i.Item.CompositeKey, key, StringComparison.OrdinalIgnoreCase)
                && string.Equals(i.Item.AugmentXml ?? string.Empty, loot.AugmentXml ?? string.Empty, StringComparison.Ordinal))
            : null;
        if (existing is not null)
        {
            existing.Quantity += quantity;
        }
        else
        {
            _inventory.Add(new InventoryItem { Item = loot, Quantity = quantity });
        }
        InvalidateSnapshot();
        NotifyChanged();
    }

    /// <summary>Convenience overload: add a single (non-composite) RulesElement to inventory.</summary>
    public void AddInventoryItem(RulesElement item, int quantity = 1)
        => AddInventoryItem(new LootItem { Base = item }, quantity);

    /// <summary>Add an existing rules-database item to inventory through the explicit houserule workflow.</summary>
    public void AddHouseruleInventoryItem(RulesElement item, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Houserule inventory quantity must be positive.");
        if (string.IsNullOrWhiteSpace(item.InternalId))
            throw new ArgumentException("Houserule inventory items require an existing rules-database internal id.", nameof(item));

        var fullItem = _findById(item.InternalId) ?? item;
        UpsertHouseruleGrant(fullItem, Level, HouseruleGrantKind.Inventory, quantity: quantity);
        MarkElementHouseruled(fullItem.InternalId);
        AddInventoryItem(fullItem, quantity);
    }

    /// <summary>Equip an existing rules-database item through the explicit houserule workflow.</summary>
    public void EquipHouseruleItem(string slot, RulesElement item)
    {
        if (string.IsNullOrWhiteSpace(slot))
            throw new ArgumentException("Equipment slot is required.", nameof(slot));
        ArgumentNullException.ThrowIfNull(item);
        if (string.IsNullOrWhiteSpace(item.InternalId))
            throw new ArgumentException("Houserule equipment requires an existing rules-database internal id.", nameof(item));

        var fullItem = _findById(item.InternalId) ?? item;
        UpsertHouseruleGrant(fullItem, Level, HouseruleGrantKind.Equipment, slot);
        MarkElementHouseruled(fullItem.InternalId);
        EquipItem(slot, fullItem);
    }

    private void UpsertHouseruleGrant(
        RulesElement element,
        int atLevel,
        HouseruleGrantKind kind,
        string? slot = null,
        int quantity = 1)
    {
        int level = atLevel > 0 ? atLevel : Level;
        int existingIndex = _houseruleGrants.FindIndex(g =>
            g.Kind == kind
            && g.AtLevel == level
            && string.Equals(g.Slot ?? string.Empty, slot ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            && string.Equals(g.Element.InternalId, element.InternalId, StringComparison.OrdinalIgnoreCase));

        if (existingIndex < 0)
        {
            _houseruleGrants.Add(new HouseruleGrant(element, level, kind, slot, quantity));
            return;
        }

        var existing = _houseruleGrants[existingIndex];
        int mergedQuantity = kind == HouseruleGrantKind.Inventory
            ? existing.Quantity + quantity
            : quantity;
        _houseruleGrants[existingIndex] = existing with { Quantity = mergedQuantity };
    }

    /// <summary>Set the inventory quantity for an item; removes the entry if quantity ≤ 0.</summary>
    public void SetInventoryQuantity(string internalId, int quantity)
    {
        var existing = _inventory.FirstOrDefault(i => string.Equals(i.Item.Base.InternalId, internalId, StringComparison.OrdinalIgnoreCase));
        if (existing is null) return;
        if (quantity <= 0)
        {
            _inventory.Remove(existing);
        }
        else
        {
            existing.Quantity = quantity;
        }
        InvalidateSnapshot();
        NotifyChanged();
    }

    /// <summary>Remove an inventory entry entirely.</summary>
    public bool RemoveInventoryItem(string internalId)
    {
        var existing = _inventory.FirstOrDefault(i => string.Equals(i.Item.Base.InternalId, internalId, StringComparison.OrdinalIgnoreCase));
        if (existing is null) return false;
        _inventory.Remove(existing);
        InvalidateSnapshot();
        NotifyChanged();
        return true;
    }
}
