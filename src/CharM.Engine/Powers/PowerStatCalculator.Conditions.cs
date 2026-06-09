using System.Text;
using System.Text.RegularExpressions;
using CharM.Engine.Evaluation;
using CharM.Engine.Rules;

namespace CharM.Engine.Powers;

public static partial class PowerStatCalculator
{
    /// <summary>
    /// Collect OCB-style conditional power-card text from active statadd pieces.
    /// Conditional contributions are display-only for base stat math, but OCB
    /// walks the applicable attack/damage/info stats and serializes them under
    /// <c>&lt;Conditions&gt;</c> on each PowerStats weapon block.
    /// </summary>
    public static string CollectConditions(
        StatBlock stats,
        RulesElement power,
        RulesElement? weapon = null,
        int characterLevel = 1,
        Func<string, string?>? sourceNameResolver = null,
        Func<string, RulesElement?>? sourceElementResolver = null)
    {
        var lines = new List<string>();
        var seenLines = new HashSet<string>(StringComparer.Ordinal);
        var seenStats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var keywordSet = BuildConditionKeywordSet(power, sourceElementResolver);
        var weaponGroups = GetWeaponGroups(weapon);
        bool hasAttack = PowerFieldParser.GetAttackAbility(power) is not null
            || PowerFieldParser.HasLeadingAttackField(power);
        bool hasDirectDamage = HasDirectDamageClause(power, characterLevel);
        bool hasDamageRoll = HasDamageRoll(power, characterLevel);
        bool isWeaponPower = PowerFieldParser.IsWeaponPower(power);
        // For display-card condition gates we use the STRICT definition (actual basic
        // attack power), not the widened "usable-as-MBA via _Associated Feats" form.
        // OCB only applies basic-attack-only / opportunity-attack-only / charge-only
        // damage bonuses on the regular power card when the power is itself an MBA/RBA;
        // an encounter power that happens to be substitutable as an MBA via a feat
        // (e.g. Fury of the Sirocco + Hunting Spear Chieftain) does NOT pick up those
        // bonuses on its own card.
        bool isBasicAttack = IsActualBasicAttackPower(power);

        void AddLine(string line)
        {
            line = line.Trim();
            if (line.Length == 0) return;
            if (seenLines.Add(line))
                lines.Add(line);
        }

        void AddStat(string statName, string label, bool infoOnly = false)
        {
            string statKey = infoOnly ? statName + "\0info" : statName + "\0" + label;
            if (!seenStats.Add(statKey)) return;
            var stat = stats.TryGetStat(statName);
            if (stat is null) return;

            foreach (var contribution in stat.Contributions)
            {
                if (!contribution.Active) continue;
                if (!TierFeatureContributionApplies(contribution, sourceElementResolver, characterLevel)) continue;

                if (infoOnly)
                {
                    if (!string.IsNullOrWhiteSpace(contribution.StringPayload))
                    {
                        string payload = contribution.StringPayload.Trim();
                        AddLine(payload.EndsWith(".", StringComparison.Ordinal) ? payload : payload + ".");
                    }
                    continue;
                }

                if (string.IsNullOrWhiteSpace(contribution.Condition)) continue;
                if (IsBasicAttackOnlyCondition(contribution.Condition) && !isBasicAttack) continue;
                if (IsOpportunityAttackOnlyCondition(contribution.Condition) && !isBasicAttack) continue;
                if (IsGlobalDamageStatName(statName) && IsChargeOnlyCondition(contribution.Condition) && !isBasicAttack) continue;

                if (!string.IsNullOrWhiteSpace(contribution.StringPayload))
                {
                    AddLine(FormatStringConditional(contribution, label));
                    continue;
                }

                int value = contribution.GetEffectiveValue(stats);
                if (value == 0) continue;

                AddLine(FormatConditional(contribution, value, label, sourceNameResolver));
            }
        }

        if (hasDirectDamage)
            AddStrikerConditions(stats, power, weapon, sourceNameResolver, sourceElementResolver, AddLine);

        AddAttackLineConditions(power, AddLine);

        foreach (string statName in stats.AllStatNames)
        {
            if (!TryClassifyConditionalStat(
                    statName,
                    keywordSet,
                    weapon,
                    weaponGroups,
                    hasAttack,
                    hasDamageRoll,
                    out string label,
                    out bool infoOnly))
            {
                continue;
            }

            AddStat(statName, label, infoOnly);
        }

        return string.Join("\n", MergeConsecutiveConditionals(lines));
    }

    private static void AddAttackLineConditions(RulesElement power, Action<string> addLine)
    {
        string? attack = PowerFieldParser.GetAttackText(power);
        if (string.IsNullOrWhiteSpace(attack))
            return;

        var match = ConditionalAttackPowerModifierPattern.Match(attack);
        if (!match.Success || !int.TryParse(match.Groups["value"].Value, out int value))
            return;

        if (match.Groups["sign"].Value == "-")
            value = -value;

        string condition = match.Groups["condition"].Value.Trim();
        string signed = value >= 0 ? "+" + value.ToString() : value.ToString();
        addLine($"{signed} attack bonus {condition}.");
    }

