using CharM.Engine.CharacterModel;
using CharM.Engine.Evaluation;
using CharM.Engine.Prerequisites;
using CharM.Engine.Rules;

namespace CharM.Engine.Orchestration;

public sealed partial class CharacterBuilder
{
    /// <summary>
    /// Execute Phase 2 (computational) directives across all collected elements.
    /// </summary>
    /// <summary>
    /// Execute Phase 2 (computational) directives across all collected elements.
    /// Deduplicates by InternalId to prevent double-processing when elements appear
    /// in both grant chains and the tally supplement.
    /// </summary>
    private void ExecuteAllPhase2(int maxCharacterLevel)
    {
        // Group by InternalId. Keep the MINIMUM characterLevel seen — that's
        // the level the element was acquired at, which is what OCB stamps onto
        // every <statadd Level="N"> the element produces. Without this we'd
        // emit Level=<current character level> on contributions from feats /
        // class features taken many levels ago, blowing up the StatBlock diff
        // (e.g. an Improved Defenses taken at L14 on an L18 character would
        // emit Level="18" on round-trip instead of Level="14").
        //
        // Note: we still pass maxCharacterLevel separately into ExecutePhase2
        // so tier-gated feats (Master at Arms, Versatile Expertise, etc.) see
        // every requires-check predicate fire at the character's current tier.
        var bestEntries = new Dictionary<string, (RulesElement Element, CharacterElement Parent, int Level)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var entry in _pendingPhase2)
        {
            if (!bestEntries.TryGetValue(entry.Element.InternalId, out var existing) ||
                entry.Level < existing.Level)
            {
                bestEntries[entry.Element.InternalId] = entry;
            }
        }

