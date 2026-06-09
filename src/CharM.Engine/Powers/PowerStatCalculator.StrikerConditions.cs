using System.Text;
using System.Text.RegularExpressions;
using CharM.Engine.Evaluation;
using CharM.Engine.Rules;

namespace CharM.Engine.Powers;

public static partial class PowerStatCalculator
{
    private static void AddStrikerConditions(
        StatBlock stats,
        RulesElement power,
        RulesElement? weapon,
        Func<string, string?>? sourceNameResolver,
        Func<string, RulesElement?>? sourceElementResolver,
        Action<string> addLine)
    {
        if (sourceNameResolver is null) return;

        // Striker damage boost features trigger on hitting a marked
        // foe regardless of who wields the weapon — the rider applies to
        // any attack against the quarry / cursed target, including a beast
        // companion's bite or claw. OCB emits these on every weapon block
        // of a beast power.
        AddStrikerCondition(
            stats,
            power,
            sourceNameResolver,
            sourceElementResolver,
            addLine,
            normalFeatureId: "ID_FMP_CLASS_FEATURE_602",
            hybridFeatureId: "ID_FMP_CLASS_FEATURE_1530",
            classId: "ID_FMP_CLASS_5",
            className: "Ranger",
            featureName: "Hunter's Quarry",
            defaultUsage: "round");

        AddStrikerCondition(
            stats,
            power,
            sourceNameResolver,
            sourceElementResolver,
            addLine,
            normalFeatureId: "ID_FMP_CLASS_FEATURE_605",
            hybridFeatureId: "ID_FMP_CLASS_FEATURE_1533",
            classId: "ID_FMP_CLASS_7",
            className: "Warlock",
            featureName: "Warlock's Curse",
            defaultUsage: "round");

        // Sneak Attack requires the CHARACTER to attack with a light blade,
        // hand crossbow, shortbow, or sling. Beast attacks (Beast Melee
        // Basic Attack, Synchronized Strike, Companion: X) are made by the
        // beast itself — even if the listed weapon is a light blade, the
        // beast can't claim the character's Sneak Attack rider. OCB
        // suppresses Sneak Attack on beast attacks; we mirror by checking
        // the power's attack ability for "Beast's attack bonus".
        string? attackAbility = PowerFieldParser.GetAttackAbility(power);
        bool isBeastAttack = !string.IsNullOrEmpty(attackAbility)
            && attackAbility.Contains("Beast", StringComparison.OrdinalIgnoreCase)
            && attackAbility.Contains("attack", StringComparison.OrdinalIgnoreCase);

        bool normalSneakApplies = IsActive(sourceNameResolver, "ID_FMP_CLASS_FEATURE_322");
        bool hybridSneakApplies = IsActive(sourceNameResolver, "ID_FMP_CLASS_FEATURE_1531")
                && (IsClassPower(power, "ID_FMP_CLASS_6")
                    || IsActiveParagonPathPower(power, sourceNameResolver, sourceElementResolver, "Rogue"));
        if (!isBeastAttack
            && (normalSneakApplies || hybridSneakApplies)
            && PowerFieldParser.IsWeaponPower(power)
            && WeaponQualifiesForSneakAttack(stats, weapon))
        {
            string usage = normalSneakApplies
                ? GetFeatureUsage(sourceElementResolver, "ID_FMP_CLASS_FEATURE_322", "turn")
                : GetFeatureUsage(sourceElementResolver, "ID_FMP_CLASS_FEATURE_1531", "turn");
            AddStrikerLine(stats, addLine, "Sneak Attack", usage);
        }
    }

    private static void AddStrikerCondition(
        StatBlock stats,
        RulesElement power,
        Func<string, string?> sourceNameResolver,
        Func<string, RulesElement?>? sourceElementResolver,
        Action<string> addLine,
        string normalFeatureId,
        string hybridFeatureId,
        string classId,
        string className,
        string featureName,
        string defaultUsage)
    {
        bool normalApplies = IsActive(sourceNameResolver, normalFeatureId);
        bool hybridApplies = IsActive(sourceNameResolver, hybridFeatureId)
                && (IsClassPower(power, classId)
                    || IsActiveParagonPathPower(power, sourceNameResolver, sourceElementResolver, className));
        if (!normalApplies && !hybridApplies) return;

        string usage = normalApplies
            ? GetFeatureUsage(sourceElementResolver, normalFeatureId, defaultUsage)
            : GetFeatureUsage(sourceElementResolver, hybridFeatureId, defaultUsage);
        AddStrikerLine(stats, addLine, featureName, usage);
    }

