namespace CharM.Engine.CharacterModel;

/// <summary>
/// Read-only view of character state for prerequisite evaluation,
/// legality checking, and candidate filtering. Bundles all queries
/// the rules system needs into one interface so consumers don't need
/// to thread individual callbacks.
/// </summary>
public interface ICharacterState
{
    /// <summary>Check if the character has an element by name or InternalId.</summary>
    bool HasElement(string name);

    /// <summary>Check if the character has any element of the given type whose name/fields contain the category.</summary>
    bool HasElementOfTypeAndCategory(string type, string category);

    /// <summary>Get an ability score value by name (e.g., "Strength", "Str"). Returns 0 if unknown.</summary>
    int GetAbilityScore(string abilityName);

    /// <summary>The character's current total level.</summary>
    int Level { get; }
}
