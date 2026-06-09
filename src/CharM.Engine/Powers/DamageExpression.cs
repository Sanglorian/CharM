using System.Text.RegularExpressions;

namespace CharM.Engine.Powers;

/// <summary>
/// A parsed damage or healing expression component.
/// </summary>
public abstract record DamageComponent
{
    /// <summary>Dice expression: NdM (e.g., 2d6, 1d10)</summary>
    public sealed record Dice(int Count, int Sides) : DamageComponent;

    /// <summary>Weapon damage dice: [W] multiplied by a factor (e.g., 2[W])</summary>
    public sealed record WeaponDice(int Multiplier = 1) : DamageComponent;

    /// <summary>Beast companion damage dice: [B] multiplied by a factor (e.g., 1[B]).
    /// Resolved from the companion element's Damage field.</summary>
    public sealed record BeastDice(int Multiplier = 1) : DamageComponent;

    /// <summary>Flat numeric bonus</summary>
    public sealed record FlatBonus(int Value) : DamageComponent;

    /// <summary>Ability modifier reference (e.g., "Strength modifier").
    /// <see cref="Multiplier"/> captures inline multiplier prefixes:
    /// "twice ... modifier" (2.0) — present in OCB's string anchors — and
    /// "half ... modifier" (0.5) — NOT scanned by OCB organically, but
    /// produced by our HALF-DMG: key-ability override (Melee Training feats,
    /// PHB3 p. 200) when it rewrites the Hit text. Default is 1.0.
    /// <para>
    /// <see cref="Alternatives"/> is populated when the source text reads
    /// "X or Y modifier" or "X modifier or Y modifier" — a player choice in
    /// 4e. The full candidate set is { <see cref="AbilityName"/> } ∪
    /// <see cref="Alternatives"/>. The resolver picks the candidate matching
    /// the character's active "Ability Choice" element (e.g. warlock
    /// Eldritch Blast Charisma vs Constitution); falls back to highest
    /// modifier when no candidate matches a chosen ability. Null/empty when
    /// the source text named a single ability.
    /// </para></summary>
    public sealed record AbilityMod(string AbilityName, double Multiplier = 1.0) : DamageComponent
    {
        public IReadOnlyList<string>? Alternatives { get; init; }

        /// <summary>
        /// True when the source text said "beast's X modifier" (vs the
        /// default "X modifier" / "your X modifier"). When set, the resolver
        /// uses the companion's <c>Companion.{AbilityName}</c> stat for the
        /// numeric value and prefixes the display label with "beast's".
        /// </summary>
        public bool IsBeast { get; init; }
    }
}

/// <summary>
/// A complete damage expression: a list of components that are summed.
/// Example: "2[W] + 1d6 + Strength modifier" → [WeaponDice(2), Dice(1,6), AbilityMod("Strength")]
/// </summary>
public sealed partial class DamageExpression
{
    private const string AbilityTokenPattern =
        "primary ability|ability|Strength|Dexterity|Constitution|Intelligence|Wisdom|Charisma|str|dex|con|int|wis|cha";
    private const string DamageTypeTokenPattern =
        "acid|cold|fire|force|lightning|necrotic|poison|psychic|radiant|thunder";

    // Matches weapon dice: optional multiplier followed by [W]
    private static Regex WeaponDicePattern => WeaponDiceRegex();

    // Matches beast damage dice: optional multiplier followed by [B]
    private static Regex BeastDicePattern => BeastDiceRegex();

    // Matches dice expressions: NdM
    private static Regex DicePattern => DiceRegex();

    // Matches ability modifier references with an optional inline multiplier.
    // OCB binary inspection: only "twice" appears as a multiplier prefix on
    // ability modifiers (literal anchor: "twice intelligence modifier damage.").
    // OCB does NOT scan for "one-half" / "one half" prefixes organically —
    // those don't appear in D20RulesEngine.dll's string table. The "half"
    // anchor in OCB is exclusively for "half your level", a different concept.
    // We DO recognize "half X modifier" here because our HALF-DMG: key-ability
    // override injection (Melee Training feats — PHB3 p. 200) rewrites the Hit
    // text to "half <ability> modifier damage", and the parser then needs to
    // apply a 0.5 multiplier so the displayed damage matches OCB.
    // Possessives: only "your" and "beast's" appear in OCB's string anchors;
    // "the target's" is fictional and is omitted.
    // Captures: 1=multiplier word ("twice"|"half", optional), 2=possessive,
    // 3=ability name.
    private static Regex AbilityModPattern => AbilityModRegex();

