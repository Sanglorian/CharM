using CharM.Engine.CharacterModel;
using CharM.Engine.Creation;
using CharM.Engine.Orchestration;
using CharM.Engine.Rules;
using CharM.Engine.Selection;
using CharM.RulesDb.Storage;

namespace CharM.ImportExport.SummaryText.Blocks;

/// <summary>
/// "POWERS\r\n&lt;Source&gt;[ Spellbook]: &lt;Power&gt;[ (replaces &lt;Old&gt;)]\r\n...".
/// See decompiled <c>PowerSummary.cs</c>.
///
/// The source label is derived from the power's own Class + Power Usage +
/// Level fields (most powers carry these). For hybrid characters whose
/// class id matches the power's class, we substitute "Hybrid" for the
/// class name. Falls back to the slot owner's name for powers without
/// usage/level fields (hybrid swap powers like Improved Blood Drinker;
/// feat-granted powers like Telekinetic Grasp via Wild Talent Master).
/// </summary>
internal sealed class PowerSummaryBlock : SummaryBlock
{
    public const string Header = "POWERS";

    public override string Write(CharacterSession session, IRulesDatabase database)
    {
        // PRIMARY PATH: ChoiceHistory + Replacements. The engine state
        // object is now the canonical source of truth — the importer
        // populates Replacements with correct per-swap levels via the
        // tally acquisition-level map (Dnd4eImporter.ResolveSwapLevel).
        // This path also works correctly for UI-built characters that
        // have no captured XML.
        //
        // FALLBACK PATH: captured-tree walker. Used only for legacy
        // session paths where Replacements hasn't been populated — kept
        // as a safety net to avoid silent loss of swap rows.
        return WriteFromChoiceHistory(session, database);
    }

