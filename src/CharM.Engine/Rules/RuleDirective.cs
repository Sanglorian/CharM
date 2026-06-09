namespace CharM.Engine.Rules;

/// <summary>
/// Base type for the 9 XML rule directive types.
/// Each directive maps to an opcode (0-8) in the native engine.
/// Directives split into two phases:
///   Skeleton (opcodes 3-6): grant, drop, select, replace — build element tree
///   Full (opcodes 0-2, 7-8): stat, statadd, statalias, suggest, modify — compute values
/// </summary>
public abstract record RuleDirective
{
    /// <summary>Character level at which this directive activates. Null = always active.</summary>
    public int? Level { get; init; }

    /// <summary>
    /// Condition expression evaluated at runtime. Directive only applies if met.
    /// Syntax: element names, ! negation, | OR, &amp; AND, Type:Category checks.
    /// </summary>
    public string? Requires { get; init; }
}

// ============================================================================
// Opcode 0/1: stat / statadd — numeric stat contributions (18,659 occurrences)
// ============================================================================

/// <summary>
/// Add a numeric contribution to a named stat.
/// Core mechanic for ability scores, defenses, HP, attack/damage bonuses, resistances.
/// </summary>
public sealed record StatAddDirective : RuleDirective
{
    /// <summary>Target stat name. May contain ':' for keyword-scoped stats (e.g., "melee:attack").</summary>
    public required string Name { get; init; }

    /// <summary>Value expression: literal (+N), stat reference (+StatName), ability modifier, etc.</summary>
    public required ValueExpression Value { get; init; }

    /// <summary>
    /// Bonus type for stacking rules. Same-type bonuses don't stack (highest wins).
    /// Null = untyped (always stacks). Common types: Enhancement, Feat, item, Armor, Shield.
    /// </summary>
    public string? BonusType { get; init; }

    /// <summary>Display-only condition text (e.g., "while bloodied"). Not mechanically evaluated.</summary>
    public string? Condition { get; init; }

    /// <summary>Equipment condition: applies only when wearing matching equipment (e.g., "armor:heavy").</summary>
    public string? Wearing { get; init; }

    /// <summary>Inverse equipment condition: applies only when NOT wearing matching equipment.</summary>
    public string? NotWearing { get; init; }

    /// <summary>
    /// When true, contribution goes into the "zero" bucket — applies even when base stat is 0.
    /// Used for granting base resistances.
    /// </summary>
    public bool Zero { get; init; }

    /// <summary>
    /// When true, contribution goes into the "non-zero" bucket — applies only when stat has non-zero base.
    /// Used for enhancing existing resistances.
    /// </summary>
    public bool NonZero { get; init; }

    /// <summary>When true, half-point rounding logic is applied to this contribution.</summary>
    public bool HalfPoint { get; init; }

    /// <summary>
    /// Minimum stat threshold gate. Format: "StatName N".
    /// Contribution applies only if the named stat >= N.
    /// </summary>
    public string? StatMin { get; init; }
}

// ============================================================================
// Opcode 2: statalias — stat name aliasing (10 occurrences)
// ============================================================================

/// <summary>
/// Create an alternate name for a stat. Both names resolve to the same stat object.
/// Example: "Strength" aliased to "str", "AC" aliased to "Armor Class".
/// </summary>
public sealed record StatAliasDirective : RuleDirective
{
    /// <summary>Primary stat name.</summary>
    public required string Name { get; init; }

    /// <summary>Alternate name that resolves to the same stat.</summary>
    public required string Alias { get; init; }
}

// ============================================================================
// Opcode 3: grant — attach rules elements (16,802 occurrences)
// ============================================================================

/// <summary>
/// Add a rules element to the character as a child of the current element.
/// Grants are structural — they attach feats, powers, class features, proficiencies, etc.
/// </summary>
public sealed record GrantDirective : RuleDirective
{
    /// <summary>Internal ID of the element to grant (e.g., "ID_FMP_POWER_3570").</summary>
    public required string Name { get; init; }

    /// <summary>Type of the granted element (e.g., "Power", "Class Feature", "Proficiency").</summary>
    public required string ElementType { get; init; }
}

// ============================================================================
// Opcode 4: drop — remove elements (10 occurrences)
// ============================================================================

/// <summary>
/// Remove an element or select slot from the character.
/// Rare but critical — used when a feat or feature replaces a default grant.
/// </summary>
public sealed record DropDirective : RuleDirective
{
    /// <summary>Name of a select slot to drop. Mutually exclusive with Name/ElementType.</summary>
    public string? SelectSlot { get; init; }

    /// <summary>Internal ID of the element to drop. Used with ElementType.</summary>
    public string? Name { get; init; }

    /// <summary>Type of the element to drop.</summary>
    public string? ElementType { get; init; }
}

// ============================================================================
// Opcode 5: select — user choice slots (1,209 occurrences)
// ============================================================================

/// <summary>
/// Create a user-facing choice slot. The builder presents valid options matching
/// the category filter; the chosen element is executed as a child.
/// </summary>
public sealed record SelectDirective : RuleDirective
{
    /// <summary>Type of elements to choose from (e.g., "Power", "Feat", "Skill Training").</summary>
    public required string ElementType { get; init; }

    /// <summary>Number of selections required (typically 1).</summary>
    public int Number { get; init; } = 1;