        foreach (var (element, parent, sourceLevel) in bestEntries.Values)
        {
            ExecutePhase2(element, parent, maxCharacterLevel, sourceLevel);
        }
        _pendingPhase2.Clear();
    }

    /// <summary>
    /// Execute Phase 2 (computational) directives on a single element.
    /// Handles: statadd, statalias, modify, textstring.
    /// </summary>
    private void ExecutePhase2(RulesElement element, CharacterElement parent, int characterLevel, int? sourceLevel = null)
    {
        // Track the most recent select directive's chosen element so that subsequent
        // modify directives without a Name field (the implicit-target pattern used by
        // ~38 multiclass/training feats — e.g. Arcane Initiate's Power Usage→Encounter)
        // patch the element the user just picked.
        int selectOrdinal = 0;
        RulesElement? lastSelectedTarget = null;

        foreach (var directive in element.Rules)
        {
            if (directive.Level.HasValue && directive.Level.Value > characterLevel)
                continue;

            switch (directive)
            {
                case SelectDirective:
                    lastSelectedTarget = FindChosenElementForSelect(parent, element.InternalId, selectOrdinal);
                    selectOrdinal++;
                    break;

                case GrantDirective grant:
                    // A grant followed by a nameless modify (used by pact-initiate class
                    // features: `grant Power X; modify Power Usage = "Encounter"`) must
                    // patch the just-granted element. Only update the implicit target if
                    // the grant actually landed in the tree (requires may have deferred it).
                    if (ElementTree.HasElement(grant.Name))
                    {
                        var granted = _findById(grant.Name);
                        if (granted is not null)
                            lastSelectedTarget = granted;
                    }
                    break;

                case StatAddDirective statAdd:
                    {
                        // Always emit the contribution (so it round-trips on export)
                        // but mark it inactive when its gating predicate doesn't fire.
                        bool requiresOk = directive.Requires is null ||
                            RequiresEvaluator.Evaluate(
                                directive.Requires,
                                ElementTree.HasElement,
                                ElementTree.HasElementOfTypeAndCategory,
                                characterLevel);

                        bool wearingOk = statAdd.Wearing is null || CheckWearing(statAdd.Wearing);
                        bool notWearingOk = statAdd.NotWearing is null || !CheckWearing(statAdd.NotWearing);

                        bool active = requiresOk && wearingOk && notWearingOk;

                        TrackStat(statAdd.Name);
                        StatAddProcessor.Process(statAdd, Stats, element.InternalId, sourceLevel ?? characterLevel, active);
                        break;
                    }

                case StatAliasDirective alias:
                    TrackStat(alias.Name);
                    TrackStat(alias.Alias);
                    Stats.AddAlias(alias.Name, alias.Alias);
                    break;

                case ModifyDirective modify:
                    if (directive.Requires is not null &&
                        !RequiresEvaluator.Evaluate(
                            directive.Requires,
                            ElementTree.HasElement,
                            ElementTree.HasElementOfTypeAndCategory,
                            characterLevel))
                        continue;

                    RulesElement? target = null;
                    if (modify.SelectSlot is not null)
                        target = FindChosenElementForSelectSlot(parent, modify.SelectSlot);
                    else if (modify.Name is not null && modify.ElementType is not null)
                        target = _findById(modify.Name) ?? _findByNameAndType(modify.Name, modify.ElementType);
                    else if (modify.Name is not null)
                        target = _findById(modify.Name);
                    else
                        // Implicit target = element chosen by the immediately-preceding select.
                        target = lastSelectedTarget;

                    if (target is not null)
                        Overlay.Apply(modify, target);
                    break;

                case TextStringDirective text:
                    ElementTree.ProcessTextString(text);
                    // Also surface as a String-payload contribution so the
                    // StatBlock export emits <statadd String="..." value="0"/>.
                    TrackStat(text.Name);
                    var stringStat = Stats.GetOrCreateStat(text.Name);
                    stringStat.AddContribution(new StatContribution
                    {
                        Value = 0,
                        StringPayload = text.Value,
                        SourceElementId = element.InternalId,
                        Level = text.Level ?? sourceLevel ?? characterLevel,
                        Condition = text.Condition,
                    });
                    break;
            }
        }
    }

    /// <summary>
    /// Locate the chosen element produced by the N-th select directive in
    /// <paramref name="ownerInternalId"/>'s rule list.
    /// <para>
    /// Two tree shapes are supported:
    /// </para>
    /// <list type="number">
    /// <item>Wizard / ExportTreeBuilder: select created N placeholder children
    /// under <paramref name="parent"/>, the first carrying the
    /// <see cref="ChoiceSlot"/>, with <c>RulesElement</c> filled by
    /// <c>MakeChoice</c> once the user picks.</item>
    /// <item>Flat-import (CharacterBuilder rebuilding from a .dnd4e tally):
    /// the chosen element is a Root child whose
    /// <see cref="CharacterElement.SlotOwnerInternalId"/> equals the directive
    /// owner. Slots themselves remain as empty placeholders alongside.</item>
    /// </list>
    /// </summary>
    private RulesElement? FindChosenElementForSelect(
        CharacterElement parent, string ownerInternalId, int selectOrdinal)
    {
        // Slot-filled tree: walk the directive owner's children for matching slots.
        int matched = 0;
        foreach (var child in parent.Children)
        {
            if (child.Choice is null)
                continue;
            if (child.Choice.OwnerInternalId is { } owner
                && !string.Equals(owner, ownerInternalId, StringComparison.OrdinalIgnoreCase))
                continue;

            if (matched == selectOrdinal && child.RulesElement is not null)
                return child.RulesElement;

            matched++;
        }

        // Flat-import tree: scan all root descendants for elements that point
        // back at this owner via SlotOwnerInternalId.
        int seen = 0;
        foreach (var node in ElementTree.Root.GetAllDescendants())
        {
            if (node.RulesElement is null) continue;
            if (!string.Equals(node.SlotOwnerInternalId, ownerInternalId, StringComparison.OrdinalIgnoreCase))
                continue;

            if (seen == selectOrdinal)
                return node.RulesElement;
            seen++;
        }

        return null;
    }

    private RulesElement? FindChosenElementForSelectSlot(CharacterElement parent, string selectSlot)
    {
        foreach (var slotNode in ElementTree.Root.GetAllDescendants())
        {
            if (slotNode.Choice is null || slotNode.RulesElement is null)
                continue;
            if (string.Equals(slotNode.Choice.Name, selectSlot, StringComparison.OrdinalIgnoreCase))
                return slotNode.RulesElement;
        }

        foreach (var ownerNode in FindSelectSlotOwnerNodes(parent, selectSlot))
        {
            var owner = ownerNode.RulesElement;
            if (owner is null || string.IsNullOrWhiteSpace(owner.InternalId))
                continue;

            foreach (var select in owner.Rules.OfType<SelectDirective>()
                         .Where(s => string.Equals(s.Name, selectSlot, StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var child in ownerNode.Children)
                {
                    if (child.RulesElement is not null && ElementMatchesSelect(child.RulesElement, select))
                        return child.RulesElement;
                }

                foreach (var node in ElementTree.Root.GetAllDescendants())
                {
                    if (node.RulesElement is null)
                        continue;
                    if (!string.Equals(node.SlotOwnerInternalId, owner.InternalId, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (ElementMatchesSelect(node.RulesElement, select))
                        return node.RulesElement;
                }
            }
        }

        return null;
    }

    private IEnumerable<CharacterElement> FindSelectSlotOwnerNodes(CharacterElement parent, string selectSlot)
    {
        for (var node = parent; node is not null; node = node.Parent)
        {
            if (NodeOwnsSelectSlot(node, selectSlot))
                yield return node;
        }

        foreach (var node in ElementTree.Root.GetAllDescendants())
        {
            if (NodeOwnsSelectSlot(node, selectSlot))
                yield return node;
        }
    }

    private static bool NodeOwnsSelectSlot(CharacterElement node, string selectSlot)
    {
        var element = node.RulesElement;
        if (element is null)
            return false;

        return string.Equals(element.InternalId, selectSlot, StringComparison.OrdinalIgnoreCase)
            || string.Equals(element.Name, selectSlot, StringComparison.OrdinalIgnoreCase)
            || element.Rules.OfType<SelectDirective>()
                .Any(s => string.Equals(s.Name, selectSlot, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ElementMatchesSelect(RulesElement element, SelectDirective select)
        => string.Equals(element.Type, select.ElementType, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Get the computed value of a stat.
}