    private static string GetFeatureUsage(
        Func<string, RulesElement?>? sourceElementResolver,
        string featureId,
        string defaultUsage)
    {
        string usage = sourceElementResolver?.Invoke(featureId)?.Fields.TryGetValue("Usage", out string? value) == true
            && !string.IsNullOrWhiteSpace(value)
                ? value
                : defaultUsage;
        return usage.Trim().ToLowerInvariant();
    }

    private static void AddStrikerLine(StatBlock stats, Action<string> addLine, string featureName, string usage)
    {
        var diceStat = stats.TryGetStat(featureName + " Dice");
        int dice = diceStat?.ComputeValue(stats) ?? 0;
        if (dice <= 0) return;

        string die = GetStrikerDie(stats, featureName);
        int bonus = stats.TryGetStat(featureName)?.ComputeValue(stats) ?? 0;
        string damage = AppendSignedDamageBonus($"{dice}{die}", bonus);
        addLine($"+{damage} to damage once per {usage} ({featureName})");
    }

    private static string GetStrikerDie(StatBlock stats, string featureName)
    {
        var dieStat = stats.TryGetStat(featureName + " Die");
        if (dieStat is null)
            return "d6";

        foreach (var contribution in dieStat.Contributions)
        {
            if (contribution.Active && !string.IsNullOrWhiteSpace(contribution.StringPayload))
                return contribution.StringPayload.Trim();
        }

        return "d6";
    }

    private static bool IsActive(Func<string, string?> sourceNameResolver, string internalId)
        => !string.IsNullOrWhiteSpace(sourceNameResolver(internalId));

