namespace CharM.Engine.Rules;

/// <summary>
/// A named, typed game object from the rules database.
/// Represents races, classes, feats, powers, magic items, class features, etc.
/// Each element may contain rule directives that define its mechanical effects.
/// </summary>
public sealed class RulesElement
{
    /// <summary>Unique internal ID (e.g., "ID_FMP_RACE_1").</summary>
    public required string InternalId { get; init; }

    /// <summary>Display name (e.g., "Dragonborn").</summary>
    public required string Name { get; init; }

    /// <summary>Element type (e.g., "Race", "Class", "Feat", "Power").</summary>
    public required string Type { get; init; }

    /// <summary>Source book (e.g., "Player's Handbook").</summary>
    public string? Source { get; init; }

    /// <summary>
    /// Fields from &lt;specific&gt; child elements, keyed by name attribute.
    /// This is the first-wins lookup view — matches OCB's
    /// RulesElementField behavior for a single named query (the OCB engine
    /// returns the first matching specific). For elements that legitimately
    /// have duplicate keys (e.g. Ravening Thought emits two
    /// <c>&lt;specific name="Hit"&gt;</c> children, one for the primary
    /// attack and one for the secondary, and Form of the First Hunter Attack
    /// has two Requirement specifics), use <see cref="FieldEntries"/> to
    /// see all values in document order.
    /// </summary>
    public Dictionary<string, string> Fields { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// All &lt;specific&gt; children in document order, preserving duplicates.
    /// OCB keeps the full multiset around at load time and lets downstream
    /// callers select a winner per-query (first match for most lookups,
    /// position-based for primary-vs-secondary attack panes). This is the
    /// faithful storage; <see cref="Fields"/> is a derived first-wins lookup
    /// for convenience.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> FieldEntries { get; init; } = [];

    /// <summary>
    /// Raw prerequisite text from &lt;Prereqs&gt; element.
    /// Parsed at evaluation time by the prerequisite evaluator.
    /// Example: "Str 13, Cha 13; ~MULTICLASS or Unlimited Multiclass"
    /// </summary>
    public string? Prereqs { get; init; }

    /// <summary>
    /// Parsed rule directives from &lt;rules&gt; block, in document order.
    /// Executed in two phases: skeleton (grant/drop/select/replace) then full (statadd/modify/etc).
    /// </summary>
    public IReadOnlyList<RuleDirective> Rules { get; init; } = [];

    /// <summary>
    /// Category IDs from the element_categories junction table.
    /// Used by select directives to filter valid choices.
    /// Example: ["ID_INTERNAL_CATEGORY_AUGMENTABLE_AT-WILL", "ID_FMP_CLASS_362", "1"]
    /// </summary>
    public List<string> Categories { get; set; } = [];

    public override string ToString() => $"{Type}: {Name} ({InternalId})";
}
