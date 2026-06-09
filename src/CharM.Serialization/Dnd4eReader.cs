using System.Xml.Linq;
using CharM.Engine.Creation;

namespace CharM.Serialization;

/// <summary>
/// Reads a .dnd4e character file and produces a <see cref="CharacterSnapshot"/>.
/// </summary>
public static class Dnd4eReader
{
    private static string? Attr(XElement element, string name)
        => element.Attribute(name)?.Value;

    private static bool AttrEquals(XElement element, string name, string value)
        => string.Equals(Attr(element, name), value, StringComparison.OrdinalIgnoreCase);

    public static CharacterSnapshot Read(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Read(stream);
    }

    public static async Task<CharacterSnapshot> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var buffered = new MemoryStream();
        await stream.CopyToAsync(buffered, cancellationToken);
        buffered.Position = 0;
        return Read(buffered);
    }

    public static CharacterSnapshot Read(Stream stream)
    {
        var doc = XDocument.Load(stream);
        var root = doc.Root ?? throw new InvalidOperationException("Missing root element.");
        var snapshot = new CharacterSnapshot();

        var charSheet = root.Element("CharacterSheet");

        ParseDetails(charSheet, snapshot);
        ParseAbilityScores(charSheet, snapshot);
        ParseStatBlock(charSheet, snapshot);
        ParseRulesElementTally(charSheet, snapshot);
        ParseLootTally(charSheet, snapshot);
        ParsePowerStats(charSheet, snapshot);
        ParseLevels(root, snapshot);
        ParseTextStrings(root, snapshot);
        CaptureSourceMetadata(root, snapshot);
        CaptureRawSections(root, charSheet, snapshot);
        ParseHouseruleOverlay(root, charSheet, snapshot);

        // Derive level from build choices if not set by Details
        if (snapshot.Level == 0 && snapshot.BuildChoices.Count > 0)
            snapshot.Level = snapshot.BuildChoices.Keys.Max();

        return snapshot;
    }

    private static void ParseDetails(XElement? charSheet, CharacterSnapshot snapshot)
    {
        if (charSheet is null) return;

        // The character name element varies — it can be <Details> or the character's
        // actual name as the element tag (e.g., <Elizabeth>, <Binwin Bronzebottom>).
        // Strategy: look for <Details> first, then fall back to first non-comment child.
        var details = charSheet.Element("Details");
        if (details is not null)
        {
            foreach (var field in details.Elements())
            {
                var key = field.Name.LocalName.Trim();
                var value = field.Value.Trim();
                snapshot.Details[key] = value;
            }

            if (snapshot.Details.TryGetValue("name", out var name))
                snapshot.Name = name;
            if (snapshot.Details.TryGetValue("Level", out var levelText) &&
                int.TryParse(levelText, out var lvl))
                snapshot.Level = lvl;
            return;
        }

        // Fallback: first element child of CharacterSheet is the name element.
        // Its tag IS the character name, and it may contain level/money as text children.
        var firstChild = charSheet.Elements().FirstOrDefault();
        if (firstChild is not null && firstChild.Name.LocalName != "AbilityScores"
            && firstChild.Name.LocalName != "StatBlock")
        {
            snapshot.Name = firstChild.Name.LocalName.Trim();
        }
    }

    private static void ParseAbilityScores(XElement? charSheet, CharacterSnapshot snapshot)
    {
        // Use the AbilityScores section which has PRE-racial, PRE-level-up base scores.
        // The engine will add racial bonuses (from grant chains and tally supplement)
        // and level-up increases (from Level elements with +1 statadds).
        var section = charSheet?.Element("AbilityScores");
        if (section is null) return;

        foreach (var el in section.Elements())
        {
            var score = el.Attribute("score");
            if (score is not null && int.TryParse(score.Value, out var value))
                snapshot.BaseAbilityScores[el.Name.LocalName] = value;
        }
    }

    private static void ParseStatBlock(XElement? charSheet, CharacterSnapshot snapshot)
    {
        var statBlock = charSheet?.Element("StatBlock");
        if (statBlock is null) return;

        foreach (var stat in statBlock.Elements("Stat"))
        {
            // Stats may have name as an attribute OR only as alias children
            var name = stat.Attribute("name")?.Value;
            
            // Fall back to first alias if no name attribute
            if (name is null)
            {
                name = stat.Elements("alias").FirstOrDefault()?.Attribute("name")?.Value;
                if (name is null) continue;
            }

            var valueStr = stat.Attribute("value")?.Value;
            if (!int.TryParse(valueStr, out var value)) continue;

            var expected = new ExpectedStat { Name = name, Value = value };

            foreach (var alias in stat.Elements("alias"))
            {
                var aliasName = alias.Attribute("name")?.Value;
                if (aliasName is not null)
                    expected.Aliases.Add(aliasName);
            }

            foreach (var add in stat.Elements("statadd"))
            {
                expected.Contributions.Add(ParseStatAdd(add));
            }

            snapshot.Stats[name] = expected;
        }
    }

    private static ExpectedStatAdd ParseStatAdd(XElement add)
    {
        int.TryParse(add.Attribute("value")?.Value, out var value);
        int? level = int.TryParse(add.Attribute("Level")?.Value, out var l) ? l : null;

        return new ExpectedStatAdd
        {
            Value = value,
            Type = add.Attribute("type")?.Value,
            Level = level,
            CharElem = add.Attribute("charelem")?.Value,
            StatLink = add.Attribute("statlink")?.Value,
            AbilMod = string.Equals(add.Attribute("abilmod")?.Value, "true", StringComparison.OrdinalIgnoreCase),
            Requires = add.Attribute("requires")?.Value,
            Wearing = add.Attribute("wearing")?.Value,
            NotWearing = add.Attribute("not-wearing")?.Value,
            Condition = add.Attribute("conditional")?.Value,
        };
    }

    private static void ParseRulesElementTally(XElement? charSheet, CharacterSnapshot snapshot)
    {
        var tally = charSheet?.Element("RulesElementTally");
        if (tally is null) return;

        // OCB writes the tally as a flat document-ordered list of every
        // active element, with <RulesElement type="Level" name="N"/>
        // markers between rows. The marker preceding a row is its
        // acquisition level — used to drive retraining-swap level
        // attribution on import (Improved Blood Drinker chains nested
        // deep in the L1 captured subtree are otherwise mis-leveled).
        int? currentLevel = null;
        foreach (var el in tally.Elements("RulesElement"))
        {
            var name = el.Attribute("name")?.Value ?? "";
            var type = el.Attribute("type")?.Value ?? "";

            if (string.Equals(type, "Level", StringComparison.Ordinal)
                && int.TryParse(name, out var lvl))
            {
                currentLevel = lvl;
                // Still emit the Level marker as a TallyElement so the
                // exporter / round-trip path can re-emit it. The
                // exporter is the source of truth for whether to include
                // these on output (it currently does — section-stage S4
                // parity depends on it).
                snapshot.ElementTally.Add(new TallyElement(
                    InternalId: el.Attribute("internal-id")?.Value,
                    Name: name,
                    Type: type,
                    Specifics: null,
                    Url: el.Attribute("url")?.Value,
                    Replaces: el.Attribute("replaces")?.Value,
                    Charelem: el.Attribute("charelem")?.Value,
                    AcquisitionLevel: currentLevel));
                continue;
            }

            var id = el.Attribute("internal-id")?.Value;
            var url = el.Attribute("url")?.Value;
            var replaces = el.Attribute("replaces")?.Value;
            var charelem = el.Attribute("charelem")?.Value;

            Dictionary<string, string>? specifics = null;
            foreach (var spec in el.Elements("specific"))
            {
                var key = spec.Attribute("name")?.Value;
                if (string.IsNullOrEmpty(key)) continue;
                specifics ??= new Dictionary<string, string>(StringComparer.Ordinal);
                specifics[key] = spec.Value ?? "";
            }

            snapshot.ElementTally.Add(new TallyElement(
                id, name, type, specifics, url, replaces,
                ExtraSpecifics: null,
                Charelem: charelem,
                AcquisitionLevel: currentLevel));
        }
    }

    private static void ParseLootTally(XElement? charSheet, CharacterSnapshot snapshot)
    {
        var lootTally = charSheet?.Element("LootTally");
        if (lootTally is null) return;

        foreach (var loot in lootTally.Elements("loot"))
        {
            int count = ParseIntAttr(loot, "count", 1);
            int equipCount = ParseIntAttr(loot, "equip-count", 0);
            bool showPowerCard = ParseIntAttr(loot, "ShowPowerCard", 1) != 0;
            bool isInAlternateSlot = ParseIntAttr(loot, "_AlternateSlot", 0) != 0;
            var compositeName = loot.Attribute("name")?.Value;
            var damageOverride = loot.Attribute("Damage")?.Value;
            var augmentXml = loot.Attribute("augment")?.Value;
            double? weight = null;
            var weightAttr = loot.Attribute("Weight")?.Value;
            if (weightAttr is not null && double.TryParse(weightAttr,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var w))
                weight = w;

            var entry = new LootEntry
            {
                Count = count,
                EquipCount = equipCount,
                ShowPowerCard = showPowerCard,
                CompositeName = compositeName,
                DamageOverride = damageOverride,
                AugmentXml = augmentXml,
                Weight = weight,
                IsInAlternateSlot = isInAlternateSlot,
            };

            foreach (var el in loot.Elements("RulesElement"))
            {
                var name = el.Attribute("name")?.Value ?? "";
                var type = el.Attribute("type")?.Value ?? "";
                var id = el.Attribute("internal-id")?.Value;

                // Worn-state: a nested <RulesElement type="Category" .../> under
                // the base element, e.g. WearingOffHandLightBlade. Capture its
                // InternalId so the writer can re-emit it.
                var wornCategory = el.Elements("RulesElement")
                    .FirstOrDefault(c => string.Equals(c.Attribute("type")?.Value, "Category", StringComparison.OrdinalIgnoreCase));
                var wornId = wornCategory?.Attribute("internal-id")?.Value;

                // Other nested RulesElements are cascaded grants (Class Features,
                // Powers, Racial Traits, etc. produced by the component's select
                // directives — Harper Pin blessing CFs, Echo of Ty'h'kadi
                // Elemental Origin, Firepulse Power racial-power select).
                // Preserve them verbatim for round-trip; we don't re-evaluate
                // the underlying directives.
                var cascades = el.Elements("RulesElement")
                    .Where(c => c != wornCategory)
                    .Select(c => new XElement(c))
                    .ToList();

                entry.Components.Add(new LootComponent(
                    new TallyElement(id, name, type),
                    wornId)
                {
                    CascadedGrants = cascades,
                });
            }
            snapshot.Equipment.Add(entry);
        }
    }

    private static int ParseIntAttr(XElement el, string name, int fallback)
    {
        var v = el.Attribute(name)?.Value;
        return v is not null && int.TryParse(v, out var i) ? i : fallback;
    }

    private static void ParsePowerStats(XElement? charSheet, CharacterSnapshot snapshot)
    {
        var powerStats = charSheet?.Element("PowerStats");
        if (powerStats is null) return;

        foreach (var power in powerStats.Elements("Power"))
        {
            var name = power.Attribute("name")?.Value;
            if (name is null) continue;
            snapshot.PowerStats.Add(new ExpectedPowerStat { Name = name });
        }
    }

    private static void ParseLevels(XElement root, CharacterSnapshot snapshot)
    {
        foreach (var levelContainer in root.Elements("Level"))
        {
            var topRe = levelContainer.Element("RulesElement");
            if (topRe is null) continue;

            var levelName = topRe.Attribute("name")?.Value;
            if (!int.TryParse(levelName, out var levelNum)) continue;

            // Build the structured Level tree — preserves document order, empty
            // placeholders, and replaces metadata. This is what the importer
            // walks for positional alignment against the wizard's slot list.
            var rootNode = ParseImportedNode(topRe);
            var importedLevel = new ImportedLevel { Level = levelNum, Root = rootNode };
            foreach (var loot in levelContainer.Elements("loot"))
                importedLevel.SourceLoot.Add(new XElement(loot));
            snapshot.LevelTrees.Add(importedLevel);

            // Also produce the legacy flat BuildChoices list for the validator
            // and other consumers that don't care about tree position. We
            // intentionally keep this lossless (no dedupe, no IsGranted skip)
            // so it accurately reflects every element present.
            var choices = new List<BuildChoice>();
            FlattenForBuildChoices(topRe, parentId: null, inGrantsChain: false, choices);
            snapshot.BuildChoices[levelNum] = choices;
        }
    }

    private static void ParseTextStrings(XElement root, CharacterSnapshot snapshot)
    {
        foreach (var ts in root.Elements("textstring"))
        {
            var name = ts.Attribute("name")?.Value;
            if (string.IsNullOrEmpty(name)) continue;
            snapshot.TextStrings[name] = ts.Value ?? string.Empty;
        }
    }

    private static ImportedRulesElement ParseImportedNode(XElement element)
    {
        var node = new ImportedRulesElement
        {
            Name = Attr(element, "name") ?? "",
            Type = Attr(element, "type") ?? "",
            InternalId = Attr(element, "internal-id"),
            Replaces = Attr(element, "replaces"),
            Charelem = Attr(element, "charelem"),
            Legality = Attr(element, "legality"),
            SourceElement = new XElement(element),
        };

        foreach (var child in element.Elements("RulesElement"))
            node.Children.Add(ParseImportedNode(child));

        return node;
    }

    private static void FlattenForBuildChoices(
        XElement node,
        string? parentId,
        bool inGrantsChain,
        List<BuildChoice> choices)
    {
        foreach (var child in node.Elements("RulesElement"))
        {
            var name = child.Attribute("name")?.Value ?? "";
            var type = child.Attribute("type")?.Value ?? "";
            var id = child.Attribute("internal-id")?.Value;

            bool isPlaceholder = string.IsNullOrEmpty(name) && string.IsNullOrEmpty(type);

            string? nextParentId = !string.IsNullOrEmpty(id) ? id : parentId;
            bool nextInGrants = inGrantsChain
                || string.Equals(type, "Grants", StringComparison.OrdinalIgnoreCase);

            if (!isPlaceholder)
                choices.Add(new BuildChoice(id, name, type, parentId, inGrantsChain));

            FlattenForBuildChoices(child, nextParentId, nextInGrants, choices);
        }
    }

    /// <summary>
    /// Walk every <c>&lt;RulesElement&gt;</c> in the document (tally, loot,
    /// level tree) and capture per-internal-id metadata we don't otherwise
    /// model: <c>url</c>, <c>charelem</c>, <c>replaces</c>, and child
    /// <c>&lt;specific&gt;</c> values. The exporter uses this to re-emit
    /// attributes and power-card text for elements unchanged since import.
    /// </summary>
    private static void CaptureSourceMetadata(XElement root, CharacterSnapshot snapshot)
    {
        foreach (var re in root.DescendantsAndSelf("RulesElement"))
        {
            var id = Attr(re, "internal-id");
            if (string.IsNullOrEmpty(id))
                continue;

            // Don't overwrite an entry that already has more data than this
            // occurrence (e.g., the tally usually has <specific> children but
            // the level-tree occurrence usually doesn't).
            var url = Attr(re, "url");
            var charelem = Attr(re, "charelem");
            var replaces = Attr(re, "replaces");
            var name = Attr(re, "name");
            var legality = Attr(re, "legality");
            // Some community files emit duplicate <specific name="X"> entries
            // (e.g., two "Short Description" rows). Last-wins keeps export
            // round-trip stable instead of throwing on the duplicate key.
            var specifics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            // Track per-occurrence count of each specific name so the exporter
            // can re-emit legitimate duplicates (Hamadryad/Satyr races carry
            // <specific name="Short Description"> twice). Track the MAX count
            // observed across all occurrences of this internal-id rather than
            // summing — same RulesElement can appear in flat tally AND under a
            // level-tree subtree without our wanting to multiply counts.
            var specificCountsThisOccurrence = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in re.Elements("specific"))
            {
                var sName = Attr(s, "name");
                if (sName is null) continue;
                specifics[sName] = s.Value;
                specificCountsThisOccurrence[sName] =
                    (specificCountsThisOccurrence.TryGetValue(sName, out var c) ? c : 0) + 1;
            }

            if (snapshot.SourceMetadata.TryGetValue(id, out var existing))
            {
                // Merge: prefer non-null/non-empty existing values, but pull in
                // anything new this occurrence has. EXCEPTION: `replaces` is
                // per-occurrence — the flat tally row (first occurrence in
                // document order) has its own replaces value (often empty),
                // while a later LevelTree occurrence may carry replaces="X"
                // for a retraining swap. Mixing the two writes the level-tree
                // replaces onto the tally row when re-exported. Keep the first
                // occurrence's value verbatim (existing.Replaces) and ignore
                // this occurrence's replaces attribute entirely.
                id = existing.InternalId ?? id;
                url ??= existing.Url;
                charelem ??= existing.Charelem;
                replaces = existing.Replaces;
                name ??= existing.Name;
                // Legality is per-occurrence (a level-tree retraining node may
                // carry "houserule" while the flat tally row stays
                // "rules-legal"). First-wins on existing — same rationale as
                // `replaces` above.
                legality = existing.Legality;
                foreach (var (k, v) in existing.Specifics)
                    specifics.TryAdd(k, v);
                // Roll the previously-observed SpecificCounts into this occurrence's
                // running maxes before the metadata object gets replaced below —
                // otherwise the replacement throws away the flat-tally row's count
                // when a later self-closing level-tree occurrence overrides with
                // zeros, losing the duplicate-emission signal for Hamadryad/Satyr.
                foreach (var (k, v) in existing.SpecificCounts)
                {
                    specificCountsThisOccurrence[k] = specificCountsThisOccurrence.TryGetValue(k, out var prev)
                        ? Math.Max(prev, v)
                        : v;
                }
            }

            snapshot.SourceMetadata[id] = new ElementSourceMetadata
            {
                InternalId = id,
                Url = url,
                Charelem = charelem,
                Replaces = replaces,
                Name = name,
                Legality = legality,
            };

            foreach (var (k, v) in specifics)
                snapshot.SourceMetadata[id].Specifics[k] = v;

            // SpecificCounts uses MAX across occurrences (not sum) — the same
            // element appearing self-closing in the level tree shouldn't
            // increment the flat-tally row's expected duplicate count.
            var countsMap = snapshot.SourceMetadata[id].SpecificCounts;
            foreach (var (k, v) in specificCountsThisOccurrence)
                countsMap[k] = countsMap.TryGetValue(k, out var prev) ? Math.Max(prev, v) : v;
        }
    }

    /// <summary>
    /// Capture verbatim XML for sections we don't model so the exporter can
    /// pass them through unmodified. Currently: <c>D20CampaignSetting</c>,
    /// <c>Grabbag</c> (root level), and <c>Companions</c>, <c>Journal</c>,
    /// <c>PowerStats</c> (CharacterSheet level). Structured handling for
    /// these is tracked under separate todos.
    /// </summary>
    private static void CaptureRawSections(XElement root, XElement? charSheet, CharacterSnapshot snapshot)
    {
        string[] rootSections = { "D20CampaignSetting", "Grabbag" };
        foreach (var name in rootSections)
        {
            var el = root.Element(name);
            if (el is not null)
                snapshot.RawSections[name] = new XElement(el);
        }

        // Grabbag also carries a <rules><grant/></rules> block of force-grants
        // (Inherent Bonuses, Spellscarred, etc.). Surface those structurally so
        // the importer can apply them in addition to round-tripping the raw XML.
        var grabbag = root.Element("Grabbag");
        if (grabbag is not null)
        {
            var rules = grabbag.Element("rules");
            if (rules is not null)
            {
                // Build a display-name lookup from the inner pseudo-RE block.
                var nameLookup = new Dictionary<string, (string Name, string Type)>(StringComparer.OrdinalIgnoreCase);
                foreach (var re in grabbag.Descendants("RulesElement"))
                {
                    var iid = (string?)re.Attribute("internal-id");
                    if (string.IsNullOrEmpty(iid)) continue;
                    nameLookup[iid] = ((string?)re.Attribute("name") ?? string.Empty,
                                       (string?)re.Attribute("type") ?? string.Empty);
                }

                foreach (var g in rules.Elements("grant"))
                {
                    var iid = (string?)g.Attribute("name");
                    var type = (string?)g.Attribute("type") ?? string.Empty;
                    if (string.IsNullOrEmpty(iid)) continue;
                    var display = nameLookup.TryGetValue(iid, out var n) ? n.Name : iid;
                    if (string.IsNullOrEmpty(type) && nameLookup.TryGetValue(iid, out var n2))
                        type = n2.Type;
                    snapshot.GrabbagGrants.Add(new TallyElement(iid, display, type));
                }
            }
        }

        if (charSheet is null) return;
        // StatBlock is preserved verbatim by default to mirror OCB's
        // load-as-is / save-as-is behavior for <statadd> rows. OCB only
        // gates new statadd insertions on `requires` at directive-process
        // time (D20RulesEngine_ExecuteRulesStatement case 1); the file-load
        // path (ParseStatAdd, -Module-.cs line 10219) pushes every <statadd>
        // verbatim with no `requires` check, and SaveStats writes the list
        // back unchanged. Stale-but-inactive statadds (e.g. War Wizard's
        // Expertise arena-group rows on chars without an arena weapon) thus
        // persist across save cycles. Regenerating from directives produces
        // a different set than the source, breaking round-trip parity, so
        // we preserve the verbatim block when one was captured.
        string[] csSections = { "StatBlock", "Companions", "Journal", "PowerStats" };
        foreach (var name in csSections)
        {
            var el = charSheet.Element(name);
            if (el is not null)
                snapshot.RawSections[name] = new XElement(el);
        }
    }

    /// <summary>
    /// Parse the OCB houserule overlay (Forms A + B + C; Form D auto-passes
    /// through <see cref="CaptureRawSections"/> via <c>D20CampaignSetting</c>).
    ///
    /// <para>Form A: each <c>&lt;Level&gt;/&lt;UserEdit&gt;</c> block is captured
    /// verbatim per level. The wrapper subtree contains picks plus their
    /// cascade-tagged grant chains; we walk every descendant
    /// <c>&lt;RulesElement&gt;</c> (skipping the empty wrapper itself) and
    /// stash them in <see cref="HouseruleOverlay.FormATallyMirror"/> for tally
    /// re-emission (Form C).</para>
    ///
    /// <para>Form B: tally rows with <c>legality="houserule"</c> whose
    /// (internal-id, charelem) is NOT present in any captured UserEdit subtree
    /// are preserved as <see cref="HouseruleOverlay.LegacyTallyRows"/>.</para>
    ///
    /// <para>Sets <see cref="HouseruleOverlay.IsCharacterHouseruled"/> when any
    /// Form A or Form B picks land — drives the legality cascade on
    /// <c>D20Character</c> and <c>AbilityScores</c> at export time.</para>
    /// </summary>
    private static void ParseHouseruleOverlay(XElement root, XElement? charSheet, CharacterSnapshot snapshot)
    {
        var overlay = snapshot.Houserules;

        // Index every (id, charelem) found inside any <Level>'s top
        // RulesElement subtree. Picks that live in the level tree (even if
        // legality-tagged "houserule" because of prereq bypass) are processed
        // by the engine via the normal grant chain — we must NOT also re-emit
        // them from Form A/B paths or we'd double-count.
        var levelTreeKeys = new HashSet<(string, string)>();
        foreach (var levelEl in root.Elements("Level"))
        {
            var topRe = levelEl.Element("RulesElement");
            if (topRe is null) continue;
            foreach (var inner in topRe.DescendantsAndSelf("RulesElement"))
            {
                levelTreeKeys.Add(
                    (Attr(inner, "internal-id") ?? "",
                     Attr(inner, "charelem") ?? ""));
            }
        }

        // Also collect keys from the <LootTally> subtree. Loot-nested rules
        // elements (Ring of Borrowed Spells → Vital Spell, Harper Pin →
        // Lliira's Grace, etc.) are restored by the engine's loot cascade
        // and re-emitted in the rebuilt flat tally, so a Form B legacy
        // row mirroring the same (id, charelem) would double-emit.
        var lootTally = charSheet?.Element("LootTally");
        if (lootTally is not null)
        {
            foreach (var inner in lootTally.Descendants("RulesElement"))
            {
                levelTreeKeys.Add(
                    (Attr(inner, "internal-id") ?? "",
                     Attr(inner, "charelem") ?? ""));
            }
        }

        // Form A: capture <UserEdit> per Level. Index every (id, charelem) of
        // descendants for cross-matching tally rows below.
        var formAKeys = new HashSet<(string, string)>();
        // Dedup FormATallyMirror entries by internal-id across the whole
        // character. OCB emits each unique element ONCE in the flat tally
        // even when the same element appears in multiple UserEdit cascades
        // (e.g. sm_art-shyr has Arcane Admixture II AND Arcane Admixture III
        // — two feats whose Power select Existing=True both reference Magic
        // Weapon, so MW appears as a cascade child under both UserEdits with
        // different charelems). Without this dedup we'd mirror MW twice,
        // producing one extra flat-tally row.
        var mirroredIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var levelEl in root.Elements("Level"))
        {
            var topRe = levelEl.Element("RulesElement");
            if (topRe is null) continue;
            if (!int.TryParse(Attr(topRe, "name"), out var levelNum)) continue;

            var userEdits = levelEl.Elements("UserEdit").ToList();
            if (userEdits.Count == 0) continue;

            foreach (var ue in userEdits)
            {
                var clone = new XElement(ue);
                if (!overlay.LevelUserEdits.TryGetValue(levelNum, out var list))
                {
                    list = new List<XElement>();
                    overlay.LevelUserEdits[levelNum] = list;
                }
                list.Add(clone);

                foreach (var wrapper in ue.Elements("RulesElement"))
                {
                    foreach (var inner in wrapper.Descendants("RulesElement"))
                    {
                        var iid = Attr(inner, "internal-id") ?? "";
                        var ce  = Attr(inner, "charelem") ?? "";
                        formAKeys.Add((iid, ce));

                        var name = Attr(inner, "name") ?? "";
                        var type = Attr(inner, "type") ?? "";
                        if (name.Length == 0 && type.Length == 0) continue;

                        // Don't mirror to tally if the engine will already
                        // produce this entry from the Level tree.
                        if (levelTreeKeys.Contains((iid, ce))) continue;

                        // Dedup by internal-id across the entire character —
                        // see mirroredIds comment above.
                        if (iid.Length > 0 && !mirroredIds.Add(iid)) continue;

                        // Clone the inner WITHOUT its nested RulesElement
                        // children. OCB's flat tally lists each UserEdit pick
                        // as a standalone top-level row (only <specific>
                        // children allowed). The cascade hierarchy lives in
                        // the verbatim <UserEdit> block and in the Level
                        // subtree under the picked element — emitting the
                        // nested children here would double-count the inner
                        // picks (which we also visit as separate descendants
                        // in this loop) and produce nested rows in the flat
                        // tally where source has flat siblings.
                        var mirrorClone = new XElement(inner);
                        mirrorClone.Elements("RulesElement").Remove();
                        overlay.FormATallyMirror.Add(mirrorClone);
                    }
                    formAKeys.Add(
                        (Attr(wrapper, "internal-id") ?? "",
                         Attr(wrapper, "charelem") ?? ""));
                }
            }
        }

        // Form B: tally rows tagged legality="houserule" whose (id, charelem)
        // is NOT inside any captured UserEdit subtree AND NOT anywhere in the
        // Level tree. Pure legacy inline houserule entries (RaidonKane2-style
        // and free-Expertise houserules from pre-houserule-system files).
        var tally = charSheet?.Element("RulesElementTally");
        if (tally is not null)
        {
            foreach (var re in tally.Elements("RulesElement"))
            {
                if (!AttrEquals(re, "legality", "houserule")) continue;
                var key = (Attr(re, "internal-id") ?? "",
                           Attr(re, "charelem") ?? "");
                if (formAKeys.Contains(key)) continue;
                if (levelTreeKeys.Contains(key)) continue;
                overlay.LegacyTallyRows.Add(new XElement(re));
            }
        }

        overlay.IsCharacterHouseruled =
            overlay.LevelUserEdits.Count > 0 || overlay.LegacyTallyRows.Count > 0;

        // Capture the set of every element tagged legality="houserule" anywhere
        // in the source file. Used by the writer to preserve per-element
        // legality on round-trip (e.g. a feat the user took without meeting
        // prereqs stays tagged "houserule" instead of being silently promoted
        // to "rules-legal"). Walking from the document root catches Level-tree
        // entries, tally rows, and UserEdit subtree picks in one pass.
        foreach (var re in root.DescendantsAndSelf("RulesElement"))
        {
            if (!AttrEquals(re, "legality", "houserule")) continue;
            var iid = Attr(re, "internal-id");
            if (!string.IsNullOrEmpty(iid))
                overlay.HouseruledElementIds.Add(iid);
        }
    }
}
