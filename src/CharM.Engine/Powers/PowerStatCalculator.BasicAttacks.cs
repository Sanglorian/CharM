using System.Text;
using System.Text.RegularExpressions;
using CharM.Engine.Evaluation;
using CharM.Engine.Rules;

namespace CharM.Engine.Powers;

public static partial class PowerStatCalculator
{
    private static bool IsBasicAttack(RulesElement power, Func<string, RulesElement?>? sourceElementResolver)
        => IsActualBasicAttackPower(power)
            || TryGetAssociatedBasicAttackKind(power, sourceElementResolver, out _);

    // Strict "this RulesElement is itself a basic-attack power" check, with no
    // widening to _Associated Feats. Used for display-card condition gates where
    // we only want to apply basic-attack-only bonuses to actual MBAs/RBAs.
    private static bool IsActualBasicAttackPower(RulesElement power)
        => string.Equals(power.Name, "Melee Basic Attack", StringComparison.OrdinalIgnoreCase)
            || string.Equals(power.Name, "Ranged Basic Attack", StringComparison.OrdinalIgnoreCase)
            || (power.Fields.TryGetValue("_BasicAttack", out string? basicAttack)
                && !string.IsNullOrWhiteSpace(basicAttack));

    private static bool UsesStrengthForRangedBasicAttack(RulesElement power, RulesElement weapon)
        => (string.Equals(power.Name, "Ranged Basic Attack", StringComparison.OrdinalIgnoreCase)
                || string.Equals(power.InternalId, "ID_INTERNAL_POWER_RANGED_BASIC_ATTACK", StringComparison.OrdinalIgnoreCase))
            && WeaponHasProperty(weapon, "Heavy Thrown");

    private static Regex RangedBasicDexterityModifierPattern => RangedBasicDexterityModifierRegex();

    private static Regex WeaponModeVariantPattern => WeaponModeVariantRegex();

    private static Regex WeaponModeAttackVariantPattern => WeaponModeAttackVariantRegex();

    private static Regex AttackPowerModifierPattern => AttackPowerModifierRegex();

    private static Regex ConditionalAttackPowerModifierPattern => ConditionalAttackPowerModifierRegex();

    private static Regex DamageAbilityModifierPattern => DamageAbilityModifierRegex();

    private static Regex TrailingFlatDamageBonusPattern => TrailingFlatDamageBonusRegex();

    private static string CollectZeroUnselectedModeVariantAbilityComponents(
        string? rawDamageText,
        string selectedDamageText,
        StatBlock stats)
    {
        if (string.IsNullOrWhiteSpace(rawDamageText))
            return string.Empty;

        var selectedAbilities = ExtractDamageAbilityModifierNames(selectedDamageText)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var components = new StringBuilder();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string rawLine in rawDamageText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            if (!line.StartsWith("Increase damage to ", StringComparison.OrdinalIgnoreCase))
                continue;

            var match = WeaponModeVariantPattern.Match(line);
            if (!match.Success)
                continue;

            AddUnselectedZeroAbilities(match.Groups["first"].Value);
            AddUnselectedZeroAbilities(match.Groups["second"].Value);
        }

        return components.ToString();

