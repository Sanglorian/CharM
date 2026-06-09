using CharM.Engine.Creation;
using CharM.Engine.Rules;

namespace CharM.ImportExport;

public static partial class PowerStatsBuilder
{
    private static bool RequiresTwoMeleeWeaponsOrRangedWeapon(RulesElement power)
        => GetNormalizedWieldingTokens(power)
            .Any(token => string.Equals(
                token,
                "two melee weapons or a ranged weapon",
                StringComparison.OrdinalIgnoreCase));

    private static bool RequiresBothThrownWeaponAndMeleeWeapon(RulesElement power)
        => GetNormalizedWieldingTokens(power)
            .Any(token => string.Equals(
                token,
                "both a thrown weapon and a melee weapon",
                StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> GetNormalizedWieldingTokens(RulesElement power)
    {
        if (!power.Fields.TryGetValue("Requirement", out var req)
            || string.IsNullOrWhiteSpace(req))
            yield break;

        int idx = req.IndexOf("wielding", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) yield break;

        var tail = req.Substring(idx + "wielding".Length);
        foreach (var rawToken in tail.Split(','))
        {
            var token = rawToken.Trim();
            if (token.StartsWith("a ", StringComparison.OrdinalIgnoreCase))
                token = token.Substring(2).TrimStart();
            else if (token.StartsWith("or a ", StringComparison.OrdinalIgnoreCase))
                token = token.Substring(5).TrimStart();

            if (token.EndsWith(".", StringComparison.Ordinal))
                token = token.Substring(0, token.Length - 1);

            if (token.Length > 0)
                yield return token;
        }
    }

    private static bool SatisfiesTwoMeleeWeaponsOrRangedWeapon(
        LootItem loot,
        IReadOnlyList<LootItem> weaponCandidates,
        IReadOnlySet<string>? wieldedLootKeys,
        IReadOnlyDictionary<string, int>? equippedCompositeKeyCounts,
        Engine.Creation.CharacterSnapshot snapshot,
        bool melee,
        bool ranged)
    {
        // OCB's exact IsRestricted branch for "two melee weapons or a ranged
        // weapon" first allows any ranged-capable candidate. Only melee-only
        // candidates need a DetermineOffhand-style partner.
        if (ranged) return true;
        if (!melee) return false;

        return HasMeleeOffhandForCandidate(
            loot, weaponCandidates, wieldedLootKeys,
            equippedCompositeKeyCounts, snapshot);
    }

    private static bool SatisfiesBothThrownWeaponAndMeleeWeapon(
        LootItem loot,
        IReadOnlyList<LootItem> weaponCandidates,
        IReadOnlyDictionary<string, int>? equippedCompositeKeyCounts,
        Engine.Creation.CharacterSnapshot snapshot)
    {
        if (equippedCompositeKeyCounts is null) return true;

        var mainEquiv = ResolveWeaponEquiv(loot, snapshot);
        var mainBase = mainEquiv ?? loot.Base;
        if (mainBase is null) return false;

        var offhandBase = DetermineOffhandWeaponBase(
            loot, weaponCandidates, equippedCompositeKeyCounts, snapshot);
        if (offhandBase is null) return false;

        bool mainMelee = IsMeleeWeapon(mainBase, snapshot);
        bool offhandMelee = IsMeleeWeapon(offhandBase, snapshot);
        bool mainThrown = HasThrownProperty(mainBase, snapshot);
        bool offhandThrown = HasThrownProperty(offhandBase, snapshot);

        return (mainMelee && offhandThrown) || (offhandMelee && mainThrown);
    }

    private static RulesElement? DetermineOffhandWeaponBase(
        LootItem loot,
        IReadOnlyList<LootItem> weaponCandidates,
        IReadOnlyDictionary<string, int> equippedCompositeKeyCounts,
        Engine.Creation.CharacterSnapshot snapshot)
    {
        if (equippedCompositeKeyCounts.TryGetValue(loot.CompositeKey, out int equipCount)
            && equipCount > 1)
        {
            return ResolveWeaponEquiv(loot, snapshot) ?? loot.Base;
        }

        if (IsDoubleWeapon(loot, snapshot))
            return ResolveWeaponEquiv(loot, snapshot) ?? loot.Base;

        bool candidateIsEquipped = equippedCompositeKeyCounts.ContainsKey(loot.CompositeKey);
        foreach (var other in weaponCandidates)
        {
            if (!equippedCompositeKeyCounts.ContainsKey(other.CompositeKey))
                continue;
            if (candidateIsEquipped
                && string.Equals(other.CompositeKey, loot.CompositeKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var otherBase = ResolveWeaponEquiv(other, snapshot) ?? other.Base;
            if (IsWeaponLikeForOffhand(otherBase))
                return otherBase;
        }

        return null;
    }

    private static bool IsWeaponLikeForOffhand(RulesElement? element)
    {
        if (element is null) return false;
        if (string.Equals(element.Type, "Weapon", StringComparison.OrdinalIgnoreCase))
            return true;
        return IsWeaponMagicItem(element);
    }

    private static bool IsMeleeWeapon(RulesElement element, Engine.Creation.CharacterSnapshot snapshot)
    {
        string fullText = snapshot.Builder.Overlay.GetField(element, "Full Text") ?? string.Empty;
        if (fullText.IndexOf("melee weapon", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (fullText.IndexOf("ranged weapon", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        return string.Equals(element.Type, "Weapon", StringComparison.OrdinalIgnoreCase)
            || IsWeaponMagicItem(element);
    }

    private static bool HasThrownProperty(RulesElement element, Engine.Creation.CharacterSnapshot snapshot)
    {
        string properties = snapshot.Builder.Overlay.GetField(element, "Properties") ?? string.Empty;
        return properties.IndexOf("thrown", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool HasMeleeOffhandForCandidate(
        LootItem loot,
        IReadOnlyList<LootItem> weaponCandidates,
        IReadOnlySet<string>? wieldedLootKeys,
        IReadOnlyDictionary<string, int>? equippedCompositeKeyCounts,
        Engine.Creation.CharacterSnapshot snapshot)
    {
        // Preserve legacy behavior for callers that do not provide equipped
        // state. The normal exporter always provides it.
        if (wieldedLootKeys is null) return true;

        // DetermineOffhand returns self when CharLootEquip > 1.
        if (equippedCompositeKeyCounts is not null
            && equippedCompositeKeyCounts.TryGetValue(loot.CompositeKey, out int equipCount)
            && equipCount > 1)
        {
            return true;
        }

        // DetermineOffhand also returns self for double weapons, even when the
        // candidate itself is not equipped.
        if (IsDoubleWeapon(loot, snapshot)) return true;

        if (!wieldedLootKeys.Contains(loot.CompositeKey))
            return false;

        foreach (var other in weaponCandidates)
        {
            if (string.Equals(other.CompositeKey, loot.CompositeKey, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!wieldedLootKeys.Contains(other.CompositeKey))
                continue;

            var otherEquiv = ResolveWeaponEquiv(other, snapshot);
            if (FigureWeaponType(other, otherEquiv, snapshot).Melee)
                return true;
        }

        return false;
    }
}