    /// <summary>
    /// Category filter expression. Terms separated by ',' (AND), '|' (OR).
    /// May contain element IDs, keywords, or $$VARIABLES.
    /// Example: "ID_FMP_CLASS_9,daily,1" or "Strength|Wisdom"
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Stable slot identifier — sourced from the <c>name="..."</c> attribute
    /// on the <c>&lt;select&gt;</c> element. Cross-referenced by
    /// <see cref="ModifyDirective.SelectSlot"/> and other directives that
    /// need to reach into this slot's chosen element. Convention in the OCB
    /// rules data: matches the parent element's name, so each parameterizable
    /// feat/feature has a unique slot identifier (e.g. the Arcane Admixture
    /// feat's Power slot is named "Arcane Admixture").
    /// Falls back to inner text when the attribute is absent (for selects
    /// where no other directive references the slot — Dragon Breath's
    /// Racial Trait choice is structured this way).
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Display label — sourced from the <c>&lt;select&gt;</c> element's inner
    /// text. Shown by the picker UI / SummaryText emission as the prompt for
    /// the slot ("Pick a power for: Arcane Admixture Power"). Distinct from
    /// <see cref="Name"/>: the same select can have a stable internal
    /// identifier AND a different user-facing label. <c>null</c> when the
    /// element has no inner text.
    /// </summary>
    public string? DisplayLabel { get; init; }

    /// <summary>Element ID for prepare-phase modification tracking (spellbook classes).</summary>
    public string? Prepare { get; init; }

    /// <summary>Spellbook slot name for prepared caster classes.</summary>
    public string? Spellbook { get; init; }

    /// <summary>When true, user may skip this choice.</summary>
    public bool Optional { get; init; }

    /// <summary>When true, choose from elements already on the character (retraining/swapping).</summary>
    public bool Existing { get; init; }

    /// <summary>Default element ID or name pre-selected for this choice.</summary>
    public string? Default { get; init; }

    /// <summary>Directly grant a specific element instead of presenting a choice.</summary>
    public string? Grant { get; init; }
}

// ============================================================================
// Opcode 6: replace — power/feature swapping (393 occurrences)
// ============================================================================

/// <summary>
/// Allow the user to swap one power or feature for another.
/// Used for multiclass power swapping and feature replacement at higher levels.
/// </summary>
public sealed record ReplaceDirective : RuleDirective
{
    /// <summary>Name of the replacement slot (e.g., "encounter swap").</summary>
    public string? Name { get; init; }

    /// <summary>Power usage type for multiclass swapping: "At-Will", "Encounter", "Daily", "Utility".</summary>
    public string? Multiclass { get; init; }

    /// <summary>
    /// Category filter for power swap choices.
    /// Format: "$$CLASS,frequency,level" or "name:frequency,level+".
    /// </summary>
    public string? PowerSwap { get; init; }

    /// <summary>
    /// Direct power replacement specification.
    /// Format: "ReplacementName:frequency,level" or "ReplacementName:ID".
    /// </summary>
    public string? PowerReplace { get; init; }

    /// <summary>When true, user may skip this replacement.</summary>
    public bool Optional { get; init; }
}

// ============================================================================
// Opcode 7: suggest — auto-level recommendations (1,716 occurrences)
// ============================================================================

/// <summary>
/// Suggest a rules element during character building. Does not add anything —
/// only populates the suggestion list for UI presentation.
/// </summary>
public sealed record SuggestDirective : RuleDirective
{
    /// <summary>Internal ID of the suggested element.</summary>
    public required string Name { get; init; }

    /// <summary>Type of the suggested element (typically "Feat").</summary>
    public required string ElementType { get; init; }
}

// ============================================================================
// Opcode 8: modify — runtime field patching (13,793 occurrences)
// ============================================================================

/// <summary>
/// Override a field value on an existing rules element. Creates an overlay that
/// intercepts field lookups. The most complex directive — 67% are conditional.
/// Can alter power text, weapon properties, ability scores, skill lists, etc.
/// </summary>
public sealed record ModifyDirective : RuleDirective
{
    /// <summary>Name of the field to override (e.g., "Properties", "Power Usage", "Attack").</summary>
    public required string Field { get; init; }

    /// <summary>Internal ID or display name of the target element. Mutually exclusive with SelectSlot.</summary>
    public string? Name { get; init; }

    /// <summary>Type of the target element (for disambiguation).</summary>
    public string? ElementType { get; init; }

    /// <summary>New value to set for the field.</summary>
    public string? Value { get; init; }

    /// <summary>Target the element chosen in this named select slot instead of by name/type.</summary>
    public string? SelectSlot { get; init; }

    /// <summary>Append this value to a comma-separated list field instead of replacing.</summary>
    public string? ListAddition { get; init; }

    /// <summary>Equipment condition for this modification.</summary>
    public string? Wearing { get; init; }

    /// <summary>Increase the weapon's damage die by N steps (e.g., d6→d8).</summary>
    public int? DieIncrease { get; init; }
}

// ============================================================================
// textstring — character text properties (1,301 occurrences)
// ============================================================================

/// <summary>
/// Set a key-value text string on the character (size, height, weight, vision, etc.).
/// Not a numbered opcode — stored in character's text string list.
/// </summary>
public sealed record TextStringDirective : RuleDirective
{
    /// <summary>String key name (e.g., "Size", "Average Height").</summary>
    public required string Name { get; init; }

    /// <summary>String value (e.g., "Medium", "6' 2\"-6' 8\"").</summary>
    public required string Value { get; init; }

    /// <summary>
    /// Display-only condition text (e.g., "if the target is granting combat
    /// advantage to you"). Carried verbatim onto the synthesized
    /// <c>&lt;statadd value="0" conditional="..."/&gt;</c> on export so that
    /// magic items like Flameheart Totem round-trip with their conditional
    /// damage text intact.
    /// </summary>
    public string? Condition { get; init; }
}
