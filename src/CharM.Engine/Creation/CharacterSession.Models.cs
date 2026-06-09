using CharM.Engine.CharacterModel;
using CharM.Engine.Orchestration;
using CharM.Engine.Rules;
using System.Xml.Linq;

namespace CharM.Engine.Creation;

/// <summary>
/// A record of a single user choice, with enough context to replay or display.
/// </summary>
public sealed record ChoiceRecord(
    RulesElement Element,
    ChoiceSlot Slot,
    int SequenceNumber,
    int Level = 1);

/// <summary>
/// Snapshot of the character's current state after building.
/// Contains computed stats, accumulated choices, and the element tree.
/// </summary>
public sealed class CharacterSnapshot
{
    public required CharacterBuilder Builder { get; init; }
    public required IReadOnlyList<ElementChoice> AccumulatedChoices { get; init; }
    public required AbilityScoreSet? AbilityScores { get; init; }
    public required int Level { get; init; }

    /// <summary>
    /// InternalIds of elements present in the element tree that should be
    /// emitted in the tally / level structure (so the source's vestigial
    /// row round-trips) but excluded from the rebuilt
    /// <c>&lt;PowerStats&gt;</c> section. Mirrors the OCB behavior where
    /// orphan powers (a tally row with no live build slot — typically
    /// follows an empty <c>&lt;RulesElement name="" type=""/&gt;</c>
    /// placeholder) appear in <c>&lt;RulesElementTally&gt;</c> but get no
    /// <c>&lt;Power&gt;</c> card. Populated by the importer's power-leaf
    /// fallback when it resorts to a free-grant for a deferred Power that
    /// never matched a real slot.
    /// </summary>
    public IReadOnlySet<string> PowerStatsExcludedIds { get; init; }
        = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// InternalIds of elements that live in the per-level
    /// (<c>&lt;Level&gt;</c>) tree for round-trip fidelity but must NOT
    /// appear in the flat <c>&lt;RulesElementTally&gt;</c> NOR in
    /// <c>&lt;PowerStats&gt;</c>. Mirrors OCB's handling of
    /// Wizard Spellbook entries (powers stored in the spellbook but not
    /// currently prepared) and of phantom retraining-noise rows: each
    /// appears nested under its owning class feature in the level tree
    /// but is absent from the source's flat tally and the source's
    /// power cards. The <see cref="PowerStatsExcludedIds"/> set is
    /// stricter and applies across the board, so this set is also
    /// merged into it for PowerStats filtering — callers only need to
    /// consult one of the two when building &lt;PowerStats&gt;.
    /// </summary>
    public IReadOnlySet<string> LevelNestedOnlyIds { get; init; }
        = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// InternalIds of elements granted via
    /// <see cref="CharacterSession.AddUserEditPick"/>. These elements are
    /// present in the engine tree (so their stat-add directives apply and
    /// any granted powers materialize as power cards) but must NOT appear
    /// in the rebuilt flat <c>&lt;RulesElementTally&gt;</c> NOR in the
    /// rebuilt per-<c>&lt;Level&gt;</c> tree on export — both of those
    /// channels are already covered by the verbatim
    /// <see cref="CharacterSession.HouseruleLevelUserEdits"/> and
    /// <see cref="CharacterSession.HouseruleFormATallyMirror"/>
    /// passthrough so we'd otherwise double-emit. Power cards (rebuilt
    /// <c>&lt;PowerStats&gt;</c>) are still produced — that's the whole
    /// point of the engine integration.
    /// </summary>
    public IReadOnlySet<string> UserEditPickIds { get; init; }
        = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public int GetStat(string name) => Builder.GetStatValue(name);
    public Dictionary<string, int> GetAllStats() => Builder.GetAllStatValues();
    public CharacterElementTree ElementTree => Builder.ElementTree;
}

/// <summary>
/// A composite piece of loot as the .dnd4e file actually models it: a
/// base item (Weapon, Armor, or a standalone Magic Item like a Cloak),
/// optionally enchanted with a Magic Item, and optionally augmented with a
/// further Magic Item. The OCB has no "attach enchantment" UX — pairings
/// are constructed when the loot entry is created and persisted as a single
/// &lt;loot&gt; group in the XML.
/// </summary>
public sealed class LootItem
{
    /// <summary>Base item: Weapon, Armor, Implement, or a standalone Magic Item.</summary>
    public required RulesElement Base { get; init; }

    /// <summary>Optional Magic Item enchanting <see cref="Base"/>.</summary>
    public RulesElement? Enchantment { get; init; }

    /// <summary>Optional Magic Item augmenting <see cref="Enchantment"/>.</summary>
    public RulesElement? Augment { get; init; }