    private static Regex HighestAbilityModPattern => HighestAbilityModRegex();

    // Matches "X or Y modifier" / "X modifier or Y modifier" — a player choice
    // in 4e (most prominently the warlock Eldritch Blast / pact at-will
    // Cha-or-Con damage). The first "modifier" is optional so this handles both
    // forms. Without this pass, the bare AbilityModPattern either misses the
    // first ability (when only Y is followed by "modifier", e.g. Eldritch
    // Blast: "1d10 + Charisma or Constitution modifier") or DOUBLE-COUNTS by
    // emitting both abilities as separate components (e.g. Hand of Blight:
    // "1d8 + Charisma modifier or Constitution modifier"). Captures:
    // 1=multiplier word, 2=first ability, 3=second ability.
    private static Regex AbilityChoicePattern => AbilityChoiceRegex();

    private static Regex AbilityChoiceDamagePattern => AbilityChoiceDamageRegex();

    private static Regex AbilityListDamagePattern => AbilityListDamageRegex();

    private static Regex AbilityDamagePattern => AbilityDamageRegex();

    // Matches flat integers (standalone, not part of NdM or N[W])
    private static Regex FlatPattern => FlatRegex();

    // "twice" followed by a weapon-dice / dice / numeric token (NOT followed by an
    // ability-mod construct). Used so the top-level Twice flag doesn't eat the
    // word from "twice your Strength modifier".
    private static Regex TwiceWeaponDicePrefix => TwiceWeaponDicePrefixRegex();

    public List<DamageComponent> Components { get; } = [];

    /// <summary>
    /// When true, dice counts are doubled ("twice" prefix).
    /// </summary>
    public bool Twice { get; set; }

