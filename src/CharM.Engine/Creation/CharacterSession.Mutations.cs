using CharM.Engine.CharacterModel;
using CharM.Engine.Orchestration;
using CharM.Engine.Rules;
using System.Xml.Linq;

namespace CharM.Engine.Creation;

public sealed partial class CharacterSession
{
    // --- Mutations ---

    /// <summary>Make a selection for a specific choice slot.</summary>
    public void MakeChoice(ChoiceSlot slot, RulesElement element)
    {
        var fullElement = _findById(element.InternalId) ?? element;
        _wizard.MakeChoiceForSlot(fullElement, slot);

        int choiceLevel = _wizard.DetermineSlotLevel(slot);
        _choiceHistory.Add(new ChoiceRecord(fullElement, slot, _sequenceCounter++, choiceLevel));
        MarkLevelTreeDirty(choiceLevel);
        InvalidateSnapshot();
        NotifyChanged();
    }

    /// <summary>
    /// When true, <see cref="MarkLevelTreeDirty"/> is a no-op. Set by
    /// <c>Dnd4eImporter</c> during pick replay so import-time
    /// MakeChoice / AddReplacement calls don't invalidate the captured
    /// trees we just populated. Reset to false at the end of import so
    /// genuine UI mutations dirty levels normally.
    /// </summary>
    public bool SuppressLevelTreeDirty { get; set; }

    /// <summary>
    /// Invalidate the captured per-level tree for <paramref name="level"/>
    /// (see <see cref="CapturedLevelTrees"/>). The exporter will fall back to
    /// engine-state rebuild for that level on the next export. No-op when no
    /// capture exists for the level or when <see cref="SuppressLevelTreeDirty"/>
    /// is set. Called from any mutation that changes the level's grant
    /// structure (choice add/edit/skip, undo, retrain).
    /// </summary>
    public void MarkLevelTreeDirty(int level)
    {
        if (SuppressLevelTreeDirty) return;
        if (level <= 0) return;
        if (!CapturedLevelTrees.ContainsKey(level)) return;
        if (LevelTreeDirty.Add(level))
            InvalidateSnapshot();
    }

    /// <summary>Set base ability scores.</summary>
    public void SetAbilityScores(AbilityScoreSet scores)
    {
        _abilityScores = scores;
        _wizard.SetAbilityScores(scores);
        InvalidateSnapshot();
        NotifyChanged();
    }

    /// <summary>
    /// Apply a Grabbag-style detached grant — for OCB campaign-settings entries
    /// (Inherent Bonuses, Spellscarred, House Vadalis, etc.) that the source
    /// file force-grants outside the normal Level subtree. The element is added
    /// directly under the tree root, its grant chain executes, but it is NOT
    /// recorded in the user choice history. Replayed on <see cref="UndoLastChoice"/>
    /// and <see cref="ChangeChoice"/> so its effects survive rebuilds.
    /// </summary>
    /// <param name="atLevel">
    /// Optional acquisition level. When omitted (0), the element is granted at
    /// the session's current level. Pass an explicit level when force-granting
    /// an element that the source file placed under a specific &lt;Level&gt;
    /// (e.g. a deferred Power pick from the importer's level walk) so it
    /// re-emits under the same level on round-trip.
    /// </param>
    /// <param name="tallyVestige">
    /// When <c>true</c> the element is treated as an orphan tally vestige:
    /// it appears in the tally / level structure for round-trip fidelity but
    /// is excluded from the rebuilt <c>&lt;PowerStats&gt;</c> section. Used
    /// by the importer's Power-leaf fallback when a source pick can't match
    /// any live build slot (e.g. a Hybrid character with an extra Daily-1
    /// power that exceeds the slot count). Mirrors OCB which keeps the
    /// tally row but emits no power card. Does NOT add the element to the
    /// <see cref="GrabbagGrants"/> list — it never came from a real
    /// <c>&lt;Grabbag&gt;&lt;rules&gt;</c> block.
    /// </param>
    public void AddGrabbagGrant(RulesElement element, int atLevel = 0, bool tallyVestige = false)
    {
        var fullElement = _findById(element.InternalId) ?? element;
        if (tallyVestige)
        {
            if (!string.IsNullOrEmpty(fullElement.InternalId))
                _powerStatsExcludedIds.Add(fullElement.InternalId);
            _wizard.AddFreeGrant(fullElement, atLevel);
        }
        else
        {
            _grabbagGrants.Add(fullElement);
            _wizard.AddFreeGrant(fullElement, atLevel);
        }
        InvalidateSnapshot();
        NotifyChanged();
    }

