using System.Text;
using System.Text.RegularExpressions;
using CharM.Engine.Evaluation;
using CharM.Engine.Rules;

namespace CharM.Engine.Powers;

public static partial class PowerStatCalculator
{
    private static string ReplaceDamageAbility(string damageText, string displayAbility, bool halfDamage = false)
    {
        string replacement = halfDamage
            ? "half " + displayAbility + " modifier"
            : displayAbility + " modifier";
        return DamageAbilityModifierPattern.Replace(damageText, replacement);
    }

    private static bool TryGetAssociatedBasicAttackKind(
        RulesElement power,
        Func<string, RulesElement?>? sourceElementResolver,
        out string kind)
    {
        kind = "";
        if (sourceElementResolver is null
            || !power.Fields.TryGetValue("_Associated Feats", out string? associatedFeats)
            || string.IsNullOrWhiteSpace(associatedFeats))
        {
            return false;
        }

        foreach (string featId in associatedFeats.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var feat = sourceElementResolver(featId);
            if (feat is null
                || !feat.Fields.TryGetValue("Associated Power Info", out string? info)
                || !info.Contains("basic attack", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (power.Fields.TryGetValue("Attack Type", out string? attackType)
                && attackType.Contains("ranged", StringComparison.OrdinalIgnoreCase))
            {
                kind = "ranged basic";
            }
            else
            {
                kind = "melee basic";
            }

            return true;
        }

        return false;
    }

    private static bool IsBasicAttackOnlyCondition(string condition)
        => condition.Contains("basic attack", StringComparison.OrdinalIgnoreCase)
            && !condition.Contains("at-will attack", StringComparison.OrdinalIgnoreCase);

    private static bool IsOpportunityAttackOnlyCondition(string condition)
        => condition.Contains("opportunity attack", StringComparison.OrdinalIgnoreCase);

    private static bool IsChargeOnlyCondition(string condition)
        => condition.Contains("charge", StringComparison.OrdinalIgnoreCase);

    private static bool IsGlobalDamageStatName(string statName)
        => statName.Equals("Damage", StringComparison.OrdinalIgnoreCase)
            || statName.Equals("damage rolls", StringComparison.OrdinalIgnoreCase);

    private static string ApplyOneSizeLarger(
        string weaponDice,
        RulesElement weapon,
        Func<string, string?>? sourceNameResolver)
    {
        // We leave the helper in place — `_Loot Damage Override` is
        // still meaningful to mark equipped loot whose Damage is already
        // a Large variant — but no longer scale on the
        // ID_INTERNAL_INTERNAL_ONE_SIZE_LARGER presence.
        return weaponDice;
    }

    private static bool IsTwoHandedWeapon(RulesElement weapon)
    {
        string? hands = GetWeaponFieldPreferBase(weapon, "Hands Required");
        if (hands is not null && hands.Contains("Two-Handed", StringComparison.OrdinalIgnoreCase))
            return true;

        string? itemSlot = GetWeaponFieldPreferBase(weapon, "Item Slot");
        return itemSlot is not null
            && itemSlot.Contains("Two-Hands", StringComparison.OrdinalIgnoreCase);
    }

    private static bool WeaponHasProperty(RulesElement weapon, string property)
    {
        string? properties = GetWeaponFieldPreferBase(weapon, "Properties");
        return properties is not null
            && properties.Contains(property, StringComparison.OrdinalIgnoreCase);
    }

    private static (int Count, int Sides)? ParseDiceString(string dice)
    {
        dice = dice.Trim();
        int dIndex = dice.IndexOf('d', StringComparison.OrdinalIgnoreCase);
        if (dIndex < 0)
            return null;

        int count = dIndex == 0 ? 1 : 0;
        if ((dIndex == 0 || int.TryParse(dice[..dIndex], out count))
            && int.TryParse(dice[(dIndex + 1)..], out int sides))
        {
            return (count, sides);
        }

        return null;
    }

    /// <summary>
    /// Parse a magic-item Enhancement field. The field is free-text in the
    /// rules DB, with one of two common shapes:
    ///   "+3"                                      (legacy / pure-number)
    ///   "+3 attack rolls and damage rolls"        (most modern entries)
    ///   "3"                                       (rare)
    /// We extract the leading signed integer and ignore the descriptive tail.
    /// Returns false when no leading number is found.
    /// </summary>
    internal static bool TryParseEnhancement(string text, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var span = text.AsSpan().TrimStart();
        int sign = 1;
        if (span.Length > 0 && (span[0] == '+' || span[0] == '-'))
        {
            if (span[0] == '-') sign = -1;
            span = span[1..];
        }
        int i = 0;
        while (i < span.Length && char.IsDigit(span[i])) i++;
        if (i == 0) return false;
        if (!int.TryParse(span[..i], out int n)) return false;
        value = sign * n;
        return true;
    }

    private static string ResolveAttackAbility(StatBlock stats, string attackAbility, out int abilityScore)
    {
        // Beast companion powers use a pre-computed stat, not an ability score
        if (IsBeastAttackStat(attackAbility))
        {
            abilityScore = ComputeBeastAttackBonus(stats);
            return "Beast's attack bonus";
        }

        if (TryGetAttackAbilityWithoutPowerModifier(attackAbility, out string baseAttackAbility)
            && TryNormalizeAbilityName(baseAttackAbility, out string baseStatAbility, out string baseDisplayAbility))
        {
            abilityScore = stats.TryGetStat(baseStatAbility)?.ComputeValue(stats) ?? 0;
            return baseDisplayAbility;
        }

        var matches = FindAbilityNameMatches(attackAbility);

        if (matches.Count == 0)
        {
            if (IsHighestAbilityReference(attackAbility))
                return SelectHighestAbility(stats, out abilityScore);

            if (TryNormalizeAbilityName(attackAbility, out string statAbility, out string displayAbility))
            {
                abilityScore = stats.TryGetStat(statAbility)?.ComputeValue(stats) ?? 0;
                return displayAbility;
            }

            abilityScore = stats.TryGetStat(attackAbility)?.ComputeValue(stats) ?? 0;
            return attackAbility;
        }

        // Player choice ("X or Y vs. defense") - honor the active "Ability
        // Choice" element when one of the candidates matches. Otherwise fall
        // back to the historical "highest modifier" heuristic. Single-ability
        // texts (matches.Count == 1) skip the choice check naturally.
        if (matches.Count > 1 && stats.ChosenAbilities.Count > 0)
        {
            foreach (var match in matches)
            {
                if (!stats.ChosenAbilities.Contains(match.Name))
                    continue;

                abilityScore = stats.TryGetStat(match.Name)?.ComputeValue(stats) ?? 0;
                return match.Name;
            }
        }

        string bestAbility = matches[0].Name;
        abilityScore = stats.TryGetStat(bestAbility)?.ComputeValue(stats) ?? 0;
        int bestMod = StatEvaluator.GetAbilityMod(abilityScore);

        for (int i = 1; i < matches.Count; i++)
        {
            var match = matches[i];
            int candidateScore = stats.TryGetStat(match.Name)?.ComputeValue(stats) ?? 0;
            int candidateMod = StatEvaluator.GetAbilityMod(candidateScore);
            if (candidateMod > bestMod)
            {
                bestAbility = match.Name;
                abilityScore = candidateScore;
                bestMod = candidateMod;
            }
        }

        return bestAbility;
    }

    private static readonly (string Abbrev, string Full)[] AbilityAbbreviations =
    [
        ("Str", "Strength"),
        ("Con", "Constitution"),
        ("Dex", "Dexterity"),
        ("Int", "Intelligence"),
        ("Wis", "Wisdom"),
        ("Cha", "Charisma"),
    ];

    private static List<(string Name, int Index)> FindAbilityNameMatches(string text)
    {
        var matches = new List<(string Name, int Index)>(AbilityNameOrder.Length);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Full names first.
        foreach (var name in AbilityNameOrder)
        {
            int index = text.IndexOf(name, StringComparison.OrdinalIgnoreCase);
            if (index >= 0 && seen.Add(name))
                matches.Add((name, index));
        }

        // Abbreviations — only when the match is a whole word (next char is not a letter,
        // preventing "Str" from matching the "Str" inside "Strength").
        foreach (var (abbrev, full) in AbilityAbbreviations)
        {
            if (seen.Contains(full)) continue;
            int index = text.IndexOf(abbrev, StringComparison.OrdinalIgnoreCase);
            if (index < 0) continue;
            int after = index + abbrev.Length;
            if (after < text.Length && char.IsLetter(text[after])) continue;
            if (seen.Add(full))
                matches.Add((full, index));
        }

        matches.Sort(static (left, right) => left.Index.CompareTo(right.Index));
        return matches;
    }

    private static bool TryGetAttackAbilityWithoutPowerModifier(string attackAbility, out string baseAttackAbility)
    {
        baseAttackAbility = string.Empty;
        var match = AttackPowerModifierPattern.Match(attackAbility);
        if (!match.Success)
            return false;

        baseAttackAbility = attackAbility[..match.Groups["sign"].Index].Trim();
        return baseAttackAbility.Length > 0;
    }

    private static bool TryNormalizeAbilityName(string text, out string statAbility, out string displayAbility)
    {
        string token = text.Trim();
        if (Creation.AbilityNames.TryParse(token, out var ability))
        {
            string mappedAbility = Creation.AbilityNames.GetFullName(ability);
            statAbility = mappedAbility;
            displayAbility = AbilityNameOrder.Contains(token, StringComparer.OrdinalIgnoreCase)
                ? statAbility
                : token;
            return true;
        }

        displayAbility = string.Empty;
        statAbility = string.Empty;
        return false;
    }

    private static string GetAbilityStatName(string abilityName)
        => TryNormalizeAbilityName(abilityName, out string statAbility, out _)
            ? statAbility
            : abilityName;

    /// <summary>
    /// Pick an ability for an "X or Y modifier" damage component. Prefers
    /// the candidate matching the player's active "Ability Choice" element
    /// (warlock pact selection); falls back to the highest modifier when no
    /// candidate matches a chosen ability. When <paramref name="alternatives"/>
    /// is null/empty this returns <paramref name="primary"/> unchanged.
    /// </summary>
    private static string SelectAbility(StatBlock stats, string primary, IReadOnlyList<string>? alternatives)
    {
        if (alternatives is null || alternatives.Count == 0)
        {
            if (IsHighestAbilityReference(primary))
                return SelectHighestAbility(stats, out _);
            if (TryNormalizeAbilityName(primary, out _, out string displayAbility))
                return displayAbility;
            return primary;
        }

        var candidates = new List<string> { primary };
        candidates.AddRange(alternatives);

        if (stats.ChosenAbilities.Count > 0)
        {
            var picked = candidates.FirstOrDefault(c => stats.ChosenAbilities.Contains(GetAbilityStatName(c)));
            if (picked is not null)
                return TryNormalizeAbilityName(picked, out _, out string displayAbility)
                    ? displayAbility
                    : picked;
        }

        // Fallback: highest modifier wins (mirrors the historical attack-line
        // tie-break). Tie keeps the first candidate (the source-text order).
        string best = candidates[0];
        int bestMod = StatEvaluator.GetAbilityMod(
            stats.TryGetStat(GetAbilityStatName(best))?.ComputeValue(stats) ?? 0);
        for (int i = 1; i < candidates.Count; i++)
        {
            string cand = candidates[i];
            int candMod = StatEvaluator.GetAbilityMod(
                stats.TryGetStat(GetAbilityStatName(cand))?.ComputeValue(stats) ?? 0);
            if (candMod > bestMod)
            {
                best = cand;
                bestMod = candMod;
            }
        }
        return TryNormalizeAbilityName(best, out _, out string bestDisplayAbility)
            ? bestDisplayAbility
            : best;
    }

    private static bool IsHighestAbilityReference(string text)
    {
        string trimmed = text.Trim();
        return trimmed.IndexOf("highest ability", StringComparison.OrdinalIgnoreCase) >= 0
            || trimmed.IndexOf("primary ability", StringComparison.OrdinalIgnoreCase) >= 0
            || trimmed.Equals("ability", StringComparison.OrdinalIgnoreCase);
    }

    private static string SelectHighestAbility(StatBlock stats, out int abilityScore)
    {
        string bestAbility = AbilityNameOrder[0];
        abilityScore = stats.TryGetStat(bestAbility)?.ComputeValue(stats) ?? 0;
        int bestMod = StatEvaluator.GetAbilityMod(abilityScore);

        for (int i = 1; i < AbilityNameOrder.Length; i++)
        {
            string ability = AbilityNameOrder[i];
            int candidateScore = stats.TryGetStat(ability)?.ComputeValue(stats) ?? 0;
            int candidateMod = StatEvaluator.GetAbilityMod(candidateScore);
            if (candidateMod > bestMod)
            {
                bestAbility = ability;
                abilityScore = candidateScore;
                bestMod = candidateMod;
            }
        }

        return bestAbility;
    }

    /// <summary>
    /// Detect "Beast's attack bonus" or similar beast companion attack stat references.
    /// </summary>
    private static bool IsBeastAttackStat(string attackAbility)
        => attackAbility.Contains("Beast", StringComparison.OrdinalIgnoreCase)
        && attackAbility.Contains("attack", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Compute Beast's attack bonus from companion stats.
    /// Formula: character level + highest companion ability modifier.
    /// The companion element's "Attack Bonus" field is the base, which equals
    /// level + highest companion ability mod at level 1 in the source data.
    /// At runtime we compute: level + max(companion ability mods).
    /// </summary>
    private static int ComputeBeastAttackBonus(StatBlock stats)
    {
        int level = stats.TryGetStat("Level")?.ComputeValue(stats) ?? 1;

        // Find highest companion ability modifier
        int highestMod = int.MinValue;
        foreach (var abilityName in CompanionAbilityStats)
        {
            var stat = stats.TryGetStat(abilityName);
            if (stat is null) continue;
            int score = stat.ComputeValue(stats);
            int mod = StatEvaluator.GetAbilityMod(score);
            if (mod > highestMod) highestMod = mod;
        }

        if (highestMod == int.MinValue)
            return 0; // no companion stats present

        return level + highestMod;
    }

    /// <summary>
    /// Compute beast's ability modifier (highest companion ability modifier).
    /// Used for damage on beast powers ("beast's ability modifier damage").
    /// </summary>
    internal static int ComputeBeastAbilityModifier(StatBlock stats)
    {
        int highestMod = 0;
        foreach (var abilityName in CompanionAbilityStats)
        {
            var stat = stats.TryGetStat(abilityName);
            if (stat is null) continue;
            int score = stat.ComputeValue(stats);
            int mod = StatEvaluator.GetAbilityMod(score);
            if (mod > highestMod) highestMod = mod;
        }
        return highestMod;
    }

    private static readonly string[] CompanionAbilityStats =
    [
        "Companion.Strength", "Companion.Constitution", "Companion.Dexterity",
        "Companion.Intelligence", "Companion.Wisdom", "Companion.Charisma",
    ];
}
