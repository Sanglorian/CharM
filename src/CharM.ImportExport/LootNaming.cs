using CharM.Engine.Creation;
using CharM.Engine.Rules;
using CharM.Serialization;

namespace CharM.ImportExport;

/// <summary>
/// When a magic-item enchantment is applied to a specific base item, the
/// engine composes a displayed name like
/// <c>"Subtle Rapier +3"</c> from base "Rapier" + enchant "Subtle Weapon +3",
/// or <c>"Rhythm Blade Dagger +1"</c> from base "Dagger" + enchant
/// "Rhythm Blade +1".
///
/// The <c>&lt;loot name="..."&gt;</c> attribute on a <c>&lt;loot&gt;</c> row
/// only carries an explicit composite name when the item has an augment
/// (per <see cref="LootItem.CompositeName"/>'s contract). For non-augmented
/// magic weapons / armor / implements the engine recomputes the displayed
/// name on the fly when writing <c>&lt;PowerStats&gt;/&lt;Weapon&gt;</c>
/// blocks. Without this composition the export emits the raw enchant name
/// (e.g. "Subtle Weapon +3") which doesn't match what OCB would write.
/// </summary>
internal static class LootNaming
{
    /// <summary>
    /// Compose the display name for a loot item, mirroring OCB's
    /// <c>CharLootName(loot)</c>:
    /// <list type="number">
    ///   <item>If the loot has an explicit composite name (augmented items),
    ///     return it verbatim .</item>
    ///   <item>If there is no enchantment, return the base name.</item>
    ///   <item>If the enchantment's "Magic Item Type" field is "Artifact",
    ///     return the enchantment name verbatim.</item>
    ///   <item>Run the keyword-substitution chain (weapon / armor / implement
    ///     keywords) plus the always-tried "Ki Focused " prefix special
    ///     case; the first match wins.</item>
    ///   <item>Fall back to inserting the base name before the
    ///     <c>+N</c> or <c>(...)</c> suffix in the enchantment name.</item>
    /// </list>
    /// </summary>
    internal static string Compose(LootItem loot)
    {
        if (!string.IsNullOrEmpty(loot.CompositeName))
            return loot.CompositeName!;

        var baseName = loot.Base.Name ?? string.Empty;
        var enchant = loot.Enchantment;
        if (enchant is null) return baseName;

        return Compose(baseName, enchant.Name ?? string.Empty, enchant, loot.Base);
    }

    /// <summary>
    /// Core composition routine. Public for unit-test access.
    /// </summary>
    internal static string Compose(
        string baseName,
        string enchantName,
        RulesElement enchant,
        RulesElement @base)
    {
        if (string.IsNullOrEmpty(baseName)) return enchantName;
        if (string.IsNullOrEmpty(enchantName)) return baseName;

        // Artifact short-circuit: 
        // Enchant's "Magic Item Type" field == "Artifact" 
        // → return enchant name as-is.
        if (enchant.Fields.TryGetValue("Magic Item Type", out var mit)
            && string.Equals(mit?.Trim(), "Artifact", StringComparison.OrdinalIgnoreCase))
        {
            return enchantName;
        }

        // Universal try-keywords for any base type.
        if (TrySubstitute(enchantName, "weapon", baseName, out var r1)) return r1!;
        if (TrySubstitute(enchantName, "armor", baseName, out var r2)) return r2!;

        // Implement-only keywords. Only attempted when the base is NOT a
        // Weapon and NOT an Armor (OCB gates implement-keyword substitution, 
        // in practice this gate fires for Implement / Superior Implement / Gear-typed
        // bases, i.e. anything that isn't a melee/ranged weapon or worn
        // armor). Without this, "Staff of Ruin +3" applied to a
        // "Mindwarp staff" (Superior Implement) base falls through to
        // the dumb fallback and produces "Staff of Ruin Mindwarp staff +3"
        // instead of "Mindwarp staff of Ruin +3".
        bool baseIsImplementLike =
            !string.Equals(@base.Type, "Weapon", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(@base.Type, "Armor", StringComparison.OrdinalIgnoreCase);
        if (baseIsImplementLike)
        {
            if (TrySubstitute(enchantName, "holy symbol", baseName, out var ri1)) return ri1!;
            if (TrySubstitute(enchantName, "symbol", baseName, out var ri2)) return ri2!;
            if (TrySubstitute(enchantName, "orb", baseName, out var ri3)) return ri3!;
            if (TrySubstitute(enchantName, "rod", baseName, out var ri4)) return ri4!;
            if (TrySubstitute(enchantName, "staff", baseName, out var ri5)) return ri5!;
            if (TrySubstitute(enchantName, "tome", baseName, out var ri6)) return ri6!;
            if (TrySubstitute(enchantName, "totem", baseName, out var ri7)) return ri7!;
            if (TrySubstitute(enchantName, "wand", baseName, out var ri8)) return ri8!;
            if (TrySubstitute(enchantName, "ki focus", baseName, out var ri9)) return ri9!;
        }

        // Substitute "Ki Focused " + baseName for "ki focus" — handles weapons
        // wielded as Ki Focuses where the magic item name contains
        // "Ki Focus" (e.g. "Rain of Hammers Ki Focus +1" + Dagger →
        // "Rain of Hammers Ki Focused Dagger +1").
        if (TrySubstitute(enchantName, "ki focus", "Ki Focused " + baseName, out var rk)) return rk!;

        // Insert " " + baseName before the " +N" / " (...)" suffix in the enchantment name. ptr2 in the
        // decompile points at '+' or '(', then steps back one to land on
        // the preceding space; we mirror that with idx-1.
        int idx = enchantName.IndexOf('+');
        if (idx < 0) idx = enchantName.IndexOf('(');
        if (idx > 0)
        {
            int spaceIdx = idx - 1;
            return enchantName[..spaceIdx] + " " + baseName + enchantName[spaceIdx..];
        }
        return enchantName + " " + baseName;
    }

    /// <summary>
    /// Case-insensitive find of <paramref name="lookFor"/> in
    /// <paramref name="enchantName"/>; if found, replace with
    /// <paramref name="replacement"/>.
    /// </summary>
    private static bool TrySubstitute(string enchantName, string lookFor, string replacement, out string? result)
    {
        int idx = enchantName.IndexOf(lookFor, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            result = null;
            return false;
        }
        if (idx == 0)
        {
            result = replacement + enchantName[lookFor.Length..];
        }
        else
        {
            result = enchantName[..idx] + replacement + enchantName[(idx + lookFor.Length)..];
        }
        return true;
    }
}