    /// <summary>
    /// Walk <see cref="CharacterSession.CapturedLevelTrees"/> in level
    /// order and emit a POWERS line for every <c>&lt;RulesElement
    /// type="Power" ...&gt;</c> we encounter. Mirrors how OCB itself
    /// reads the level tree on save (see decompiled
    /// <c>D20RulesEngine -Module-.cs WriteLevel</c>) — the source XML is
    /// authoritative, including stale-state intermediate retrain links.
    ///
    /// Powers that were AUTO-GRANTED by a feature (Shield Adept's
    /// Shield Wall, Eternal Defender's Implacable Destruction) are
    /// filtered out — OCB only surfaces user picks. We classify "user
    /// pick" as any captured power whose <c>internal-id</c> appears
    /// either in <see cref="CharacterSession.ChoiceHistory"/> OR as the
    /// OLD/NEW side of a <see cref="CharacterSession.Replacements"/>
    /// entry. Retraining swaps always have <c>replaces=</c> set so they
    /// match through the Replacements set even when the latest engine
    /// state has dropped the intermediate link.
    /// </summary>
    private static string WriteFromCapturedTrees(CharacterSession session, IRulesDatabase database)
    {
        var userPickIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rec in session.ChoiceHistory)
        {
            if (string.Equals(rec.Element.Type, "Power", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(rec.Element.InternalId))
                userPickIds.Add(rec.Element.InternalId);
        }
        foreach (var (_, list) in session.Replacements)
        {
            foreach (var rep in list)
            {
                if (!string.IsNullOrEmpty(rep.OldInternalId)) userPickIds.Add(rep.OldInternalId);
                if (!string.IsNullOrEmpty(rep.NewInternalId)) userPickIds.Add(rep.NewInternalId);
            }
        }

        // Build a charelem -> (InternalId, Name) map across all captured
        // trees so `replaces=` attributes can resolve to the original
        // element's name regardless of which level it was acquired in.
        var charelemMap = new Dictionary<string, (string InternalId, string Name)>(StringComparer.Ordinal);
        foreach (var (_, tree) in session.CapturedLevelTrees)
        {
            foreach (var re in tree.DescendantsAndSelf("RulesElement"))
            {
                string? charelem = re.Attribute("charelem")?.Value;
                string? iid = re.Attribute("internal-id")?.Value;
                string name = re.Attribute("name")?.Value ?? string.Empty;
                if (string.IsNullOrEmpty(charelem) || string.IsNullOrEmpty(iid)) continue;
                charelemMap[charelem!] = (iid!, name);
            }
        }

        // Walk parent chain to find the nearest non-Level ancestor and
        // classify whether the swap-OWNER is a Feat. Feat-granted swaps
        // (Thirst for Blood -> Blood Drinker) are suppressed because the
        // feat itself is in the FEATS section.
        bool OwnerIsFeat(System.Xml.Linq.XElement powerNode)
        {
            var parent = powerNode.Parent;
            while (parent is not null && parent.Name == "RulesElement")
            {
                string? ownerType = parent.Attribute("type")?.Value;
                if (string.Equals(ownerType, "Level", StringComparison.Ordinal))
                {
                    parent = parent.Parent;
                    continue;
                }
                return string.Equals(ownerType, "Feat", StringComparison.Ordinal);
            }
            return false;
        }

        var lines = new List<(int Level, int Order, string Text)>();
        int order = 0;

        foreach (var (level, tree) in session.CapturedLevelTrees.OrderBy(kv => kv.Key))
        {
            foreach (var powerRe in tree.DescendantsAndSelf("RulesElement")
                .Where(re => string.Equals(re.Attribute("type")?.Value, "Power", StringComparison.Ordinal)))
            {
                string? iid = powerRe.Attribute("internal-id")?.Value;
                if (string.IsNullOrEmpty(iid)) continue;

                // Skip nested Power children (e.g. Reality Meltdown Attack
                // under Reality Meltdown) — only the top-level power
                // appears in the POWERS section.
                bool nestedUnderPower = false;
                var ancestor = powerRe.Parent;
                while (ancestor is not null && ancestor.Name == "RulesElement")
                {
                    if (string.Equals(ancestor.Attribute("type")?.Value, "Power", StringComparison.Ordinal))
                    { nestedUnderPower = true; break; }
                    ancestor = ancestor.Parent;
                }
                if (nestedUnderPower) continue;

                // Suppress feat-OWNED SWAPS only (e.g. Thirst for Blood
                // deterministically replaces Serve Me Well with Blood
                // Drinker — implicit in the feat itself). Feat-owned
                // USER PICKS (Wild Talent Master grants 3 user-chosen
                // cantrips; Fey Cantrip grants one chosen cantrip) must
                // surface — those are real picks with the feat name as
                // the source label.
                bool hasReplaces = !string.IsNullOrEmpty(powerRe.Attribute("replaces")?.Value);
                if (hasReplaces && OwnerIsFeat(powerRe)) continue;

                // User-pick filter: surface only powers that exist in our
                // engine state as user picks. Retrain rows always have
                // replaces= set; those map through Replacements.
                if (!userPickIds.Contains(iid!)) continue;

                var powerEl = database.FindByInternalId(iid!);
                if (powerEl is null) continue;

                // Resolve the slot owner (nearest non-Level ancestor) so
                // SlotLabel can produce "Wild Talent Master" /
                // "Hybrid Power Point Option" / etc.
                string? ownerInternalId = null;
                var walk = powerRe.Parent;
                while (walk is not null && walk.Name == "RulesElement")
                {
                    string? wt = walk.Attribute("type")?.Value;
                    string? wid = walk.Attribute("internal-id")?.Value;
                    if (!string.Equals(wt, "Level", StringComparison.Ordinal) && !string.IsNullOrEmpty(wid))
                    {
                        ownerInternalId = wid;
                        break;
                    }
                    walk = walk.Parent;
                }
                var slot = ownerInternalId is null
                    ? null
                    : new ChoiceSlot { ElementType = "Power", OwnerInternalId = ownerInternalId };

                // `replaces="X"` -> old element name via the charelem map.
                string? oldName = null;
                string? repCharelem = powerRe.Attribute("replaces")?.Value;
                if (!string.IsNullOrEmpty(repCharelem) && charelemMap.TryGetValue(repCharelem!, out var oldInfo))
                    oldName = oldInfo.Name;

                string text = FormatLine(session, database, powerEl, level, oldName, slot);
                lines.Add((level, order++, text));
            }
        }

        if (lines.Count == 0) return $"{Header}{Newline}{Newline}";

        var sb = new System.Text.StringBuilder();
        sb.Append(Header).Append(Newline);
        foreach (var (_, _, text) in lines)
            sb.Append(text);
        return sb.ToString();
    }

