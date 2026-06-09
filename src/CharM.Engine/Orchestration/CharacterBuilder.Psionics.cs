using CharM.Engine.CharacterModel;
using CharM.Engine.Evaluation;
using CharM.Engine.Prerequisites;
using CharM.Engine.Rules;

namespace CharM.Engine.Orchestration;

public sealed partial class CharacterBuilder
{
    /// <summary>
    /// Mirrors OCB's hardcoded Psionics pass: synthesize Hybrid Power Points
    /// from unlocked augmentable at-will attack powers, strip the visible
    /// Augmentable keyword from locked powers, and apply the Psionic
    /// Conventionalist power-point loss for its current multiswap.
    /// </summary>
    private void ApplyPsionicPowerPointSpecialCases()
    {
        int totalHybridPowerPoints = 0;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in ElementTree.Root.GetAllDescendants())
        {
            var element = node.RulesElement;
            if (!node.IsActive || element is null || !IsAugmentablePower(element))
                continue;

            if (!visited.Add(element.InternalId))
                continue;

            if (!IsUnlockedAugmentablePower(element))
            {
                StripVisibleAugmentableKeyword(element);
                continue;
            }

            if (!IsLockedReplacementPower(element)
                && IsHybridPowerPointContributingPower(element, useOverlay: true))
            {
                totalHybridPowerPoints += PowerPointTierValue(ReadPowerLevel(element, useOverlay: true));
            }
        }

        if (HasHybridPowerPointSink())
        {
            TrackStat("Hybrid Power Points");
            var hybridPowerPoints = Stats.GetOrCreateStat("Hybrid Power Points");
            hybridPowerPoints.AddContribution(new StatContribution { Value = totalHybridPowerPoints });
        }

        int conventionalistPenalty = ComputePsionicConventionalistPenalty();
        if (conventionalistPenalty != 0)
        {
            TrackStat("Power Points");
            var powerPoints = Stats.GetOrCreateStat("Power Points");
            powerPoints.AddContribution(new StatContribution { Value = -conventionalistPenalty });
        }
    }

    private bool HasHybridPowerPointSink()
    {
        if (ElementTree.HasElement("Hybrid Power Points")
            || ElementTree.HasElement("ID_INTERNAL_INTERNAL_HYBRID_POWER_POINTS"))
        {
            return true;
        }

        var powerPoints = Stats.TryGetStat("Power Points");
        return powerPoints?.Contributions.Any(c =>
            c.Active
            && c.Expression is ValueExpression.StatReference statRef
            && string.Equals(statRef.StatName, "Hybrid Power Points", StringComparison.OrdinalIgnoreCase)) == true;
    }

    private int ComputePsionicConventionalistPenalty()
    {
        if (!ElementTree.HasElement("ID_FMP_FEAT_2732"))
            return 0;

        int penalty = 0;
        foreach (var applied in _appliedReplacements)
        {
            if (!string.Equals(
                    applied.Replacement.SwapOwnerInternalId,
                    "ID_FMP_FEAT_2732",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (applied.OldElement is not { } oldElement)
                continue;

            if (IsHybridPowerPointContributingPower(oldElement, useOverlay: false))
                penalty += PowerPointTierValue(ReadPowerLevel(oldElement, useOverlay: false));
        }

        return penalty;
    }

    private bool IsLockedReplacementPower(RulesElement element)
    {
        foreach (var applied in _appliedReplacements)
        {
            if (!string.Equals(applied.NewElement.InternalId, element.InternalId, StringComparison.OrdinalIgnoreCase))
                continue;

            string? ownerId = applied.Replacement.SwapOwnerInternalId;
            if (string.IsNullOrWhiteSpace(ownerId)
                || ownerId.StartsWith("ID_INTERNAL_LEVEL_", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ownerId, "ID_FMP_FEAT_2698", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ownerId, "ID_INTERNAL_INTERNAL_PSIONIC_AUGMENTATION_(HYBRID)", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ownerId, "ID_INTERNAL_CLASS_FEATURE_HYBRID_ENCOUNTER_POWER", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool IsHybridPowerPointContributingPower(RulesElement element, bool useOverlay)
    {
        if (!string.Equals(element.Type, "Power", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!IsUnlockedAugmentablePower(element, useOverlay))
            return false;

        string? classId = GetField(element, "Class", useOverlay);
        if (string.IsNullOrWhiteSpace(classId)
            || _findById(classId.Trim()) is not { Type: "Class" })
        {
            return false;
        }

        return string.Equals(GetField(element, "Power Usage", useOverlay)?.Trim(), "At-Will", StringComparison.OrdinalIgnoreCase)
            && string.Equals(GetField(element, "Power Type", useOverlay)?.Trim(), "Attack", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsAugmentablePower(RulesElement element)
        => !string.IsNullOrWhiteSpace(GetField(element, "_AugmentVersions", useOverlay: false));

    private bool IsUnlockedAugmentablePower(RulesElement element)
        => IsUnlockedAugmentablePower(element, useOverlay: true);

    private bool IsUnlockedAugmentablePower(RulesElement element, bool useOverlay)
        => IsAugmentablePower(element)
            && string.IsNullOrWhiteSpace(GetField(element, "_SuppressAugments", useOverlay));

    private string? GetField(RulesElement element, string fieldName, bool useOverlay)
        => useOverlay
            ? Overlay.GetField(element, fieldName)
            : element.Fields.TryGetValue(fieldName, out var value) ? value : null;

    private void StripVisibleAugmentableKeyword(RulesElement element)
    {
        string? keywords = Overlay.GetField(element, "Keywords");
        if (string.IsNullOrWhiteSpace(keywords)
            || !keywords.Contains("Augmentable", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var updatedKeywords = string.Join(", ",
            keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(keyword => !string.Equals(keyword, "Augmentable", StringComparison.OrdinalIgnoreCase)));

        Overlay.Apply(new ModifyDirective
        {
            Field = "Keywords",
            Value = updatedKeywords,
        }, element);
    }

    private int ReadPowerLevel(RulesElement element, bool useOverlay)
    {
        string? rawLevel = GetField(element, "Level", useOverlay);
        return int.TryParse(rawLevel, out int level) ? level : 0;
    }

    private static int PowerPointTierValue(int powerLevel)
        => powerLevel switch
        {
            >= 21 => 6,
            >= 11 => 4,
            _ => 2,
        };
}
