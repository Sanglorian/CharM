using System.Text.RegularExpressions;
using CharM.Engine.CharacterModel;
using CharM.Engine.Creation;
using CharM.Engine.Evaluation;
using CharM.Engine.Prerequisites;
using CharM.Engine.Rules;
using CharM.Engine.Selection;

namespace CharM.Engine.Orchestration;

public sealed partial class CharacterBuilder
{
    private static Regex ProficiencyNamePattern => ProficiencyNameRegex();

    private void IndexProficiencies()
    {
        Stats.TrainedWeapons.Clear();
        Stats.TrainedImplements.Clear();
        foreach (var re in ElementTree.GetActiveElements())
        {
            if (!string.Equals(re.Type, "Proficiency", StringComparison.OrdinalIgnoreCase))
                continue;
            var m = ProficiencyNamePattern.Match(re.Name);
            if (!m.Success) continue;
            var target = m.Groups["target"].Value.Trim();
            if (string.Equals(m.Groups["kind"].Value, "Weapon", StringComparison.OrdinalIgnoreCase))
                Stats.TrainedWeapons.Add(target);
            else
                Stats.TrainedImplements.Add(target);
        }
    }

    private void IndexAbilityChoices()
    {
        Stats.ChosenAbilities.Clear();
        foreach (var re in ElementTree.GetActiveElements())
        {
            // The "Ability Choice" category marks elements that record the
            // player's pick for a "use X or Y" power (warlock pact ability,
            // Bravelands champion, etc.). The chosen ability is the trailing
            // token of the element's Name — e.g. "Eldritch Blast Charisma".
            bool isAbilityChoice = re.Categories.Any(c =>
                string.Equals(c, "Ability Choice", StringComparison.OrdinalIgnoreCase));
            if (!isAbilityChoice) continue;

            var tokens = re.Name.Split(
                ' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length == 0) continue;
            var last = tokens[^1];
            if (AbilityNames.TryParse(last, out var ability))
                Stats.ChosenAbilities.Add(AbilityNames.GetFullName(ability));
        }
    }

    private void IndexKeyAbilitySwaps()
    {
        Stats.KeyAbilitySwaps.Clear();
        foreach (var re in ElementTree.GetActiveElements())
        {
            // "Key Ability Swap" elements record an ability a character may use
            // in place of a discipline power's printed key ability when higher
            // (Orcus class-key substitution). The ability is the trailing token
            // of the element's Name — e.g. "Priest Key Wisdom".
            bool isSwap = re.Categories.Any(c =>
                string.Equals(c, "Key Ability Swap", StringComparison.OrdinalIgnoreCase));
            if (!isSwap) continue;

            var tokens = re.Name.Split(
                ' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length == 0) continue;
            if (AbilityNames.TryParse(tokens[^1], out var ability))
                Stats.KeyAbilitySwaps.Add(AbilityNames.GetFullName(ability));
        }
    }

    private void IndexClassEquivalents()
    {
        Stats.ClassEquivalents.Clear();
        foreach (var id in SelectVariables.GetActiveClassIds(ElementTree))
            Stats.ClassEquivalents.Add(id);
    }

    /// <summary>
    /// Process equipped items — resolve each from the rules DB, execute their directives,
    /// and register their equipment categories for wearing/not-wearing condition checks.
    /// </summary>
    private void ProcessEquipment(IReadOnlyList<ElementChoice> items)
    {
        int maxLevel = ElementTree.Root.Children.Count > 0
            ? ElementTree.Root.Children.Max(c => c.Level)
            : 1;

        var equippedWeaponSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var equippedWeapons = new List<RulesElement>();
        var doubleWeaponSecondaries = new List<RulesElement>();

        foreach (var item in items)
        {
            RulesElement? element = null;
            if (item.InternalId is not null)
                element = _findById(item.InternalId);
            element ??= _findByNameAndType(item.Name, item.Type);

            if (element is null)
                continue;

            // Register equipment categories for wearing/not-wearing checks
            RegisterEquipmentCategories(element);

            if (string.Equals(element.Type, "Weapon", StringComparison.OrdinalIgnoreCase)
                || IsWieldedImplement(element)
                || string.Equals(element.Type, "Superior Implement", StringComparison.OrdinalIgnoreCase))
            {
                equippedWeapons.Add(element);
                if (element.Fields.TryGetValue("Item Slot", out var weaponSlot)
                    && !string.IsNullOrWhiteSpace(weaponSlot))
                {
                    equippedWeaponSlots.Add(weaponSlot.Trim());
                }

                // OCB redirectsimplement-style bases (Superior Implement, staff-group
                // implements) to their underlying weapon when evaluating
                // wearing-condition predicates. Mirror that here so a
                // Quickbeam staff (Superior Implement, Item Slot=Off-hand,
                // WeaponEquiv=Quarterstaff) projects Quarterstaff's
                // Item Slot=Two-Hands and Group=Staff into the wearing-
                // category set — enabling weapon:two-handed / only-weapon:
                // staff feats like Hafted Defense to fire while the staff
                // is the sole equipped weapon.
                var equiv = ResolveImplementWeaponEquiv(element);
                if (equiv is not null)
                {
                    RegisterEquipmentCategories(equiv);
                    if (equiv.Fields.TryGetValue("Item Slot", out var equivSlot)
                        && !string.IsNullOrWhiteSpace(equivSlot))
                    {
                        equippedWeaponSlots.Add(equivSlot.Trim());
                    }
                }
                // Double weapons (Kusari-gama, Double sword, Urgrosh, etc.) carry
                // a _Secondary End reference on the primary. Their off-hand end
                // is not separately listed in the equipped loot — equipping the
                // primary implicitly wields both ends.
                if (element.Fields.TryGetValue("_Secondary End", out var secEnd)
                    && !string.IsNullOrWhiteSpace(secEnd))
                {
                    var secElement = _findById(secEnd.Trim());
                    if (secElement is not null)
                    {
                        doubleWeaponSecondaries.Add(secElement);
                        // Register the secondary end's categories too so its
                        // Group/Properties participate in wearing checks.
                        RegisterEquipmentCategories(secElement);
                    }
                }
            }

            var charElement = ElementTree.Root.AddChild(element, maxLevel);
            ExecutePhase1(element, charElement, maxLevel);
        }

        RegisterAggregateEquipmentCategories(
            equippedWeaponSlots, equippedWeapons, doubleWeaponSecondaries);
    }

    /// <summary>
    /// Project derived "wearing categories" that depend on the full set of
    /// equipped items rather than a single item. Registers:
    /// <list type="bullet">
    /// <item><c>DUAL-WIELDING:</c> when off-hand+main-hand weapons are
    /// equipped, or a double weapon (whose secondary end is implicit).</item>
    /// <item><c>DUAL-WIELDING:&lt;group&gt;</c> for each Group token of each
    /// wielded weapon when dual-wielding (e.g. Hafted Defense's
    /// DUAL-WIELDING:staff for a quarterstaff in each hand).</item>
    /// <item><c>weapon:two-handed</c> when any equipped weapon's Item Slot
    /// is Two-Hands. Used by Hafted Defense and Marauder Fighting Style.</item>
    /// <item><c>only-weapon:&lt;group&gt;</c> when exactly one weapon is
    /// equipped (and no double weapon), for each Group token of that weapon.
    /// Hafted Defense uses this for solo-staff / solo-polearm.</item>
    /// </list>
    /// </summary>
    private void RegisterAggregateEquipmentCategories(
        HashSet<string> equippedWeaponSlots,
        List<RulesElement> equippedWeapons,
        List<RulesElement> doubleWeaponSecondaries)
    {
        // Off-hand-property weapons (Whip, Dagger, Hand crossbow, Shuriken,
        // etc.) carry Item Slot=Off-hand in the rules data as a *category*
        // hint — they CAN be wielded off-hand, not that they MUST be. The
        // .dnd4e file doesn't record which hand the user actually placed
        // them in; we infer:
        //   - if an equipped Type=Weapon HAS the Off-hand property AND
        //     there is no equipped Type=Weapon WITHOUT the Off-hand
        //     property, then the off-hand-property weapon is the only
        //     weapon and therefore occupies the MAIN hand → suppress
        //     SLOT:off hand so NotWearing=SLOT:off hand directives fire.
        //   - implements (held: Wand/Rod/Orb/Tome/Staff; carried: Ki
        //     Focus/Holy Symbol/Totem) that register SLOT:Off-hand via
        //     their own Item Slot field are NOT off-hand-property
        //     weapons, so they do NOT trigger this suppression. A held
        //     implement (Magic Wand) in off-hand correctly keeps
        //     SLOT:off hand active and blocks shield-bonus features.
        bool hasOffHandPropWeapon = equippedWeapons.Any(w =>
            string.Equals(w.Type, "Weapon", StringComparison.OrdinalIgnoreCase)
            && HasOffHandProperty(w));
        bool anyNonOffHandWeapon = equippedWeapons.Any(w =>
            string.Equals(w.Type, "Weapon", StringComparison.OrdinalIgnoreCase)
            && !HasOffHandProperty(w));
        if (hasOffHandPropWeapon && !anyNonOffHandWeapon)
        {
            _equippedCategories.Remove("SLOT:Off-hand");
            _equippedCategories.Remove("SLOT:Off hand");
        }

        bool hasOffHandWeapon = equippedWeaponSlots.Contains("Off-hand")
                                || equippedWeaponSlots.Contains("Off Hand");
        // The slot-set check still uses the original (pre-suppression) slot
        // categories to detect dual-wielding intent; suppress only affects
        // the wearing-category set the directives consult.
        hasOffHandWeapon = hasOffHandWeapon && anyNonOffHandWeapon;
        bool hasMainHandWeapon = equippedWeaponSlots.Contains("One-hand")
                                 || equippedWeaponSlots.Contains("One Hand")
                                 || equippedWeaponSlots.Contains("Versatile");
        bool hasDoubleWeapon = doubleWeaponSecondaries.Count > 0;
        bool isDualWielding = hasDoubleWeapon || (hasOffHandWeapon && hasMainHandWeapon);

        if (isDualWielding)
        {
            _equippedCategories.Add("DUAL-WIELDING:");
            foreach (var w in equippedWeapons.Concat(doubleWeaponSecondaries))
                foreach (var grp in EnumerateWeaponGroups(w))
                    _equippedCategories.Add($"DUAL-WIELDING:{grp}");
        }

        // weapon:two-handed — projected when any equipped weapon is wielded
        // two-handed (Item Slot = Two-Hands). A versatile weapon used in
        // two hands also qualifies in principle, but OCB tracks "Item Slot"
        // not "current wield mode", so we mirror the slot check.
        if (equippedWeaponSlots.Contains("Two-Hands")
            || equippedWeaponSlots.Contains("Two Hands"))
        {
            _equippedCategories.Add("weapon:two-handed");
        }

        // only-weapon:<group> — when exactly one weapon is equipped (and not
        // a double weapon, which counts as two), project the solo weapon's
        // group(s). Used only by Hafted Defense's polearm/staff branches.
        if (equippedWeapons.Count == 1 && !hasDoubleWeapon)
        {
            foreach (var grp in EnumerateWeaponGroups(equippedWeapons[0]))
                _equippedCategories.Add($"only-weapon:{grp}");
        }
    }

    private static bool HasOffHandProperty(RulesElement weapon)
    {
        if (!weapon.Fields.TryGetValue("Properties", out var props) || string.IsNullOrWhiteSpace(props))
            return false;
        foreach (var p in props.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(p, "Off-hand", StringComparison.OrdinalIgnoreCase)
                || string.Equals(p, "Off Hand", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static IEnumerable<string> EnumerateWeaponGroups(RulesElement weapon)
    {
        if (weapon.Fields.TryGetValue("Group", out var groups) && !string.IsNullOrEmpty(groups))
        {
            foreach (var g in groups.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                yield return g.ToLowerInvariant();
            yield break;
        }
        // Magic-item implements with no base weapon — fall back to Magic Item
        // Type when it's also a weapon-group token (Staff).
        if (string.Equals(weapon.Type, "Magic Item", StringComparison.OrdinalIgnoreCase)
            && weapon.Fields.TryGetValue("Magic Item Type", out var mit)
            && IsImplementWeaponGroup(mit))
        {
            yield return mit.Trim().ToLowerInvariant();
        }
    }

    // Magic Item Type values that double as weapon-group tokens in the rules
    // XML. Staff is currently the only one used by a `weapon:` wearing
    // condition (Quarterstaff and Magic-Item Staff implements are
    // interchangeable for Hafted Defense, Marauder Fighting Style, etc.).
    // Other implement types (Wand, Orb, Rod, Tome, Holy Symbol, Ki Focus,
    // Totem) are not weapon groups and live in `implement:*` space instead.
    private static bool IsImplementWeaponGroup(string? magicItemType)
    {
        if (string.IsNullOrWhiteSpace(magicItemType)) return false;
        return string.Equals(magicItemType.Trim(), "Staff", StringComparison.OrdinalIgnoreCase);
    }

    // A Magic Item that is wielded in hand (implement) rather than worn
    // (Boots, Belt, Helm, Wondrous Item, etc.). Used so that magic
    // implements participate in DUAL-WIELDING:, only-weapon:, and
    // weapon:two-handed projections even when no base Weapon entry is in
    // equipped loot. The set mirrors the implement-category Magic Item Type
    // values from the rules data.
    private static readonly HashSet<string> WieldedImplementTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Staff", "Wand", "Orb", "Rod", "Tome",
            "Holy Symbol", "Ki Focus", "Totem",
        };

    private static bool IsWieldedImplement(RulesElement element)
    {
        if (!string.Equals(element.Type, "Magic Item", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!element.Fields.TryGetValue("Magic Item Type", out var mit) || string.IsNullOrWhiteSpace(mit))
            return false;
        return WieldedImplementTypes.Contains(mit.Trim());
    }

    /// <summary>
    /// Resolves the underlying base weapon for an equipped implement so that
    /// implement-as-weapon wielding (staff-group implements, Superior
    /// Implements with explicit WeaponEquiv) projects the base weapon's
    /// Item Slot / Group / Properties into the wearing-category set. Returns
    /// null when the element is not an implement or has no equivalent.
    /// </summary>
    private RulesElement? ResolveImplementWeaponEquiv(RulesElement element)
    {
        if (element.Fields.TryGetValue("WeaponEquiv", out var equivId)
            && !string.IsNullOrWhiteSpace(equivId))
        {
            var resolved = _findById(equivId.Trim());
            if (resolved is not null) return resolved;
        }
        // Staff-group implement with no explicit WeaponEquiv → quarterstaff
        // (OCB's IsStaff branch). Implement / Superior Implement / Magic Item
        // types only — base weapons obviously already have correct fields.
        if ((string.Equals(element.Type, "Superior Implement", StringComparison.OrdinalIgnoreCase)
                || string.Equals(element.Type, "Implement", StringComparison.OrdinalIgnoreCase)
                || string.Equals(element.Type, "Magic Item", StringComparison.OrdinalIgnoreCase))
            && element.Fields.TryGetValue("Group", out var group)
            && !string.IsNullOrWhiteSpace(group)
            && group.Split(',', StringSplitOptions.TrimEntries)
                .Any(g => string.Equals(g, "Staff", StringComparison.OrdinalIgnoreCase)))
        {
            return _findById("ID_FMP_WEAPON_10");
        }
        return null;
    }

    [GeneratedRegex(@"^(?<kind>Weapon|Implement)\s+Proficiency\s*\((?<target>.+)\)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ProficiencyNameRegex();

    /// <summary>
    /// After all loot has been processed, iterate every <c>"&lt;setId&gt; Set Count"</c> stat
    /// created by item set members (each set item carries a
    /// <c>StatAdd Name="&lt;setId&gt; Set Count" Value=1</c>). For each
    /// <c>Item Set</c> whose count is non-zero, look at the set's
    /// <c>Benefits</c> field and activate every <c>Item Set Benefit</c>
    /// whose <c>Piece Count</c> threshold the count satisfies. Each
    /// activated benefit goes through the normal Phase1 → Phase2 pipeline
    /// so its <c>StatAddDirective</c>s (whose value is a
    /// <see cref="StatReference"/> to the Set Count) contribute properly
    /// scaled bonuses at compute time.
    /// </summary>
    private void ApplyItemSetBenefits()
    {
        const string CountSuffix = " Set Count";

        int maxLevel = ElementTree.Root.Children.Count > 0
            ? ElementTree.Root.Children.Max(c => c.Level)
            : 1;

        // Snapshot stat names because Phase1 on the benefit may create more.
        var setCountStats = Stats.AllStatNames
            .Where(n => n.EndsWith(CountSuffix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var statName in setCountStats)
        {
            string setId = statName[..^CountSuffix.Length].Trim();
            if (setId.Length == 0)
                continue;

            var setElement = _findById(setId);
            if (setElement is null
                || !string.Equals(setElement.Type, "Item Set", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int count = Stats.TryGetStat(statName)?.ComputeValue(Stats) ?? 0;
            if (count <= 0)
                continue;

            if (!setElement.Fields.TryGetValue("Benefits", out var benefitsField)
                || string.IsNullOrWhiteSpace(benefitsField))
            {
                continue;
            }

            foreach (var rawId in benefitsField.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string benefitId = rawId.Trim();
                if (benefitId.Length == 0)
                    continue;

                var benefit = _findById(benefitId);
                if (benefit is null)
                    continue;

                int threshold = 0;
                if (benefit.Fields.TryGetValue("Piece Count", out var pc)
                    && int.TryParse(pc.Trim(), out var parsed))
                {
                    threshold = parsed;
                }

                if (count < threshold)
                    continue;

                // Skip if already in the tree (idempotent — guards against
                // a benefit being granted some other way).
                if (ElementTree.Root.GetAllDescendants()
                    .Any(c => string.Equals(c.RulesElement?.InternalId, benefit.InternalId,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var charElement = ElementTree.Root.AddChild(benefit, maxLevel);
                ExecutePhase1(benefit, charElement, maxLevel);
            }
        }
    }

    /// <summary>
    /// Process inventory items that carry active rule directives (boons,
    /// active Wondrous Items, Deck of Many Things cards). Same Phase1 path
    /// as ProcessEquipment but skips RegisterEquipmentCategories — an item
    /// in your pack does not satisfy "wearing armor:heavy" conditions.
    /// </summary>
    private void ProcessInventoryDirectives(IReadOnlyList<ElementChoice> items)
    {
        int maxLevel = ElementTree.Root.Children.Count > 0
            ? ElementTree.Root.Children.Max(c => c.Level)
            : 1;

        foreach (var item in items)
        {
            RulesElement? element = null;
            if (item.InternalId is not null)
                element = _findById(item.InternalId);
            element ??= _findByNameAndType(item.Name, item.Type);

            if (element is null)
                continue;

            var charElement = ElementTree.Root.AddChild(element, maxLevel);
            ExecutePhase1(element, charElement, maxLevel);
        }
    }

    /// <summary>
    /// Register an equipped item's categories for wearing/not-wearing condition evaluation.
    /// Examines the element's Fields for armor type, weapon group, etc.
    /// </summary>
    private void RegisterEquipmentCategories(RulesElement element)
    {
        string typeLower = element.Type.ToLowerInvariant();

        // Register the base type as a category (e.g., "armor:", "weapon:")
        _equippedCategories.Add($"{typeLower}:");

        // Register specific armor categories from Fields
        if (element.Fields.TryGetValue("Armor Type", out var armorType) && !string.IsNullOrEmpty(armorType))
        {
            _equippedCategories.Add($"armor:{armorType.Trim().ToLowerInvariant()}");
        }

        if (element.Fields.TryGetValue("Armor Category", out var armorCat) && !string.IsNullOrEmpty(armorCat))
        {
            _equippedCategories.Add($"armor:{armorCat.Trim().ToLowerInvariant()}");
        }

        // Register item slot categories for SLOT: wearing conditions
        // (e.g., SLOT:Body, SLOT:off hand)
        if (element.Fields.TryGetValue("Item Slot", out var slot) && !string.IsNullOrEmpty(slot))
        {
            string slotTrimmed = slot.Trim();
            _equippedCategories.Add($"SLOT:{slotTrimmed}");

            // Also register normalized form without hyphens for matching
            // (data uses "Off-hand" but wearing condition uses "off hand")
            string normalized = slotTrimmed.Replace("-", " ");
            if (normalized != slotTrimmed)
                _equippedCategories.Add($"SLOT:{normalized}");
        }

        // Register "armor:shield" only for actual shields (Armor Type = Shield),
        // not for any off-hand item (Monk Unarmed Strike is off-hand but not a shield)
        if (string.Equals(armorType?.Trim(), "Shield", StringComparison.OrdinalIgnoreCase))
        {
            _equippedCategories.Add("armor:shield");
        }

        // Register weapon categories
        if (element.Fields.TryGetValue("Group", out var weaponGroup) && !string.IsNullOrEmpty(weaponGroup))
        {
            foreach (var group in weaponGroup.Split(',', StringSplitOptions.TrimEntries))
                _equippedCategories.Add($"weapon:{group.ToLowerInvariant()}");
        }

        // Magic-item implements (Staff of Ruin, Wand of X, etc.) carry their
        // implement category in the "Magic Item Type" field rather than the
        // "Group" field that base weapons use. Project it as a weapon group so
        // wearing conditions like "weapon:staff" / "only-weapon:staff" fire
        // when the character wields only the magic implement (no separate
        // base-weapon entry in equipped loot). The set is restricted to
        // values that double as weapon-group tokens in the rules data.
        if (string.Equals(element.Type, "Magic Item", StringComparison.OrdinalIgnoreCase)
            && element.Fields.TryGetValue("Magic Item Type", out var mit))
        {
            if (IsImplementWeaponGroup(mit))
                _equippedCategories.Add($"weapon:{mit.Trim().ToLowerInvariant()}");

            // A wielded magic-item implement (Ki Focus, Wand, Rod, Tome, Holy
            // Symbol, Totem, Orb, Staff) also satisfies the bare `weapon:`
            // category — OCB treats wielding any implement as wielding a
            // weapon for the purposes of feats like Brawler Guard
            // (`wearing=weapon:` / `not-wearing=SLOT:off hand`).
            if (IsWieldedImplement(element))
                _equippedCategories.Add("weapon:");
        }

        // Register weapon properties as UPPERCASE: wearing categories
        // (e.g., Longsword has Properties=Versatile → VERSATILE:; Spear has
        // Properties=Reach → REACH:). Used by feats like Small Warrior's
        // Defense (+2 AC/Ref wearing VERSATILE:) and similar property-gated
        // bonuses. Each comma-separated property becomes one category with
        // the property name uppercased and a trailing colon.
        if (element.Fields.TryGetValue("Properties", out var weaponProps) && !string.IsNullOrEmpty(weaponProps))
        {
            foreach (var prop in weaponProps.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                _equippedCategories.Add($"{prop.ToUpperInvariant()}:");
        }
    }

    /// <summary>
    /// Check if a wearing condition is met by the currently equipped items.
    /// Supports comma-separated AND-conjunction with prefix inheritance:
    /// the first comma-part may carry a <c>prefix:value</c> form (e.g.
    /// <c>weapon:two-handed</c>); subsequent parts that contain no <c>:</c>
    /// inherit the leading prefix. So <c>weapon:two-handed,staff</c> means
    /// <c>weapon:two-handed AND weapon:staff</c>, and
    /// <c>DUAL-WIELDING:heavy blade,light blade</c> means
    /// <c>DUAL-WIELDING:heavy blade AND DUAL-WIELDING:light blade</c>.
    /// Rules-DB only ships four such patterns, all matching this shape.
    /// </summary>
    private bool CheckWearing(string wearingCondition)
    {
        if (wearingCondition.IndexOf(',') < 0)
            return _equippedCategories.Contains(wearingCondition);

        string? inheritedPrefix = null;
        int firstColon = wearingCondition.IndexOf(':');
        int firstComma = wearingCondition.IndexOf(',');
        if (firstColon >= 0 && firstColon < firstComma)
            inheritedPrefix = wearingCondition.Substring(0, firstColon + 1);

        foreach (var part in wearingCondition.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            string token = part;
            if (inheritedPrefix != null && token.IndexOf(':') < 0)
                token = inheritedPrefix + token;
            if (!_equippedCategories.Contains(token))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Hybrid armor proficiency rule: when a character has 2+ Hybrid
    /// Class grant elements, they only get the armor and shield proficiencies 
    /// that appear in BOTH classes' grant lists. The rules data ships each
    /// <c>ID_INTERNAL_GRANTS_HYBRID_*</c> element with the full per-class
    /// proficiency set, so we reconcile post-Phase-1 by computing the
    /// intersection and removing the off-list profs from each grant element's
    /// child collection. Other proficiency types (weapon categories, weapon
    /// keywords) are not filtered — only Armor and Shield.
    ///
    /// Heavy/Plate that legitimately reach the character via a separate
    /// source (e.g. Warden's Armored Might via the Hybrid Talent feat)
    /// remain because that grant lives outside the Hybrid Grants subtree.
    /// </summary>
    private void ReconcileHybridArmorProficiencies()
    {
        var hybridGrantNodes = ElementTree.Root.GetAllDescendants()
            .Where(ce => ce.RulesElement is not null
                         && ce.RulesElement.InternalId is { } id
                         && id.StartsWith("ID_INTERNAL_GRANTS_HYBRID_", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (hybridGrantNodes.Count < 2)
            return;

        var perGrantProfs = new List<HashSet<string>>(hybridGrantNodes.Count);
        foreach (var node in hybridGrantNodes)
        {
            var def = _findById(node.RulesElement!.InternalId);
            if (def is null) return; // bail out conservatively if we can't see the source data
            var profs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in def.Rules)
            {
                if (rule is GrantDirective g
                    && IsArmorOrShieldProficiencyId(g.Name))
                {
                    profs.Add(g.Name);
                }
            }
            // Only the class-level "Hybrid <Classname>" grants list armor/shield
            // proficiencies. Sub-grants like "Hybrid Runepriest Implements"
            // contain only Implement keywords and would zero the intersection.
            if (profs.Count > 0)
                perGrantProfs.Add(profs);
        }

        if (perGrantProfs.Count < 2)
            return;

        // Intersection across all hybrid grants
        var intersection = new HashSet<string>(perGrantProfs[0], StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < perGrantProfs.Count; i++)
            intersection.IntersectWith(perGrantProfs[i]);

        // Drop any direct child of a hybrid-grants node that is an armor/shield
        // proficiency NOT in the intersection.
        foreach (var node in hybridGrantNodes)
        {
            for (int i = node.Children.Count - 1; i >= 0; i--)
            {
                var child = node.Children[i];
                var cid = child.RulesElement?.InternalId;
                if (cid is null) continue;
                if (!IsArmorOrShieldProficiencyId(cid)) continue;
                if (intersection.Contains(cid)) continue;

                child.Parent = null;
                node.Children.RemoveAt(i);

                _pendingPhase2.RemoveAll(e =>
                    string.Equals(e.Element.InternalId, cid, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    private static bool IsArmorOrShieldProficiencyId(string? internalId)
    {
        if (internalId is null) return false;
        return internalId.StartsWith("ID_INTERNAL_PROFICIENCY_ARMOR_", StringComparison.OrdinalIgnoreCase)
            || internalId.StartsWith("ID_INTERNAL_PROFICIENCY_SHIELD_", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Process elements from the RulesElementTally that weren't already resolved
    /// via the build choices or their grant chains. This catches elements from
    /// select choices (skill training, racial traits, proficiencies) that our
    /// orchestrator doesn't yet replay.
    /// </summary>
    private void ProcessTallySupplement(IReadOnlyList<ElementChoice> tally)
    {
        // Track what we've already processed by InternalId
        var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var elem in ElementTree.Root.GetAllDescendants())
        {
            if (elem.RulesElement?.InternalId is { } id)
                processed.Add(id);
        }

        foreach (var entry in tally)
        {
            // Slot-owned duplicate entries can be selection targets for later
            // modify select="..." directives, even when their rules already ran.
            bool alreadyProcessed = entry.InternalId is not null && processed.Contains(entry.InternalId);
            if (alreadyProcessed && string.IsNullOrWhiteSpace(entry.SlotOwnerInternalId))
                continue;

            RulesElement? element = null;
            if (entry.InternalId is not null)
                element = _findById(entry.InternalId);
            element ??= _findByNameAndType(entry.Name, entry.Type);

            if (element is null)
                continue;

            var charElement = ElementTree.Root.AddChild(element, 1);
            charElement.SlotOwnerInternalId = entry.SlotOwnerInternalId;
            if (processed.Contains(element.InternalId))
                continue;

            processed.Add(element.InternalId);
            int phase1Level = entry.AcquiredAtLevel
                ?? ElementTree.Root.Children.Max(c => c.Level);
            ExecutePhase1(element, charElement, phase1Level);
        }
    }
}
