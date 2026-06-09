using System.Text;
using System.Text.RegularExpressions;
using CharM.Engine.Evaluation;
using CharM.Engine.Rules;

namespace CharM.Engine.Powers;

public static partial class PowerStatCalculator
{
    private static readonly Dictionary<string, string> DamageTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Acid"] = "Acid",
        ["Cold"] = "Cold",
        ["Fire"] = "Fire",
        ["Force"] = "Force",
        ["Lightning"] = "Lightning",
        ["Necrotic"] = "Necrotic",
        ["Poison"] = "Poison",
        ["Psychic"] = "Psychic",
        ["Radiant"] = "Radiant",
        ["Thunder"] = "Thunder",
    };

    /// <summary>
    /// Collect keyword-scoped bonuses from the stat block.
    /// Looks for stats named "keyword:attack" and "keyword:damage".
    /// </summary>
    public static (int attackBonus, int damageBonus, string conditions) CollectKeywordBonuses(
        StatBlock stats,
        IEnumerable<string> powerKeywords)
    {
        int attackBonus = 0;
        int damageBonus = 0;
        var conditionsList = new List<string>();

        foreach (string keyword in powerKeywords)
        {
            // Check for keyword:attack
            var attackStat = stats.TryGetStat($"{keyword}:attack");
            if (attackStat is not null)
            {
                int val = attackStat.ComputeValue(stats);
                attackBonus += val;

                // Collect conditions from contributions
                foreach (var c in attackStat.Contributions)
                {
                    if (!string.IsNullOrEmpty(c.Condition))
                    {
                        string sign = c.Value >= 0 ? "+" : "";
                        conditionsList.Add($"{sign}{c.Value} to attack {c.Condition}");
                    }
                }
            }

            // Check for keyword:damage
            var damageStat = stats.TryGetStat($"{keyword}:damage");
            if (damageStat is not null)
            {
                damageBonus += damageStat.ComputeValue(stats);
            }

            // Check for keyword:info (informational text)
            var infoStat = stats.TryGetStat($"{keyword}:info");
            if (infoStat is not null)
            {
                foreach (var c in infoStat.Contributions)
                {
                    if (!string.IsNullOrEmpty(c.Condition))
                        conditionsList.Add(c.Condition);
                }
            }
        }

        return (attackBonus, damageBonus, string.Join("\n", conditionsList));
    }

    /// <summary>
    /// Collect bonuses from comma-joined predicate stat names like
    /// "light blade group,weapon:attack". Each comma-separated token is a
    /// predicate that must match for the bonus to apply:
    ///   - "<X> group"      ΓåÆ weapon's Group field contains X
    ///   - "arena group"    ΓåÆ weapon's Group field contains Arena
    ///   - any other token  ΓåÆ power has that keyword
    /// The trailing :attack / :damage suffix on the LAST token determines the
    /// scope; both are recognized regardless of which token carries the suffix.
    /// </summary>
    public static (int attackBonus, int damageBonus) CollectComboPredicateBonuses(
        StatBlock stats,
        RulesElement? weapon,
        IEnumerable<string> powerKeywords)
    {
        int attackBonus = 0;
        int damageBonus = 0;
        var keywordSet = new HashSet<string>(powerKeywords, StringComparer.OrdinalIgnoreCase);

        string[]? weaponGroups = null;
        if (weapon is not null
            && weapon.Fields.TryGetValue("Group", out var wgroup)
            && !string.IsNullOrEmpty(wgroup))
        {
            weaponGroups = wgroup
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        }

        foreach (string statName in stats.AllStatNames)
        {
            // Must contain a comma (otherwise CollectKeywordBonuses already handled it).
            if (statName.IndexOf(',') < 0) continue;

            bool isAttack = statName.EndsWith(":attack", StringComparison.OrdinalIgnoreCase);
            bool isDamage = statName.EndsWith(":damage", StringComparison.OrdinalIgnoreCase);
            if (!isAttack && !isDamage) continue;

            int colonIdx = statName.LastIndexOf(':');
            string predicates = statName[..colonIdx];
            var tokens = predicates.Split(',',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2) continue;

            bool allMatch = true;
            foreach (var tok in tokens)
            {
                if (tok.EndsWith(" group", StringComparison.OrdinalIgnoreCase))
                {
                    string gname = tok[..^" group".Length].Trim();
                    if (weaponGroups is null
                        || !weaponGroups.Any(g => string.Equals(g, gname, StringComparison.OrdinalIgnoreCase)))
                    {
                        allMatch = false;
                        break;
                    }
                }
                else
                {
                    if (!keywordSet.Contains(tok))
                    {
                        allMatch = false;
                        break;
                    }
                }
            }
            if (!allMatch) continue;

            var stat = stats.TryGetStat(statName);
            if (stat is null) continue;
            int val = stat.ComputeValue(stats);
            if (isAttack) attackBonus += val;
            else damageBonus += val;
        }

        return (attackBonus, damageBonus);
    }

    private static (int Total, string Components, List<string> BonusDice) CollectDamageBonuses(
        StatBlock stats,
        RulesElement power,
        RulesElement? weapon,
        int characterLevel,
        BonusComponentAccumulator accumulator,
        bool hasResolvedDamageRoll,
        Func<string, string?>? sourceNameResolver,
        Func<string, RulesElement?>? sourceElementResolver)
    {
        var seenStats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var bonusDice = new List<string>();
        var stringComponents = new StringBuilder();
        var keywordSet = BuildDamageKeywordSet(power, sourceElementResolver);
        var weaponGroups = GetWeaponGroups(weapon);
        bool hasDamage = PowerFieldParser.GetDamageText(power, characterLevel) is not null;
        bool hasDamageRoll = hasResolvedDamageRoll;
        bool isWeaponPower = PowerFieldParser.IsWeaponPower(power);

        void AddStat(string statName)
        {
            if (!seenStats.Add(statName)) return;
            var stat = stats.TryGetStat(statName);
            if (stat is null) return;

            foreach (var contribution in stat.Contributions)
            {
                if (TryGetUnconditionalDamageDicePayload(contribution, out string dicePayload))
                {
                    bonusDice.Add(dicePayload);
                    stringComponents.Append('+').Append(dicePayload).Append(" bonus damage.\n");
                    continue;
                }

                if (!IsUnconditionalNumericContribution(contribution)) continue;

                int value = GetPowerStatContributionValue(contribution, stats);
                if (value == 0)
                {
                    accumulator.AddDisplayOnlyContribution(contribution, sourceNameResolver);
                    continue;
                }

                accumulator.AddContribution(contribution, value, sourceNameResolver);
            }
        }

        foreach (string statName in stats.AllStatNames)
        {
            if (!TryClassifyDamageBonusStat(
                    statName,
                    keywordSet,
                    weapon,
                    weaponGroups,
                    hasDamageRoll,
                    out string matchedStatName))
            {
                continue;
            }

            AddStat(matchedStatName);
        }

        if (weapon is not null && isWeaponPower)
            AddStat("weapon damage rolls");

        if (hasDamage)
            AddStat("damage");

        if (hasDamageRoll)
            AddStat("damage rolls");

        return (accumulator.Total, accumulator.Components + stringComponents, bonusDice);
    }

    private static (int Total, string Components) CollectPowerFieldDamageBonuses(
        StatBlock stats,
        RulesElement power,
        RulesElement? weapon,
        Func<string, string?>? sourceNameResolver)
    {
        int total = 0;
        var components = new StringBuilder();

        foreach (var (rawFieldName, text) in power.Fields)
        {
            string fieldName = rawFieldName.Trim();
            if (fieldName.Length == 0 || string.IsNullOrWhiteSpace(text))
                continue;
            if (text.Contains("triggering attack", StringComparison.OrdinalIgnoreCase))
                continue;

            var match = fieldName.Equals("Weapon", StringComparison.OrdinalIgnoreCase)
                ? WeaponFieldExtraDamagePattern.Match(text)
                : PowerFieldExtraDamagePattern.Match(text);
            if (!match.Success)
                continue;

            string abilityName = match.Groups["ability"].Value;
            int value = GetAbilityModifier(stats, abilityName);
            if (value == 0)
                continue;

            if (fieldName.Equals("Weapon", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryMatchWeaponExtraDamage(text, weapon, out string usingText))
                    continue;

                total += value;
                components.Append(value >= 0 ? "+" : string.Empty)
                    .Append(value)
                    .Append(" for using ")
                    .Append(usingText)
                    .Append('\n');
                continue;
            }

            if (!ActiveElementNameMatches(power, stats, sourceNameResolver, fieldName)
                && !WeaponMatchesOptionFieldName(weapon, fieldName))
            {
                continue;
            }

            total += value;
            components.Append(FormatAbilityComponent(value, abilityName, 1.0));
        }

        return (total, components.ToString());
    }

    private static bool WeaponMatchesOptionFieldName(RulesElement? weapon, string fieldName)
    {
        if (weapon is null)
            return false;

        var groups = GetWeaponGroups(weapon);
        if (groups is null || groups.Length == 0)
            return false;

        foreach (string rawToken in OrSplitPattern.Split(fieldName))
        {
            foreach (string rawPart in rawToken.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                string token = rawPart.Trim();
                if (token.Length == 0)
                    continue;

                if (groups.Any(group => group.Equals(token, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
        }

        return false;
    }

    private static bool TryMatchWeaponExtraDamage(string text, RulesElement? weapon, out string usingText)
    {
        usingText = string.Empty;
        if (weapon is null)
            return false;

        var match = WeaponSpecificExtraDamagePattern.Match(text);
        if (!match.Success)
            return false;

        usingText = match.Groups["weapons"].Value.Trim();
        if (usingText.Contains("two ", StringComparison.OrdinalIgnoreCase))
            return false;

        string[]? weaponGroups = GetWeaponGroups(weapon);
        if (weaponGroups is null)
            return false;

        foreach (string token in SplitWeaponList(usingText))
        {
            if (weaponGroups.Any(group => group.Equals(token, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }

    private static IEnumerable<string> SplitWeaponList(string text)
    {
        foreach (string raw in WeaponListSplitPattern.Split(text))
        {
            string token = raw.Trim();
            if (token.StartsWith("a ", StringComparison.OrdinalIgnoreCase))
                token = token[2..].TrimStart();
            else if (token.StartsWith("an ", StringComparison.OrdinalIgnoreCase))
                token = token[3..].TrimStart();

            if (token.Length == 0) continue;
            yield return System.Globalization.CultureInfo.InvariantCulture.TextInfo
                .ToTitleCase(token.ToLowerInvariant());
        }
    }

    private static bool ActiveElementNameMatches(
        RulesElement power,
        StatBlock stats,
        Func<string, string?>? sourceNameResolver,
        string expectedName)
    {
        if (power.Fields.TryGetValue("_Active Option Fields", out string? activeOptionFields)
            && activeOptionFields.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Any(name => name.Equals(expectedName, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (sourceNameResolver is null)
            return false;

        foreach (string statName in stats.AllStatNames)
        {
            var stat = stats.TryGetStat(statName);
            if (stat is null) continue;

            foreach (var contribution in stat.Contributions)
            {
                if (!contribution.Active || string.IsNullOrWhiteSpace(contribution.SourceElementId))
                    continue;

                string? sourceName = sourceNameResolver(contribution.SourceElementId);
                if (sourceName is not null && sourceName.Equals(expectedName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private static int GetAbilityModifier(StatBlock stats, string abilityName)
    {
        var abilityStat = stats.TryGetStat(abilityName);
        int abilityScore = abilityStat?.ComputeValue(stats) ?? 0;
        return StatEvaluator.GetAbilityMod(abilityScore);
    }

    private static Regex PowerFieldExtraDamagePattern => PowerFieldExtraDamageRegex();

    private static Regex WeaponFieldExtraDamagePattern => WeaponFieldExtraDamageRegex();

    private static Regex WeaponSpecificExtraDamagePattern => WeaponSpecificExtraDamageRegex();

    private static Regex WeaponListSplitPattern => WeaponListSplitRegex();

    [GeneratedRegex(@"(?:extra\s+damage\s+equal\s+to\s+your|^you\s+gain\s+a\s+bonus\s+to\s+the\s+damage\s+roll\s+equal\s+to\s+your)\s+(?<ability>Strength|Constitution|Dexterity|Intelligence|Wisdom|Charisma)\s+modifier", RegexOptions.IgnoreCase)]
    private static partial Regex PowerFieldExtraDamageRegex();

    [GeneratedRegex(@"(?:extra\s+damage\s+equal\s+to\s+your|bonus\s+to\s+the\s+damage\s+roll\s+equal\s+to\s+your)\s+(?<ability>Strength|Constitution|Dexterity|Intelligence|Wisdom|Charisma)\s+modifier", RegexOptions.IgnoreCase)]
    private static partial Regex WeaponFieldExtraDamageRegex();

    [GeneratedRegex(@"wielding\s+(?<weapons>.+?),\s+(?:(?:the|your|primary|secondary)\s+attacks?\s+deals?\s+extra\s+damage|you\s+gain\s+a\s+bonus\s+to\s+the\s+damage\s+roll\s+equal)", RegexOptions.IgnoreCase)]
    private static partial Regex WeaponSpecificExtraDamageRegex();

    [GeneratedRegex(@"\s*,\s*|\s+or\s+", RegexOptions.IgnoreCase)]
    private static partial Regex WeaponListSplitRegex();

}