    /// <summary>
    /// Fallback path used when the session has no captured-XML trees
    /// (characters built from scratch via the wizard rather than imported
    /// from a .dnd4e file). Derives originals from ChoiceHistory + swap
    /// lines from <see cref="CharacterSession.Replacements"/>.
    /// </summary>
    private static string WriteFromChoiceHistory(CharacterSession session, IRulesDatabase database)
    {
        var powers = session.ChoiceHistory
            .Where(r => string.Equals(r.Element.Type, "Power", StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.Level)
            .ThenBy(r => r.SequenceNumber)
            .ToList();

        var swapsByLevel = new SortedDictionary<int, List<(string OldId, string NewId, string OldName, ChoiceSlot? Slot)>>();
        foreach (var (level, list) in session.Replacements.OrderBy(kv => kv.Key))
        {
            foreach (var rep in list)
            {
                if (string.IsNullOrEmpty(rep.OldInternalId) || string.IsNullOrEmpty(rep.NewInternalId))
                    continue;
                if (IsFeatGrantedSwap(rep, database))
                    continue;
                var oldEl = database.FindByInternalId(rep.OldInternalId);
                if (oldEl is null) continue;
                if (!swapsByLevel.TryGetValue(level, out var bucket))
                { bucket = new(); swapsByLevel[level] = bucket; }

                // Slot for the swap row:
                //   1) Owner's matching ReplaceDirective (Name carries the
                //      authoritative slot label — "Hybrid At-Will/Encounter
                //      13"). Pair by directive.Level == this swap's level.
                //   2) Trace the OLD element's original ChoiceSlot
                //      (preserves compound usage when the owner has no
                //      directive metadata for THIS level).
                //   3) Synthetic slot with owner only.
                ChoiceSlot? swapSlot = TryResolveDirectiveSlot(database, rep.SwapOwnerInternalId, level);
                if (swapSlot is null)
                {
                    ChoiceSlot? originalSlot = TraceOriginalSlot(session, rep.OldInternalId!);
                    if (originalSlot is not null && IsFeatureGrantedSlot(originalSlot, database))
                        originalSlot = null;
                    swapSlot = originalSlot
                        ?? (rep.SwapOwnerInternalId is null
                            ? null
                            : new ChoiceSlot { ElementType = "Power", OwnerInternalId = rep.SwapOwnerInternalId });
                }
                bucket.Add((rep.OldInternalId, rep.NewInternalId, oldEl.Name, swapSlot));
            }
        }

        if (powers.Count == 0 && swapsByLevel.Count == 0) return $"{Header}{Newline}{Newline}";

        var sb = new System.Text.StringBuilder();
        sb.Append(Header).Append(Newline);

        var lines = new List<(int Level, int Order, string Text)>();
        int origIdx = 0;
        foreach (var record in powers)
        {
            string line = FormatLine(session, database, record.Element, record.Level, oldName: null, record.Slot);
            lines.Add((record.Level, origIdx++, line));
        }

        foreach (var (level, list) in swapsByLevel)
        {
            int swapIdx = 0;
            foreach (var (_, newId, oldName, swapSlot) in list)
            {
                var newEl = database.FindByInternalId(newId);
                if (newEl is null) continue;
                string line = FormatLine(session, database, newEl, level, oldName, swapSlot);
                lines.Add((level, 1_000_000 + swapIdx++, line));
            }
        }

        foreach (var (_, _, text) in lines.OrderBy(t => t.Level).ThenBy(t => t.Order))
            sb.Append(text);
        return sb.ToString();
    }

    private static string FormatLine(
        CharacterSession session,
        IRulesDatabase database,
        RulesElement powerEl,
        int characterLevel,
        string? oldName,
        ChoiceSlot? slot)
    {
        string source = ResolvePowerSource(session, database, powerEl, characterLevel, slot);
        var sb = new System.Text.StringBuilder();
        sb.Append(source).Append(": ").Append(powerEl.Name);
        if (!string.IsNullOrEmpty(oldName))
            sb.Append(" (replaces ").Append(oldName).Append(')');
        sb.Append(Newline);
        return sb.ToString();
    }

    /// <summary>
    /// True when the swap's owning element is a Feat — those swaps are
    /// automatic consequences of taking the feat (e.g. Thirst for Blood
    /// replaces Serve Me Well with Blood Drinker) and OCB doesn't surface
    /// them in SummaryText. The feat itself appears in the Feats section.
    /// </summary>
    /// <summary>
    /// Walk back through <see cref="CharacterSession.Replacements"/> to
    /// find the ORIGINAL pick whose slot started this retrain chain, and
    /// return its <see cref="ChoiceRecord.Slot"/>. Used so the swap
    /// line uses the same source label as the original pick
    /// (e.g. "Hybrid at-will/encounter 17" for chained IBD retrains
    /// of an original Kinetic Buffer pick made via the
    /// "Hybrid At-Will/Encounter 7" slot). Returns null when nothing
    /// in ChoiceHistory matches.
    /// </summary>
    private static ChoiceSlot? TraceOriginalSlot(CharacterSession session, string oldInternalId)
    {
        const int safety = 8;
        string id = oldInternalId;
        for (int i = 0; i < safety; i++)
        {
            var record = session.ChoiceHistory.FirstOrDefault(r =>
                string.Equals(r.Element.InternalId, id, StringComparison.OrdinalIgnoreCase));
            if (record is not null) return record.Slot;

            // Chain through: OLD might itself be the NEW of an earlier swap.
            var earlier = session.Replacements
                .SelectMany(kv => kv.Value)
                .FirstOrDefault(rep =>
                    string.Equals(rep.NewInternalId, id, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(rep.OldInternalId));
            if (earlier is null) return null;
            id = earlier.OldInternalId;
        }
        return null;
    }

    private static bool IsFeatGrantedSwap(ElementReplacement rep, IRulesDatabase database)
    {
        if (string.IsNullOrEmpty(rep.SwapOwnerInternalId)) return false;
        var owner = database.FindByInternalId(rep.SwapOwnerInternalId);
        return owner is not null
            && string.Equals(owner.Type, "Feat", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Find a <see cref="ReplaceDirective"/> on the owner element whose
    /// <c>Level</c> matches <paramref name="level"/> and produce a synthetic
    /// <see cref="ChoiceSlot"/> carrying the directive's <c>Name</c> /
    /// <c>Level</c>. This is the authoritative source-label signal for
    /// retrain swaps — the owner element's ReplaceDirective.Name is what
    /// the OCB uses ("Hybrid At-Will/Encounter 13") rather than tracing
    /// the OLD element's original slot which would inherit the prior
    /// slot's possibly-simpler label.
    /// </summary>
    private static ChoiceSlot? TryResolveDirectiveSlot(
        IRulesDatabase database, string? ownerInternalId, int level)
    {
        if (string.IsNullOrEmpty(ownerInternalId)) return null;
        var owner = database.FindByInternalId(ownerInternalId);
        if (owner is null) return null;
        foreach (var rd in owner.Rules.OfType<ReplaceDirective>())
        {
            if (!rd.Level.HasValue || rd.Level.Value != level) continue;
            if (string.IsNullOrEmpty(rd.Name)) continue;
            return new ChoiceSlot
            {
                ElementType = "Power",
                OwnerInternalId = ownerInternalId,
                Name = rd.Name,
                Level = rd.Level,
            };
        }
        return null;
    }

    /// <summary>
    /// Build the OCB source label for a power. Three patterns:
    ///   A. Standard class-power slot (Level-owned): use class + usage +
    ///      character-level from the power's own fields.
    ///   B. Augmentable power slot (Internal-owned, slot name like
    ///      "Hybrid At-Will/Encounter 7"): preserve the compound usage
    ///      from the slot name, append the character level.
    ///   C. Feature-granted power (Feat or Class Feature owned): use the
    ///      owner element's name verbatim.
    /// </summary>
    private static string ResolvePowerSource(
        CharacterSession session,
        IRulesDatabase database,
        RulesElement power,
        int characterLevel,
        ChoiceSlot? slot)
    {
        // Pattern C: feature-granted (Feat / Class Feature owner).
        if (slot is not null && IsFeatureGrantedSlot(slot, database))
        {
            if (!string.IsNullOrEmpty(slot.OwnerInternalId))
            {
                var owner = database.FindByInternalId(slot.OwnerInternalId);
                if (owner is not null && !string.IsNullOrWhiteSpace(owner.Name))
                    return owner.Name;
            }
        }

        // Pattern B: augmentable power slot — slot.Name carries the
        // compound usage ("Hybrid At-Will/Encounter 7").
        if (slot is not null && TryFormatAugmentableSlot(slot, characterLevel, out var augLabel))
            return augLabel!;

        // Pattern A: standard class-power slot — derive from power fields.
        var dbEl = database.FindByInternalId(power.InternalId ?? "") ?? power;

        bool isHybridChar = session.GetSelectedElements("Hybrid Class").Count > 0;
        HashSet<string> hybridClassIds = isHybridChar
            ? CharM.Engine.Selection.SelectVariables.GetActiveClassIds(
                (session.GetSnapshot() ?? session.GetPartialSnapshot())!.Builder.ElementTree)
            : new HashSet<string>();

        string className = ResolveClassName(database, dbEl, hybridClassIds);
        if (string.IsNullOrEmpty(className))
            className = ResolveCharacterClassLabel(session);

        string usage = string.Empty;
        if (dbEl.Fields.TryGetValue("Power Type", out var ptype)
            && string.Equals(ptype, "Utility", StringComparison.OrdinalIgnoreCase))
        {
            usage = "utility";
        }
        else if (dbEl.Fields.TryGetValue("Power Usage", out var pusage) && !string.IsNullOrWhiteSpace(pusage))
        {
            usage = pusage.ToLowerInvariant();
        }

        if (!string.IsNullOrEmpty(className) && !string.IsNullOrEmpty(usage))
            return $"{className} {usage} {characterLevel}";

        if (dbEl.Fields.TryGetValue("Display", out var disp) && !string.IsNullOrWhiteSpace(disp))
            return disp;

        return $"Level {characterLevel}";
    }

    /// <summary>
    /// Detect and format augmentable-power slots: slot name like
    /// "Hybrid At-Will/Encounter 7" → "Hybrid at-will/encounter &lt;charLvl&gt;".
    /// Only triggers when the slot name contains a compound usage
    /// (slash-separated) — the simpler "Hybrid Daily 5" case is handled
    /// by the class+usage+level path below.
    /// </summary>
    private static bool TryFormatAugmentableSlot(ChoiceSlot slot, int characterLevel, out string? label)
    {
        label = null;
        // Augmentable-power slots ("Hybrid At-Will/Encounter 7") consistently
        // carry the compound label in the `name=` attribute. Display label
        // takes precedence when set, falling back to Name.
        var name = slot.DisplayLabel ?? slot.Name;
        if (string.IsNullOrEmpty(name)) return false;
        if (!name.Contains('/')) return false;

        var parts = name.Split(' ');
        if (parts.Length < 2) return false;
        if (!int.TryParse(parts[^1], out _)) return false;

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (i > 0) sb.Append(' ');
            string p = parts[i];
            sb.Append(IsCompoundUsage(p) ? p.ToLowerInvariant() : p);
        }
        sb.Append(' ').Append(characterLevel);
        label = sb.ToString();
        return true;
    }

    private static bool IsCompoundUsage(string s)
    {
        if (!s.Contains('/')) return false;
        foreach (var part in s.Split('/'))
        {
            if (!IsUsageWord(part)) return false;
        }
        return true;
    }

    private static bool IsUsageWord(string s)
        => s.Equals("At-Will", StringComparison.OrdinalIgnoreCase)
        || s.Equals("Encounter", StringComparison.OrdinalIgnoreCase)
        || s.Equals("Daily", StringComparison.OrdinalIgnoreCase)
        || s.Equals("Utility", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when the slot was created by a non-class-power source —
    /// typically a Feat, a granting Class Feature, or an Internal
    /// option-decider. Class-power slots have slot names like
    /// "Power At-Will 1" / "Power Daily 5" / "Hybrid At-Will/Encounter 7"
    /// with category "$$CLASS,...". Feature-granted slots either have NO
    /// slot name (Wild Talent Master / Fey Cantrip) or have an owner that
    /// isn't a Level / Class element.
    /// </summary>
    private static bool IsFeatureGrantedSlot(ChoiceSlot slot, IRulesDatabase database)
    {
        // No owner → can't derive feature-grant label.
        if (string.IsNullOrEmpty(slot.OwnerInternalId)) return false;

        var owner = database.FindByInternalId(slot.OwnerInternalId);
        if (owner is null) return false;

        // OCB convention (verified against the round-2 fixtures): use the
        // owner element's name as the source label when the owner is a
        // named selectable feature the user is aware of:
        //   - Feat (Wild Talent Master, Fey Cantrip)
        //   - Class Feature (Hybrid Power Point Option, Werebear)
        // Use class + usage + level when the owner is structural:
        //   - Level (normal class-power slots)
        //   - Class / Hybrid Class (Race ability bonuses, build options)
        //   - Internal (Psionic Augmentation (Hybrid) decider, etc.)
        //   - Racial Trait, Theme, etc.
        return owner.Type switch
        {
            "Feat" or "Class Feature" => true,
            _ => false,
        };
    }

    private static string ResolveClassName(IRulesDatabase database, RulesElement power, HashSet<string> hybridClassIds)
    {
        if (!power.Fields.TryGetValue("Class", out var classId) || string.IsNullOrWhiteSpace(classId))
            return string.Empty;

        if (hybridClassIds.Count > 0 && hybridClassIds.Contains(classId))
            return "Hybrid";

        var classEl = database.FindByInternalId(classId);
        if (classEl is null) return string.Empty;

        // Only return the resolved name when it's an actual Class or
        // Hybrid Class element. Powers granted by Themes (Werebear's
        // Enraged Bear, classID points at the Theme), Skill Powers
        // (classID at Skill element), and Racial Traits all set Class
        // to the granting element's id. OCB ignores those and labels by
        // the character's actual class — see ResolvePowerSource fallback.
        if (string.Equals(classEl.Type, "Class", StringComparison.OrdinalIgnoreCase)
            || string.Equals(classEl.Type, "Hybrid Class", StringComparison.OrdinalIgnoreCase))
        {
            return classEl.Name;
        }
        return string.Empty;
    }

    /// <summary>
    /// Fallback when a power has no <c>Class</c> field (skill powers like
    /// Far Sight, Confusing Blather, Improvisational Arcana). OCB labels
    /// these with the CHARACTER's class, not the skill the power keys off.
    /// </summary>
    private static string ResolveCharacterClassLabel(CharacterSession session)
    {
        if (session.GetSelectedElements("Hybrid Class").Count > 0)
            return "Hybrid";
        return session.GetSelectedElement("Class")?.Name ?? string.Empty;
    }


    /// <summary>
    /// Lowercase the usage marker ("Daily" → "daily"), strip a trailing
    /// "Attack" / "Utility Attack", and preserve compound markers like
    /// "At-Will/Encounter". Examples:
    ///   "Fighter Daily Attack 1"   → "Fighter daily 1"
    ///   "Hybrid Utility 2"         → "Hybrid utility 2"
    ///   "Hybrid At-Will/Encounter Attack 13" → "Hybrid at-will/encounter 13"
    ///   "Wild Talent Master"       → "Wild Talent Master" (no number)
    /// </summary>
    private static string NormalizeSlotOwnerName(string raw)
    {
        // Strip " Attack" tokens but preserve "At-Will/Encounter"-style compounds.
        var sb = new System.Text.StringBuilder();
        bool lowered = false;
        foreach (var part in raw.Split(' '))
        {
            if (string.IsNullOrEmpty(part)) continue;
            string s = part;
            if (s.Equals("Attack", StringComparison.Ordinal)) continue;

            // Lowercase known usage tokens — but only once per source label
            // (so "Wizard" stays capitalized, "Daily" becomes "daily").
            if (IsUsageToken(s) && !lowered)
            {
                s = s.ToLowerInvariant();
                lowered = true;
            }

            if (sb.Length > 0) sb.Append(' ');
            sb.Append(s);
        }
        return sb.ToString();
    }

    private static bool IsUsageToken(string s)
    {
        if (s.Contains('/'))
        {
            // Compound like At-Will/Encounter — lowercase the whole compound.
            return s.Split('/').All(IsUsageToken);
        }
        return s.Equals("At-Will", StringComparison.OrdinalIgnoreCase)
            || s.Equals("Encounter", StringComparison.OrdinalIgnoreCase)
            || s.Equals("Daily", StringComparison.OrdinalIgnoreCase)
            || s.Equals("Utility", StringComparison.OrdinalIgnoreCase);
    }

    public override bool TryRead(CharacterSession session, IRulesDatabase database, ref string input)
    {
        if (input.StartsWith(Header + Newline, StringComparison.Ordinal))
        {
            input = input[(Header.Length + Newline.Length)..];
            return true;
        }

        int colon = input.IndexOf(": ", StringComparison.Ordinal);
        int nl = input.IndexOf(Newline, StringComparison.Ordinal);
        if (colon == -1 || nl == -1 || colon > nl) return false;

        string source = input[..colon];
        // Source heuristic: contains a lowercased "at-will", "encounter",
        // "daily", "utility" OR is a known marker.
        if (!LooksLikePowerSource(source))
            return false;

        string rest = input[(colon + 2)..nl];
        RemoveRetraining(ref rest, out _, out _);
        _ = RemoveReplaces(ref rest);

        string powerName = rest.Trim();
        var power = database.FindByNameAndType(powerName, "Power");
        if (power is null) return false;

        SessionReplayHelpers.PlacePositionally(session, power);
        input = input[(nl + Newline.Length)..];
        return true;
    }

    private static bool LooksLikePowerSource(string s)
    {
        if (s.Contains("at-will", StringComparison.OrdinalIgnoreCase)) return true;
        if (s.Contains("encounter", StringComparison.OrdinalIgnoreCase)) return true;
        if (s.Contains("daily", StringComparison.OrdinalIgnoreCase)) return true;
        if (s.Contains("utility", StringComparison.OrdinalIgnoreCase)) return true;
        if (s.Contains("spellbook", StringComparison.OrdinalIgnoreCase)) return true;
        if (s.StartsWith("Level ", StringComparison.Ordinal)) return true;
        return false;
    }
}
