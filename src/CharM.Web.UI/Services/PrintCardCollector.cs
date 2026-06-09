using CharM.Engine.CharacterModel;
using CharM.Engine.Creation;
using CharM.Engine.Powers;
using CharM.Engine.Rules;
using CharM.Serialization;
using CharM.Web.Components.Shared;

// Disambiguate: there's also a CharM.Serialization.CharacterSnapshot from the
// using above. The session API uses the Creation one.
using CharacterSnapshot = CharM.Engine.Creation.CharacterSnapshot;

namespace CharM.Web.Services;

/// <summary>
/// Card collection for the print packet (page 1 summary + power card grid).
/// Mirrors the data assembly in <c>Pages/Powers.razor</c> but lives in a
/// service so the print page can reuse it without copying a thousand-line
/// .razor file. If Powers.razor's logic changes, update this in lockstep.
/// </summary>
public sealed class PrintCardCollector
{
    private readonly CharacterSessionService _sessionService;

    public PrintCardCollector(CharacterSessionService sessionService)
    {
        _sessionService = sessionService;
    }

    public PrintCardCollection Collect()
    {
        var session = _sessionService.Session
            ?? throw new InvalidOperationException("No active character session.");
        var snapshot = session.GetPartialSnapshot();
        var powerStats = _sessionService.GetRebuiltPowerStatsForDisplay();

        var standardCards = BuildPowerCards(session, snapshot, powerStats);
        var basicCards = standardCards.Where(IsBasicPowerCard).ToList();
        var nonBasicCards = standardCards.Where(c => !IsBasicPowerCard(c)).ToList();
        var magicItemCards = BuildMagicItemPowerCards(session).ToList();

        var companionData = session.GetCompanionData();
        // Drop OCB-export placeholder entries — those exist so the .dnd4e
        // writer can emit one <Beast> block per active Companion CharElement
        // (one per level for animal companions), but they are not separate
        // companions and would otherwise render one duplicate mini-sheet +
        // attack card per level. Dedupe by anchor power id as a belt-and-
        // suspenders measure for any other near-duplicate companion entries.
        companionData = DedupeCompanionDataForDisplay(companionData);
        var miniSheetAnchors = new HashSet<string>(
            companionData.Where(c => c.IsMinion || c.IsSummon)
                         .Select(c => c.AnchorPowerInternalId)
                         .Where(id => id is not null)!,
            StringComparer.OrdinalIgnoreCase);

        bool IsStandardSectionEligible(PowerDisplayCard card)
            => !IsCompanionPowerCard(card)
               && !miniSheetAnchors.Contains(card.InternalId)
               && !IsChannelDivinityCard(card);

        var atWill = nonBasicCards.Where(c => c.Section == PowerSectionKeys.AtWill && IsStandardSectionEligible(c)).ToList();
        var cantrip = nonBasicCards.Where(c => c.Section == PowerSectionKeys.Cantrip && IsStandardSectionEligible(c)).ToList();
        var encounter = nonBasicCards.Where(c => c.Section == PowerSectionKeys.Encounter && IsStandardSectionEligible(c)).ToList();
        var daily = nonBasicCards.Where(c => c.Section == PowerSectionKeys.Daily && IsStandardSectionEligible(c)).ToList();
        var utility = nonBasicCards.Where(c => c.Section == PowerSectionKeys.Utility && IsStandardSectionEligible(c)).ToList();
        var channelDivinity = nonBasicCards.Where(IsChannelDivinityCard).ToList();

        var allCompanionCards = nonBasicCards.Where(IsCompanionPowerCard).ToList();
        bool hasSpecificCompanion = allCompanionCards.Any(c =>
            c.Name.StartsWith("Companion:", StringComparison.OrdinalIgnoreCase));
        var companionCards = allCompanionCards
            .Where(c => !hasSpecificCompanion || !IsGenericBeastAttack(c))
            .ToList();

        // Build companion groups the same way Powers.razor does so the print
        // sheet can render mini-sheets paired with their attack cards.
        var renderedAttackCardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var companionGroups = new List<(CompanionData? Mini, PowerDisplayCard? Card, string? NameOverride)>();
        foreach (var beast in companionData)
        {
            companionGroups.Add((beast, null, null));
            if (beast.IsMinion || beast.IsSummon) continue;

            var attackCard = companionCards.FirstOrDefault(c =>
                beast.AnchorPowerInternalId is not null
                && string.Equals(c.InternalId, beast.AnchorPowerInternalId, StringComparison.OrdinalIgnoreCase));
            if (attackCard is not null)
            {
                // Resolve "DISPLAYNAME" placeholder names using the beast's category.
                string? nameOverride = attackCard.Name.Equals("DISPLAYNAME", StringComparison.OrdinalIgnoreCase)
                    ? $"Companion: {beast.Category} Basic Attack"
                    : attackCard.Name.StartsWith("Companion:", StringComparison.OrdinalIgnoreCase)
                        ? $"{attackCard.Name} Basic Attack"
                        : null;
                companionGroups.Add((null, attackCard, nameOverride));
                renderedAttackCardIds.Add(attackCard.InternalId);
            }
        }
        var unpairedCompanionCards = companionCards
            .Where(c => !renderedAttackCardIds.Contains(c.InternalId)
                     && !miniSheetAnchors.Contains(c.InternalId))
            .Select(c => (Card: c, NameOverride: (string?)null))
            .ToList();

        // Print packet ordering: standard powers then channel divinity then magic
        // items then companion attack cards then basic powers.
        var allCards = atWill
            .Concat(cantrip)
            .Concat(encounter)
            .Concat(daily)
            .Concat(utility)
            .Concat(channelDivinity)
            .Concat(magicItemCards)
            .Concat(companionCards)
            .Concat(basicCards)
            .ToList();

        // Non-companion subset for the regular card grid (companion rendered separately).
        var allNonCompanion = atWill
            .Concat(cantrip)
            .Concat(encounter)
            .Concat(daily)
            .Concat(utility)
            .Concat(channelDivinity)
            .Concat(magicItemCards)
            .Concat(basicCards)
            .ToList();

        var pendingChoices = GetPendingPowerChoices(session);

        return new PrintCardCollection
        {
            AtWill = atWill,
            Cantrip = cantrip,
            Encounter = encounter,
            Daily = daily,
            Utility = utility,
            ChannelDivinity = channelDivinity,
            MagicItem = magicItemCards,
            Companion = companionCards,
            Basic = basicCards,
            All = allCards,
            AllNonCompanion = allNonCompanion,
            CompanionGroups = companionGroups,
            UnpairedCompanionCards = unpairedCompanionCards,
            CompanionData = companionData,
            MiniSheetAnchors = miniSheetAnchors,
            PendingPowerChoices = pendingChoices,
            Snapshot = snapshot,
        };
    }

