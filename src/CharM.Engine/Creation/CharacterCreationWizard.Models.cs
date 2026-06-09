using CharM.Engine.CharacterModel;
using CharM.Engine.Orchestration;
using CharM.Engine.Prerequisites;
using CharM.Engine.Rules;
using CharM.Engine.Selection;
using System.Runtime.CompilerServices;

namespace CharM.Engine.Creation;

/// <summary>
/// Steps in the character creation wizard, ordered by workflow progression.
/// </summary>
public enum WizardStep
{
    Race,
    Class,
    AbilityScores,
    Background,
    Skills,
    Feats,
    Powers,
    ParagonPath,   // Level ≥ 11 only
    EpicDestiny,   // Level ≥ 21 only
    Equipment,
    Details,
    Complete
}

/// <summary>
/// A single pending choice the user must make, mapped to a wizard step.
/// </summary>
public sealed record PendingChoice(
    WizardStep Step,
    string Description,
    ChoiceSlot Slot);

/// <summary>
/// Result of building a character from wizard choices.
/// </summary>
public sealed record CharacterBuildResult(
    bool Success,
    CharacterBuilder? Builder = null,
    string? Error = null);
