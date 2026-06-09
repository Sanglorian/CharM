namespace CharM.Engine.Orchestration;

/// <summary>
/// An expected stat value with its contribution chain, used for validation.
/// This is an Engine-native type — callers convert from CharacterSnapshot.
/// </summary>
public sealed record ExpectedStatValue
{
    public required string Name { get; init; }
    public required int Value { get; init; }
    public List<ExpectedContribution> Contributions { get; init; } = [];
}

/// <summary>
/// A single expected contribution to a stat value.
/// </summary>
public sealed record ExpectedContribution(int Value, string? Type, string? Source);

/// <summary>
/// Validates a character build by comparing computed stats against expected values
/// from a .dnd4e file. Produces a detailed report with contribution chain diffs
/// for any mismatches.
/// </summary>
public static class CharacterValidator
{
    /// <summary>
    /// Stats that are user-entered or framework-internal data, not rules-computed values.
    /// These are excluded from validation since the engine can't derive them from rules.
    /// </summary>
    private static readonly HashSet<string> ExcludedStats = new(StringComparer.OrdinalIgnoreCase)
    {
        "Weight",              // character physical weight in lbs (user-entered)
        "Average Height",      // racial average height text (stored as numeric stat with value 0)
        "Average Weight",      // racial average weight text (same)
        "_CLASSNAME",          // internal framework stat (class name as numeric — always 0)
        "Hybrid Power Points", // internal psionic constant set outside statadd pipeline
        "XP Needed",           // cumulative XP threshold — level 30 cap produces 0 but we sum all
    };

    /// <summary>
    /// Validate computed stats against expected stat values.
    /// </summary>
    /// <param name="builder">The CharacterBuilder after Build() has been called.</param>
    /// <param name="characterName">Character name for the report.</param>
    /// <param name="characterLevel">Character level for the report.</param>
    /// <param name="expectedStats">Expected stat values keyed by stat name.</param>
    /// <param name="knownErrata">Optional set of stat names to mark as known errata.</param>
    /// <returns>Validation result with match/mismatch details.</returns>
    public static ValidationResult Validate(
        CharacterBuilder builder,
        string characterName,
        int characterLevel,
        IReadOnlyDictionary<string, ExpectedStatValue> expectedStats,
        HashSet<string>? knownErrata = null)
    {
        knownErrata ??= [];

        var computedAll = builder.GetAllStatValues();

        int matched = 0;
        int mismatched = 0;
        int missing = 0;
        var mismatches = new List<StatMismatch>();

        var accountedFor = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (statName, expectedStat) in expectedStats)
        {
            // Skip user-entered stats that aren't rules-computed
            if (ExcludedStats.Contains(statName))
                continue;

            accountedFor.Add(statName);

            int computedValue = builder.GetStatValue(statName);
            bool hasComputed = computedAll.ContainsKey(statName) ||
                               builder.Stats.TryGetStat(statName) is not null;

            if (!hasComputed)
            {
                // If expected value is 0 and we never created the stat, that's still a match —
                // the stat exists in the file as a zero-value placeholder but our engine
                // correctly produces 0 (by not computing it at all).
                if (expectedStat.Value == 0)
                {
                    matched++;
                    continue;
                }

                missing++;
                mismatches.Add(CreateMismatch(
                    statName, expectedStat, computedValue, builder, knownErrata));
                continue;
            }

            if (computedValue == expectedStat.Value)
            {
                matched++;
            }
            else
            {
                mismatched++;
                mismatches.Add(CreateMismatch(
                    statName, expectedStat, computedValue, builder, knownErrata));
            }
        }

        int extra = 0;
        foreach (var name in computedAll.Keys)
        {
            if (!accountedFor.Contains(name))
                extra++;
        }

        var result = new ValidationResult
        {
            CharacterName = characterName,
            CharacterLevel = characterLevel,
            TotalStats = expectedStats.Count,
            MatchedStats = matched,
            MismatchedStats = mismatched,
            MissingStats = missing,
            ExtraStats = extra,
        };

        result.Mismatches.AddRange(mismatches);
        return result;
    }

    private static StatMismatch CreateMismatch(
        string statName,
        ExpectedStatValue expectedStat,
        int computedValue,
        CharacterBuilder builder,
        HashSet<string> knownErrata)
    {
        var mismatch = new StatMismatch
        {
            StatName = statName,
            ExpectedValue = expectedStat.Value,
            ComputedValue = computedValue,
            IsKnownErrata = knownErrata.Contains(statName),
        };

        foreach (var contrib in expectedStat.Contributions)
        {
            string typeTag = string.IsNullOrEmpty(contrib.Type) ? "" : contrib.Type;
            string source = contrib.Source ?? "";
            mismatch.ExpectedContributions.Add(
                $"{(contrib.Value >= 0 ? "+" : "")}{contrib.Value} [{typeTag}] from {source}");
        }

        var stat = builder.Stats.TryGetStat(statName);
        if (stat is not null)
        {
            foreach (var contrib in stat.Contributions)
            {
                int effectiveValue = contrib.GetEffectiveValue(builder.Stats);
                string typeTag = string.IsNullOrEmpty(contrib.BonusType) ? "" : contrib.BonusType;
                string source = contrib.SourceElementId ?? "";
                string exprTag = contrib.Expression is not null ? $" expr={contrib.Expression}" : "";
                string activeTag = contrib.Active ? "" : " [INACTIVE]";
                string gateTag = "";
                if (!string.IsNullOrEmpty(contrib.Wearing)) gateTag += $" wearing={contrib.Wearing}";
                if (!string.IsNullOrEmpty(contrib.NotWearing)) gateTag += $" not-wearing={contrib.NotWearing}";
                mismatch.ComputedContributions.Add(
                    $"{(effectiveValue >= 0 ? "+" : "")}{effectiveValue} [{typeTag}] from {source}{activeTag}{gateTag}{exprTag}");
            }
        }

        return mismatch;
    }
}