    // ---- helpers extracted from Powers.razor ----------------------------------
    // Keep these in sync with Powers.razor. When the latter changes, mirror the
    // change here. (TODO: a future refactor should have Powers.razor delegate
    // directly to this collector.)

    private IReadOnlyList<PowerDisplayCard> BuildPowerCards(
        CharacterSession session,
        CharacterSnapshot? snapshot,
        IReadOnlyList<PowerStatEntry> powerStats)
    {
        var stats = snapshot?.Builder.Stats;
        var cantripIds = CollectCantripPowerIds(snapshot);

        var powers = GetDisplayPowers(session, snapshot)
            .Where(power => !PowerCardFactory.IsFamiliarCardPower(power))
            .Where(power => !IsAugmentVariant(power));

        return PowerCardFactory.BuildSessionCards(
            powers,
            stats,
            powerStats,
            session.IsHouseruledElement,
            sectionOverride: power =>
            {
                if (!string.IsNullOrEmpty(power.InternalId)
                    && cantripIds.Contains(power.InternalId))
                {
                    return PowerSectionKeys.Cantrip;
                }

                return null;
            },
            augmentVersions: GetAugmentVersions);
    }

    private IEnumerable<RulesElement> GetDisplayPowers(CharacterSession session, CharacterSnapshot? snapshot)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (snapshot is not null)
        {
            foreach (var node in snapshot.Builder.ElementTree.Root.GetAllDescendants())
            {
                if (node.RulesElement is not { } power) continue;
                if (!power.Type.Equals("Power", StringComparison.OrdinalIgnoreCase)) continue;
                if (IsAugmentVariant(power)) continue;
                if (!string.IsNullOrEmpty(power.InternalId)
                    && (snapshot.PowerStatsExcludedIds.Contains(power.InternalId)
                        || snapshot.LevelNestedOnlyIds.Contains(power.InternalId)))
                {
                    continue;
                }

                var key = !string.IsNullOrEmpty(power.InternalId)
                    ? power.InternalId
                    : "name:" + power.Name;
                if (!seen.Add(key)) continue;

                yield return !string.IsNullOrEmpty(power.InternalId)
                    ? _sessionService.GetElementDetails(power.InternalId) ?? power
                    : power;
            }

            yield break;
        }

