using System.Text;
using System.Text.RegularExpressions;
using CharM.Engine.Evaluation;
using CharM.Engine.Rules;

namespace CharM.Engine.Powers;

public static partial class PowerStatCalculator
{
    private static (int Total, string Components) CollectAttackBonuses(
        StatBlock stats,
        RulesElement power,
        RulesElement? weapon,
        BonusComponentAccumulator accumulator,
        Func<string, string?>? sourceNameResolver,
        Func<string, RulesElement?>? sourceElementResolver)
    {
        var seenStats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var keywordSet = BuildDamageKeywordSet(power, sourceElementResolver);
        var weaponGroups = GetWeaponGroups(weapon);
        bool hasAttack = PowerFieldParser.GetAttackAbility(power) is not null
            || PowerFieldParser.HasLeadingAttackField(power);
        bool isWeaponPower = PowerFieldParser.IsWeaponPower(power);

        void AddStat(string statName)
        {
            if (!seenStats.Add(statName)) return;
            var stat = stats.TryGetStat(statName);
            if (stat is null) return;

            foreach (var contribution in stat.Contributions)
            {
                if (!IsUnconditionalNumericContribution(contribution)) continue;

                int value = GetPowerStatContributionValue(contribution, stats);
                if (value == 0)
                {
                    if (ShouldDisplayZeroContribution(contribution))
                        accumulator.AddDisplayOnlyContribution(contribution, sourceNameResolver);
                    continue;
                }

                accumulator.AddContribution(contribution, value, sourceNameResolver);
            }
        }

        foreach (string statName in stats.AllStatNames)
        {
            if (!TryClassifyAttackBonusStat(
                    statName,
                    keywordSet,
                    weapon,
                    weaponGroups,
                    hasAttack,
                    out string matchedStatName))
            {
                continue;
            }

            AddStat(matchedStatName);
        }

        if (hasAttack)
            AddStat("attack rolls");

        if (weapon is not null && isWeaponPower)
            AddStat("weapon attack rolls");

        return (accumulator.Total, accumulator.Components);
    }

    private static HashSet<string> BuildDamageKeywordSet(
        RulesElement power,
        Func<string, RulesElement?>? sourceElementResolver)
    {
        var set = BuildConditionKeywordSet(power, sourceElementResolver);

        string? attackAbility = PowerFieldParser.GetAttackAbility(power);
        if (!string.IsNullOrWhiteSpace(attackAbility))
            AddKeywordToken(set, attackAbility);

        string? defense = PowerFieldParser.GetDefense(power);
        if (!string.IsNullOrWhiteSpace(defense))
            AddKeywordToken(set, defense);

        return set;
    }

