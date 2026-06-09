using CharM.Engine.CharacterModel;
using CharM.Engine.Evaluation;
using CharM.Engine.Prerequisites;
using CharM.Engine.Rules;

namespace CharM.Engine.Orchestration;

/// <summary>
/// A single build choice: identifies a rules element the player picked at a given level.
/// SlotOwnerInternalId tracks which element's select directive created the slot
/// this choice was assigned to (for correct Level tree nesting in exports).
/// AcquiredAtLevel records the character level at which the element was added —
/// used to stamp <c>&lt;statadd Level="N"&gt;</c> contributions correctly when
/// the choice arrives via the tally / grabbag supplement path (which otherwise
/// has no level context). Null for choices that flow through the per-level
/// build pipeline, where the dictionary key already supplies the level.
/// </summary>
public sealed record ElementChoice(
    string? InternalId,
    string Name,
    string Type,
    string? SlotOwnerInternalId = null,
    int? AcquiredAtLevel = null);

/// <summary>
/// A retraining swap applied at a given level: drop the element identified by
/// <paramref name="OldInternalId"/> from the character and grant the element
/// identified by <paramref name="NewInternalId"/> in its place. Models the
/// `<replace>` directive that allows feat / power / skill swapping at level-up.
/// Resolved by InternalId; falls back to name+type if the old element isn't
/// found by ID (rare — happens when the source rebuilt the script with
/// stale IDs).
/// </summary>
public sealed record ElementReplacement(
    string OldInternalId,
    string NewInternalId,
    string? NewName = null,
    string? NewType = null,
    bool PreserveOld = false,
    string? SwapOwnerInternalId = null);

internal sealed record AppliedReplacement(
    ElementReplacement Replacement,
    RulesElement? OldElement,
    RulesElement NewElement);
