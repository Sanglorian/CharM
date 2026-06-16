using System.Text;
using System.Text.RegularExpressions;
using CharM.Engine.Creation;
using CharM.Engine.Evaluation;
using CharM.Engine.Rules;

namespace CharM.Engine.Powers;

/// <summary>
/// Calculates attack bonuses, damage, and other stats for a power card.
/// </summary>
public static partial class PowerStatCalculator
{
    private static readonly string[] AbilityNameOrder = Creation.AbilityNames.FullNames;

    /// <summary>
    /// Calculate power stats given a power element, character stats, and optional weapon.
    /// <paramref name="characterLevel"/> is used to select the appropriate
    /// "Level NN:" tier-up clause inside Hit/Effect text — at-will weapon
    /// powers like Magic Missile and Reaping Strike scale dice at L11/L21.
    /// </summary>
    public static PowerStatBlock Calculate(
        RulesElement power,
        StatBlock stats,
        RulesElement? weapon = null,
        int characterLevel = 1,
        Func<string, string?>? sourceNameResolver = null,
        Func<string, RulesElement?>? sourceElementResolver = null,
        Func<string, string?>? healingSourceNameResolver = null,
        bool displayZeroUnselectedModeVariant = false,
        ModifyOverlay? overlay = null,
        int? precomputedBeastAttackBonus = null)
    {
        var result = new PowerStatBlock();

        // Orcus class-key substitution: for a class-discipline power (tagged
        // "ability-swap"), weave the character's substitutable key abilities
        // into the attack/damage ability references so the engine's existing
        // "X or Y -> highest modifier" logic lets the character use the higher
        // of {printed key, class key}. Fully isolated: a no-op unless the power
        // carries the tag AND the character has Key Ability Swap elements, so
        // WotC content is unaffected. Works with or without a weapon equipped.
        if (stats.KeyAbilitySwaps.Count > 0
            && power.Categories.Contains("ability-swap", StringComparer.OrdinalIgnoreCase))
        {
            power = ApplyKeyAbilitySwaps(power, stats.KeyAbilitySwaps);
        }

        // Extract fields from the power
        string? attackAbility = PowerFieldParser.GetAttackAbility(power, overlay);
        result.Defense = PowerFieldParser.GetDefense(power, overlay);
        result.Keywords.AddRange(PowerFieldParser.GetKeywords(power));

        // Determine weapon properties
        int? proficiencyBonus = null;
        int? enhancementBonus = null;
        string? weaponDice = null;

        KeyAbilityOverride? keyAbilityOverride = null;
        bool strengthRangedBasicAttack = false;

        if (weapon is not null)
        {
            string? attackText = PowerFieldParser.GetAttackText(power);
            if (TrySelectWeaponModeVariant(attackText, weapon, out string selectedAttackText))
            {
                attackAbility = ExtractAttackAbility(selectedAttackText);
                result.Defense = ExtractDefense(selectedAttackText);
            }

            bool isWeaponPower = PowerFieldParser.IsWeaponPower(power);
            strengthRangedBasicAttack = UsesStrengthForRangedBasicAttack(power, weapon);
            keyAbilityOverride = ResolveKeyAbilityOverride(
                stats,
                power,
                attackAbility,
                weapon,
                sourceNameResolver,
                sourceElementResolver);
            if (keyAbilityOverride is not null)
                attackAbility = ApplyAttackPowerModifier(
                    keyAbilityOverride.Value.DisplayAbility,
                    attackAbility);
            else if (strengthRangedBasicAttack)
                attackAbility = "Strength";

            // Proficiency bonus applies only to weapon powers, not implement
            // powers, AND only when the character is trained with the weapon.
            // The XML field is "Proficiency Bonus" (with a space). Training is
            // tracked in StatBlock.TrainedWeapons, populated from active
            // "Weapon Proficiency (X)" elements during character build.
            //
            // Arena Training exception (Dark Sun gladiator
            // ID_FMP_CLASS_FEATURE_2893): when active AND the character is
            // NOT proficient with the wielded weapon, OCB treats the weapon
            // as IMPROVISED 
            // The improvised treatment overrides three things on the power card:
            //   1. proficiency bonus becomes +2 (regardless of base weapon)
            //   2. weapon damage die becomes 1d8 (one-handed) or 1d10
            //      (two-handed) regardless of base weapon damage
            //   3. weapon-group / proficiency-key feats still don't fire
            //      because the character is genuinely untrained
            // Catalog entry: docs/engine-special-cases.md §1 (Arena Training
            // ID_FMP_CLASS_FEATURE_2893).
            bool arenaImprovised = isWeaponPower
                && sourceNameResolver is not null
                && !IsTrainedWithWeapon(stats, weapon)
                && IsActive(sourceNameResolver, "ID_FMP_CLASS_FEATURE_2893");
            if (isWeaponPower
                && weapon.Fields.TryGetValue("Proficiency Bonus", out var prof)
                && int.TryParse(prof, out int p)
                && IsTrainedWithWeapon(stats, weapon))
            {
                proficiencyBonus = p;
            }
            else if (arenaImprovised)
            {
                proficiencyBonus = 2;
            }
            if (weapon.Fields.TryGetValue("Enhancement", out var enh) && TryParseEnhancement(enh, out int e))
                enhancementBonus = e;
            if (weapon.Fields.TryGetValue("Damage", out var dmg))
                weaponDice = ApplyOneSizeLarger(dmg, weapon, sourceNameResolver);
            if (arenaImprovised)
                weaponDice = IsTwoHandedWeapon(weapon) ? "1d10" : "1d8";
        }

        // Calculate attack bonus
        if (attackAbility is not null)
        {
            // Resolve "X or Y" choice constructs and surface the picked
            // ability for OCB's <AttackStat> tag (avoids leaking the raw
            // "Charisma or Constitution" text into power-card output).
            // Idempotent — CalcAttackBonus runs the same resolver internally.
            result.ResolvedAttackStat = ResolveAttackAbility(stats, attackAbility, out _);
            result.AttackBonus = CalcAttackBonus(stats, attackAbility, proficiencyBonus, enhancementBonus,
                out string components, precomputedBeastAttackBonus);
            result.AttackComponents = components;
        }
        else if (PowerFieldParser.HasLeadingAttackField(power))
        {
            result.ResolvedAttackStat = "Unknown";
            result.Defense = "Unknown";
            result.AttackBonus = CalcUnknownAttackBonus(stats, proficiencyBonus, enhancementBonus, out string components);
            result.AttackComponents = components;
        }

        // Skip per-feat / per-class attack bonuses for beast attacks.
        // OCB emits ONLY "+N Beast Attack Bonus" (and optional enhancement)
        // on beast power cards; character feats like Weapon Expertise /
        // Weapon Focus belong to the wielder, not the beast, and do not
        // apply to the beast's roll.
        bool isBeastAttack = attackAbility is not null && IsBeastAttackStat(attackAbility);

        if (!isBeastAttack
            && (attackAbility is not null || result.ResolvedAttackStat.Equals("Unknown", StringComparison.OrdinalIgnoreCase)))
        {
            var attackBonusAccumulator = new BonusComponentAccumulator(displaySuppressedTypedBonuses: true);
            if (enhancementBonus is { } enhancement && enhancement != 0)
                attackBonusAccumulator.Seed(enhancement, "Enhancement");

            var collectedAttackBonuses = CollectAttackBonuses(
                stats,
                power,
                weapon,
                attackBonusAccumulator,
                sourceNameResolver,
                sourceElementResolver);
            result.AttackBonus += collectedAttackBonuses.Total;
            result.AttackComponents += collectedAttackBonuses.Components;
        }

        // Parse and resolve damage
        var damagePower = weapon is not null
            ? ApplyWeaponModeVariantsToHit(power, weapon)
            : power;
        string? rawDamageText = power.Fields.TryGetValue("Hit", out string? rawHit) ? rawHit : null;
        string? damageText = PowerFieldParser.GetDamageText(damagePower, characterLevel);
        if (string.Equals(damageText, "As Above", StringComparison.OrdinalIgnoreCase))
        {
            result.DamageExpression = "As Above";
            damageText = null;
        }
        if (damageText is not null)
        {
            if (keyAbilityOverride is { AppliesToDamage: true })
                damageText = ReplaceDamageAbility(damageText, keyAbilityOverride.Value.DisplayAbility, keyAbilityOverride.Value.HalfDamage);
            else if (weapon is not null && strengthRangedBasicAttack)
                damageText = RangedBasicDexterityModifierPattern.Replace(damageText, "Strength modifier");

            var expr = Powers.DamageExpression.Parse(damageText);
            var damageComponents = new StringBuilder();
            if (displayZeroUnselectedModeVariant)
                damageComponents.Append(CollectZeroUnselectedModeVariantAbilityComponents(rawDamageText, damageText, stats));
            bool omitUnresolvedWeaponDice = weapon is not null && weaponDice is null;
            string? beastDice = ResolveBeastDice(power, sourceElementResolver);
            string resolved = ResolveDamage(expr, stats, weaponDice, beastDice, damageComponents, omitUnresolvedWeaponDice);
            var damageBonusAccumulator = new BonusComponentAccumulator(displaySuppressedTypedBonuses: true);
            bool hasDamageRoll = HasResolvedDamageRoll(expr, weaponDice);
            int totalDamageBonus = 0;

            if (weapon is not null && enhancementBonus is { } enhancement && enhancement != 0 && hasDamageRoll)
            {
                totalDamageBonus += enhancement;
                damageBonusAccumulator.Seed(enhancement, "Enhancement");
                damageComponents.Append(FormatFixedComponent(enhancement, "enhancement bonus"));
            }

            if (weapon is not null
                && hasDamageRoll
                && TryGetDualImplementSpellcasterBonus(weapon, out int dualImplementBonus))
            {
                totalDamageBonus += dualImplementBonus;
                damageComponents.Append(FormatFixedComponent(dualImplementBonus, "off-hand enhancement bonus"));
            }

            if (weapon is not null
                && hasDamageRoll
                && PowerFieldParser.IsWeaponPower(power)
                && TryGetVersatileUsedTwoHandedBonus(weapon, out int versatileBonus))
            {
                totalDamageBonus += versatileBonus;
                damageComponents.Append("+1 versatile weapon used two-handed.\n");
            }

            (int Total, string Components, List<string> BonusDice) collectedDamageBonuses = isBeastAttack
                ? (0, "", new List<string>())
                : CollectDamageBonuses(
                    stats,
                    power,
                    weapon,
                    characterLevel,
                    damageBonusAccumulator,
                    hasDamageRoll,
                    sourceNameResolver,
                    sourceElementResolver);
            totalDamageBonus += collectedDamageBonuses.Total;
            damageComponents.Append(collectedDamageBonuses.Components);
            if (collectedDamageBonuses.BonusDice.Count > 0)
                resolved = AddDamageDiceBonuses(resolved, collectedDamageBonuses.BonusDice);

            (int Total, string Components) powerFieldDamageBonuses = isBeastAttack
                ? (0, "")
                : CollectPowerFieldDamageBonuses(
                    stats,
                    power,
                    weapon,
                    sourceNameResolver);
            totalDamageBonus += powerFieldDamageBonuses.Total;
            damageComponents.Append(powerFieldDamageBonuses.Components);

            if (totalDamageBonus != 0)
            {
                resolved = AddDamageBonus(resolved, totalDamageBonus);
            }

            if (string.Equals(resolved, "0", StringComparison.Ordinal) && IsZeroOnlyAbilityDamage(expr))
            {
                result.DamageExpression = string.Empty;
                result.DamageComponents = damageComponents.ToString();
            }
            else
            {
                result.DamageExpression = resolved;
                result.DamageComponents = damageComponents.ToString();
            }

            result.DamageType = ExtractDamageType(power, damageText, characterLevel);

            if (ShouldSuppressProxyAttackDamage(power, sourceElementResolver))
            {
                result.DamageExpression = string.Empty;
                result.DamageComponents = string.Empty;
            }
        }

        result.Conditions = CollectConditions(stats, power, weapon, characterLevel, sourceNameResolver, sourceElementResolver);
        (result.Healing, result.HealingComponents) = CalcHealing(stats, power, healingSourceNameResolver ?? sourceNameResolver);

        return result;
    }

