using CharM.Engine.Creation;
using CharM.Engine.Rules;
using CharM.Engine.Selection;
using CharM.RulesDb.Storage;

namespace CharM.ImportExport.SummaryText.Blocks;

/// <summary>
/// Per-feature class-related picks: "&lt;Feature&gt;: &lt;Choice&gt;\r\n".
/// See decompiled <c>ClassChoices.cs</c>. Iterates Class Feature, Racial
/// Trait, Theme, Proficiency, Deity, Hybrid choices etc. Excludes Powers,
/// Feats and Skill Training (those have dedicated blocks).
/// </summary>
internal sealed class ClassChoicesBlock : SummaryBlock
{
    /// <summary>
    /// Choice element types that OCB surfaces in the ClassChoices section.
    /// Order here defines the section emit order — OCB iterates Class
    /// Feature first, then Racial Trait, Theme, Proficiency, Background,
    /// Deity, etc. (see decompiled <c>ClassChoices.Write</c>).
    /// Deity is intentionally absent: OCB gates it behind a
    /// <c>ShouldChooseDeity()</c> predicate (true for clerics, paladins,
    /// avengers, etc.) we don't currently model — the deity carries through
    /// the CharM Extensions block instead so we never falsely surface it
    /// for Assassins, Fighters, etc.
    /// </summary>
    private static readonly string[] IncludedTypesInOrder =
    {
        "Class Feature",
        "Racial Trait",
        "Theme",
        "Proficiency",
        "Domain",
        "Power Source",
        "Companion",
        "Familiar",
        "Pseudo Class",
        "Multiclass",
    };

    private static readonly HashSet<string> IncludedTypes = new(IncludedTypesInOrder, StringComparer.OrdinalIgnoreCase);

    public override string Write(CharacterSession session, IRulesDatabase database)
    {
        var sb = new System.Text.StringBuilder();

        // Group records by type, then emit type-blocks in OCB order.
        var byType = session.ChoiceHistory
            .Where(r => IncludedTypes.Contains(r.Element.Type))
            .GroupBy(r => r.Element.Type, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var type in IncludedTypesInOrder)
        {
            if (!byType.TryGetValue(type, out var records)) continue;
            foreach (var record in records)
            {
                string feature = ResolveFeatureLabel(session, database, record);
                if (string.IsNullOrWhiteSpace(feature))
                    feature = record.Element.Type;
                sb.Append(feature).Append(": ").Append(record.Element.Name).Append(Newline);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Resolve the OCB-style "Feature: Choice" left-hand label for
    /// <paramref name="record"/>. Delegates to the shared
    /// <see cref="SlotLabel.ResolveShort"/> helper so the SummaryText
    /// "Feature" label matches what the BuildChoicesPanel and the
    /// pending-choices wizard show.
    /// </summary>
    private static string ResolveFeatureLabel(CharacterSession session, IRulesDatabase database, ChoiceRecord record)
    {
        var label = SlotLabel.ResolveShort(record.Slot, database.FindByInternalId);
        return label ?? record.Element.Type;
    }

    public override bool TryRead(CharacterSession session, IRulesDatabase database, ref string input)
    {
        int colon = input.IndexOf(": ", StringComparison.Ordinal);
        int nl = input.IndexOf(Newline, StringComparison.Ordinal);
        if (colon == -1 || nl == -1 || colon > nl) return false;

        string feature = input[..colon].Trim();
        string choice = input[(colon + 2)..nl].Trim();

        // Don't consume lines whose prefix is a class-power source label
        // ("Hybrid encounter 13", "Wizard daily 5", etc.) — those belong
        // to the dedicated PowerSummaryBlock further down the chain.
        bool looksLikePower =
            feature.Contains("at-will", StringComparison.OrdinalIgnoreCase)
            || feature.Contains("encounter", StringComparison.OrdinalIgnoreCase)
            || feature.Contains("daily", StringComparison.OrdinalIgnoreCase)
            || feature.Contains("utility", StringComparison.OrdinalIgnoreCase)
            || feature.Contains("spellbook", StringComparison.OrdinalIgnoreCase)
            || (feature.StartsWith("Level ", StringComparison.Ordinal)
                && int.TryParse(feature.AsSpan("Level ".Length), out _));

        // Try resolving the choice across all the types ClassChoices covers.
        // "Power" is included here for feat-granted power selects (e.g.
        // Arcane Admixture, Wild Talent Master) whose prefix is the
        // feat's own slot label rather than a class-power-source phrase —
        // distinguished from PowerSummaryBlock by the looksLikePower guard.
        RulesElement? resolved = null;
        foreach (var type in new[]
        {
            "Class Feature", "Racial Trait", "Theme", "Proficiency", "Deity",
            "Hybrid Class Feature", "Discipline Focus", "Hybrid Talent",
            "Power Source", "Patron", "Companion", "Familiar", "Animal Form",
            "Class Build", // also captured here on partial reads
            "Power",       // feat-granted power slots (Arcane Admixture, Wild Talent Master, ...)
        })
        {
            if (string.Equals(type, "Power", StringComparison.OrdinalIgnoreCase) && looksLikePower)
                continue;
            resolved = database.FindByNameAndType(choice, type);
            if (resolved is not null) break;
        }

        if (resolved is null) return false;

        // Return false (don't consume the input line) when the pick can't
        // land — the slot's owner may not be on the character yet. The
        // driver's rotation loop will retry this line after other blocks
        // have a chance to install the owner.
        if (!SessionReplayHelpers.MakeLabelledChoice(session, database, feature, resolved))
            return false;
        input = input[(nl + Newline.Length)..];
        return true;
    }
}