    /// <summary>Grabbag grants applied to this session (read-only view).</summary>
    public IReadOnlyList<RulesElement> GrabbagGrants => _grabbagGrants;

    /// <summary>
    /// Apply a true root-<c>Grabbag</c> campaign-setting grant. Unlike
    /// <see cref="AddGrabbagGrant"/>, this records enough structured state for
    /// the exporter to rebuild the campaign settings block when the user edits it.
    /// </summary>
    public bool AddCampaignSettingGrant(RulesElement element, bool markDirty = false)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (string.IsNullOrWhiteSpace(element.InternalId))
            throw new ArgumentException("Campaign setting grants require an internal id.", nameof(element));

        if (HasCampaignSettingGrant(element.InternalId))
            return false;

        var fullElement = _findById(element.InternalId) ?? element;
        _campaignSettingGrants.Add(fullElement);
        _grabbagGrants.Add(fullElement);
        _wizard.AddFreeGrant(fullElement);

        if (markDirty)
            CampaignSettingsDirty = true;

        InvalidateSnapshot();
        NotifyChanged();
        return true;
    }

    public bool RemoveCampaignSettingGrant(string internalId, bool markDirty = false)
    {
        if (string.IsNullOrWhiteSpace(internalId))
            throw new ArgumentException("Campaign setting grant id is required.", nameof(internalId));

        var removedCampaign = _campaignSettingGrants.RemoveAll(e =>
            string.Equals(e.InternalId, internalId, StringComparison.OrdinalIgnoreCase));
        if (removedCampaign == 0)
            return false;

        _grabbagGrants.RemoveAll(e =>
            string.Equals(e.InternalId, internalId, StringComparison.OrdinalIgnoreCase));
        _powerStatsExcludedIds.Remove(internalId);

        if (markDirty)
            CampaignSettingsDirty = true;

        RebuildFromHistory();
        return true;
    }

    public bool SetCampaignSettingGrant(RulesElement element, bool enabled, bool markDirty = true)
        => enabled
            ? AddCampaignSettingGrant(element, markDirty)
            : RemoveCampaignSettingGrant(element.InternalId ?? string.Empty, markDirty);

    public bool HasCampaignSettingGrant(string internalId)
        => _campaignSettingGrants.Any(e =>
            string.Equals(e.InternalId, internalId, StringComparison.OrdinalIgnoreCase));

    public void SetDetailField(string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(field))
            throw new ArgumentException("Detail field name is required.", nameof(field));

        var normalized = value ?? string.Empty;
        if (Details.TryGetValue(field, out var existing)
            && string.Equals(existing, normalized, StringComparison.Ordinal))
        {
            return;
        }

        Details[field] = normalized;
        InvalidateSnapshot();
        NotifyChanged();
    }

    public void SetTextString(string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Textstring name is required.", nameof(name));

        var normalized = value ?? string.Empty;
        if (TextStrings.TryGetValue(name, out var existing)
            && string.Equals(existing, normalized, StringComparison.Ordinal))
        {
            return;
        }

        TextStrings[name] = normalized;
        InvalidateSnapshot();
        NotifyChanged();
    }

    /// <summary>
    /// Add a slot-owned supplemental element from a serialized level subtree
    /// whose owning slot is materialized too late for normal import alignment.
    /// This preserves select-owner relationships for later modify directives
    /// without treating the element as a verbatim UserEdit pick.
    /// </summary>
    public void AddSlotOwnedSupplement(RulesElement element, int atLevel = 0, string? slotOwnerInternalId = null)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (string.IsNullOrEmpty(element.InternalId)) return;
        if (_slotOwnedSupplements.Any(p =>
                string.Equals(p.Element.InternalId, element.InternalId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(p.SlotOwnerInternalId, slotOwnerInternalId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var fullElement = _findById(element.InternalId) ?? element;
        _slotOwnedSupplements.Add(new SlotOwnedSupplement(fullElement, atLevel, slotOwnerInternalId));
        _wizard.AddFreeGrant(fullElement, atLevel, slotOwnerInternalId);
        InvalidateSnapshot();
        NotifyChanged();
    }

    /// <summary>
    /// Add a rules-database element through the explicit houserule workflow.
    /// This records the grant for generated UserEdit export, marks the element
    /// houseruled for UI/legality display, and applies its directives through
    /// the same engine path used by imported UserEdit picks.
    /// </summary>
    public void AddHouseruleGrant(RulesElement element, int atLevel = 0, string? slotOwnerInternalId = null)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (string.IsNullOrWhiteSpace(element.InternalId))
            throw new ArgumentException("Houserule grants require an existing rules-database internal id.", nameof(element));

        int level = atLevel > 0 ? atLevel : Level;
        var fullElement = _findById(element.InternalId) ?? element;
        if (!_houseruleGrants.Any(g =>
                g.Kind == HouseruleGrantKind.RulesElement
                && g.AtLevel == level
                && string.Equals(g.Element.InternalId, fullElement.InternalId, StringComparison.OrdinalIgnoreCase)))
        {
            _houseruleGrants.Add(new HouseruleGrant(fullElement, level, HouseruleGrantKind.RulesElement));
        }

        MarkElementHouseruled(fullElement.InternalId);
        AddUserEditPick(fullElement, level, slotOwnerInternalId);
    }

    public bool RemoveHouseruleGrant(string internalId, HouseruleGrantKind kind, string? slot = null)
    {
        if (string.IsNullOrWhiteSpace(internalId))
            throw new ArgumentException("Houserule grant id is required.", nameof(internalId));

        int removed = _houseruleGrants.RemoveAll(g =>
            g.Kind == kind
            && string.Equals(g.Slot ?? string.Empty, slot ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            && string.Equals(g.Element.InternalId, internalId, StringComparison.OrdinalIgnoreCase));
        if (removed == 0)
            return false;

        if (kind == HouseruleGrantKind.RulesElement)
        {
            _userEditPicks.RemoveAll(p =>
                string.Equals(p.Element.InternalId, internalId, StringComparison.OrdinalIgnoreCase));
            if (!_userEditPicks.Any(p => string.Equals(p.Element.InternalId, internalId, StringComparison.OrdinalIgnoreCase)))
                _userEditPickIds.Remove(internalId);
            if (!_houseruleGrants.Any(g => string.Equals(g.Element.InternalId, internalId, StringComparison.OrdinalIgnoreCase)))
                HouseruledElementIds.Remove(internalId);
            IsCharacterHouseruled = HouseruleLevelUserEdits.Count > 0
                || HouseruleLegacyTallyRows.Count > 0
                || _houseruleGrants.Count > 0;
            RebuildFromHistory();
            return true;
        }

        if (kind == HouseruleGrantKind.Inventory)
            RemoveInventoryItem(internalId);
        else if (slot is not null)
            UnequipItem(slot);

        if (!_houseruleGrants.Any(g => string.Equals(g.Element.InternalId, internalId, StringComparison.OrdinalIgnoreCase)))
            HouseruledElementIds.Remove(internalId);
        IsCharacterHouseruled = HouseruleLevelUserEdits.Count > 0
            || HouseruleLegacyTallyRows.Count > 0
            || _houseruleGrants.Count > 0;
        InvalidateSnapshot();
        NotifyChanged();
        return true;
    }

    /// <summary>
    /// Apply a houserule UserEdit pick: an element captured inside a
    /// <c>&lt;UserEdit&gt;</c> block in the source file. Fires the element's
    /// grant cascade like <see cref="AddGrabbagGrant"/> but the resulting
    /// engine tree node is suppressed from the rebuilt
    /// <c>&lt;Level&gt;</c> tree and the rebuilt flat
    /// <c>&lt;RulesElementTally&gt;</c> on export — both of those output
    /// channels are already covered by the verbatim
    /// <see cref="HouseruleLevelUserEdits"/> and
    /// <see cref="HouseruleFormATallyMirror"/> passthrough so we'd otherwise
    /// double-emit. The element IS still considered for stat contributions,
    /// granted-power cascade, and PowerStats (which is the whole point — the
    /// reason we need engine integration is so that houseruled feats and
    /// class features apply their stat-add directives and any granted
    /// powers materialize as power cards).
    /// </summary>
    public void AddUserEditPick(RulesElement element, int atLevel = 0, string? slotOwnerInternalId = null)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (string.IsNullOrEmpty(element.InternalId)) return;
        if (_userEditPicks.Any(p =>
                string.Equals(p.Element.InternalId, element.InternalId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(p.SlotOwnerInternalId, slotOwnerInternalId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var fullElement = _findById(element.InternalId) ?? element;
        _userEditPicks.Add(new UserEditPick(fullElement, atLevel, slotOwnerInternalId));
        _userEditPickIds.Add(element.InternalId);
        _wizard.AddFreeGrant(fullElement, atLevel, slotOwnerInternalId);
        InvalidateSnapshot();
        NotifyChanged();
    }

    private void MarkElementHouseruled(string? internalId)
    {
        if (string.IsNullOrWhiteSpace(internalId))
            return;

        IsCharacterHouseruled = true;
        HouseruledElementIds.Add(internalId);
    }

    /// <summary>
    /// InternalIds of elements granted via <see cref="AddUserEditPick"/>.
    /// Used by the exporter to suppress these (and their tree descendants)
    /// from the rebuilt level tree and flat tally so the verbatim
    /// <c>&lt;UserEdit&gt;</c> blocks aren't double-emitted.
    /// </summary>
    public IReadOnlySet<string> UserEditPickIds => _userEditPickIds;

    /// <summary>
    /// InternalIds of elements present in the tree that should NOT be
    /// emitted in the rebuilt <c>&lt;PowerStats&gt;</c> section but should
    /// still appear in the tally. See <see cref="AddGrabbagGrant"/>'s
    /// <c>tallyVestige</c> parameter and
    /// <see cref="CharacterSnapshot.PowerStatsExcludedIds"/>.
    /// </summary>
    public IReadOnlySet<string> PowerStatsExcludedIds => _powerStatsExcludedIds;

    /// <summary>
    /// InternalIds of elements that exist in the level tree for round-trip
    /// fidelity but must NOT appear in the flat
    /// <c>&lt;RulesElementTally&gt;</c> nor in <c>&lt;PowerStats&gt;</c>.
    /// See <see cref="CharacterSnapshot.LevelNestedOnlyIds"/>.
    /// </summary>
    public IReadOnlySet<string> LevelNestedOnlyIds => _levelNestedOnlyIds;

    /// <summary>
    /// Mark an element as level-tree-only: it stays in the per-level
    /// structural output (so the source's <c>&lt;Level&gt;</c> nesting
    /// round-trips) but is suppressed from the rebuilt flat tally and
    /// power cards. Implies <see cref="PowerStatsExcludedIds"/> too.
    /// Mirrors OCB's treatment of Wizard Spellbook entries (powers
    /// stored but not currently prepared) and phantom retrain-noise rows
    /// that the source's flat tally never promoted to active.
    /// </summary>
    public void MarkAsLevelNestedOnly(string internalId)
    {
        if (string.IsNullOrEmpty(internalId)) return;
        _levelNestedOnlyIds.Add(internalId);
        _powerStatsExcludedIds.Add(internalId);
        InvalidateSnapshot();
    }

    /// <summary>
    /// Record a retraining swap to apply at the given character level.
    /// The old element (identified by InternalId) is dropped during build,
    /// the new element is granted in its place. Models the OCB <c>replaces=</c>
    /// attribute on a level-tree RulesElement (e.g., a level-25 power that
    /// retrains an earlier-acquired power).
    /// </summary>
    public void AddReplacement(int level, ElementReplacement replacement)
    {
        if (!_replacements.TryGetValue(level, out var list))
        {
            list = new List<ElementReplacement>();
            _replacements[level] = list;
        }
        list.Add(replacement);
        MarkLevelTreeDirty(level);
        InvalidateSnapshot();
        NotifyChanged();
    }

    /// <summary>
    /// Remove all retraining swaps recorded at <paramref name="level"/>. Symmetric
    /// with <see cref="AddReplacement"/>. Snapshot is invalidated so the next
    /// build re-asserts the originally-acquired elements; the wizard tree
    /// itself doesn't need to be rebuilt because replacements are consumed at
    /// snapshot-build time (see <c>CharacterBuilder.Build</c>), not during
    /// wizard replay. Returns <c>true</c> if a level entry existed.
    /// </summary>
    public bool RemoveReplacement(int level)
    {
        if (!_replacements.Remove(level))
            return false;
        MarkLevelTreeDirty(level);
        InvalidateSnapshot();
        NotifyChanged();
        return true;
    }

    /// <summary>
    /// Remove one specific retraining swap (matched by OldInternalId +
    /// NewInternalId) at <paramref name="level"/>. Returns <c>true</c> if a
    /// matching entry was removed. Use this when a level carries more than
    /// one swap and only one should be cleared; <see cref="RemoveReplacement"/>
    /// (no swap argument) clears the entire level.
    /// </summary>
    public bool RemoveReplacement(int level, ElementReplacement replacement)
    {
        if (!_replacements.TryGetValue(level, out var list))
            return false;

        int idx = list.FindIndex(r =>
            string.Equals(r.OldInternalId, replacement.OldInternalId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.NewInternalId, replacement.NewInternalId, StringComparison.OrdinalIgnoreCase));
        if (idx < 0)
            return false;

        list.RemoveAt(idx);
        if (list.Count == 0)
            _replacements.Remove(level);

        MarkLevelTreeDirty(level);
        InvalidateSnapshot();
        NotifyChanged();
        return true;
    }

    /// <summary>Per-level retraining swaps applied to this session (read-only view).</summary>
    public IReadOnlyDictionary<int, IReadOnlyList<ElementReplacement>> Replacements
        => _replacements.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<ElementReplacement>)kv.Value);

    private IReadOnlyDictionary<int, IReadOnlyList<ElementReplacement>>? GetReplacementsByLevel()
    {
        if (_replacements.Count == 0) return null;
        return _replacements.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<ElementReplacement>)kv.Value);
    }

    /// <summary>
    /// Change the character level. Rebuilds the wizard from scratch.
    /// Decreasing level removes choices made at higher levels.
    /// </summary>
    public void SetLevel(int level)
    {
        if (level < Level)
        {
            // Remove choices that belong to levels above the new level
            _choiceHistory.RemoveAll(r => r.Level > level);
        }
        Level = level;
        RebuildFromHistory();
    }

    /// <summary>
    /// Undo the last user choice. Rebuilds the wizard from scratch
    /// and replays all choices except the last one.
    /// </summary>
    public bool UndoLastChoice()
    {
        if (_choiceHistory.Count == 0)
            return false;

        var removed = _choiceHistory[^1];
        _choiceHistory.RemoveAt(_choiceHistory.Count - 1);
        MarkLevelTreeDirty(removed.Level);
        RebuildFromHistory();
        return true;
    }

    /// <summary>
    /// Remove a specific choice from history and rebuild.
    /// Returns false if the choice was not found.
    /// </summary>
    public bool RemoveChoice(ChoiceRecord record)
    {
        if (!_choiceHistory.Remove(record))
            return false;

        MarkLevelTreeDirty(record.Level);
        RebuildFromHistory();
        return true;
    }

    /// <summary>
    /// Change a specific choice to a different element. Rebuilds the wizard.
    /// </summary>
    public void ChangeChoice(ChoiceRecord oldRecord, RulesElement newElement)
    {
        var idx = _choiceHistory.IndexOf(oldRecord);
        if (idx < 0)
            throw new InvalidOperationException("Choice not found in history.");

        var fullElement = _findById(newElement.InternalId) ?? newElement;
        _choiceHistory[idx] = new ChoiceRecord(fullElement, oldRecord.Slot, oldRecord.SequenceNumber, oldRecord.Level);
        MarkLevelTreeDirty(oldRecord.Level);
        RebuildFromHistory();
    }

    /// <summary>Skip a choice slot (mark it as not-applicable with current sources).</summary>
    public void SkipSlot(ChoiceSlot slot)
    {
        int skipLevel = _wizard.DetermineSlotLevel(slot);
        _wizard.SkipSlot(slot);
        MarkLevelTreeDirty(skipLevel);
        InvalidateSnapshot();
        NotifyChanged();
    }

    /// <summary>
    /// Attempt to apply an element to the first pending slot that currently
    /// accepts it (positional fill, no slot identification).
    /// <para>
    /// <b>Correct usage:</b> caller has no per-pick label or owner context.
    /// Used by the SummaryText importer for blocks whose lines genuinely lack
    /// a disambiguator (RaceClass, Background, BuildSummary, skill list, etc.)
    /// and by test fixtures that intentionally exercise positional fill.
    /// </para>
    /// <para>
    /// <b>Footgun warning:</b> when the caller DOES know which slot the element
    /// should land in (e.g. SummaryText lines with a slot-label prefix, the
    /// .dnd4e tree position, or a UI picker click), use
    /// <see cref="MakeChoice(ChoiceSlot, RulesElement)"/> directly. Positional
    /// fill is silent on misplacement — when two pending slots share an
    /// ElementType, the first one wins regardless of intent.
    /// </para>
    /// </summary>
    public bool TryMakeChoice(RulesElement element, bool skipPrereqs = true)
        => TryMakeChoice(element, parentInternalIdHint: null, skipPrereqs: skipPrereqs);

    /// <summary>
    /// Try to make a choice with a preferred-owner hint. When multiple pending
    /// slots could accept this element, prefer the one whose OwnerInternalId
    /// matches <paramref name="parentInternalIdHint"/>. Used by the wizard
    /// import path so a chosen element gets routed to the right wizard slot
    /// (e.g., Race Ability Bonus → the racial slot, not a generic one).
    /// </summary>
    public bool TryMakeChoice(RulesElement element, string? parentInternalIdHint, bool skipPrereqs = true)
    {
        var pending = GetAllPendingChoices()
            .Where(choice => string.Equals(choice.Slot.ElementType, element.Type, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // When a parent hint is provided, try slots whose owner matches the
        // hint FIRST, but always fall through to the rest. We do this only as
        // an ordering preference — never drop a candidate slot.
        IEnumerable<PendingChoice> ordered = pending;
        if (!string.IsNullOrEmpty(parentInternalIdHint))
        {
            ordered = pending
                .OrderByDescending(p => string.Equals(p.Slot.OwnerInternalId, parentInternalIdHint, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var choice in ordered)
        {
            var candidates = GetCandidatesForSlot(choice.Slot, skipPrereqs: skipPrereqs);
            if (candidates.Any(candidate => string.Equals(candidate.InternalId, element.InternalId, StringComparison.OrdinalIgnoreCase)))
            {
                MakeChoice(choice.Slot, element);
                return true;
            }
        }

        return false;
    }
}
