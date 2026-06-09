namespace CharM.Serialization;

using System.Xml.Linq;
using CharM.Engine.Creation;

/// <summary>
/// Parsed snapshot of a .dnd4e character file.
/// Contains the pre-computed expected values we compare our engine's output against.
/// </summary>
public sealed class CharacterSnapshot
{
    public string? Name { get; set; }
    public int Level { get; set; }
    public Dictionary<string, string> Details { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Build choices per level. Key = level number, Value = list of (internalId, name, type).</summary>
    public Dictionary<int, List<BuildChoice>> BuildChoices { get; } = [];

    /// <summary>Pre-computed stat values with full contribution chains.</summary>
    public Dictionary<string, ExpectedStat> Stats { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Flat list of all active element internal IDs.</summary>
    public List<TallyElement> ElementTally { get; } = [];

    /// <summary>Equipment entries.</summary>
    public List<LootEntry> Equipment { get; } = [];

    /// <summary>Pre-computed power stats.</summary>
    public List<ExpectedPowerStat> PowerStats { get; } = [];

    /// <summary>Base ability scores (before racial modifiers).</summary>
    public Dictionary<string, int> BaseAbilityScores { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Root-level <c>&lt;textstring&gt;</c> values — character fluff we don't
    /// otherwise interpret (Player Name, Carried/Stored Money, Residuum, per-level
    /// money trackers, Notes, etc.). Preserved verbatim for round-trip fidelity.
    /// </summary>
    public Dictionary<string, string> TextStrings { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Per-level structured Level subtree, preserving the document order, parent
    /// context, empty placeholders, and <c>replaces</c> annotations. This is the
    /// authoritative input for positional-alignment import — the replay engine
    /// walks each level's tree in lockstep with the wizard's slot iteration, so
    /// every element ends up in the slot it occupies in the source file.
    ///
    /// The flat <see cref="BuildChoices"/> list above remains for back-compat
    /// with the validator (which just iterates elements regardless of position).
    /// </summary>
    public List<ImportedLevel> LevelTrees { get; } = [];

    /// <summary>
    /// Per-element pass-through metadata captured from the source file
    /// (<c>url</c>, <c>charelem</c>, child <c>&lt;specific&gt;</c> blocks,
    /// retraining <c>replaces</c> hints). Keyed by <c>internal-id</c>. Lets
    /// the exporter re-emit attributes/children we don't ourselves model so
    /// round-trips don't lose data we never interpreted.
    /// </summary>
    public Dictionary<string, CharM.Engine.Creation.ElementSourceMetadata> SourceMetadata { get; }
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Verbatim copies of root- and CharacterSheet-level sections we don't
    /// model: <c>D20CampaignSetting</c>, <c>Grabbag</c>, <c>Companions</c>,
    /// <c>Journal</c>, <c>PowerStats</c>. Keyed by element local-name. We
    /// re-emit these as-is so unchanged characters round-trip cleanly even
    /// while the structured models for these sections are still TBD.
    /// </summary>
    public Dictionary<string, System.Xml.Linq.XElement> RawSections { get; }
        = new(StringComparer.Ordinal);

    /// <summary>
    /// Free grants from the source file's <c>&lt;Grabbag&gt;&lt;rules&gt;</c>
    /// block — the OCB "campaign settings" container used to inject elements
    /// like Inherent Bonuses, Spellscarred, House Vadalis, etc. that are not
    /// part of the normal Level subtree. Each entry is the element's
    /// internal-id + display name + type as recorded in the source, to be
    /// resolved against the rules database and applied to the session as
    /// detached root-level grants on import.
    /// </summary>
    public List<TallyElement> GrabbagGrants { get; } = [];

    /// <summary>
    /// Captured houserule overlay (Forms A/B/C/D — see
    /// <see cref="HouseruleOverlay"/>). Populated by <see cref="Dnd4eReader"/>
    /// when the source file contains UserEdit blocks, legacy inline tally
    /// houserule rows, or campaign houserule definitions. Carried through to
    /// <see cref="CharM.Engine.Creation.CharacterSession"/> so the exporter can
    /// re-emit the picks and cascade <c>legality</c> attributes.
    /// </summary>
    public HouseruleOverlay Houserules { get; set; } = new();
}

public sealed record BuildChoice(
    string? InternalId,
    string Name,
    string Type,
    string? ParentInternalId = null,
    bool IsGranted = false);

/// <summary>
/// A single level's <c>&lt;Level&gt;&lt;RulesElement type="Level"…&gt;</c>
/// subtree, preserved verbatim for positional-alignment import.
/// </summary>
public sealed class ImportedLevel
{
    public required int Level { get; init; }
    public required ImportedRulesElement Root { get; init; }

    /// <summary>
    /// Direct <c>&lt;loot&gt;</c> entries stored under this level in the source
    /// file. These form OCB's acquisition journal and duplicate rows from
    /// <c>&lt;LootTally&gt;</c>; preserve them separately so the rebuilt
    /// Level tree does not lose the source's per-level loot history.
    /// </summary>
    public List<XElement> SourceLoot { get; } = [];
}

/// <summary>
/// A node in the imported Level subtree. Mirrors the shape of a
/// <c>&lt;RulesElement&gt;</c> in the .dnd4e file. Empty placeholders
/// (<c>name=""</c> and <c>type=""</c>) are preserved as
/// <see cref="IsEmptyPlaceholder"/> nodes — they encode "user deliberately
/// left this slot unfilled" and the importer must call SkipSlot for them.
/// </summary>
public sealed class ImportedRulesElement
{
    public string? InternalId { get; init; }
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    /// <summary>charelem of the element this one replaces (retraining metadata).</summary>
    public string? Replaces { get; init; }
    /// <summary>Original charelem from the file (opaque; OCB regenerates on load).</summary>
    public string? Charelem { get; init; }
    /// <summary>
    /// Raw <c>legality</c> attribute from the source <c>&lt;RulesElement&gt;</c>
    /// (typically <c>"rules-legal"</c> or <c>"houserule"</c>). Carried through
    /// so the importer can flag a node as houseruled and the exporter can
    /// re-emit per-element legality without rediscovering it from the raw XML.
    /// </summary>
    public string? Legality { get; init; }
    /// <summary>
    /// Verbatim source <c>&lt;RulesElement&gt;</c> XElement preserved for
    /// passthrough emission when the importer can't resolve this node against
    /// the rules database (out-of-DB content, houseruled additions, etc.).
    /// The exporter writes this back into the level tree at the same nesting
    /// position so round-trip parity holds for character files that depend
    /// on .part files / houserules the local rules DB doesn't contain.
    /// </summary>
    public System.Xml.Linq.XElement? SourceElement { get; init; }
    public List<ImportedRulesElement> Children { get; } = [];

    public bool IsEmptyPlaceholder
        => string.IsNullOrEmpty(Name) && string.IsNullOrEmpty(Type);
}

public sealed class ExpectedStat
{
    public required string Name { get; init; }
    public required int Value { get; init; }
    public List<string> Aliases { get; } = [];
    public List<ExpectedStatAdd> Contributions { get; } = [];
}

public sealed record ExpectedStatAdd
{
    public int Value { get; init; }
    public string? Type { get; init; }
    public int? Level { get; init; }
    public string? CharElem { get; init; }
    public string? StatLink { get; init; }
    public bool AbilMod { get; init; }
    public string? Requires { get; init; }
    public string? Wearing { get; init; }
    public string? NotWearing { get; init; }
    public string? Condition { get; init; }
}

/// <summary>
/// One element on the flat tally row. <see cref="Specifics"/> holds named
/// <c>&lt;specific&gt;</c> children (first-wins by name). <see cref="ExtraSpecifics"/>
/// is for the rare case where OCB emits the SAME specific name multiple times on
/// a tally row — currently only seen on Heroes-of-the-Feywild races (Hamadryad,
/// Satyr) whose rules XML carries two <c>&lt;specific name="Short Description"&gt;</c>
/// entries (a long Physical-Qualities-derived blurb plus a short summary).
/// The writer emits <see cref="Specifics"/> first then <see cref="ExtraSpecifics"/>
/// in order, so callers control both presence and order.
/// </summary>
public sealed record TallyElement(
    string? InternalId,
    string Name,
    string Type,
    Dictionary<string, string>? Specifics = null,
    string? Url = null,
    string? Replaces = null,
    IReadOnlyList<KeyValuePair<string, string>>? ExtraSpecifics = null,
    string? Charelem = null,
    int? AcquisitionLevel = null);

/// <summary>
/// One physical component of a composite loot entry: a TallyElement plus
/// optional "worn" state captured as a nested Category RulesElement under the
/// base in the source XML (e.g. <c>WearingOffHandLightBlade</c>).
/// </summary>
public sealed record LootComponent(TallyElement Element, string? WornCategoryId = null)
{
    /// <summary>
    /// Verbatim &lt;RulesElement&gt; subtrees nested under this component in the
    /// source file (excluding the worn-state Category, which is captured
    /// separately on <see cref="WornCategoryId"/>). These are typically
    /// engine-granted Class Features / Powers / Racial Traits cascading from
    /// the component's select directives (e.g. Harper Pin's blessing CFs).
    /// Preserved opaquely so we can re-emit nested under the same component
    /// AND surface flat tally rows for round-trip fidelity.
    /// </summary>
    public List<XElement> CascadedGrants { get; init; } = [];
}

public sealed record LootEntry
{
    /// <summary>Ordered components: base first, then enchantment, then augment.</summary>
    public List<LootComponent> Components { get; } = [];

    /// <summary>Number of this item the character owns. Mirrors <c>&lt;loot count="..."&gt;</c>.</summary>
    public int Count { get; init; } = 1;

    /// <summary>Number of this item equipped (0 = in inventory, 1+ = equipped).</summary>
    public int EquipCount { get; init; }

    /// <summary>Mirrors the <c>ShowPowerCard</c> XML attribute (defaults to true).</summary>
    public bool ShowPowerCard { get; init; } = true;

    /// <summary>Composite display name (the <c>&lt;loot name="..."&gt;</c> attribute).</summary>
    public string? CompositeName { get; init; }

    /// <summary>Optional loot-level weapon damage override from the <c>Damage</c> XML attribute.</summary>
    public string? DamageOverride { get; init; }

    /// <summary>Optional cached weight (only present in source files for augmented items).</summary>
    public double? Weight { get; init; }

    /// <summary>Raw <c>augment="..."</c> attribute payload preserved verbatim.</summary>
    public string? AugmentXml { get; init; }

    /// <summary>
    /// True when the source emitted <c>_AlternateSlot="1"</c> on the
    /// loot row, signalling that this item is currently equipped in
    /// its rules-data <c>_AlternateSlot</c> field (e.g. Wrist Razors:
    /// primary slot Off-hand, alternate slot Arms — when this flag is
    /// set, the item occupies the Arms slot rather than Off-hand).
    /// Mirrors OCB's per-loot _AlternateSlot bit appended to the slot
    /// name in <c>CharLootItemSlot</c> at <c>D20Workspace.cs:3187-3190</c>.
    /// </summary>
    public bool IsInAlternateSlot { get; init; }

    /// <summary>
    /// Back-compat shim: prior code expected a flat <c>Items</c> list of
    /// <see cref="TallyElement"/>. Returns the components' Element fields.
    /// </summary>
    public IReadOnlyList<TallyElement> Items
        => Components.Select(c => c.Element).ToList();
}

public sealed record ExpectedPowerStat
{
    public required string Name { get; init; }
}