    private static int CalcUnknownAttackBonus(
        StatBlock stats,
        int? proficiencyBonus,
        int? enhancementBonus,
        out string components)
    {
        var sb = new StringBuilder();
        int total = 0;

        var halfLevelStat = stats.TryGetStat("HALF-LEVEL");
        int halfLevel = halfLevelStat?.ComputeValue(stats) ?? 0;
        total += halfLevel;
        sb.Append(halfLevel >= 0
            ? $"+{halfLevel} half your level.\n"
            : $"{halfLevel} half your level.\n");

        if (proficiencyBonus.HasValue)
        {
            total += proficiencyBonus.Value;
            sb.Append($"+{proficiencyBonus.Value} proficiency bonus.\n");
        }

        if (enhancementBonus.HasValue)
        {
            total += enhancementBonus.Value;
            sb.Append($"+{enhancementBonus.Value} enhancement bonus.\n");
        }

        components = sb.ToString();
        return total;
    }

    /// <summary>
    /// Calculate base attack bonus (CalcBAB equivalent).
    /// ability_mod + half_level + proficiency + enhancement
    /// </summary>
    public static int CalcAttackBonus(
        StatBlock stats,
        string attackAbility,
        int? proficiencyBonus,
        int? enhancementBonus,
        out string components,
        int? precomputedBeastAttackBonus = null)
    {
        var sb = new StringBuilder();
        int total = 0;

        int powerModifier = ExtractAttackPowerModifier(attackAbility);
        if (powerModifier != 0)
        {
            total += powerModifier;
            sb.Append(powerModifier > 0
                ? $"+{powerModifier} power modifier.\n"
                : $"{powerModifier} power modifier.\n");
        }

        // Beast powers use a pre-computed attack bonus. OCB reads the
        // canonical value from the source file's <Companions><Beast>
        // <AttackBonus> field — that's the value the player can override
        // (e.g. for homebrew beasts whose ability scores are tweaked). If
        // the caller supplied that precomputed value, prefer it; else fall
        // back to the level + highest companion ability mod formula.
        if (IsBeastAttackStat(attackAbility))
        {
            int beastBonus = precomputedBeastAttackBonus
                ?? ComputeBeastAttackBonus(stats);
            total += beastBonus;
            sb.Append(beastBonus >= 0
                ? $"+{beastBonus} Beast Attack Bonus.\n"
                : $"{beastBonus} Beast Attack Bonus.\n");

            if (enhancementBonus.HasValue)
            {
                total += enhancementBonus.Value;
                sb.Append($"+{enhancementBonus.Value} enhancement bonus.\n");
            }

            components = sb.ToString();
            return total;
        }

        // 1. Ability modifier
        string resolvedAttackAbility = ResolveAttackAbility(stats, attackAbility, out int abilityScore);
        int abilityMod = StatEvaluator.GetAbilityMod(abilityScore);
        total += abilityMod;
        sb.Append(abilityMod >= 0
            ? $"+{abilityMod} {resolvedAttackAbility} modifier.\n"
            : $"{abilityMod} {resolvedAttackAbility} modifier.\n");

        // 2. Half-level
        var halfLevelStat = stats.TryGetStat("HALF-LEVEL");
        int halfLevel = halfLevelStat?.ComputeValue(stats) ?? 0;
        total += halfLevel;
        sb.Append(halfLevel >= 0
            ? $"+{halfLevel} half your level.\n"
            : $"{halfLevel} half your level.\n");

        // 3. Proficiency bonus (weapon powers only)
        if (proficiencyBonus.HasValue)
        {
            total += proficiencyBonus.Value;
            sb.Append($"+{proficiencyBonus.Value} proficiency bonus.\n");
        }

        // 4. Enhancement bonus
        if (enhancementBonus.HasValue)
        {
            total += enhancementBonus.Value;
            sb.Append($"+{enhancementBonus.Value} enhancement bonus.\n");
        }

        components = sb.ToString();
        return total;
    }

