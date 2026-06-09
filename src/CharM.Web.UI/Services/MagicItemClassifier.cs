using CharM.Engine.Rules;

namespace CharM.Web.Services;

/// <summary>
/// Classifies rules-DB equipment / magic-item elements into four buckets that
/// drive the equipment / magic-item pickers in the UI.
///
/// <para>
/// The four buckets and how they're identified:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <see cref="EquipmentBucket.MundaneBase"/> — Type=Weapon/Armor/Gear/
/// Superior Implement with an empty <c>Minimum Enhancement Bonus</c>
/// field. Wieldable as-is.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="EquipmentBucket.MasterworkBase"/> — Type=Weapon/Armor with
/// a non-empty <c>Minimum Enhancement Bonus</c> field (Starleather,
/// Feyleather, Godplate, etc.). Per <c>RulesEngineCommon/ArmorItem.cs</c>:
/// <c>IsMasterWork = Workspace.RulesElementField(elem, "Minimum Enhancement Bonus") != ""</c>.
/// MUST be paired with an enchantment whose enhancement bonus meets or
/// exceeds the minimum — has no mundane counterpart.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="EquipmentBucket.StandaloneMagic"/> — Type=Magic Item that is
/// not an Enchantment or Augment. Includes implements (Magic Item Type =
/// Rod / Staff / Wand / Orb / Tome / Totem / Holy Symbol / Ki Focus —
/// these have an Item Slot set such as "Off-hand" / "Ki Focus" and ARE
/// the implement, with no separate base), wearable slot items (Neck,
/// Head, Hands, Feet, Arms, Ring, Waist, Tattoo, Mount, Companion,
/// Familiar), consumables (Potion, Elixir, Alchemical, Ammunition,
/// Reagent, Consumable, Other Consumable), and boons / artifacts /
/// special items.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="EquipmentBucket.Enchantment"/> — Type=Magic Item with
/// <c>Magic Item Type</c> in {Weapon, Armor} and an empty <c>Item Slot</c>
/// (Holy Avenger +5, Frost Weapon +1, Armor of Resistance +6, etc.). MUST
/// be paired with a base of the matching kind.
/// </description>
/// </item>
/// </list>
///
/// <para>
/// Augments are a special flavor of Enchantment whose <c>Magic Item Type</c>
/// is in {Dragonshard Augment, Whetstones}. They are still classified as
/// <see cref="EquipmentBucket.Enchantment"/> for picker-filtering purposes
/// (hidden from the slot picker, reachable only through the Magic Item
/// builder) but the builder uses <see cref="IsAugment"/> to route them to
/// the augment stage instead of the enchantment stage.
/// </para>
/// </summary>
public static class MagicItemClassifier
{
    private const string MagicItemType = "Magic Item";
    private const string WeaponType = "Weapon";
    private const string ArmorType = "Armor";
    private const string GearType = "Gear";
    private const string SuperiorImplementType = "Superior Implement";

    private const string MinEnhancementField = "Minimum Enhancement Bonus";
    private const string MagicItemTypeField = "Magic Item Type";
    private const string ItemSlotField = "Item Slot";
    private const string EnhancementField = "Enhancement";
    private const string WeaponField = "Weapon";
    private const string ArmorField = "Armor";
    private const string SpecialField = "Special";
    private const string InternalOnlyField = "InternalOnly";

    /// <summary>
    /// Marker substring found in the <c>Special</c> field of pact-weapon
    /// artifacts (Blade of Winter's Mourning, Blade of Chaos, Starshadow
    /// Blade, etc.). These are class-feature granted and explicitly cannot
    /// be picked or enchanted by the player.
    /// </summary>
    private const string CannotBeEnchantedMarker = "cannot be enchanted";

