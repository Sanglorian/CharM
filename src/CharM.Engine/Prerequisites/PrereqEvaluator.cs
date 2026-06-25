namespace CharM.Engine.Prerequisites;

using CharM.Engine.CharacterModel;

/// <summary>
/// Evaluates a parsed <see cref="PrereqNode"/> tree against character state.
/// </summary>
public static class PrereqEvaluator
{
    /// <summary>
    /// Evaluate a prerequisite tree against full character state.
    /// Preferred overload — handles all prereq types including "any X class".
    /// </summary>
    public static bool Evaluate(PrereqNode? node, ICharacterState state)
    {
        if (node is null)
            return true;

        return node switch
        {
            PrereqNode.HasElement has =>
                has.Negate ? !state.HasElement(has.Name) : state.HasElement(has.Name),

            PrereqNode.LevelCheck level =>
                state.Level >= level.MinLevel,

            PrereqNode.AbilityCheck ability =>
                state.GetAbilityScore(ability.AbilityName) >= ability.MinScore,

            PrereqNode.AnyClassCheck anyClass =>
                // Check for Power Source (martial, arcane, divine, primal, psionic, shadow)
                // or Role (defender, striker, leader, controller) on the character
                state.HasElementOfTypeAndCategory("Power Source", anyClass.Keyword)
                || state.HasElementOfTypeAndCategory("Role", anyClass.Keyword),

            PrereqNode.ProficiencyCheck proficiency =>
                HasRequiredProficiency(state, proficiency.Target),

            PrereqNode.HasCategory category =>
                category.Negate ? !state.HasElementInCategory(category.Category)
                                : state.HasElementInCategory(category.Category),

            PrereqNode.HasKeyword keyword =>
                keyword.Negate ? !state.HasElementWithKeyword(keyword.Keyword)
                               : state.HasElementWithKeyword(keyword.Keyword),

            PrereqNode.Compound compound =>
                compound.IsAnd
                    ? Evaluate(compound.Left, state) && Evaluate(compound.Right, state)
                    : Evaluate(compound.Left, state) || Evaluate(compound.Right, state),

            _ => true,
        };
    }

    /// <summary>
    /// Evaluate a prerequisite tree with individual callbacks (legacy overload).
    /// Does not support AnyClassCheck — use the ICharacterState overload for full support.
    /// </summary>
    public static bool Evaluate(
        PrereqNode? node,
        Func<string, bool> hasElement,
        int characterLevel = 1,
        Func<string, int>? getAbilityScore = null)
    {
        if (node is null)
            return true;

        return node switch
        {
            PrereqNode.HasElement has =>
                has.Negate ? !hasElement(has.Name) : hasElement(has.Name),

            PrereqNode.LevelCheck level =>
                characterLevel >= level.MinLevel,

            PrereqNode.AbilityCheck ability =>
                getAbilityScore is not null && getAbilityScore(ability.AbilityName) >= ability.MinScore,

            PrereqNode.AnyClassCheck anyClass =>
                // Fallback: check if element named "Martial" etc. exists (Power Source element)
                hasElement(anyClass.Keyword),

            PrereqNode.ProficiencyCheck proficiency =>
                HasRequiredProficiency(hasElement, proficiency.Target),

            PrereqNode.Compound compound =>
                compound.IsAnd
                    ? Evaluate(compound.Left, hasElement, characterLevel, getAbilityScore)
                      && Evaluate(compound.Right, hasElement, characterLevel, getAbilityScore)
                    : Evaluate(compound.Left, hasElement, characterLevel, getAbilityScore)
                      || Evaluate(compound.Right, hasElement, characterLevel, getAbilityScore),

            _ => true,
        };
    }

    private static bool HasRequiredProficiency(ICharacterState state, string target)
    {
        foreach (var candidateName in GetProficiencyCandidateNames(target))
        {
            if (state.HasElement(candidateName))
                return true;
        }

        return state.HasElementOfTypeAndCategory("Proficiency", NormalizeProficiencyTarget(target));
    }

    private static bool HasRequiredProficiency(Func<string, bool> hasElement, string target)
    {
        foreach (var candidateName in GetProficiencyCandidateNames(target))
        {
            if (hasElement(candidateName))
                return true;
        }

        return false;
    }

    private static IEnumerable<string> GetProficiencyCandidateNames(string target)
    {
        string normalized = NormalizeProficiencyTarget(target);
        if (string.IsNullOrEmpty(normalized))
            yield break;

        yield return $"Weapon Proficiency ({normalized})";
        yield return $"Implement Proficiency ({normalized})";
        yield return $"Armor Proficiency ({normalized})";
        yield return $"Shield Proficiency ({normalized})";
        yield return $"{normalized} proficiency";
    }

    private static string NormalizeProficiencyTarget(string target)
    {
        target = target.Trim();

        if (target.StartsWith("a ", StringComparison.OrdinalIgnoreCase))
            target = target[2..];
        else if (target.StartsWith("an ", StringComparison.OrdinalIgnoreCase))
            target = target[3..];
        else if (target.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
            target = target[4..];

        return target.Trim();
    }
}