    private static bool TryClassifyDamageBonusStat(
        string statName,
        HashSet<string> keywordSet,
        RulesElement? weapon,
        string[]? weaponGroups,
        bool hasDamageRoll,
        out string matchedStatName)
    {
        matchedStatName = statName;
        int colonIdx = statName.LastIndexOf(':');
        if (colonIdx <= 0 || colonIdx == statName.Length - 1)
            return TryClassifyGlobalDamageStat(statName, weapon, hasDamageRoll, out matchedStatName);

        string predicates = statName[..colonIdx];
        string suffix = statName[(colonIdx + 1)..];
        string? weaponLimiter = null;
        int limiterIdx = suffix.IndexOf('(');
        if (limiterIdx >= 0)
        {
            int limiterEndIdx = suffix.LastIndexOf(')');
            if (limiterEndIdx > limiterIdx)
                weaponLimiter = suffix.Substring(limiterIdx + 1, limiterEndIdx - limiterIdx - 1).Trim();
            suffix = suffix[..limiterIdx];
        }

        suffix = suffix.Trim();
        if (suffix.Equals("damage", StringComparison.OrdinalIgnoreCase))
        {
            if (!hasDamageRoll) return false;
        }
        else if (!suffix.Equals("damage roll", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(weaponLimiter) && !WeaponMatchesLimiter(weapon, weaponLimiter))
            return false;

        return ConditionalPredicatesMatch(
            predicates,
            keywordSet,
            weapon,
            weaponGroups,
            allowBareImplementPredicate: true);
    }

    private static bool TryClassifyAttackBonusStat(
        string statName,
        HashSet<string> keywordSet,
        RulesElement? weapon,
        string[]? weaponGroups,
        bool hasAttack,
        out string matchedStatName)
    {
        matchedStatName = statName;
        if (!hasAttack)
            return false;

        int colonIdx = statName.LastIndexOf(':');
        if (colonIdx <= 0 || colonIdx == statName.Length - 1)
            return TryClassifyGlobalAttackStat(statName, weapon, out matchedStatName);

        string predicates = statName[..colonIdx];
        string suffix = statName[(colonIdx + 1)..];
        string? weaponLimiter = null;
        int limiterIdx = suffix.IndexOf('(');
        if (limiterIdx >= 0)
        {
            int limiterEndIdx = suffix.LastIndexOf(')');
            if (limiterEndIdx > limiterIdx)
                weaponLimiter = suffix.Substring(limiterIdx + 1, limiterEndIdx - limiterIdx - 1).Trim();
            suffix = suffix[..limiterIdx];
        }

        suffix = suffix.Trim();
        if (!suffix.Equals("attack", StringComparison.OrdinalIgnoreCase)
            && !suffix.Equals("attack roll", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(weaponLimiter) && !WeaponMatchesLimiter(weapon, weaponLimiter))
            return false;

        return ConditionalPredicatesMatch(predicates, keywordSet, weapon, weaponGroups);
    }

    private static bool TryClassifyGlobalAttackStat(
        string statName,
        RulesElement? weapon,
        out string matchedStatName)
    {
        matchedStatName = statName;
        string baseName = statName.Trim();
        string? weaponLimiter = null;
        int limiterIdx = baseName.IndexOf('(');
        if (limiterIdx >= 0)
        {
            int limiterEndIdx = baseName.LastIndexOf(')');
            if (limiterEndIdx <= limiterIdx)
                return false;

            weaponLimiter = baseName.Substring(limiterIdx + 1, limiterEndIdx - limiterIdx - 1).Trim();
            baseName = baseName[..limiterIdx].Trim();
        }

        if (!baseName.Equals("attack rolls", StringComparison.OrdinalIgnoreCase)
            && !baseName.Equals("attack roll", StringComparison.OrdinalIgnoreCase)
            && !baseName.Equals("attack", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(weaponLimiter)
            || WeaponMatchesLimiter(weapon, weaponLimiter);
    }

    private static bool TryClassifyGlobalDamageStat(
        string statName,
        RulesElement? weapon,
        bool hasDamageRoll,
        out string matchedStatName)
    {
        matchedStatName = statName;
        if (!hasDamageRoll)
            return false;

        string baseName = statName.Trim();
        string? weaponLimiter = null;
        int limiterIdx = baseName.IndexOf('(');
        if (limiterIdx >= 0)
        {
            int limiterEndIdx = baseName.LastIndexOf(')');
            if (limiterEndIdx <= limiterIdx)
                return false;

            weaponLimiter = baseName.Substring(limiterIdx + 1, limiterEndIdx - limiterIdx - 1).Trim();
            baseName = baseName[..limiterIdx].Trim();
        }

        if (!baseName.Equals("damage rolls", StringComparison.OrdinalIgnoreCase)
            && !baseName.Equals("damage roll", StringComparison.OrdinalIgnoreCase)
            && !baseName.Equals("damage", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(weaponLimiter)
            || WeaponMatchesLimiter(weapon, weaponLimiter);
    }

    private static bool IsUnconditionalNumericContribution(StatContribution contribution)
        => contribution.Active
            && string.IsNullOrWhiteSpace(contribution.Condition)
            && contribution.StringPayload is null;

    private static bool TryGetUnconditionalDamageDicePayload(StatContribution contribution, out string dicePayload)
    {
        dicePayload = string.Empty;
        if (!contribution.Active
            || !string.IsNullOrWhiteSpace(contribution.Condition)
            || string.IsNullOrWhiteSpace(contribution.StringPayload))
        {
            return false;
        }

        string payload = contribution.StringPayload.Trim();
        if (payload.StartsWith("+", StringComparison.Ordinal))
            payload = payload[1..].TrimStart();

        if (ParseDiceString(payload) is null)
            return false;

        dicePayload = payload;
        return true;
    }

    private static int GetPowerStatContributionValue(StatContribution contribution, StatBlock stats)
    {
        int value = contribution.GetEffectiveValue(stats);
        bool isAbilityModExpression =
            contribution.Expression is ValueExpression.AbilityModifier
                                   or ValueExpression.AbilityModFunction;

        if (contribution.HalfPoint && isAbilityModExpression)
            value /= 2;

        return value;
    }

    private static bool ShouldDisplayZeroContribution(StatContribution contribution)
    {
        if (string.Equals(contribution.BonusType, "off-hand enhancement", StringComparison.OrdinalIgnoreCase))
            return true;

        // OCB always renders the Inherent Bonuses Enhancement line on power
        // cards even when the current value is zero (heroic-tier characters
        // get +1 starting at L2, +2 at L7, etc. — Lvl 1 attack rolls still
        // show "+0 Enhancement bonus - Inherent Bonuses" to make the
        // mechanism visible). The contribution comes through as a
        // StatReference resolving to 0 with BonusType="Enhancement" and
        // SourceElementId = ID_INTERNAL_INTERNAL_INHERENT_BONUSES.
        return string.Equals(contribution.BonusType, "Enhancement", StringComparison.OrdinalIgnoreCase)
            && string.Equals(contribution.SourceElementId, "ID_INTERNAL_INTERNAL_INHERENT_BONUSES", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatAbilityComponent(int value, string abilityName, double multiplier)
    {
        var sb = new StringBuilder();
        if (value >= 0)
            sb.Append('+');
        sb.Append(value).Append(' ');
        if (Math.Abs(multiplier - 2.0) < 0.0001)
            sb.Append("twice ");
        sb.Append(abilityName).Append(" modifier.\n");
        return sb.ToString();
    }

    private static string FormatFixedComponent(int value, string label)
    {
        var sb = new StringBuilder();
        if (value >= 0)
            sb.Append('+');
        sb.Append(value).Append(' ').Append(label).Append(".\n");
        return sb.ToString();
    }

    private static string FormatBonusComponent(
        StatContribution contribution,
        int value,
        Func<string, string?>? sourceNameResolver,
        bool doesntStack = false)
    {
        var sb = new StringBuilder();
        if (value >= 0)
            sb.Append('+');
        sb.Append(value).Append(' ');

        if (TryGetMinimumOneModifierAbility(contribution.Expression, out string abilityName))
            sb.Append('(').Append(abilityName).Append(" modifier) ");

        if (!string.IsNullOrWhiteSpace(contribution.BonusType))
            sb.Append(contribution.BonusType).Append(' ');

        sb.Append(value < 0 ? "penalty" : "bonus");
        if (value < 0 && TryFormatMissingRequirementReason(contribution.RequiresText, out string missingRequirementReason))
            sb.Append(' ').Append(missingRequirementReason);
        if (!string.IsNullOrWhiteSpace(contribution.SourceElementId))
        {
            string? sourceName = sourceNameResolver?.Invoke(contribution.SourceElementId);
            if (!string.IsNullOrWhiteSpace(sourceName))
                sb.Append(" - ").Append(sourceName);
        }
        if (doesntStack)
            sb.Append(" [Doesn't Stack]");
        sb.Append('\n');
        return sb.ToString();
    }

    private static bool TryGetMinimumOneModifierAbility(ValueExpression? expression, out string abilityName)
    {
        abilityName = string.Empty;
        return expression is ValueExpression.StatReference sr
            && StatEvaluator.TryParseMinOneMod(sr.StatName, out abilityName);
    }

    private static bool TryFormatMissingRequirementReason(string? requiresText, out string reason)
    {
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(requiresText))
            return false;

        string trimmed = requiresText.Trim();
        if (!trimmed.StartsWith('!') || trimmed.Length == 1)
            return false;

        string missing = trimmed[1..].Trim();
        if (missing.Length == 0 || missing.Contains(',', StringComparison.Ordinal) || missing.Contains('|', StringComparison.Ordinal))
            return false;

        reason = "because you don't have " + missing;
        return true;
    }

    private static string AddDamageBonus(string damage, int bonus)
    {
        if (bonus == 0)
            return damage;

        if (string.IsNullOrWhiteSpace(damage))
            return bonus.ToString();

        int plus = damage.LastIndexOf('+');
        int minus = damage.LastIndexOf('-');
        int signIndex = Math.Max(plus, minus);
        if (signIndex >= 0)
        {
            string tail = damage[(signIndex + 1)..].Trim();
            if (int.TryParse(tail, out int existing))
            {
                int signedExisting = damage[signIndex] == '-' ? -existing : existing;
                int combined = signedExisting + bonus;
                damage = damage[..signIndex].TrimEnd();
                return AppendSignedDamageBonus(damage, combined);
            }
        }

        return AppendSignedDamageBonus(damage.TrimEnd(), bonus);
    }

    private static string AddDamageDiceBonuses(string damage, IEnumerable<string> diceBonuses)
    {
        string result = damage.TrimEnd();
        foreach (string rawBonus in diceBonuses)
        {
            string diceBonus = rawBonus.Trim();
            if (diceBonus.Length == 0)
                continue;

            result = AddDamageDiceBonus(result, diceBonus);
        }

        return result;
    }

    private static string AddDamageDiceBonus(string damage, string diceBonus)
    {
        if (string.IsNullOrWhiteSpace(damage))
            return diceBonus;

        var match = TrailingFlatDamageBonusPattern.Match(damage);
        if (match.Success)
        {
            string prefix = match.Groups["prefix"].Value.TrimEnd();
            string flat = match.Groups["flat"].Value;
            if (prefix.Length > 0)
                return prefix + "+" + diceBonus + flat;
        }

        return damage + "+" + diceBonus;
    }

    private static string AppendSignedDamageBonus(string damage, int bonus)
    {
        if (bonus == 0)
            return damage.Length == 0 ? "0" : damage;

        string suffix = bonus > 0 ? $"+{bonus}" : bonus.ToString();
        return damage.Length == 0 ? suffix.TrimStart('+') : damage + suffix;
    }

}