    /// <summary>
    /// Resolve a damage expression against character stats and weapon.
    /// Substitutes [W] with actual weapon dice, resolves ability modifier references.
    /// </summary>
    public static string ResolveDamage(
        DamageExpression expr,
        StatBlock stats,
        string? weaponDice)
        => ResolveDamage(expr, stats, weaponDice, beastDice: null, components: null);

    private static string ResolveDamage(
        DamageExpression expr,
        StatBlock stats,
        string? weaponDice,
        StringBuilder? components)
        => ResolveDamage(expr, stats, weaponDice, beastDice: null, components, omitUnresolvedWeaponDice: false);

    private static string ResolveDamage(
        DamageExpression expr,
        StatBlock stats,
        string? weaponDice,
        string? beastDice,
        StringBuilder? components,
        bool omitUnresolvedWeaponDice = false)
    {
        var parts = new List<string>();
        int flatTotal = 0;
        bool sawFlatTerm = false;

        foreach (var component in expr.Components)
        {
            switch (component)
            {
                case DamageComponent.WeaponDice wd:
                    int multiplier = wd.Multiplier;
                    if (expr.Twice)
                        multiplier *= 2;

                    if (weaponDice is not null)
                    {
                        if (multiplier == 1)
                        {
                            parts.Add(weaponDice);
                        }
                        else
                        {
                            // Parse weapon dice to multiply: "1d8" with multiplier 2 → "2d8"
                            var parsed = ParseDiceString(weaponDice);
                            if (parsed.HasValue)
                                parts.Add($"{parsed.Value.Count * multiplier}d{parsed.Value.Sides}");
                            else
                                parts.Add(weaponDice);
                        }
                    }
                    else
                    {
                        if (!omitUnresolvedWeaponDice)
                            parts.Add(multiplier == 1 ? "1[W]" : $"{multiplier}[W]");
                    }
                    break;

                case DamageComponent.BeastDice bd:
                    int beastMultiplier = bd.Multiplier;
                    if (expr.Twice)
                        beastMultiplier *= 2;

                    if (beastDice is not null)
                    {
                        var parsed = ParseDiceString(beastDice);
                        if (parsed.HasValue)
                            parts.Add($"{parsed.Value.Count * beastMultiplier}d{parsed.Value.Sides}");
                        else
                            parts.Add(beastDice);
                    }
                    else
                    {
                        parts.Add(beastMultiplier == 1 ? "1[B]" : $"{beastMultiplier}[B]");
                    }
                    break;

                case DamageComponent.Dice dice:
                    int count = expr.Twice ? dice.Count * 2 : dice.Count;
                    parts.Add($"{count}d{dice.Sides}");
                    break;

                case DamageComponent.FlatBonus flat:
                    flatTotal += flat.Value;
                    sawFlatTerm = true;
                    break;

                case DamageComponent.AbilityMod am:
                    // Check beast-ability semantics on the RAW am.AbilityName
                    // before SelectAbility/IsHighestAbilityReference rewrites
                    // "ability" / "primary ability" to the character's
                    // highest character ability. "beast's ability modifier"
                    // means highest COMPANION ability, not highest character
                    // ability.
                    bool beastAnyAbility = am.IsBeast
                        && (string.Equals(am.AbilityName, "ability", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(am.AbilityName, "primary ability", StringComparison.OrdinalIgnoreCase));

                    string chosenAbility;
                    string statAbility;
                    int abilityScore;
                    int rawMod;

                    if (beastAnyAbility)
                    {
                        rawMod = ComputeBeastAbilityModifier(stats);
                        chosenAbility = "beast's Ability";
                    }
                    else if (am.IsBeast)
                    {
                        // "beast's <specific ability> modifier" — use
                        // Companion.X score and tag the component for display.
                        chosenAbility = SelectAbility(stats, am.AbilityName, am.Alternatives);
                        statAbility = GetAbilityStatName(chosenAbility);
                        string companionStat = "Companion." + statAbility;
                        var beastStat = stats.TryGetStat(companionStat);
                        abilityScore = beastStat?.ComputeValue(stats) ?? 0;
                        rawMod = StatEvaluator.GetAbilityMod(abilityScore);
                        chosenAbility = "beast's " + chosenAbility;
                    }
                    // Legacy fallback (no IsBeast flag, but the character has
                    // Companion.* stats AND the parsed ability is "ability"):
                    // preserved for the historical "beast's ability modifier"
                    // sites that some non-AbilityMod-regex paths might still
                    // produce.
                    else if (string.Equals(am.AbilityName, "ability", StringComparison.OrdinalIgnoreCase)
                        && stats.TryGetStat("Companion.Strength") is not null)
                    {
                        rawMod = ComputeBeastAbilityModifier(stats);
                        chosenAbility = "beast's Ability";
                    }
                    else
                    {
                        chosenAbility = SelectAbility(stats, am.AbilityName, am.Alternatives);
                        statAbility = GetAbilityStatName(chosenAbility);
                        var abilityStat = stats.TryGetStat(statAbility);
                        abilityScore = abilityStat?.ComputeValue(stats) ?? 0;
                        rawMod = StatEvaluator.GetAbilityMod(abilityScore);
                    }

                    // Inline scaling like "half your Wisdom modifier" or "twice your Strength
                    // modifier". Round toward zero to mirror 4e's halving convention.
                    int mod = (int)(rawMod * am.Multiplier);
                    flatTotal += mod;
                    sawFlatTerm = true;
                    components?.Append(FormatAbilityComponent(mod, chosenAbility, am.Multiplier));
                    break;
            }
        }

        string joined = string.Join("+", parts);
        if (flatTotal != 0 || (parts.Count == 0 && sawFlatTerm))
            joined = AppendSignedDamageBonus(joined, flatTotal);

        return joined;
    }

    private static bool HasResolvedDamageRoll(DamageExpression expr, string? weaponDice)
    {
        foreach (var component in expr.Components)
        {
            if (component is DamageComponent.Dice)
                return true;
            if (component is DamageComponent.WeaponDice && weaponDice is not null)
                return true;
            if (component is DamageComponent.BeastDice)
                return true;
        }

        return false;
    }

    private static bool IsZeroOnlyAbilityDamage(DamageExpression expr)
        => expr.Components.Count > 0
            && expr.Components.All(static component => component is DamageComponent.AbilityMod);

    /// <summary>
    /// Resolve the beast damage dice (for [B] tokens) from companion elements.
    /// Looks up companion elements via sourceElementResolver and reads their Damage field.
    /// </summary>
    private static string? ResolveBeastDice(
        RulesElement power,
        Func<string, RulesElement?>? sourceElementResolver)
    {
        // If the power itself has a Damage field (some companion cards do), use it
        if (power.Fields.TryGetValue("Damage", out var dmg) && !string.IsNullOrWhiteSpace(dmg))
            return dmg.Trim();

        // Otherwise, try to find the companion element from the power's categories
        // Companion powers from Tivaan's cards have IDs like ID_TIV_COMPANION-CAT-1
        // Their companion base has ID_FMP_COMPANION_N with a Damage field
        if (sourceElementResolver is null)
            return null;

        // Check common companion IDs by looking at the character's companion elements
        foreach (var companionId in CommonCompanionIds)
        {
            var companion = sourceElementResolver(companionId);
            if (companion is not null
                && companion.Fields.TryGetValue("Damage", out var companionDmg)
                && !string.IsNullOrWhiteSpace(companionDmg))
            {
                return companionDmg.Trim();
            }
        }

        return null;
    }

    // The 8 core ranger companion IDs from Martial Power
    private static readonly string[] CommonCompanionIds =
    [
        "ID_FMP_COMPANION_1", // Bear
        "ID_FMP_COMPANION_2", // Boar
        "ID_FMP_COMPANION_3", // Cat
        "ID_FMP_COMPANION_4", // Lizard
        "ID_FMP_COMPANION_5", // Raptor
        "ID_FMP_COMPANION_6", // Serpent
        "ID_FMP_COMPANION_7", // Spider
        "ID_FMP_COMPANION_8", // Wolf
    ];

    private static string ExtractDamageType(RulesElement power, string damageText, int characterLevel)
    {
        if (power.Fields.TryGetValue("Hit", out string? rawHit) && HasActiveLevelTierLine(rawHit, characterLevel))
            return string.Empty;

        string text = damageText.Trim().TrimEnd('.');
        if (!StartsWithDirectDamageClause(text))
            return string.Empty;

        foreach (var (type, canonical) in DamageTypeNames)
        {
            if (!text.EndsWith(" " + type, StringComparison.OrdinalIgnoreCase))
                continue;

            string beforeType = text[..^type.Length].TrimEnd();
            if (EndsWithSingleAbilityModifier(power, beforeType) || IsDiceOnlyDamageType(power, beforeType))
                return canonical;
        }

        if (TryExtractVariantAttackCompositeDamageType(power, text, out string variantCompositeType))
            return variantCompositeType;

        return string.Empty;
    }

    private static bool TryExtractVariantAttackCompositeDamageType(RulesElement power, string text, out string damageType)
    {
        damageType = string.Empty;
        if (power.Fields.TryGetValue("Attack", out string? attack) && !string.IsNullOrWhiteSpace(attack))
            return false;
        if (!power.Fields.Keys.Any(field => field.StartsWith("Attack (", StringComparison.OrdinalIgnoreCase)))
            return false;

        int modifierIndex = text.IndexOf(" modifier ", StringComparison.OrdinalIgnoreCase);
        if (modifierIndex < 0)
            return false;

        string afterModifier = text[(modifierIndex + " modifier ".Length)..].TrimStart();
        foreach (var (type, canonical) in DamageTypeNames)
        {
            if (!afterModifier.StartsWith(type, StringComparison.OrdinalIgnoreCase))
                continue;
            string tail = afterModifier[type.Length..].TrimStart();
            if (tail.StartsWith("and ", StringComparison.OrdinalIgnoreCase)
                || tail.StartsWith(",", StringComparison.OrdinalIgnoreCase))
            {
                damageType = canonical;
                return true;
            }
        }

        return false;
    }

    private static bool StartsWithDirectDamageClause(string text)
    {
        string trimmed = text.TrimStart();
        if (trimmed.Length == 0)
            return false;
        if (char.IsDigit(trimmed[0]) || trimmed[0] == '[')
            return true;
        if (trimmed.StartsWith("your ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("ability modifier", StringComparison.OrdinalIgnoreCase))
            return true;
        foreach (string ability in AbilityNameOrder.Concat(
            Creation.AbilityNames.StandardOrder.Select(Creation.AbilityNames.GetAbbreviation)))
        {
            if (trimmed.StartsWith(ability + " modifier", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool HasActiveLevelTierLine(string text, int characterLevel)
    {
        foreach (string line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("Level ", StringComparison.OrdinalIgnoreCase))
            {
                int start = "Level ".Length;
                if (HasReachedThreshold(trimmed, start, characterLevel))
                    return true;
            }

            const string increasePrefix = "Increase damage to ";
            if (trimmed.StartsWith(increasePrefix, StringComparison.OrdinalIgnoreCase))
            {
                int levelIndex = trimmed.LastIndexOf(" level", StringComparison.OrdinalIgnoreCase);
                if (levelIndex < 0)
                    continue;
                int end = levelIndex;
                int start = end;
                while (start > 0 && char.IsLetter(trimmed[start - 1]))
                    start--;
                end = start;
                while (start > 0 && char.IsDigit(trimmed[start - 1]))
                    start--;
                if (HasReachedThreshold(trimmed, start, characterLevel))
                    return true;
            }
        }
        return false;
    }

    private static bool HasReachedThreshold(string text, int start, int characterLevel)
    {
        int end = start;
        while (end < text.Length && char.IsDigit(text[end]))
            end++;
        return end > start
            && int.TryParse(text[start..end], out int threshold)
            && characterLevel >= threshold;
    }

    private static bool EndsWithSingleAbilityModifier(RulesElement power, string text)
    {
        const string suffix = " modifier";
        if (!text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return false;

        string beforeModifier = text[..^suffix.Length].TrimEnd();
        int plusIndex = beforeModifier.LastIndexOf('+');
        string segment = (plusIndex >= 0 ? beforeModifier[(plusIndex + 1)..] : beforeModifier).Trim();

        if (segment.StartsWith("twice ", StringComparison.OrdinalIgnoreCase))
            segment = segment["twice ".Length..].TrimStart();
        if (segment.StartsWith("your ", StringComparison.OrdinalIgnoreCase))
            segment = segment["your ".Length..].TrimStart();
        if (segment.StartsWith("beast's ", StringComparison.OrdinalIgnoreCase))
            segment = segment["beast's ".Length..].TrimStart();

        if (string.Equals(segment, "ability", StringComparison.OrdinalIgnoreCase))
            return power.Fields.TryGetValue("_ThemePower", out string? themePower)
                    && !string.IsNullOrWhiteSpace(themePower)
                || power.Fields.TryGetValue("Class", out string? classId)
                    && classId.StartsWith("ID_FMP_THEME_", StringComparison.OrdinalIgnoreCase);

        return plusIndex >= 0 && TryNormalizeAbilityName(segment, out _, out _);
    }

    private static bool IsDiceOnlyDamageType(RulesElement power, string text)
    {
        if (text.IndexOf('+') >= 0 || text.IndexOf("modifier", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;

        string trimmed = text.Trim();
        return IsMultiAttackWithoutAttackField(power) && IsSimpleDiceTerm(trimmed);
    }

    private static bool IsMultiAttackWithoutAttackField(RulesElement power)
        => (!power.Fields.TryGetValue("Attack", out string? attack) || string.IsNullOrWhiteSpace(attack))
            && (power.Fields.TryGetValue("Primary Attack", out string? primaryAttack)
                    && !string.IsNullOrWhiteSpace(primaryAttack)
                || power.Fields.TryGetValue("Secondary Attack", out string? secondaryAttack)
                    && !string.IsNullOrWhiteSpace(secondaryAttack));

    private static bool IsSimpleDiceTerm(string text)
    {
        if (ParseDiceString(text) is not null)
            return true;

        if (!text.EndsWith("[W]", StringComparison.OrdinalIgnoreCase))
            return false;

        string prefix = text[..^"[W]".Length].Trim();
        return prefix.Length == 0 || int.TryParse(prefix, out _);
    }

    private static (string healing, string components) CalcHealing(
        StatBlock stats,
        RulesElement power,
        Func<string, string?>? sourceNameResolver)
    {
        if (!PowerFieldParser.GetKeywords(power).Any(k => k.Equals("Healing", StringComparison.OrdinalIgnoreCase)))
            return (string.Empty, string.Empty);

        int total = 0;
        bool sawHealingStat = false;
        int healingComponentCount = 0;
        var components = new StringBuilder();

        AddHealingStat("Healing");
        AddHealingStat(power.Name + ":healing");
        if (power.Fields.TryGetValue("Class", out string? classId) && !string.IsNullOrWhiteSpace(classId))
            AddHealingStat(classId + ":healing");

        if (sawHealingStat && healingComponentCount == 0)
            components.Insert(0, "0 bonus\n");

        string healing = total != 0
            ? $"regain an additional {total} hit points."
            : string.Empty;
        return (healing, components.ToString());

        void AddHealingStat(string statName)
        {
            var stat = stats.TryGetStat(statName);
            if (stat is null) return;

            sawHealingStat = true;
            total += stat.ComputeValue(stats);
            foreach (var contribution in stat.Contributions)
            {
                if (!contribution.Active
                    || !string.IsNullOrWhiteSpace(contribution.Condition)
                    || contribution.StringPayload is not null)
                    continue;
                components.Append(FormatHealingComponent(contribution, stats, sourceNameResolver));
                healingComponentCount++;
            }
        }
    }

    private static bool TryGetDualImplementSpellcasterBonus(RulesElement weapon, out int bonus)
    {
        bonus = 0;
        if (!weapon.Fields.TryGetValue("_Dual Implement Spellcaster Other Enhancement", out string? rawBonus)
            || !int.TryParse(rawBonus, out int parsed)
            || parsed == 0)
        {
            return false;
        }

        bonus = parsed;
        return true;
    }

    /// <summary>
    /// Mirror of OCB's "delegated attack" detection. Returns true for powers
    /// whose parent Effect explicitly hands the power to an ally or target
    /// ("The target can use the power X.") rather than the caster. By 4e
    /// rules-as-written these powers SHOULD emit zero <c>&lt;Weapon&gt;</c>
    /// blocks in PowerStats — the ally chooses their own weapon on their
    /// turn — but OCB only suppresses weapon emission for the single
    /// hardcoded id <c>ID_FMP_POWER_11615</c> (Coordinated Assault Attack).
    /// For other powers in the class (Destructive Surprise Attack,
    /// Beckoning Strike Attack, Steel Unity Attack — 229 powers total) OCB
    /// emits weapons against the caster's attack stats but zeroes Damage
    /// via the private <c>ShouldSuppressProxyAttackDamage</c> path.
    ///
    /// Exposed publicly so future rules-correct mode can gate weapon
    /// emission on this predicate. See <c>PowerStatsBuilder.BuildEntry</c>
    /// for the commented call-site.
    /// </summary>
    public static bool IsProxyDelegatedAttack(
        RulesElement power,
        Func<string, RulesElement?>? sourceElementResolver)
        => ShouldSuppressProxyAttackDamage(power, sourceElementResolver);

    private static bool ShouldSuppressProxyAttackDamage(
        RulesElement power,
        Func<string, RulesElement?>? sourceElementResolver)
    {
        if (sourceElementResolver is null
            || !power.Fields.TryGetValue("_ParentPower", out string? parentId)
            || string.IsNullOrWhiteSpace(parentId)
            || !power.Fields.TryGetValue("Requirement", out string? requirement)
            || !requirement.Contains("must be active", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parent = sourceElementResolver(parentId);
        return parent is not null
            && parent.Fields.TryGetValue("Effect", out string? effect)
            && (effect.Contains("target can use the power", StringComparison.OrdinalIgnoreCase)
                || effect.Contains("ally can use the power", StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryGetVersatileUsedTwoHandedBonus(RulesElement weapon, out int bonus)
    {
        bonus = 0;
        if (!weapon.Fields.TryGetValue("_Versatile Used Two-Handed", out string? raw)
            || !int.TryParse(raw, out int parsed)
            || parsed == 0
            || !WeaponHasProperty(weapon, "Versatile"))
        {
            return false;
        }

        bonus = 1;
        return true;
    }

    private static string FormatHealingComponent(
        StatContribution contribution,
        StatBlock stats,
        Func<string, string?>? sourceNameResolver)
    {
        int value = contribution.GetEffectiveValue(stats);
        var sb = new StringBuilder();
        if (value >= 0)
            sb.Append('+');
        sb.Append(value).Append(' ');

        if (!string.IsNullOrWhiteSpace(contribution.BonusType))
            sb.Append(contribution.BonusType).Append(' ');

        sb.Append(value < 0 ? "penalty" : "bonus");
        if (value < 0 && TryFormatMissingRequirementReason(contribution.RequiresText, out string missingRequirementReason))
            sb.Append(' ').Append(missingRequirementReason);
        if (!string.IsNullOrWhiteSpace(contribution.SourceElementId))
        {
            string? sourceName = sourceNameResolver?.Invoke(contribution.SourceElementId);
            if (!string.IsNullOrWhiteSpace(sourceName))
                sb.Append(" - ").Append(sourceName);
        }
        sb.Append('\n');
        return sb.ToString();
    }

    // ===== Orcus class-key ability substitution =================================

    /// <summary>
    /// Return a copy of <paramref name="power"/> whose attack and damage ability
    /// references include the character's substitutable key abilities as "or X"
    /// alternatives. The existing resolver then takes the highest-modifier
    /// ability among them, implementing Orcus's "use your class key ability
    /// instead, if higher" without forcing it. Only called for "ability-swap"
    /// powers when swaps exist, so it never touches other content.
    /// </summary>
    private static RulesElement ApplyKeyAbilitySwaps(RulesElement power, IReadOnlySet<string> swaps)
    {
        var source = power.FieldEntries.Count > 0
            ? power.FieldEntries
            : power.Fields.Select(p => new KeyValuePair<string, string>(p.Key, p.Value)).ToList();

        var entries = new List<KeyValuePair<string, string>>(source.Count);
        foreach (var kv in source)
        {
            string value = kv.Value;
            if (IsAttackAbilityField(kv.Key))
                value = AddAttackAbilityAlternatives(value, swaps);
            else if (kv.Key.Equals("Hit", StringComparison.OrdinalIgnoreCase))
                value = AddDamageAbilityAlternatives(value, swaps);
            entries.Add(new KeyValuePair<string, string>(kv.Key, value));
        }

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries)
            fields.TryAdd(e.Key, e.Value);

        return new RulesElement
        {
            InternalId = power.InternalId,
            Name = power.Name,
            Type = power.Type,
            Source = power.Source,
            Prereqs = power.Prereqs,
            Fields = fields,
            FieldEntries = entries,
            Rules = power.Rules,
            Categories = [.. power.Categories],
        };
    }

    private static bool IsAttackAbilityField(string key) =>
        key.Equals("Attack", StringComparison.OrdinalIgnoreCase)
        || key.Equals("Primary Attack", StringComparison.OrdinalIgnoreCase)
        || key.Equals("Secondary Attack", StringComparison.OrdinalIgnoreCase)
        || key.Equals("Tertiary Attack", StringComparison.OrdinalIgnoreCase);

    /// <summary>Insert " or &lt;ability&gt;" before the " vs &lt;defense&gt;" of an attack line.</summary>
    private static string AddAttackAbilityAlternatives(string text, IReadOnlySet<string> swaps)
    {
        if (!AbilityNameOrder.Any(a => ContainsAbilityWord(text, a)))
            return text; // no ability reference to extend
        string addition = BuildAdditions(text, swaps);
        if (addition.Length == 0)
            return text;
        int vs = text.IndexOf(" vs", StringComparison.OrdinalIgnoreCase);
        return vs >= 0 ? text.Insert(vs, addition) : text + addition;
    }

    /// <summary>Insert " or &lt;ability&gt;" before " modifier" in a damage line.</summary>
    private static string AddDamageAbilityAlternatives(string text, IReadOnlySet<string> swaps)
    {
        var match = DamageAbilityModifierRegex().Match(text);
        if (!match.Success)
            return text;
        string addition = BuildAdditions(text, swaps);
        if (addition.Length == 0)
            return text;
        int insertAt = match.Index + match.Length - " modifier".Length;
        return text.Insert(insertAt, addition);
    }

    private static string BuildAdditions(string text, IReadOnlySet<string> swaps)
    {
        var toAdd = swaps.Where(s => !ContainsAbilityWord(text, s)).ToList();
        return toAdd.Count == 0 ? string.Empty : " or " + string.Join(" or ", toAdd);
    }

    private static bool ContainsAbilityWord(string text, string ability)
    {
        int i = text.IndexOf(ability, StringComparison.OrdinalIgnoreCase);
        return i >= 0;
    }
}