        foreach (var power in session.GetAllElementsOfType("Power"))
        {
            if (IsAugmentVariant(power)) continue;
            var key = !string.IsNullOrEmpty(power.InternalId)
                ? power.InternalId
                : "name:" + power.Name;
            if (seen.Add(key))
                yield return !string.IsNullOrEmpty(power.InternalId)
                    ? _sessionService.GetElementDetails(power.InternalId) ?? power
                    : power;
        }
    }

    private IReadOnlyList<PowerDisplayCard> BuildMagicItemPowerCards(CharacterSession session)
    {
        var cards = new List<PowerDisplayCard>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddLoot(LootItem loot)
        {
            foreach (var component in loot.Components())
            {
                if (!component.Type.Equals("Magic Item", StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var card in PowerCardFactory.BuildItemPowerCards(component, PowerSectionKeys.MagicItem))
                {
                    if (seen.Add(card.InternalId))
                        cards.Add(card);
                }
            }
        }

        foreach (var loot in session.GetEquippedLoot().Values)
            AddLoot(loot);

        foreach (var inventoryItem in session.GetInventory().Where(item => item.Quantity >= 1))
            AddLoot(inventoryItem.Item);

        return cards
            .OrderBy(c => c.Level)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IReadOnlyList<RulesElement> GetAugmentVersions(RulesElement power)
    {
        if (!power.Fields.TryGetValue("_AugmentVersions", out var raw) || string.IsNullOrWhiteSpace(raw))
            return [];

        var variants = new List<RulesElement>();
        foreach (var id in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var variant = _sessionService.GetElementDetails(id);
            if (variant is not null)
                variants.Add(variant);
        }

        return variants;
    }

    private bool IsChannelDivinityCard(PowerDisplayCard card)
    {
        var detail = _sessionService.GetElementDetails(card.InternalId);
        return detail is not null && detail.Fields.ContainsKey("Channel Divinity");
    }

    private static HashSet<string> CollectCantripPowerIds(CharacterSnapshot? snapshot)
    {
        // Cantrip detection lives on the snapshot; if we don't have one we
        // can't classify cantrips and they'll fall back to whatever the
        // factory infers from the rules element.
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (snapshot is null) return ids;

        foreach (var node in snapshot.Builder.ElementTree.Root.GetAllDescendants())
        {
            if (node.RulesElement is not { } power) continue;
            if (!power.Type.Equals("Power", StringComparison.OrdinalIgnoreCase)) continue;
            if (power.Fields.TryGetValue("Power Usage", out var usage)
                && usage.Trim().StartsWith("Cantrip", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(power.InternalId))
                    ids.Add(power.InternalId);
            }
        }

        return ids;
    }

    private static IReadOnlyList<CompanionData> DedupeCompanionDataForDisplay(IReadOnlyList<CompanionData> source)
    {
        if (source.Count == 0) return source;
        var seenAnchors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<CompanionData>(source.Count);
        foreach (var beast in source)
        {
            // Placeholder rows exist for OCB export parity only — they share
            // identity with the active companion (one per level) and would
            // re-render the same mini-sheet/attack card. Skip them.
            if (beast.IsPlaceholderForActiveBeast) continue;

            // Dedupe by anchor power id when available so two companion
            // entries sharing the same attack card render once.
            if (!string.IsNullOrEmpty(beast.AnchorPowerInternalId)
                && !seenAnchors.Add(beast.AnchorPowerInternalId))
            {
                continue;
            }

            result.Add(beast);
        }
        return result;
    }

    private static bool IsAugmentVariant(RulesElement power)
        => power.Fields.ContainsKey("_AugmentParent");

    private static bool IsCompanionPowerCard(PowerDisplayCard card)
        => card.InternalId.StartsWith("ID_TIV_COMPANION-", StringComparison.OrdinalIgnoreCase)
        || card.InternalId.StartsWith("ID_TIV_ANIMAL_COMPANION-", StringComparison.OrdinalIgnoreCase)
        || card.InternalId.StartsWith("ID_INTERNAL_POWER_BEAST_", StringComparison.OrdinalIgnoreCase)
        || card.Name.StartsWith("Companion:", StringComparison.OrdinalIgnoreCase)
        || card.Name.StartsWith("Animal Master's Companion:", StringComparison.OrdinalIgnoreCase)
        || card.Name.StartsWith("Animal Companion:", StringComparison.OrdinalIgnoreCase)
        || card.Name.StartsWith("Beast ", StringComparison.OrdinalIgnoreCase);

    private static bool IsGenericBeastAttack(PowerDisplayCard card)
        => card.InternalId.StartsWith("ID_INTERNAL_POWER_BEAST_", StringComparison.OrdinalIgnoreCase)
        || card.Name.Equals("Beast Melee Basic Attack", StringComparison.OrdinalIgnoreCase)
        || card.Name.Equals("Beast Ranged Basic Attack", StringComparison.OrdinalIgnoreCase);

    private static bool IsBasicPowerCard(PowerDisplayCard card)
    {
        var id = card.InternalId;
        if (id.Equals("ID_INTERNAL_POWER_MELEE_BASIC_ATTACK", StringComparison.OrdinalIgnoreCase)
            || id.Equals("ID_INTERNAL_POWER_RANGED_BASIC_ATTACK", StringComparison.OrdinalIgnoreCase)
            || id.Equals("ID_INTERNAL_POWER_BULL_RUSH_ATTACK", StringComparison.OrdinalIgnoreCase)
            || id.Equals("ID_INTERNAL_POWER_GRAB_ATTACK", StringComparison.OrdinalIgnoreCase)
            || id.Equals("ID_INTERNAL_POWER_OPPORTUNITY_ATTACK", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return card.Name.Equals("Melee Basic Attack", StringComparison.OrdinalIgnoreCase)
            || card.Name.Equals("Ranged Basic Attack", StringComparison.OrdinalIgnoreCase)
            || card.Name.Equals("Bull Rush Attack", StringComparison.OrdinalIgnoreCase)
            || card.Name.Equals("Grab Attack", StringComparison.OrdinalIgnoreCase)
            || card.Name.Equals("Opportunity Attack", StringComparison.OrdinalIgnoreCase);
    }



    private static IReadOnlyList<PendingChoice> GetPendingPowerChoices(CharacterSession session)
        => session.GetAllPendingChoices()
            .Where(choice => choice.Slot.ElementType.Equals("Power", StringComparison.OrdinalIgnoreCase))
            .ToList();
}

public sealed class PrintCardCollection
{
    public required IReadOnlyList<PowerDisplayCard> AtWill { get; init; }
    public required IReadOnlyList<PowerDisplayCard> Cantrip { get; init; }
    public required IReadOnlyList<PowerDisplayCard> Encounter { get; init; }
    public required IReadOnlyList<PowerDisplayCard> Daily { get; init; }
    public required IReadOnlyList<PowerDisplayCard> Utility { get; init; }
    public required IReadOnlyList<PowerDisplayCard> ChannelDivinity { get; init; }
    public required IReadOnlyList<PowerDisplayCard> MagicItem { get; init; }
    public required IReadOnlyList<PowerDisplayCard> Companion { get; init; }
    public required IReadOnlyList<PowerDisplayCard> Basic { get; init; }
    /// <summary>All cards including companion attack cards.</summary>
    public required IReadOnlyList<PowerDisplayCard> All { get; init; }
    /// <summary>Non-companion cards for the regular card grid; companion section rendered separately.</summary>
    public required IReadOnlyList<PowerDisplayCard> AllNonCompanion { get; init; }
    /// <summary>Ordered pairing of companion mini-sheets and their attack cards, mirroring Powers.razor grouping.</summary>
    public required IReadOnlyList<(CompanionData? Mini, PowerDisplayCard? Card, string? NameOverride)> CompanionGroups { get; init; }
    /// <summary>Companion attack cards not paired to a mini-sheet.</summary>
    public required IReadOnlyList<(PowerDisplayCard Card, string? NameOverride)> UnpairedCompanionCards { get; init; }
    public required IReadOnlyList<CompanionData> CompanionData { get; init; }
    public required HashSet<string> MiniSheetAnchors { get; init; }
    public required IReadOnlyList<PendingChoice> PendingPowerChoices { get; init; }
    public required CharacterSnapshot? Snapshot { get; init; }

    public int TotalAtWill => AtWill.Count;
    public int TotalEncounter => Encounter.Count;
    public int TotalDaily => Daily.Count;
    public int TotalUtility => Utility.Count;
    public int TotalCantrip => Cantrip.Count;
    public int TotalChannelDivinity => ChannelDivinity.Count;
    public int TotalMagicItem => MagicItem.Count;
}
