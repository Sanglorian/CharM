using CharM.Engine.Rules;

namespace CharM.Engine.CharacterModel;

/// <summary>
/// A user-facing choice slot created by a select directive.
/// The character builder presents valid options; when the user selects,
/// the chosen RulesElements are tracked. Number specifies how many
/// selections are required (193 directives in the data need Number >= 2).
/// </summary>
public sealed class ChoiceSlot
{
    /// <summary>Display name for this choice (from select's name attribute).</summary>
    public string? Name { get; init; }

    /// <summary>
    /// Display label sourced from the <c>&lt;select&gt;</c> element's
    /// inner text. Distinct from <see cref="Name"/> (which is the stable
    /// slot identifier). UI / SummaryText consumers prefer
    /// <c>DisplayLabel ?? Name</c> for the user-facing prompt.
    /// </summary>
    public string? DisplayLabel { get; init; }

    /// <summary>Type of elements to choose from.</summary>
    public required string ElementType { get; init; }

    /// <summary>Number of selections required.</summary>
    public int Number { get; init; } = 1;

    /// <summary>Category filter expression.</summary>
    public string? Category { get; init; }

    /// <summary>
    /// Original requires expression from the select directive, if any.
    /// This lets the wizard re-evaluate whether the slot should still exist
    /// after later grants or drops mutate the tree.
    /// </summary>
    public string? Requires { get; init; }

    /// <summary>Whether this choice is optional.</summary>
    public bool Optional { get; init; }

    /// <summary>Whether to choose from existing character elements.</summary>
    public bool Existing { get; init; }

    /// <summary>
    /// InternalId of the element whose select directive created this slot.
    /// Used to place user choices under the correct parent in export.
    /// </summary>
    public string? OwnerInternalId { get; init; }

    /// <summary>
    /// Stable key for the originating select directive within its owner element.
    /// Used to reconcile slot add/remove behavior when requires conditions change.
    /// </summary>
    public string? DirectiveKey { get; init; }

    /// <summary>
    /// Character level at which this slot is presented, sourced from the
    /// originating <see cref="SelectDirective.Level"/> (or
    /// <see cref="ReplaceDirective.Level"/>) when set. Authoritative for
    /// acquisition-level recording: <c>DetermineSlotLevel</c> prefers this
    /// over the slot's parent-chain ancestry, because owner elements
    /// granted at L1 frequently carry select/replace directives that
    /// present their slots at later levels (e.g. Psionic Augmentation
    /// (Hybrid) is granted at L1 but its Hybrid Encounter Power slot
    /// is presented at L7).
    /// </summary>
    public int? Level { get; init; }

    /// <summary>The selected elements (may need up to Number selections).</summary>
    public List<RulesElement> SelectedElements { get; } = [];

    /// <summary>Whether the required number of selections have been made (or the choice is optional).</summary>
    public bool IsComplete => SelectedElements.Count >= Number || Optional;

    /// <summary>How many more selections are needed.</summary>
    public int Remaining => Math.Max(0, Number - SelectedElements.Count);
}
