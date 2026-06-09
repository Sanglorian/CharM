using System.Text;
using System.Text.RegularExpressions;
using CharM.Engine.Evaluation;
using CharM.Engine.Rules;

namespace CharM.Engine.Powers;

public static partial class PowerStatCalculator
{
    private static bool WeaponMatchesLimiter(RulesElement? weapon, string limiter)
    {
        if (weapon is null) return false;
        if (weapon.Name.Equals(limiter, StringComparison.OrdinalIgnoreCase)
            || weapon.InternalId.Equals(limiter, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return WeaponFieldEquals(weapon, "_Composite Name", limiter)
            || WeaponFieldEquals(weapon, "_Enchantment Name", limiter)
            || WeaponFieldEquals(weapon, "_Enchantment InternalId", limiter)
            || WeaponFieldEquals(weapon, "_Augment Name", limiter)
            || WeaponFieldEquals(weapon, "_Augment InternalId", limiter)
            || WeaponFieldEquals(weapon, "_WeaponEquiv Name", limiter)
            || WeaponFieldEquals(weapon, "_WeaponEquiv InternalId", limiter);
    }

    private static bool WeaponFieldEquals(RulesElement weapon, string fieldName, string value)
        => weapon.Fields.TryGetValue(fieldName, out string? fieldValue)
            && fieldValue.Equals(value, StringComparison.OrdinalIgnoreCase);

    private static bool ConditionalPredicatesMatch(
        string predicates,
        HashSet<string> keywordSet,
        RulesElement? weapon,
        string[]? weaponGroups,
        bool allowBareImplementPredicate = false)
    {
        foreach (string rawToken in predicates.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            string token = rawToken.Trim();
            if (token.Length == 0) continue;

            if (token.EndsWith(" implement group", StringComparison.OrdinalIgnoreCase))
            {
                string groupName = token[..^" implement group".Length].Trim();
                if (!keywordSet.Contains("implement")
                    || weaponGroups is null
                    || !weaponGroups.Any(g => string.Equals(g, groupName, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }
                continue;
            }

            if (token.EndsWith(" group", StringComparison.OrdinalIgnoreCase))
            {
                string groupName = token[..^" group".Length].Trim();
                if (weaponGroups is null
                    || !weaponGroups.Any(g => string.Equals(g, groupName, StringComparison.OrdinalIgnoreCase)))
                    return false;
                continue;
            }

            if (IsWeaponStatePredicate(token))
            {
                if (!WeaponMatchesStatePredicate(weapon, token))
                    return false;
                continue;
            }

            if (token.Equals("weapon", StringComparison.OrdinalIgnoreCase)
                || token.Equals("implement", StringComparison.OrdinalIgnoreCase))
            {
                if (!keywordSet.Contains(token))
                    return false;
                continue;
            }

            if (WeaponMatchesImplementPredicate(weapon, token))
                continue;

            if (allowBareImplementPredicate
                && keywordSet.Contains("implement")
                && WeaponMatchesBareImplementPredicate(weapon, token))
            {
                continue;
            }

            if (WeaponMatchesKindPredicate(weapon, token))
                continue;

            if (token.Equals("ranged", StringComparison.OrdinalIgnoreCase)
                && IsFlexibleMeleeOrRangedWeaponPower(keywordSet)
                && weapon is not null)
            {
                if (!WeaponMatchesBareRangePredicate(weapon, token))
                    return false;
                continue;
            }

            if (token.Equals("melee", StringComparison.OrdinalIgnoreCase)
                && WeaponHasRangeCategory(weapon))
            {
                if (!keywordSet.Contains("melee")
                    && !IsFlexibleMeleeOrRangedWeaponPower(keywordSet))
                {
                    return false;
                }

                if (keywordSet.Contains("weapon")
                    && !WeaponMatchesBareRangePredicate(weapon, token))
                {
                    return false;
                }

                continue;
            }

            if (token.Equals("melee", StringComparison.OrdinalIgnoreCase)
                && weapon is not null
                && keywordSet.Contains("weapon")
                && !WeaponHasRangeCategory(weapon))
            {
                return false;
            }

            if (WeaponMatchesPropertyPredicate(weapon, token))
                continue;

            if (WeaponMatchesNamedPredicate(weapon, token))
                continue;

            if (!keywordSet.Contains(token))
                return false;
        }

        return true;
    }

    private static bool IsFlexibleMeleeOrRangedWeaponPower(HashSet<string> keywordSet)
        => keywordSet.Contains("Melee or Ranged weapon");

    private static bool IsWeaponStatePredicate(string token)
        => token.Equals("two-hands", StringComparison.OrdinalIgnoreCase)
            || token.Equals("one-hand", StringComparison.OrdinalIgnoreCase)
            || token.Equals("off-hand", StringComparison.OrdinalIgnoreCase)
            || token.Equals("Ki Focuses implement", StringComparison.OrdinalIgnoreCase);

    private static bool WeaponMatchesStatePredicate(RulesElement? weapon, string token)
    {
        if (weapon is null) return false;

        if (token.Equals("two-hands", StringComparison.OrdinalIgnoreCase))
        {
            string? hands = GetWeaponFieldPreferBase(weapon, "Hands Required");
            return hands is not null
                && hands.Contains("Two-Handed", StringComparison.OrdinalIgnoreCase);
        }

        if (token.Equals("one-hand", StringComparison.OrdinalIgnoreCase))
        {
            string? hands = GetWeaponFieldPreferBase(weapon, "Hands Required");
            string? itemSlot = GetWeaponFieldPreferBase(weapon, "Item Slot");
            return hands is not null
                && hands.Contains("One-Handed", StringComparison.OrdinalIgnoreCase)
                && (itemSlot is null || !itemSlot.Contains("Two-Hands", StringComparison.OrdinalIgnoreCase));
        }

        if (token.Equals("off-hand", StringComparison.OrdinalIgnoreCase))
        {
            string? properties = GetWeaponFieldPreferBase(weapon, "Properties");
            return properties is not null
                && properties.Contains("Off-Hand", StringComparison.OrdinalIgnoreCase);
        }

        if (token.Equals("Ki Focuses implement", StringComparison.OrdinalIgnoreCase))
        {
            return (weapon.Fields.TryGetValue("Item Slot", out string? slot)
                    && slot.Contains("Ki Focus", StringComparison.OrdinalIgnoreCase))
                || (weapon.Fields.TryGetValue("Magic Item Type", out string? magicItemType)
                    && magicItemType.Contains("Ki Focus", StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }

    private static string? GetWeaponFieldPreferBase(RulesElement weapon, string fieldName)
    {
        if (weapon.Fields.TryGetValue("_Base " + fieldName, out string? baseValue)
            && !string.IsNullOrWhiteSpace(baseValue))
        {
            return baseValue;
        }

        return weapon.Fields.TryGetValue(fieldName, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    private static bool WeaponMatchesNamedPredicate(RulesElement? weapon, string token)
        => weapon is not null
            && (string.Equals(weapon.InternalId, token, StringComparison.OrdinalIgnoreCase)
                || string.Equals(weapon.Name, token, StringComparison.OrdinalIgnoreCase)
                || WeaponFieldEquals(weapon, "_WeaponEquiv Name", token)
                || WeaponFieldEquals(weapon, "_WeaponEquiv InternalId", token));

    private static bool IsTrainedWithWeapon(StatBlock stats, RulesElement weapon)
    {
        if (stats.TrainedWeapons.Contains(weapon.Name))
            return true;

        return WeaponFieldValueIsTrained("_WeaponEquiv Name")
            || WeaponFieldValueIsTrained("_Composite Name")
            || WeaponFieldValueIsTrained("_Enchantment Name")
            || WeaponFieldValueIsTrained("_Augment Name")
            || WeaponFieldValueIsTrained("_Supports Name");

        bool WeaponFieldValueIsTrained(string fieldName)
            => weapon.Fields.TryGetValue(fieldName, out string? value)
                && !string.IsNullOrWhiteSpace(value)
                && stats.TrainedWeapons.Contains(value);
    }

    private static bool WeaponMatchesImplementPredicate(RulesElement? weapon, string token)
    {
        if (weapon is null || !token.EndsWith(" implement", StringComparison.OrdinalIgnoreCase))
            return false;

        string implementName = token[..^" implement".Length].Trim();
        if (implementName.Length == 0)
            return false;

        return WeaponMatchesNamedPredicate(weapon, implementName)
            || WeaponFieldContainsNormalizedToken(weapon, "Magic Item Type", implementName)
            || WeaponFieldContainsNormalizedToken(weapon, "_Base Magic Item Type", implementName)
            || WeaponFieldContainsNormalizedToken(weapon, "Group", implementName)
            || WeaponFieldContainsNormalizedToken(weapon, "_Base Group", implementName)
            || WeaponFieldContainsNormalizedToken(weapon, "Item Slot", implementName)
            || WeaponFieldContainsNormalizedToken(weapon, "_Base Item Slot", implementName);
    }

    private static bool WeaponMatchesBareImplementPredicate(RulesElement? weapon, string implementName)
    {
        if (weapon is null || string.IsNullOrWhiteSpace(implementName))
            return false;

        return WeaponMatchesNamedPredicate(weapon, implementName)
            || WeaponFieldContainsNormalizedToken(weapon, "Magic Item Type", implementName)
            || WeaponFieldContainsNormalizedToken(weapon, "_Base Magic Item Type", implementName)
            || WeaponFieldContainsNormalizedToken(weapon, "Group", implementName)
            || WeaponFieldContainsNormalizedToken(weapon, "_Base Group", implementName)
            || WeaponFieldContainsNormalizedToken(weapon, "Item Slot", implementName)
            || WeaponFieldContainsNormalizedToken(weapon, "_Base Item Slot", implementName)
            || GenericImplementGearMatches(weapon, implementName);
    }

    /// <summary>
    /// Generic implement Gear items (Orb Implement, Wand Implement, Tome
    /// Implement, Staff Implement, Rod Implement — all <c>type="Gear"</c>
    /// with name pattern <c>"&lt;ImplementType&gt; Implement"</c>) carry
    /// the implement-type in their NAME, not in any structured field.
    /// They have no <c>Group</c>, no <c>Magic Item Type</c>; only the
    /// off-hand item slot is structured. Match the implement-type token
    /// against the leading word of the name so feats like Implement
    /// Focus (Orb) pick up their damage bonus on Orb Implement powers.
    /// </summary>
    private static bool GenericImplementGearMatches(RulesElement weapon, string implementName)
    {
        if (!string.Equals(weapon.Type, "Gear", StringComparison.OrdinalIgnoreCase))
            return false;

        string name = weapon.Name ?? string.Empty;
        if (!name.EndsWith(" Implement", StringComparison.OrdinalIgnoreCase))
            return false;

        string prefix = name[..^" Implement".Length].Trim();
        return string.Equals(prefix, implementName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool WeaponMatchesKindPredicate(RulesElement? weapon, string token)
    {
        if (weapon is null)
            return false;

        string? category;
        if (token.Equals("melee weapon", StringComparison.OrdinalIgnoreCase))
            category = "Melee";
        else if (token.Equals("ranged weapon", StringComparison.OrdinalIgnoreCase))
            category = "Ranged";
        else
            return false;

        return WeaponFieldContainsCategoryToken(weapon, "Weapon Category", category)
            || WeaponFieldContainsCategoryToken(weapon, "_Base Weapon Category", category);
    }

    private static bool WeaponHasRangeCategory(RulesElement? weapon)
        => weapon is not null
            && (WeaponFieldContainsCategoryToken(weapon, "Weapon Category", "Melee")
                || WeaponFieldContainsCategoryToken(weapon, "_Base Weapon Category", "Melee")
                || WeaponFieldContainsCategoryToken(weapon, "Weapon Category", "Ranged")
                || WeaponFieldContainsCategoryToken(weapon, "_Base Weapon Category", "Ranged"));

    private static bool WeaponMatchesBareRangePredicate(RulesElement? weapon, string token)
    {
        if (weapon is null)
            return false;

        string? category;
        if (token.Equals("melee", StringComparison.OrdinalIgnoreCase))
            category = "Melee";
        else if (token.Equals("ranged", StringComparison.OrdinalIgnoreCase))
            category = "Ranged";
        else
            return false;

        return WeaponFieldContainsCategoryToken(weapon, "Weapon Category", category)
            || WeaponFieldContainsCategoryToken(weapon, "_Base Weapon Category", category);
    }

    private static bool WeaponMatchesPropertyPredicate(RulesElement? weapon, string token)
    {
        if (weapon is null) return false;

        return WeaponFieldContainsToken(weapon, "Properties", token)
            || WeaponFieldContainsToken(weapon, "_Base Properties", token)
            || WeaponFieldContainsToken(weapon, "Weapon Category", token)
            || WeaponFieldContainsToken(weapon, "_Base Weapon Category", token);
    }

    private static bool WeaponFieldContainsToken(RulesElement weapon, string fieldName, string token)
    {
        if (!weapon.Fields.TryGetValue(fieldName, out string? value) || string.IsNullOrWhiteSpace(value))
            return false;

        foreach (string part in value.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Equals(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return value.Equals(token, StringComparison.OrdinalIgnoreCase);
    }

    private static bool WeaponFieldContainsCategoryToken(RulesElement weapon, string fieldName, string token)
    {
        if (!weapon.Fields.TryGetValue(fieldName, out string? value) || string.IsNullOrWhiteSpace(value))
            return false;

        foreach (string part in value.Split([',', ';', ' '], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Equals(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return value.Equals(token, StringComparison.OrdinalIgnoreCase);
    }

    private static bool WeaponFieldContainsNormalizedToken(RulesElement weapon, string fieldName, string token)
    {
        if (!weapon.Fields.TryGetValue(fieldName, out string? value) || string.IsNullOrWhiteSpace(value))
            return false;

        string normalizedToken = NormalizePredicateText(token);
        foreach (string part in value.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (NormalizePredicateText(part) == normalizedToken)
                return true;
        }

        return NormalizePredicateText(value) == normalizedToken;
    }

    private static string NormalizePredicateText(string text)
    {
        string normalized = text.Trim().ToLowerInvariant()
            .Replace("-", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal);

        if (normalized.EndsWith("focuses", StringComparison.Ordinal))
            return normalized[..^2];
        if (normalized.EndsWith("s", StringComparison.Ordinal) && normalized.Length > 1)
            return normalized[..^1];

        return normalized;
    }

}