    /// <summary>
    /// "Worn" state captured in the .dnd4e file as a Category RulesElement
    /// nested under the base (e.g. <c>WearingOffHandLightBlade</c>). Only
    /// meaningful when the item is equipped.
    /// </summary>
    public string? WornCategoryId { get; init; }

    /// <summary>
    /// Composite display name from the <c>&lt;loot name="..."&gt;</c> attribute,
    /// only present in source files when the item carries an augment.
    /// </summary>
    public string? CompositeName { get; init; }

    /// <summary>Optional loot-level weapon damage override from the <c>Damage</c> XML attribute.</summary>
    public string? DamageOverride { get; init; }

    /// <summary>Mirrors the <c>ShowPowerCard</c> XML attribute (defaults to true).</summary>
    public bool ShowPowerCard { get; init; } = true;

    /// <summary>Optional cached weight (only set on augmented items in source files).</summary>
    public double? Weight { get; init; }

    /// <summary>
    /// Raw <c>augment="..."</c> attribute payload (an embedded
    /// <c>&lt;CharLootID&gt;&lt;baseID&gt;...&lt;/baseID&gt;&lt;/CharLootID&gt;</c>
    /// fragment) preserved verbatim for round-trip fidelity.
    /// </summary>
    public string? AugmentXml { get; init; }

    /// <summary>
    /// True when the source loot row carried <c>_AlternateSlot="1"</c> —
    /// the item is currently equipped in its rules-data
    /// <c>_AlternateSlot</c> field (e.g. Wrist Razors → Arms instead of
    /// the primary Off-hand slot). Round-tripped by the writer.
    /// </summary>
    public bool IsInAlternateSlot { get; init; }

    /// <summary>
    /// Verbatim cascaded grant subtrees keyed by the InternalId of the
    /// component they nest under (e.g. a Magic Item's blessing Class
    /// Features). Round-trip passthrough — re-emitted nested in the loot
    /// block AND surfaced as flat <c>RulesElementTally</c> rows by the exporter.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<XElement>> CascadedGrantsByComponentId { get; init; }
        = new Dictionary<string, IReadOnlyList<XElement>>(StringComparer.Ordinal);

    /// <summary>
    /// Per-component worn-state Category placement, keyed by the
    /// component's InternalId. OCB attaches the generic worn category
    /// (e.g. <c>WearingRod</c>, <c>WearingPactBlade</c>) under each
    /// component in a composite — typically both the base implement
    /// AND the enchanting Magic Item — and the placement is not
    /// uniform across composites. Preserving the source layout per
    /// component is the only way to round-trip these blocks faithfully.
    /// <see cref="WornCategoryId"/> remains the canonical
    /// "slot-level" worn category (used by PowerStatsBuilder gates);
    /// this map exists solely for serialization fidelity.
    /// </summary>
    public IReadOnlyDictionary<string, string> WornCategoryIdByComponentId { get; init; }
        = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Iterate every <see cref="RulesElement"/> this composite contributes to the
    /// engine's element tree (Base, then Enchantment, then Augment when present).
    /// </summary>
    public IEnumerable<RulesElement> Components()
    {
        yield return Base;
        if (Enchantment is not null) yield return Enchantment;
        if (Augment is not null) yield return Augment;
    }

    /// <summary>Composite identity used for inventory dedup and lookups.</summary>
    public string CompositeKey =>
        string.Join('|',
            Base.InternalId ?? Base.Name,
            Enchantment?.InternalId ?? Enchantment?.Name ?? "",
            Augment?.InternalId ?? Augment?.Name ?? "");
}

/// <summary>
/// An item in the character's non-slot inventory (gear, consumables, mundane items).
/// </summary>
public sealed class InventoryItem
{
    public required LootItem Item { get; init; }
    public int Quantity { get; set; } = 1;
}

public enum HouseruleGrantKind
{
    RulesElement,
    Inventory,
    Equipment
}

/// <summary>
/// A houserule added through this application's explicit houserule workflow.
/// Imported OCB <c>UserEdit</c> blocks remain in the raw overlay; this model is
/// for newly-created grants that need generated UserEdit / loot output.
/// </summary>
public sealed record HouseruleGrant(
    RulesElement Element,
    int AtLevel,
    HouseruleGrantKind Kind,
    string? Slot = null,
    int Quantity = 1);

/// <summary>
/// A houserule UserEdit pick stored on the session for replay during
/// <c>RebuildFromHistory</c>. Mirrors the InternalRule chain stored under
/// OCB's per-level CharLevel[5] UserEdit slot.
/// </summary>
internal sealed record UserEditPick(RulesElement Element, int AtLevel, string? SlotOwnerInternalId);

internal sealed record SlotOwnedSupplement(RulesElement Element, int AtLevel, string? SlotOwnerInternalId);