    private static readonly HashSet<string> AugmentMagicItemTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dragonshard Augment",
        "Whetstones",
    };

    /// <summary>
    /// Classify a single rules element into one of the four equipment buckets,
    /// or returns <see cref="EquipmentBucket.Other"/> for elements that aren't
    /// equipment at all (powers, feats, class features, etc.).
    /// </summary>
    public static EquipmentBucket Classify(RulesElement element)
    {
        if (IsMundaneBaseType(element.Type))
        {
            return HasMinimumEnhancementBonus(element)
                ? EquipmentBucket.MasterworkBase
                : EquipmentBucket.MundaneBase;
        }

        if (string.Equals(element.Type, MagicItemType, StringComparison.OrdinalIgnoreCase))
        {
            return IsEnchantmentByFields(element)
                ? EquipmentBucket.Enchantment
                : EquipmentBucket.StandaloneMagic;
        }

        return EquipmentBucket.Other;
    }

    /// <summary>True if the element is a mundane wieldable base (no minimum-enhancement gate).</summary>
    public static bool IsMundaneBase(RulesElement element)
        => Classify(element) == EquipmentBucket.MundaneBase;

    /// <summary>True if the element is a masterwork base — non-magical body with a required magic enhancement.</summary>
    public static bool IsMasterworkBase(RulesElement element)
        => Classify(element) == EquipmentBucket.MasterworkBase;

    /// <summary>True if the element is a standalone magic item (no base required).</summary>
    public static bool IsStandaloneMagicItem(RulesElement element)
        => Classify(element) == EquipmentBucket.StandaloneMagic;

    /// <summary>
    /// True if the element is a Magic Item that MUST be paired with a base
    /// (Enchantment for a Weapon or Armor, or an Augment).
    /// </summary>
    public static bool IsEnchantment(RulesElement element)
        => Classify(element) == EquipmentBucket.Enchantment;

    /// <summary>
    /// True if the element is specifically an Augment (Dragonshard Augment or
    /// Whetstone) — a third-tier component that attaches on top of an
    /// enchantment, not directly to the base. Implies <see cref="IsEnchantment"/>.
    /// </summary>
    public static bool IsAugment(RulesElement element)
    {
        if (!string.Equals(element.Type, MagicItemType, StringComparison.OrdinalIgnoreCase))
            return false;

        return element.Fields.TryGetValue(MagicItemTypeField, out var mit)
            && !string.IsNullOrWhiteSpace(mit)
            && AugmentMagicItemTypes.Contains(mit.Trim());
    }

    /// <summary>
    /// Parse the <c>Minimum Enhancement Bonus</c> field, returning 0 when
    /// the field is absent or non-numeric.
    /// </summary>
    public static int GetMinimumEnhancementBonus(RulesElement element)
    {
        if (!element.Fields.TryGetValue(MinEnhancementField, out var raw) || string.IsNullOrWhiteSpace(raw))
            return 0;

        var trimmed = raw.Trim().TrimStart('+');
        return int.TryParse(trimmed, out var n) ? n : 0;
    }

    /// <summary>
    /// Parse the leading numeric portion of an enchantment's <c>Enhancement</c>
    /// field. The field typically reads "+3 attack rolls and damage rolls"
    /// or "+2 AC"; returns 0 when absent / unparseable.
    /// </summary>
    public static int GetEnhancementBonus(RulesElement enchantment)
    {
        if (!enchantment.Fields.TryGetValue(EnhancementField, out var raw) || string.IsNullOrWhiteSpace(raw))
            return 0;

        var s = raw.Trim().TrimStart('+');
        int end = 0;
        while (end < s.Length && char.IsDigit(s[end])) end++;
        return end > 0 && int.TryParse(s[..end], out var n) ? n : 0;
    }

    /// <summary>
    /// True when <paramref name="enchantment"/> is a valid pairing for
    /// <paramref name="baseItem"/>. Honors:
    /// <list type="bullet">
    /// <item><description>Magic Item Type must match the base kind (Weapon/Armor).</description></item>
    /// <item><description>For masterwork bases, the enchantment's bonus must meet the minimum.</description></item>
    /// <item><description>For weapon enchantments, the optional <c>Weapon</c> field
    /// (a comma-separated list of specific weapons, weapon groups, or
    /// category tokens like "Any Ranged" / "Any") restricts which bases qualify.</description></item>
    /// <item><description>For armor enchantments, the optional <c>Armor</c> field
    /// behaves analogously over armor categories / types.</description></item>
    /// </list>
    /// </summary>
    public static bool IsCompatibleEnchantment(RulesElement baseItem, RulesElement enchantment)
    {
        if (Classify(enchantment) != EquipmentBucket.Enchantment)
            return false;

        if (IsAugment(enchantment))
            return false;

        if (!enchantment.Fields.TryGetValue(MagicItemTypeField, out var mit) || string.IsNullOrWhiteSpace(mit))
            return false;

        var baseBucket = Classify(baseItem);
        if (baseBucket != EquipmentBucket.MundaneBase && baseBucket != EquipmentBucket.MasterworkBase)
            return false;

        bool kindMatches = (string.Equals(mit, WeaponType, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(baseItem.Type, WeaponType, StringComparison.OrdinalIgnoreCase))
                        || (string.Equals(mit, ArmorType, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(baseItem.Type, ArmorType, StringComparison.OrdinalIgnoreCase));
        if (!kindMatches)
            return false;

        if (baseBucket == EquipmentBucket.MasterworkBase
            && GetEnhancementBonus(enchantment) < GetMinimumEnhancementBonus(baseItem))
        {
            return false;
        }

        return string.Equals(mit, WeaponType, StringComparison.OrdinalIgnoreCase)
            ? WeaponRestrictionMatches(baseItem, enchantment)
            : ArmorRestrictionMatches(baseItem, enchantment);
    }

    /// <summary>
    /// True when <paramref name="augment"/> is a valid augment for
    /// <paramref name="enchantment"/>. v1 rule: any Augment-flavored Magic
    /// Item is considered compatible with any enchantment of the matching
    /// "kind" (Dragonshard Augments target weapons / armor / implements;
    /// Whetstones target weapons). Refined per-type filtering can be added
    /// here without touching call sites.
    /// </summary>
    public static bool IsCompatibleAugment(RulesElement enchantment, RulesElement augment)
    {
        if (!IsAugment(augment))
            return false;

        // The target must be an enchantment (#4 bucket) but not itself an augment —
        // augments attach on top of enchantments, not on top of other augments.
        if (Classify(enchantment) != EquipmentBucket.Enchantment || IsAugment(enchantment))
            return false;

        if (!enchantment.Fields.TryGetValue(MagicItemTypeField, out var enchantmentMagicItemType)
            || string.IsNullOrWhiteSpace(enchantmentMagicItemType))
        {
            return false;
        }

        if (!augment.Fields.TryGetValue(MagicItemTypeField, out var augmentMagicItemType)
            || string.IsNullOrWhiteSpace(augmentMagicItemType))
        {
            return false;
        }

        // Whetstones only apply to weapon enchantments.
        if (string.Equals(augmentMagicItemType.Trim(), "Whetstones", StringComparison.OrdinalIgnoreCase))
            return string.Equals(enchantmentMagicItemType.Trim(), WeaponType, StringComparison.OrdinalIgnoreCase);

        // Dragonshard Augments apply to weapons and armor (and implements once supported).
        var ench = enchantmentMagicItemType.Trim();
        return string.Equals(ench, WeaponType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(ench, ArmorType, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True for items that are auto-granted by another rules element (class
    /// feature, racial trait, paragon path, etc.) rather than picked by the
    /// player. Filters out the secondary ends of double weapons (Quarterstaff
    /// secondary, Cahulaks secondary) which carry <c>InternalOnly=1</c> and a
    /// <c>_Primary End</c> back-reference, and pact-weapon artifacts whose
    /// <c>Special</c> field declares "cannot be enchanted".
    /// </summary>
    public static bool IsAutoGrantedOnly(RulesElement element)
    {
        if (element.Fields.TryGetValue(InternalOnlyField, out var io)
            && !string.IsNullOrWhiteSpace(io)
            && io.Trim() == "1")
        {
            return true;
        }

        if (element.Fields.TryGetValue(SpecialField, out var special)
            && !string.IsNullOrWhiteSpace(special)
            && special.Contains(CannotBeEnchantedMarker, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// True when a Magic Item has <c>Magic Item Type=Artifact</c>. Used by the
    /// UI to highlight artifact names (e.g. orange in the picker) so DM-awarded
    /// story items are visually distinct from purchasable magic items. Note
    /// that pact-weapon artifacts are filtered out separately by
    /// <see cref="IsAutoGrantedOnly"/>.
    /// </summary>
    public static bool IsArtifact(RulesElement element)
        => element.Fields.TryGetValue(MagicItemTypeField, out var mit)
        && string.Equals(mit?.Trim(), "Artifact", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True for Large weapon variants (Bastard sword (Large), Greatsword
    /// (Large), etc.). Identified by the <c>Size</c> field equalling
    /// "Large" or by the canonical InternalId prefix
    /// <c>ID_INTERNAL_WEAPON_LARGE_</c>. These can be wielded only by
    /// characters with <see cref="IsAutoGrantedOnly"/>-style permission
    /// elements (Goliath Stone's Endurance, Warden Godlike Stature, etc.);
    /// the per-character gate lives on <c>CharacterSessionService</c>.
    /// </summary>
    public static bool IsLargeWeapon(RulesElement element)
    {
        if (!string.Equals(element.Type, WeaponType, StringComparison.OrdinalIgnoreCase))
            return false;

        if (element.Fields.TryGetValue("Size", out var size)
            && string.Equals(size?.Trim(), "Large", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrEmpty(element.InternalId)
            && element.InternalId.StartsWith("ID_INTERNAL_WEAPON_LARGE_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMundaneBaseType(string type)
        => string.Equals(type, WeaponType, StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, ArmorType, StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, GearType, StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, SuperiorImplementType, StringComparison.OrdinalIgnoreCase);

    private static bool HasMinimumEnhancementBonus(RulesElement element)
        => element.Fields.TryGetValue(MinEnhancementField, out var v)
        && !string.IsNullOrWhiteSpace(v);

    private static bool IsEnchantmentByFields(RulesElement element)
    {
        // Augments are still "enchantments" for picker-hide purposes.
        if (IsAugment(element))
            return true;

        if (!element.Fields.TryGetValue(MagicItemTypeField, out var mit) || string.IsNullOrWhiteSpace(mit))
            return false;

        var mitTrim = mit.Trim();
        bool isWeaponOrArmorEnch = string.Equals(mitTrim, WeaponType, StringComparison.OrdinalIgnoreCase)
                                || string.Equals(mitTrim, ArmorType, StringComparison.OrdinalIgnoreCase);
        if (!isWeaponOrArmorEnch)
            return false;

        // Weapon/Armor enchantments leave Item Slot empty. (Some standalone
        // wearables redundantly set Magic Item Type=Weapon while also setting
        // an Item Slot — those are not enchantments.)
        return !element.Fields.TryGetValue(ItemSlotField, out var slot)
            || string.IsNullOrWhiteSpace(slot);
    }

    private static bool WeaponRestrictionMatches(RulesElement weaponBase, RulesElement enchantment)
    {
        if (!enchantment.Fields.TryGetValue(WeaponField, out var raw) || string.IsNullOrWhiteSpace(raw))
            return true;

        var tokens = GetWeaponTokens(weaponBase);
        bool isRanged = IsRangedWeapon(weaponBase);
        bool isMelee = IsMeleeWeapon(weaponBase);

        foreach (var token in raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.StartsWith("Any", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(token, "Any", StringComparison.OrdinalIgnoreCase)
                    || token.Equals("Any Weapon", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                if (token.Contains("Ranged", StringComparison.OrdinalIgnoreCase))
                {
                    if (isRanged) return true;
                    continue;
                }
                if (token.Contains("Melee", StringComparison.OrdinalIgnoreCase))
                {
                    if (isMelee) return true;
                    continue;
                }
            }

            if (TokenMatchesBase(token, tokens))
                return true;
        }

        return false;
    }

    private static bool ArmorRestrictionMatches(RulesElement armorBase, RulesElement enchantment)
    {
        if (!enchantment.Fields.TryGetValue(ArmorField, out var raw) || string.IsNullOrWhiteSpace(raw))
            return true;

        var tokens = GetArmorTokens(armorBase);
        foreach (var token in raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.StartsWith("Any", StringComparison.OrdinalIgnoreCase))
                return true;
            if (TokenMatchesBase(token, tokens))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Match an enchantment-restriction token against a base's identifying
    /// tokens. Exact match wins first; OCB-style permissive substring is
    /// applied one-way (requested token may be a substring of a base token,
    /// matching OCB's <c>stristr</c> behavior — e.g. requested "Bow" matches
    /// base group "Crossbow"; reversed direction would over-match, so it is
    /// NOT applied).
    /// </summary>
    private static bool TokenMatchesBase(string token, IReadOnlyCollection<string> baseTokens)
    {
        foreach (var baseTok in baseTokens)
        {
            if (string.Equals(baseTok, token, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        foreach (var baseTok in baseTokens)
        {
            if (baseTok.Contains(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool IsRangedWeapon(RulesElement weapon)
        => weapon.Fields.TryGetValue("Weapon Category", out var cat)
            && !string.IsNullOrWhiteSpace(cat)
            && cat.Contains("Ranged", StringComparison.OrdinalIgnoreCase);

    private static bool IsMeleeWeapon(RulesElement weapon)
        => weapon.Fields.TryGetValue("Weapon Category", out var cat)
            && !string.IsNullOrWhiteSpace(cat)
            && cat.Contains("Melee", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyCollection<string> GetWeaponTokens(RulesElement weaponBase)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { weaponBase.Name };
        AddCommaField(weaponBase, "Group", tokens);
        AddCommaField(weaponBase, "Weapon Category", tokens);
        AddCommaField(weaponBase, "Category", tokens);
        return tokens;
    }

    private static IReadOnlyCollection<string> GetArmorTokens(RulesElement armorBase)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { armorBase.Name };
        AddCommaField(armorBase, "Armor Category", tokens);
        AddCommaField(armorBase, "Armor Type", tokens);
        return tokens;
    }

    private static void AddCommaField(RulesElement element, string field, HashSet<string> sink)
    {
        if (!element.Fields.TryGetValue(field, out var raw) || string.IsNullOrWhiteSpace(raw))
            return;
        foreach (var tok in raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            sink.Add(tok);
    }
}

/// <summary>Equipment classification buckets — see <see cref="MagicItemClassifier"/>.</summary>
public enum EquipmentBucket
{
    /// <summary>Not equipment (a power, feat, class feature, etc.).</summary>
    Other,

    /// <summary>Wieldable base item, no minimum-enhancement gate.</summary>
    MundaneBase,

    /// <summary>Wieldable base item that requires a paired enchantment (Starleather etc.).</summary>
    MasterworkBase,

    /// <summary>Magic item that functions on its own (implement, wearable, consumable, boon).</summary>
    StandaloneMagic,

    /// <summary>Magic item that MUST pair with a base (weapon/armor enchantment) or an enchantment (augment).</summary>
    Enchantment,
}
