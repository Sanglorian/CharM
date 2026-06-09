using CharM.Engine.CharacterModel;
using CharM.Engine.Rules;

namespace CharM.Engine.Selection;

/// <summary>
/// Single source of truth for "what should we call this choice slot in the UI?".
///
/// There are three layers of fallback:
///   1. The select directive's own label — captured either from
///      <c>name="..."</c> attribute OR the <c>&lt;select&gt;</c> element's
///      inner text. See <c>SelectDirective.Name</c>.
///   2. The slot owner element's name, when it adds usable context
///      (Channel Divinity, Dragon Breath, etc. — not scaffolding like
///      "DetailsRules" or "Expansion2").
///   3. A synthesized "Choose &lt;Qualifier&gt; &lt;Type&gt;" derived from
///      the slot's category expression.
///
/// Used by the wizard for pending-choice descriptions, by the
/// BuildChoicesPanel for the after-the-fact feature label, and by
/// SummaryTextExporter's ClassChoices block so its "Feature: Choice"
/// lines match what the UI showed when the choice was made.
/// </summary>
public static class SlotLabel
{
    /// <summary>
    /// Resolve a display label for <paramref name="slot"/>.
    /// <paramref name="findById"/> is used to look up the owner element's
    /// name when the slot's own label isn't set.
    /// </summary>
    public static string Resolve(ChoiceSlot slot, Func<string, RulesElement?> findById)
    {
        // Layer 1: the directive's own display label is authoritative.
        // Prefer DisplayLabel (from inner text — user-facing prompt) over
        // Name (slot identifier, used by modify/replace cross-references).
        var primary = slot.DisplayLabel ?? slot.Name;
        if (!string.IsNullOrWhiteSpace(primary))
            return primary!;

        // Layer 2: owner element's name when it isn't scaffolding.
        string? ownerName = null;
        if (!string.IsNullOrEmpty(slot.OwnerInternalId))
        {
            var owner = findById(slot.OwnerInternalId);
            if (owner is not null && !IsScaffoldingOwnerName(owner.Name))
                ownerName = owner.Name;
        }

        // Layer 3: synthesized "Choose <qualifier> <Type>" from category.
        string synthesized = SynthesizeFromCategory(slot);

        if (ownerName is null) return synthesized;

        // Avoid redundant prefix when the synthesized text already mentions
        // the owner (e.g. "Choose Wizard Daily ...")
        if (synthesized.Contains(ownerName, StringComparison.OrdinalIgnoreCase))
            return synthesized;

        return $"{ownerName}: {synthesized}";
    }

    /// <summary>
    /// Short-form label without the synthesized "Choose ..." fallback —
    /// used by SummaryText where the OCB convention is to drop to the
    /// owner's name (or the element type) rather than emit "Choose Power".
    /// Returns null when nothing better than the element's own type is
    /// available; the caller decides what to substitute.
    /// </summary>
    public static string? ResolveShort(ChoiceSlot slot, Func<string, RulesElement?> findById)
    {
        var primary = slot.DisplayLabel ?? slot.Name;
        if (!string.IsNullOrWhiteSpace(primary))
            return primary;

        if (!string.IsNullOrEmpty(slot.OwnerInternalId))
        {
            var owner = findById(slot.OwnerInternalId);
            if (owner is not null && !IsScaffoldingOwnerName(owner.Name))
                return owner.Name;
        }
        return null;
    }

    private static string SynthesizeFromCategory(ChoiceSlot slot)
    {
        if (slot.Category is null) return $"Choose {slot.ElementType}";

        var parts = slot.Category.Split(',')
            .Select(p => p.Trim())
            .Where(p =>
            {
                var bare = p.TrimStart('!');
                return !bare.StartsWith("$$") && !int.TryParse(bare, out _)
                    && !bare.StartsWith("ID_", StringComparison.OrdinalIgnoreCase)
                    && !p.Contains('|')
                    && bare.Length > 0;
            })
            .ToList();

        if (parts.Count == 0) return $"Choose {slot.ElementType}";

        var qualifier = string.Join(" ", parts.Select(p =>
        {
            bool negated = p.StartsWith('!');
            var bare = p.TrimStart('!').Replace('-', ' ');
            var title = System.Globalization.CultureInfo.CurrentCulture.TextInfo
                .ToTitleCase(bare.ToLowerInvariant());
            return negated ? $"Not {title}" : title;
        }));
        return $"Choose {qualifier} {slot.ElementType}";
    }

    private static bool IsScaffoldingOwnerName(string ownerName)
    {
        if (string.IsNullOrWhiteSpace(ownerName)) return true;
        if (ownerName.StartsWith("DetailsRules", StringComparison.OrdinalIgnoreCase)) return true;
        if (ownerName.StartsWith("Expansion", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(ownerName.AsSpan("Expansion".Length), out _)) return true;
        if (ownerName.StartsWith("Level ", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(ownerName.AsSpan("Level ".Length), out _)) return true;
        if (int.TryParse(ownerName, out _)) return true;
        return false;
    }
}