    /// <summary>
    /// Parse a damage text string into components.
    /// Handles: "[W]", "2[W]", "NdM", "N", "Ability modifier"
    /// </summary>
    public static DamageExpression Parse(string? damageText)
    {
        var expr = new DamageExpression();

        if (string.IsNullOrWhiteSpace(damageText))
            return expr;

        string text = damageText.Trim();

        // Check for "twice" prefix that scales WEAPON DICE (e.g., "twice 1[W]" or
        // "twice 1d8"). We must NOT consume "twice your Strength modifier" here —
        // that's an ability-mod multiplier handled by AbilityModPattern below.
        if (TwiceWeaponDicePrefix.IsMatch(text))
        {
            expr.Twice = true;
            text = text["twice".Length..].TrimStart();
        }

        // Collect all components with their source position for ordering
        var positioned = new List<(int Position, DamageComponent Component)>();
        var claimed = new HashSet<int>();

        // 1. Extract weapon dice: [W] or N[W]
        foreach (Match m in WeaponDicePattern.Matches(text))
        {
            int multiplier = m.Groups[1].Success ? int.Parse(m.Groups[1].Value) : 1;
            positioned.Add((m.Index, new DamageComponent.WeaponDice(multiplier)));
            ClaimRange(claimed, m.Index, m.Length);
        }

        // 1b. Extract beast damage dice: [B] or N[B]
        foreach (Match m in BeastDicePattern.Matches(text))
        {
            int multiplier = m.Groups[1].Success ? int.Parse(m.Groups[1].Value) : 1;
            positioned.Add((m.Index, new DamageComponent.BeastDice(multiplier)));
            ClaimRange(claimed, m.Index, m.Length);
        }

        // 2a. Extract "X or Y modifier" choice constructs FIRST so the bare
        //     AbilityModPattern doesn't either miss the first ability (only Y
        //     followed by "modifier") or double-count both as separate
        //     components. Each match becomes one AbilityMod with both abilities
        //     in Alternatives; the resolver picks based on player choice.
        foreach (Match m in AbilityChoicePattern.Matches(text))
        {
            double multiplier = 1.0;
            if (m.Groups[1].Success
                && m.Groups[1].Value.Equals("twice", StringComparison.OrdinalIgnoreCase))
            {
                multiplier = 2.0;
            }
            string first = m.Groups[2].Value;
            string second = m.Groups[3].Value;
            positioned.Add((m.Index, new DamageComponent.AbilityMod(first, multiplier)
            {
                Alternatives = new[] { second },
            }));
            ClaimRange(claimed, m.Index, m.Length);
        }

        foreach (Match m in AbilityChoiceDamagePattern.Matches(text))
        {
            if (claimed.Contains(m.Index))
                continue;
            string first = m.Groups[1].Value;
            string second = m.Groups[2].Value;
            positioned.Add((m.Index, new DamageComponent.AbilityMod(first)
            {
                Alternatives = new[] { second },
            }));
            ClaimRange(claimed, m.Index, m.Length);
        }

        foreach (Match m in AbilityListDamagePattern.Matches(text))
        {
            if (claimed.Contains(m.Index))
                continue;
            string[] abilities = SplitAbilityList(m.Groups["list"].Value);
            if (abilities.Length == 0)
                continue;
            positioned.Add((m.Index, new DamageComponent.AbilityMod(abilities[0])
            {
                Alternatives = abilities.Skip(1).ToArray(),
            }));
            ClaimRange(claimed, m.Index, m.Length);
        }

        foreach (Match m in HighestAbilityModPattern.Matches(text))
        {
            if (claimed.Contains(m.Index))
                continue;
            double multiplier = 1.0;
            if (m.Groups[1].Success
                && m.Groups[1].Value.Equals("twice", StringComparison.OrdinalIgnoreCase))
            {
                multiplier = 2.0;
            }
            positioned.Add((m.Index, new DamageComponent.AbilityMod("highest ability", multiplier)));
            ClaimRange(claimed, m.Index, m.Length);
        }

        // 2. Extract ability modifier references
        foreach (Match m in AbilityModPattern.Matches(text))
        {
            if (claimed.Contains(m.Index))
                continue;
            double multiplier = 1.0;
            if (m.Groups[1].Success)
            {
                if (m.Groups[1].Value.Equals("twice", StringComparison.OrdinalIgnoreCase))
                    multiplier = 2.0;
                else if (m.Groups[1].Value.Equals("half", StringComparison.OrdinalIgnoreCase))
                    multiplier = 0.5;
            }
            bool isBeast = m.Groups[2].Success
                && m.Groups[2].Value.Equals("beast's", StringComparison.OrdinalIgnoreCase);
            positioned.Add((m.Index, new DamageComponent.AbilityMod(m.Groups[3].Value, multiplier)
            {
                IsBeast = isBeast,
            }));
            ClaimRange(claimed, m.Index, m.Length);
        }

        foreach (Match m in AbilityDamagePattern.Matches(text))
        {
            if (claimed.Contains(m.Index))
                continue;
            positioned.Add((m.Index, new DamageComponent.AbilityMod(m.Groups[1].Value)));
            ClaimRange(claimed, m.Index, m.Length);
        }

        // 3. Extract dice expressions: NdM
        foreach (Match m in DicePattern.Matches(text))
        {
            if (claimed.Contains(m.Index))
                continue;
            int count = int.Parse(m.Groups[1].Value);
            int sides = int.Parse(m.Groups[2].Value);
            positioned.Add((m.Index, new DamageComponent.Dice(count, sides)));
            ClaimRange(claimed, m.Index, m.Length);
        }

        // 4. Extract flat integers (not already consumed by other patterns)
        foreach (Match m in FlatPattern.Matches(text))
        {
            if (claimed.Contains(m.Index))
                continue;
            int value = int.Parse(m.Groups[1].Value);
            positioned.Add((m.Index, new DamageComponent.FlatBonus(value)));
        }

        // Sort by position in source text so components appear in natural order
        positioned.Sort((a, b) => a.Position.CompareTo(b.Position));
        expr.Components.AddRange(positioned.Select(p => p.Component));

        return expr;
    }