        void AddUnselectedZeroAbilities(string text)
        {
            foreach (string abilityName in ExtractDamageAbilityModifierNames(text))
            {
                if (!string.Equals(abilityName, "Strength", StringComparison.OrdinalIgnoreCase)
                    || selectedAbilities.Contains(abilityName)
                    || !seen.Add(abilityName))
                    continue;

                if (GetAbilityModifier(stats, abilityName) == 0)
                    components.Append(FormatAbilityComponent(0, abilityName, 1.0));
            }
        }
    }

    private static IEnumerable<string> ExtractDamageAbilityModifierNames(string text)
    {
        foreach (Match match in DamageAbilityModifierPattern.Matches(text))
        {
            string token = match.Value[..match.Value.LastIndexOf("modifier", StringComparison.OrdinalIgnoreCase)].Trim();
            if (Creation.AbilityNames.TryParse(token, out var ability))
                yield return Creation.AbilityNames.GetFullName(ability);
        }
    }

    private static bool TrySelectWeaponModeVariant(string? text, RulesElement weapon, out string selected)
    {
        selected = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var match = WeaponModeVariantPattern.Match(text.Trim());
        string desiredKind = WeaponUsesRangedMode(weapon) ? "ranged" : "melee";
        if (match.Success)
        {
            string firstKind = match.Groups["firstKind"].Value;
            if (firstKind.Equals(desiredKind, StringComparison.OrdinalIgnoreCase))
                selected = match.Groups["first"].Value.Trim();
            else
                selected = match.Groups["second"].Value.Trim();

            if (match.Groups["prefix"].Success)
                selected = match.Groups["prefix"].Value + selected;
            if (match.Groups["suffix"].Success)
                selected += match.Groups["suffix"].Value;

            return selected.Length > 0;
        }

        match = WeaponModeAttackVariantPattern.Match(text.Trim());
        if (!match.Success)
            return false;

        bool useFirst = match.Groups["firstKind"].Value.Equals(desiredKind, StringComparison.OrdinalIgnoreCase);
        string ability = match.Groups[useFirst ? "first" : "second"].Value;
        string defense = match.Groups[useFirst ? "firstDefense" : "secondDefense"].Value;
        selected = ability + " vs. " + defense;
        return true;
    }

    private static RulesElement ApplyWeaponModeVariantsToHit(RulesElement power, RulesElement weapon)
    {
        if (!power.Fields.TryGetValue("Hit", out string? hit) || string.IsNullOrWhiteSpace(hit))
            return power;

        var lines = hit.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            return power;

        bool changed = false;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (!TrySelectWeaponModeVariant(line, weapon, out string selected))
                continue;

            lines[i] = selected;
            changed = true;
        }

        if (!changed)
            return power;

        var fields = new Dictionary<string, string>(power.Fields, StringComparer.OrdinalIgnoreCase)
        {
            ["Hit"] = string.Join(Environment.NewLine, lines)
        };

        return new RulesElement
        {
            InternalId = power.InternalId,
            Name = power.Name,
            Type = power.Type,
            Source = power.Source,
            Prereqs = power.Prereqs,
            Rules = power.Rules,
            Categories = [.. power.Categories],
            Fields = fields
        };
    }

    private static bool WeaponUsesRangedMode(RulesElement weapon)
    {
        string? category = GetWeaponFieldPreferBase(weapon, "Weapon Category");
        if (category is not null && category.Contains("Ranged", StringComparison.OrdinalIgnoreCase))
            return true;

        if (weapon.Fields.TryGetValue("Weapon", out string? weaponApplicability)
            && weaponApplicability.Contains("Ranged", StringComparison.OrdinalIgnoreCase))
            return true;

        string? range = GetWeaponFieldPreferBase(weapon, "Range");
        if (string.IsNullOrWhiteSpace(range) || range.Trim() == "-")
            return false;

        string? properties = GetWeaponFieldPreferBase(weapon, "Properties");
        return properties is not null
            && (properties.Contains("Light Thrown", StringComparison.OrdinalIgnoreCase)
                || properties.Contains("Heavy Thrown", StringComparison.OrdinalIgnoreCase));
    }

    private static string? ExtractAttackAbility(string attackText)
    {
        int vsIndex = attackText.IndexOf(" vs.", StringComparison.OrdinalIgnoreCase);
        if (vsIndex < 0)
            vsIndex = attackText.IndexOf(" vs ", StringComparison.OrdinalIgnoreCase);

        return vsIndex >= 0 ? attackText[..vsIndex].Trim() : null;
    }

    private static string? ExtractDefense(string attackText)
    {
        int vsIndex = attackText.IndexOf("vs.", StringComparison.OrdinalIgnoreCase);
        if (vsIndex < 0)
            vsIndex = attackText.IndexOf("vs ", StringComparison.OrdinalIgnoreCase);
        if (vsIndex < 0)
            return null;

        int start = vsIndex + 3;
        if (start < attackText.Length && attackText[start] == '.')
            start++;
        return PowerFieldParser.NormalizeDefenseText(attackText[start..]);
    }

    private static int ExtractAttackPowerModifier(string attackAbility)
    {
        var match = AttackPowerModifierPattern.Match(attackAbility);
        if (!match.Success || !int.TryParse(match.Groups["value"].Value, out int value))
            return 0;

        return match.Groups["sign"].Value == "-" ? -value : value;
    }

    private static string ApplyAttackPowerModifier(string replacementAbility, string? originalAttackAbility)
    {
        if (string.IsNullOrWhiteSpace(originalAttackAbility))
            return replacementAbility;

        int powerModifier = ExtractAttackPowerModifier(originalAttackAbility);
        if (powerModifier == 0)
            return replacementAbility;

        return powerModifier > 0
            ? $"{replacementAbility} + {powerModifier}"
            : $"{replacementAbility} - {Math.Abs(powerModifier)}";
    }

    private readonly record struct KeyAbilityOverride(string DisplayAbility, string StatAbility, bool AppliesToDamage, bool HalfDamage = false);

    private static KeyAbilityOverride? ResolveKeyAbilityOverride(
        StatBlock stats,
        RulesElement power,
        string? attackAbility,
        RulesElement? weapon,
        Func<string, string?>? sourceNameResolver,
        Func<string, RulesElement?>? sourceElementResolver)
    {
        var keywordSet = BuildConditionKeywordSet(power, sourceElementResolver);
        if (PowerIsMulticlassForCharacter(stats, power))
            keywordSet.Add("multiclass");
        var weaponGroups = GetWeaponGroups(weapon);
        KeyAbilityOverride? best = null;
        int bestSpecificity = -1;
        int bestOrder = -1;
        int order = 0;

        foreach (string statName in stats.AllStatNames)
        {
            if (!TryGetKeyAbilityPredicates(statName, out string predicates))
            {
                order++;
                continue;
            }

            int specificity = predicates.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Length;
            if (specificity < bestSpecificity
                || (specificity == bestSpecificity && order < bestOrder)
                || !ConditionalPredicatesMatch(predicates, keywordSet, weapon, weaponGroups)
                || !KeyAbilityOverrideCanReplaceAttackAbility(predicates, attackAbility)
                || !TryGetStringPayload(stats, statName, out string payload)
                || !TryParseDirectKeyAbilityPayload(stats, payload, out string displayAbility, out string statAbility, out var payloadStatAbilities, out bool halfDamage))
            {
                order++;
                continue;
            }

            if (PredicatesContainBasicAttack(predicates))
                SelectBestBasicAttackAbility(stats, attackAbility, ref displayAbility, ref statAbility);

            bool appliesToDamage = !predicates
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Any(token => token.Equals("multiclass", StringComparison.OrdinalIgnoreCase));

            // The TextStringDirective override (e.g. Instinctive Attack:
            // "Strength,Intelligence") says "use X instead of Y for the
            // attack roll AND the damage roll". If the final chosen attack
            // stat came from outside that payload — e.g. Deft Blade's Modify
            // widened the attack text to include Dexterity and SelectBest
            // picked Dex — then this override didn't actually engage, so
            // damage should fall back to the base ability (Strength for MBA),
            // matching OCB's behavior.
            if (appliesToDamage
                && payloadStatAbilities.Count > 0
                && !payloadStatAbilities.Contains(statAbility, StringComparer.OrdinalIgnoreCase))
            {
                appliesToDamage = false;
            }

            best = new KeyAbilityOverride(displayAbility, statAbility, appliesToDamage, halfDamage);
            bestSpecificity = specificity;
            bestOrder = order;
            order++;
        }

        return best;
    }

    private static bool KeyAbilityOverrideCanReplaceAttackAbility(string predicates, string? attackAbility)
    {
        // Basic-attack key-ability overrides (e.g. "melee basic:key ability"
        // from Wrath of the Crimson Legion, "ranged basic,bow group:key
        // ability" from Serene Archery) target the GENERIC basic attack
        // baselines (Strength MBA, Dexterity RBA). Class powers that have
        // _BasicAttack: Melee/Ranged but their own explicit ability
        // (Virtuous Strike "Charisma vs. AC", Ensnaring Shot Attack
        // "Wisdom vs. Reflex") still pick up "melee basic"/"ranged basic"
        // in the keyword set via BuildConditionKeywordSet, but their
        // class-specific ability must NOT be overridden. Gate the override
        // by what's already in the attack text.
        if (!PredicatesContainBasicAttack(predicates))
            return true;

        if (string.IsNullOrWhiteSpace(attackAbility))
            return true;

        return attackAbility.Contains("Strength", StringComparison.OrdinalIgnoreCase)
            || attackAbility.Contains("Dexterity", StringComparison.OrdinalIgnoreCase)
            || attackAbility.Contains("Primary ability", StringComparison.OrdinalIgnoreCase)
            || attackAbility.Contains("ability modifier", StringComparison.OrdinalIgnoreCase);
    }

    private static void SelectBestBasicAttackAbility(
        StatBlock stats,
        string? attackAbility,
        ref string displayAbility,
        ref string statAbility)
    {
        // OCB picks the BEST of all ability options that are unlocked for a
        // basic attack: the original (e.g. Strength from the unmodified MBA
        // Attack field), every "or X" option added by a feat ModifyDirective
        // (e.g. Deft Blade widens to "Strength or Dexterity"), and the
        // current TextStringDirective "<predicates>:key ability" override
        // (Instinctive Attack adds Intelligence, Wrath of the Crimson Legion
        // adds Charisma, etc.). The override's stat is already in
        // (statAbility, displayAbility) — keep its DISPLAY casing intact
        // unless a strictly better candidate exists in the attack text,
        // because OCB preserves the raw payload casing (e.g. "cha", "int")
        // for the AttackStat / HitComponents output.
        if (string.IsNullOrWhiteSpace(attackAbility))
            return;

        int overrideMod = StatEvaluator.GetAbilityMod(stats.TryGetStat(statAbility)?.ComputeValue(stats) ?? 0);
        string? bestExtraName = null;
        int bestExtraMod = overrideMod;

        foreach (var match in FindAbilityNameMatches(attackAbility))
        {
            if (string.Equals(match.Name, statAbility, StringComparison.OrdinalIgnoreCase))
                continue;
            int score = stats.TryGetStat(match.Name)?.ComputeValue(stats) ?? 0;
            int mod = StatEvaluator.GetAbilityMod(score);
            if (mod > bestExtraMod)
            {
                bestExtraMod = mod;
                bestExtraName = match.Name;
            }
        }

        if (bestExtraName is not null)
        {
            statAbility = bestExtraName;
            displayAbility = bestExtraName;
        }
    }

    private static bool PredicatesContainBasicAttack(string predicates)
        => predicates
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(token => token.Equals("melee basic", StringComparison.OrdinalIgnoreCase)
                || token.Equals("ranged basic", StringComparison.OrdinalIgnoreCase)
                || token.Equals("basic", StringComparison.OrdinalIgnoreCase));

    private static bool PowerIsMulticlassForCharacter(StatBlock stats, RulesElement power)
    {
        if (!power.Fields.TryGetValue("Class", out string? classField)
            || string.IsNullOrWhiteSpace(classField))
        {
            return false;
        }

        bool sawClassId = false;
        foreach (string classId in classField.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!classId.StartsWith("ID_FMP_CLASS_", StringComparison.OrdinalIgnoreCase))
                continue;

            sawClassId = true;
            // ClassEquivalents includes the character's primary Class, any
            // Hybrid Class, and every CountsAsClass _SupportsID (covers
            // hybrid bridging to a base-class id, and multiclass-feat dabbles
            // where the dabbled class IS still "yours" for attack-ability
            // purposes). Any match means this is not a foreign power.
            if (stats.ClassEquivalents.Contains(classId))
                return false;
        }

        return sawClassId;
    }

    private static bool TryGetKeyAbilityPredicates(string statName, out string predicates)
    {
        predicates = string.Empty;
        int colonIdx = statName.LastIndexOf(':');
        if (colonIdx <= 0 || colonIdx == statName.Length - 1)
            return false;

        string suffix = statName[(colonIdx + 1)..].Trim();
        if (!suffix.Equals("key ability", StringComparison.OrdinalIgnoreCase))
            return false;

        predicates = statName[..colonIdx].Trim();
        return predicates.Length > 0;
    }

    private static bool TryGetStringPayload(StatBlock stats, string statName, out string payload)
    {
        payload = string.Empty;
        var stat = stats.TryGetStat(statName);
        if (stat is null)
            return false;

        for (int i = stat.Contributions.Count - 1; i >= 0; i--)
        {
            var contribution = stat.Contributions[i];
            if (!contribution.Active || string.IsNullOrWhiteSpace(contribution.StringPayload))
                continue;

            payload = contribution.StringPayload.Trim();
            return true;
        }

        return false;
    }

    private static bool TryParseDirectKeyAbilityPayload(
        StatBlock stats,
        string payload,
        out string displayAbility,
        out string statAbility)
        => TryParseDirectKeyAbilityPayload(stats, payload, out displayAbility, out statAbility, out _, out _);

    /// <summary>
    /// Parse a key-ability TextStringDirective payload (e.g. "Strength,Intelligence",
    /// "cha", "DMG:Dex", "HALF-DMG:Intelligence*") and pick the highest-modifier
    /// stat among the listed options. Also returns the full payload's stat names
    /// — callers gate AppliesToDamage on whether the final chosen attack stat is
    /// in this set (Deft Blade's "or Dexterity" is in the ATTACK field, not in
    /// the key-ability payload, so damage should not switch to Dex even if Dex
    /// wins the attack roll).
    /// </summary>
    private static bool TryParseDirectKeyAbilityPayload(
        StatBlock stats,
        string payload,
        out string displayAbility,
        out string statAbility,
        out List<string> payloadStatAbilities,
        out bool halfDamage)
    {
        displayAbility = string.Empty;
        statAbility = string.Empty;
        payloadStatAbilities = [];
        halfDamage = false;

        string text = payload.Trim().TrimEnd('*', '!').Trim();
        if (text.StartsWith("HALF-DMG:", StringComparison.OrdinalIgnoreCase))
        {
            // Melee Training (Charisma/Constitution/Intelligence/Wisdom)
            // emits "HALF-DMG:<ability>*" — the substitute ability replaces
            // Strength for both attack AND damage on the MBA, but damage
            // applies as half the ability modifier (4e PHB3 p. 200).
            text = text["HALF-DMG:".Length..].Trim();
            halfDamage = true;
        }
        if (text.StartsWith("DMG:", StringComparison.OrdinalIgnoreCase))
            text = text["DMG:".Length..].Trim();

        if (!text.Contains(',', StringComparison.Ordinal) && !text.Contains(';', StringComparison.Ordinal))
        {
            if (!TryNormalizeAbilityName(text, out statAbility, out displayAbility))
                return false;
            payloadStatAbilities.Add(statAbility);
            return true;
        }

        int bestScore = int.MinValue;
        foreach (string token in text.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!TryNormalizeAbilityName(token, out string candidateStatAbility, out string candidateDisplayAbility))
                continue;

            if (!payloadStatAbilities.Contains(candidateStatAbility, StringComparer.OrdinalIgnoreCase))
                payloadStatAbilities.Add(candidateStatAbility);

            int score = stats.TryGetStat(candidateStatAbility)?.ComputeValue(stats) ?? int.MinValue;
            if (score <= bestScore)
                continue;

            statAbility = candidateStatAbility;
            displayAbility = candidateDisplayAbility;
            bestScore = score;
        }

        return statAbility.Length > 0;
    }

    [GeneratedRegex(@"\bDexterity\s+modifier\b", RegexOptions.IgnoreCase)]
    private static partial Regex RangedBasicDexterityModifierRegex();

    [GeneratedRegex(@"^(?<prefix>Increase damage to\s+)?(?<first>.+?)\s*\((?<firstKind>melee|ranged)[^)]*\)\s+or\s+(?<second>.+?)\s*\((?<secondKind>melee|ranged)[^)]*\)(?<suffix>\s+damage,\s*.*|\s+per\s+attack(?:\..*)?|\s+at\s+\d+(?:st|nd|rd|th)?\s+level\.?|,\s*.*|\.\s+.*)?\.?\s*$", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex WeaponModeVariantRegex();

    [GeneratedRegex(@"(?:\b\w+\s+)?(?<first>Strength|Constitution|Dexterity|Intelligence|Wisdom|Charisma|str|con|dex|int|wis|cha)\s+vs\.?\s+(?<firstDefense>AC|Fortitude|Reflex|Will)\s+attacks?\s+with\s+a\s+(?<firstKind>melee|ranged)\s+weapon\s+or\s+(?:\w+\s+)?(?<second>Strength|Constitution|Dexterity|Intelligence|Wisdom|Charisma|str|con|dex|int|wis|cha)\s+vs\.?\s+(?<secondDefense>AC|Fortitude|Reflex|Will)\s+attacks?\s+with\s+a\s+(?<secondKind>melee|ranged)\s+weapon", RegexOptions.IgnoreCase)]
    private static partial Regex WeaponModeAttackVariantRegex();

    [GeneratedRegex(@"\b(?:Strength|Constitution|Dexterity|Intelligence|Wisdom|Charisma|str|con|dex|int|wis|cha)\s*(?<sign>[+-])\s*(?<value>\d+)\b(?=\s*(?:vs\.?|$))", RegexOptions.IgnoreCase)]
    private static partial Regex AttackPowerModifierRegex();

    [GeneratedRegex(@"\b(?:Strength|Constitution|Dexterity|Intelligence|Wisdom|Charisma|str|con|dex|int|wis|cha)\s*(?<sign>[+-])\s*(?<value>\d+)\s+(?<condition>per\s+[^.]+?)\s+vs\.?", RegexOptions.IgnoreCase)]
    private static partial Regex ConditionalAttackPowerModifierRegex();

    [GeneratedRegex(@"\b(?:Strength|Constitution|Dexterity|Intelligence|Wisdom|Charisma|str|con|dex|int|wis|cha)\s+modifier\b", RegexOptions.IgnoreCase)]
    private static partial Regex DamageAbilityModifierRegex();

    [GeneratedRegex(@"^(?<prefix>.+?)(?<flat>[+-]\d+)$")]
    private static partial Regex TrailingFlatDamageBonusRegex();
}