    private static bool IsClassPower(RulesElement power, string classId)
    {
        if (power.Categories.Any(c => string.Equals(c, classId, StringComparison.OrdinalIgnoreCase)))
            return true;

        if (power.Fields.TryGetValue("Class", out string? classField)
            && classField.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Any(c => string.Equals(c, classId, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static bool IsActiveParagonPathPower(
        RulesElement power,
        Func<string, string?> sourceNameResolver,
        Func<string, RulesElement?>? sourceElementResolver,
        string className)
    {
        if (power.Fields.TryGetValue("Class", out string? classField))
        {
            foreach (string token in classField.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (ParagonPathMatchesClass(token, sourceNameResolver, sourceElementResolver, className))
                {
                    return true;
                }
            }
        }

        foreach (string category in power.Categories)
        {
            if (ParagonPathMatchesClass(category, sourceNameResolver, sourceElementResolver, className))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ParagonPathMatchesClass(
        string pathId,
        Func<string, string?> sourceNameResolver,
        Func<string, RulesElement?>? sourceElementResolver,
        string className)
    {
        if (!pathId.StartsWith("ID_FMP_PARAGON_PATH_", StringComparison.OrdinalIgnoreCase)
            || !IsActive(sourceNameResolver, pathId))
        {
            return false;
        }

        var path = sourceElementResolver?.Invoke(pathId);
        if (path is null) return false;

        if (!string.IsNullOrWhiteSpace(path.Prereqs)
            && path.Prereqs.Contains(className, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.Fields.TryGetValue("print-prereqs", out string? printPrereqs)
            && printPrereqs.Contains(className, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool WeaponQualifiesForSneakAttack(StatBlock stats, RulesElement? weapon)
    {
        if (weapon is null) return false;

        if (weapon.Fields.TryGetValue("Group", out string? group))
        {
            foreach (string token in group.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.Equals("Light Blade", StringComparison.OrdinalIgnoreCase)
                    || token.Equals("Crossbow", StringComparison.OrdinalIgnoreCase)
                    || token.Equals("Sling", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        if (weapon.Name.Contains("Hand Crossbow", StringComparison.OrdinalIgnoreCase)
            || weapon.Name.Contains("Shortbow", StringComparison.OrdinalIgnoreCase)
            || weapon.Name.Contains("Sling", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var sneakAttackWeapons = stats.TryGetStat("Sneak Attack Weapons");
        if (sneakAttackWeapons is null) return false;

        foreach (var contribution in sneakAttackWeapons.Contributions)
        {
            if (!contribution.Active || string.IsNullOrWhiteSpace(contribution.StringPayload))
                continue;

            foreach (string token in contribution.StringPayload.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (WeaponMatchesGroupOrName(weapon, token))
                    return true;
            }
        }

        return false;
    }

    private static bool WeaponMatchesGroupOrName(RulesElement weapon, string token)
    {
        if (weapon.Fields.TryGetValue("Group", out string? group))
        {
            foreach (string groupToken in group.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (groupToken.Equals(token, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (token.Equals("Quarterstaff", StringComparison.OrdinalIgnoreCase)
                    && groupToken.Equals("Staff", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return weapon.Name.Equals(token, StringComparison.OrdinalIgnoreCase)
            || weapon.Name.Contains(token, StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatConditional(
        StatContribution contribution,
        int value,
        string label,
        Func<string, string?>? sourceNameResolver)
    {
        string? sourceName = !string.IsNullOrWhiteSpace(contribution.SourceElementId)
            ? sourceNameResolver?.Invoke(contribution.SourceElementId)
            : null;
        var sb = new StringBuilder();
        if (value > 0)
            sb.Append('+');
        sb.Append(value);

        if (!string.IsNullOrWhiteSpace(contribution.BonusType)
            && !string.Equals(contribution.BonusType, sourceName, StringComparison.OrdinalIgnoreCase))
        {
            sb.Append(' ').Append(FormatConditionBonusType(contribution.BonusType)).Append(" bonus");
        }

        // Preserve the source condition's trailing punctuation verbatim.
        // OCB writes the condition text as-is — e.g. Flock Effect's
        // condition is literally "against a creature you are flanking
        // instead of the normal +2 bonus." (period included), and the
        // saved file shows that period BEFORE the " - SourceName." tail.
        // TrimEnd('.') here would erase that and produce a single-period
        // form OCB never emits.
        sb.Append(" to ").Append(label).Append(' ').Append(contribution.Condition!.Trim());

        if (!string.IsNullOrWhiteSpace(sourceName))
            sb.Append(" - ").Append(sourceName);

        sb.Append('.');
        return sb.ToString();
    }

    private static string FormatConditionBonusType(string bonusType)
        => bonusType.Equals("Item", StringComparison.Ordinal) ? "item" : bonusType;

    private static string FormatStringConditional(StatContribution contribution, string label)
    {
        string payload = contribution.StringPayload!.Trim().TrimEnd('.');
        string condition = contribution.Condition!.Trim().TrimEnd('.');
        return $"{payload} to {label} {condition}.";
    }

    private static IEnumerable<string> MergeConsecutiveConditionals(List<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (i < lines.Count - 1
                && TryAnalyzeConditional(lines[i], out int leftValue, out string? leftSuffix)
                && TryAnalyzeConditional(lines[i + 1], out int rightValue, out string? rightSuffix)
                && string.Equals(leftSuffix, rightSuffix, StringComparison.Ordinal))
            {
                yield return $"+{leftValue + rightValue}{leftSuffix}";
                i++;
                continue;
            }

            yield return lines[i];
        }
    }

    private static bool TryAnalyzeConditional(string line, out int value, out string? suffix)
    {
        value = 0;
        suffix = null;
        if (line.Length == 0 || line[0] != '+')
            return false;

        int i = 1;
        while (i < line.Length && char.IsDigit(line[i]))
        {
            value = (value * 10) + line[i] - '0';
            i++;
        }

        if (i == 1)
            return false;

        suffix = line[i..];
        return true;
    }

}