    private static bool TierFeatureContributionApplies(
        StatContribution contribution,
        Func<string, RulesElement?>? sourceElementResolver,
        int characterLevel)
    {
        if (string.IsNullOrWhiteSpace(contribution.SourceElementId) || sourceElementResolver is null)
            return true;

        var source = sourceElementResolver(contribution.SourceElementId);
        if (source is null
            || !string.Equals(source.Type, "Class Feature", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (source.Name.Contains(" - Heroic", StringComparison.OrdinalIgnoreCase))
            return characterLevel < 11;
        if (source.Name.Contains(" - Paragon", StringComparison.OrdinalIgnoreCase))
            return characterLevel is >= 11 and < 21;
        if (source.Name.Contains(" - Epic", StringComparison.OrdinalIgnoreCase))
            return characterLevel >= 21;

        return true;
    }

    private static HashSet<string> BuildConditionKeywordSet(
        RulesElement power,
        Func<string, RulesElement?>? sourceElementResolver)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string keyword in PowerFieldParser.GetKeywords(power))
            AddKeywordToken(set, keyword);

        if (!string.IsNullOrWhiteSpace(power.InternalId))
            set.Add(power.InternalId);
        if (!string.IsNullOrWhiteSpace(power.Name))
            set.Add(power.Name);

        foreach (string category in power.Categories)
        {
            AddKeywordToken(set, category);
            AddDerivedCategoryTokens(set, category, sourceElementResolver);
        }

        if (IsActiveTwoWeaponState(sourceElementResolver))
            set.Add("two-weapon");

        if (power.Fields.TryGetValue("Display", out string? display))
            AddKeywordToken(set, display);
        if (power.Fields.TryGetValue("Power Usage", out string? powerUsage))
            AddPowerUsageToken(set, powerUsage);
        if (power.Fields.TryGetValue("Power Type", out string? powerType))
            AddKeywordToken(set, powerType);
        string? defense = PowerFieldParser.GetDefense(power);
        if (!string.IsNullOrWhiteSpace(defense))
            AddKeywordToken(set, defense);
        if (power.Fields.TryGetValue("_Associated Feats", out string? associatedFeats))
        {
            foreach (string featId in associatedFeats.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                set.Add(featId + " association");
        }

        if (power.Fields.TryGetValue("Attack Type", out string? attackType))
        {
            AddKeywordToken(set, attackType);
            if (attackType.Contains("melee", StringComparison.OrdinalIgnoreCase))
                set.Add("melee");
            if (attackType.Contains("ranged", StringComparison.OrdinalIgnoreCase))
                set.Add("ranged");
            if (attackType.Contains("area", StringComparison.OrdinalIgnoreCase))
                set.Add("area");
            if (attackType.Contains("close", StringComparison.OrdinalIgnoreCase))
                set.Add("close");
        }

        if (PowerFieldParser.IsWeaponPower(power))
            set.Add("weapon");
        if (PowerFieldParser.IsImplementPower(power))
            set.Add("implement");
        if (string.Equals(power.Name, "Melee Basic Attack", StringComparison.OrdinalIgnoreCase))
            set.Add("melee basic");
        if (string.Equals(power.Name, "Ranged Basic Attack", StringComparison.OrdinalIgnoreCase))
            set.Add("ranged basic");
        if (power.Fields.TryGetValue("_BasicAttack", out string? basicAttack))
        {
            if (basicAttack.Contains("melee", StringComparison.OrdinalIgnoreCase))
                set.Add("melee basic");
            if (basicAttack.Contains("ranged", StringComparison.OrdinalIgnoreCase))
                set.Add("ranged basic");
        }
        else if (TryGetAssociatedBasicAttackKind(power, sourceElementResolver, out string associatedBasicAttackKind))
        {
            set.Add(associatedBasicAttackKind);
        }

        return set;
    }

    private static void AddDerivedCategoryTokens(
        HashSet<string> set,
        string category,
        Func<string, RulesElement?>? sourceElementResolver)
    {
        var element = sourceElementResolver?.Invoke(category);
        if (element is null) return;

        if (element.Type.Equals("Paragon Path", StringComparison.OrdinalIgnoreCase))
        {
            foreach (string classToken in ExtractPrereqClassTokens(element.Prereqs))
                set.Add("ID_INTERNAL_CATEGORY_" + classToken.ToUpperInvariant() + "_PARAGON_PATH");
        }
    }

    private static bool IsActiveTwoWeaponState(Func<string, RulesElement?>? sourceElementResolver)
        => sourceElementResolver is not null
            && (sourceElementResolver("ID_INTERNAL_WOG_WEARING_OFF_HAND_LIGHT_BLADE") is not null
                || sourceElementResolver("ID_INTERNAL_WOG_WEARING_OFF_HAND_AXE") is not null);

    private static IEnumerable<string> ExtractPrereqClassTokens(string? prereqs)
    {
        if (string.IsNullOrWhiteSpace(prereqs))
            yield break;

        foreach (string clause in prereqs.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (string raw in OrSplitPattern.Split(clause))
            {
                string token = raw.Trim();
                if (token.Length == 0) continue;
                if (!SingleWordAlphaPattern.IsMatch(token)) continue;
                yield return token;
            }
        }
    }

    private static Regex OrSplitPattern => OrSplitRegex();
    private static Regex SingleWordAlphaPattern => SingleWordAlphaRegex();

    private static bool HasDamageRoll(RulesElement power, int characterLevel)
    {
        string? damageText = PowerFieldParser.GetDamageText(power, characterLevel);
        if (string.IsNullOrWhiteSpace(damageText))
            return false;
        if (!HasDirectDamageClause(power, characterLevel))
            return false;

        return damageText.IndexOf("[W]", StringComparison.OrdinalIgnoreCase) >= 0
            || HasDiceTerm(damageText);
    }

    private static bool HasDirectDamageClause(RulesElement power, int characterLevel)
    {
        string? damageText = PowerFieldParser.GetDamageText(power, characterLevel);
        if (string.IsNullOrWhiteSpace(damageText))
            return false;

        return !power.Fields.TryGetValue("Hit", out string? rawHit)
            || StartsWithDirectDamageClause(rawHit);
    }

    private static bool HasDiceTerm(string text)
    {
        for (int i = 1; i < text.Length - 1; i++)
        {
            if (char.ToLowerInvariant(text[i]) != 'd')
                continue;
            if (char.IsDigit(text[i - 1]) && char.IsDigit(text[i + 1]))
                return true;
        }
        return false;
    }

    private static void AddKeywordToken(HashSet<string> set, string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        foreach (string token in text.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            set.Add(token);
            if (token.Contains(' '))
            {
                foreach (string part in token.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    set.Add(part);
            }
        }
    }

    private static void AddPowerUsageToken(HashSet<string> set, string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        foreach (string token in text.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            set.Add(token);
            if (token.Contains("(Special)", StringComparison.OrdinalIgnoreCase))
                continue;

            if (token.Contains(' '))
            {
                foreach (string part in token.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    set.Add(part);
            }
        }
    }

    private static string[]? GetWeaponGroups(RulesElement? weapon)
    {
        if (weapon is null
            || !weapon.Fields.TryGetValue("Group", out string? group)
            || string.IsNullOrWhiteSpace(group))
        {
            return null;
        }

        return group.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool TryClassifyConditionalStat(
        string statName,
        HashSet<string> keywordSet,
        RulesElement? weapon,
        string[]? weaponGroups,
        bool hasAttack,
        bool hasDamage,
        out string label,
        out bool infoOnly)
    {
        label = "";
        infoOnly = false;

        int colonIdx = statName.LastIndexOf(':');
        if (colonIdx <= 0 || colonIdx == statName.Length - 1)
            return TryClassifyGlobalConditionalStat(statName, weapon, hasAttack, hasDamage, out label);

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

        if (suffix.Equals("attack", StringComparison.OrdinalIgnoreCase))
        {
            if (!hasAttack) return false;
            label = "attack rolls";
        }
        else if (suffix.Equals("damage", StringComparison.OrdinalIgnoreCase))
        {
            if (!hasDamage) return false;
            label = "damage rolls";
        }
        else if (suffix.Equals("info", StringComparison.OrdinalIgnoreCase))
        {
            infoOnly = true;
        }
        else
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(weaponLimiter) && !WeaponMatchesLimiter(weapon, weaponLimiter))
            return false;

        return ConditionalPredicatesMatch(predicates, keywordSet, weapon, weaponGroups);
    }

    private static bool TryClassifyGlobalConditionalStat(
        string statName,
        RulesElement? weapon,
        bool hasAttack,
        bool hasDamage,
        out string label)
    {
        label = "";
        string normalized = statName.Trim();
        string? weaponLimiter = null;
        int limiterIdx = normalized.IndexOf('(');
        if (limiterIdx >= 0)
        {
            int limiterEndIdx = normalized.LastIndexOf(')');
            if (limiterEndIdx <= limiterIdx)
                return false;

            weaponLimiter = normalized.Substring(limiterIdx + 1, limiterEndIdx - limiterIdx - 1).Trim();
            normalized = normalized[..limiterIdx].Trim();
        }

        if (hasAttack
            && (normalized.Equals("attack rolls", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("attack roll", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("attack", StringComparison.OrdinalIgnoreCase)))
        {
            if (!string.IsNullOrWhiteSpace(weaponLimiter) && !WeaponMatchesLimiter(weapon, weaponLimiter))
                return false;
            label = "attack rolls";
            return true;
        }

        if (hasDamage
            && (normalized.Equals("Damage", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("damage rolls", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("damage roll", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("weapon damage rolls", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("weapon damage roll", StringComparison.OrdinalIgnoreCase)))
        {
            if (!string.IsNullOrWhiteSpace(weaponLimiter) && !WeaponMatchesLimiter(weapon, weaponLimiter))
                return false;
            label = "damage rolls";
            return true;
        }

        return false;
    }

    [GeneratedRegex(@"\bor\b", RegexOptions.IgnoreCase)]
    private static partial Regex OrSplitRegex();

    [GeneratedRegex(@"^[A-Za-z]+$")]
    private static partial Regex SingleWordAlphaRegex();
}