    private static string[] SplitAbilityList(string text)
        => AbilityListSplitRegex().Split(text)
            .Select(t => t.Trim())
            .Select(t => LeadingOrRegex().Replace(t, string.Empty))
            .Select(t => ModifierSuffixRegex().Replace(t, string.Empty))
            .Where(t => t.Length > 0)
            .ToArray();

    private static void ClaimRange(HashSet<int> claimed, int start, int length)
    {
        for (int i = start; i < start + length; i++)
            claimed.Add(i);
    }

    [GeneratedRegex(@"(\d+)?\[W\]", RegexOptions.IgnoreCase)]
    private static partial Regex WeaponDiceRegex();

    [GeneratedRegex(@"(\d+)?\[B\]", RegexOptions.IgnoreCase)]
    private static partial Regex BeastDiceRegex();

    [GeneratedRegex(@"(\d+)d(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex DiceRegex();

    [GeneratedRegex(@"(?:\b(twice|half)\s+)?(?:(your|beast's)\s+)?(" + AbilityTokenPattern + @")\s+modifier", RegexOptions.IgnoreCase)]
    private static partial Regex AbilityModRegex();

    [GeneratedRegex(@"(?:\b(twice)\s+)?(?:your\s+)?highest ability modifier", RegexOptions.IgnoreCase)]
    private static partial Regex HighestAbilityModRegex();

    [GeneratedRegex(@"(?:\b(twice)\s+)?(?:(?:your|beast's)\s+)?(" + AbilityTokenPattern + @")(?:\s+modifier)?\s+or\s+(?:(?:your|beast's)\s+)?(" + AbilityTokenPattern + @")\s+modifier", RegexOptions.IgnoreCase)]
    private static partial Regex AbilityChoiceRegex();

    [GeneratedRegex(@"(?:(?:your|beast's)\s+)?(" + AbilityTokenPattern + @")\s+or\s+(?:(?:your|beast's)\s+)?(" + AbilityTokenPattern + @")(?=\s*(?:$|[,.;]|\w+\s+damage\b))", RegexOptions.IgnoreCase)]
    private static partial Regex AbilityChoiceDamageRegex();

    [GeneratedRegex(@"(?<list>(?:" + AbilityTokenPattern + @")(?:\s+modifier)?\s*,\s*(?:" + AbilityTokenPattern + @")(?:\s+modifier)?\s*,\s*(?:(?:" + AbilityTokenPattern + @")(?:\s+modifier)?\s*,\s*)*(?:or\s+)?(?:" + AbilityTokenPattern + @")(?:\s+modifier)?)(?=\s*(?:$|[,.;]|\w+\s+damage\b))", RegexOptions.IgnoreCase)]
    private static partial Regex AbilityListDamageRegex();

    [GeneratedRegex(@"(?<!or\s)(?<!,\s)(?:(?:your|beast's)\s+)?(" + AbilityTokenPattern + @")(?=\s*$|\s+(?:(?:" + DamageTypeTokenPattern + @")\s*)?$|\s+(?:(?:" + DamageTypeTokenPattern + @")\s+)?damage\b)", RegexOptions.IgnoreCase)]
    private static partial Regex AbilityDamageRegex();

    [GeneratedRegex(@"(?<!\d[dD])(?<!\[)\b(\d+)\b(?!\s*\[W\])(?![dD]\d)(?!\])")]
    private static partial Regex FlatRegex();

    [GeneratedRegex(@"^twice\s+(\d+\s*\[W\]|\d+d\d+|\d+\b(?!\s+(?:your|the|" + AbilityTokenPattern + @")))", RegexOptions.IgnoreCase)]
    private static partial Regex TwiceWeaponDicePrefixRegex();

    [GeneratedRegex(@"\s*,\s*|\s+or\s+", RegexOptions.IgnoreCase)]
    private static partial Regex AbilityListSplitRegex();

    [GeneratedRegex(@"^or\s+", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingOrRegex();

    [GeneratedRegex(@"\s+modifier$", RegexOptions.IgnoreCase)]
    private static partial Regex ModifierSuffixRegex();
}
