using CharM.Engine.CharacterModel;
using CharM.Engine.Creation;
using CharM.Engine.Rules;
using CharM.Engine.Selection;
using CharM.RulesDb.Storage;

namespace CharM.ImportExport.SummaryText;

/// <summary>
/// Shared helpers used by SummaryText reader blocks to drive the wizard.
///
/// The importer blocks each translate one or two human-readable lines into
/// wizard operations (pick element for a pending slot, level up, set
/// ability score, etc.). The mechanics of those operations are concentrated
/// here so individual block implementations don't have to know about the
/// pending-choice surface.
/// </summary>
internal static class SessionReplayHelpers
{
    /// <summary>
    /// Place <paramref name="element"/> into the next pending slot of its
    /// type — positional fill, no slot identification.
    /// <para>
    /// Correct for blocks whose lines lack a per-pick disambiguator (RaceClass,
    /// Background, BuildSummary, Skill list, derived-label power sources, level-
    /// owned feat lines). When the SummaryText line carries a slot-label prefix
    /// AND the prefix disambiguates which slot, prefer
    /// <see cref="MakeLabelledChoice"/>.
    /// </para>
    /// </summary>
    public static bool PlacePositionally(CharacterSession session, RulesElement element)
        => session.TryMakeChoice(element, skipPrereqs: true);

    /// <summary>
    /// Label-aware placement: find a pending slot whose
    /// <c>DisplayLabel ?? Name</c> (or, when neither is set, the
    /// SlotLabel-resolved owner name) matches <paramref name="labelHint"/>,
    /// then place <paramref name="element"/> into that slot. Falls back to
    /// the slotless <see cref="MakeChoiceForElement"/> when no labelled
    /// slot matches — defensive for fixtures where the line prefix isn't a
    /// slot label (e.g. ItemSummary section headers).
    /// <para>
    /// This is how the SummaryText importer disambiguates lines that share
    /// an ElementType but belong to different slots — e.g. Blaze has two
    /// <c>Arcane Admixture II:</c> lines, one Power and one Class Feature,
    /// each routing into a different slot under the Arcane Admixture II feat.
    /// Without label routing, positional-fill orders matter and bugs hide.
    /// </para>
    /// </summary>
    public static bool MakeLabelledChoice(
        CharacterSession session,
        IRulesDatabase database,
        string labelHint,
        RulesElement element)
    {
        // No label hint — fall back to positional fill immediately.
        if (string.IsNullOrWhiteSpace(labelHint))
            return session.TryMakeChoice(element, skipPrereqs: true);

        foreach (var pending in session.GetAllPendingChoices())
        {
            if (!string.Equals(pending.Slot.ElementType, element.Type, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!SlotLabelMatches(pending.Slot, labelHint, database))
                continue;

            // Verify the element is actually a candidate for this slot
            // (the slot might be open but the picked element wouldn't
            // satisfy its category filter; in that case keep searching).
            var candidates = session.GetCandidatesForSlot(pending.Slot, skipPrereqs: true);
            if (!candidates.Any(c => string.Equals(c.InternalId, element.InternalId, StringComparison.OrdinalIgnoreCase)))
                continue;

            session.MakeChoice(pending.Slot, element);
            return true;
        }

        // Label provided but no matching slot is open right now. Return
        // false so the driver can rotate and retry — the slot's owner may
        // not be picked yet. (We deliberately do NOT fall back to positional
        // here: that would silently misplace the element into a slot whose
        // label doesn't match the SummaryText prefix, defeating the whole
        // point of label-aware routing.)
        return false;
    }

    private static bool SlotLabelMatches(ChoiceSlot slot, string labelHint, IRulesDatabase database)
    {
        // Direct match on the directive's display label, then the slot
        // identifier (matches OCB's `DisplayLabel ?? Name` emission convention).
        if (!string.IsNullOrWhiteSpace(slot.DisplayLabel)
            && string.Equals(slot.DisplayLabel, labelHint, StringComparison.OrdinalIgnoreCase))
            return true;
        if (!string.IsNullOrWhiteSpace(slot.Name)
            && string.Equals(slot.Name, labelHint, StringComparison.OrdinalIgnoreCase))
            return true;

        // Owner-name fallback for slots with no per-directive label
        // (Wild Talent Master grants three Power slots, all labelled
        // "Wild Talent Master:" by OCB via the owner feat's name).
        var resolved = SlotLabel.ResolveShort(slot, database.FindByInternalId);
        return resolved is not null
               && string.Equals(resolved, labelHint, StringComparison.OrdinalIgnoreCase);
    }
}
