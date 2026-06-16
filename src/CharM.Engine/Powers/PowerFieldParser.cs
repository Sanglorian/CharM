using System.Text.RegularExpressions;
using CharM.Engine.Evaluation;
using CharM.Engine.Rules;

namespace CharM.Engine.Powers;

/// <summary>
/// Extracts computation-relevant fields from a Power RulesElement.
/// Power elements store their mechanics in Fields like "Attack", "Hit", "Keywords", etc.
/// </summary>
public static partial class PowerFieldParser
{
    /// <summary>
    /// Extract the attack stat name from the Attack line (e.g., "Strength vs. AC" → "Strength").
    /// </summary>
    public static string? GetAttackAbility(RulesElement power)
        => GetAttackAbility(power, overlay: null);

    /// <summary>
    /// Overlay-aware overload. When <paramref name="overlay"/> is supplied,
    /// reads the Attack line through the overlay so that feat-driven Modify
    /// directives (e.g. Deft Blade widening the stat options on Melee Basic
    /// Attack) take effect. Caller passes null to read raw fields.
    /// </summary>
    public static string? GetAttackAbility(RulesElement power, ModifyOverlay? overlay)
    {
        string? attack = GetAttackLine(power, overlay);
        if (string.IsNullOrWhiteSpace(attack))
            return null;

        int vsIndex = attack.IndexOf(" vs.", StringComparison.OrdinalIgnoreCase);
        if (vsIndex < 0)
            vsIndex = attack.IndexOf(" vs ", StringComparison.OrdinalIgnoreCase);

        return vsIndex >= 0 ? attack[..vsIndex].Trim() : null;
    }

    /// <summary>
    /// Extract the defense from the Attack line (e.g., "Strength vs. AC" → "AC").
    /// </summary>
    public static string? GetDefense(RulesElement power)
        => GetDefense(power, overlay: null);

    public static string? GetDefense(RulesElement power, ModifyOverlay? overlay)
    {
        string? attack = GetAttackLine(power, overlay);
        if (string.IsNullOrWhiteSpace(attack))
            return null;

        int vsIndex = attack.IndexOf("vs.", StringComparison.OrdinalIgnoreCase);
        if (vsIndex < 0)
            vsIndex = attack.IndexOf("vs ", StringComparison.OrdinalIgnoreCase);

        if (vsIndex < 0)
            return null;

        // Skip past "vs." or "vs "
        int start = vsIndex + 3;
        if (start < attack.Length && attack[start] == '.')
            start++;

        return NormalizeDefenseText(attack[start..]);
    }

    /// <summary>
    /// Return the raw canonical attack line, following the same Attack /
    /// Primary Attack / Secondary Attack fallback order as the field parsers.
    /// </summary>
    public static string? GetAttackText(RulesElement power) => GetAttackLine(power, overlay: null);

    /// <inheritdoc cref="GetAttackText(RulesElement)" />
    public static string? GetAttackText(RulesElement power, ModifyOverlay? overlay) => GetAttackLine(power, overlay);

    public static bool HasLeadingAttackField(RulesElement power)
        => power.Fields.Any(field =>
            !field.Key.Equals(field.Key.Trim(), StringComparison.Ordinal)
            && AttackFieldFallback.Any(fallback => field.Key.Trim().Equals(fallback, StringComparison.OrdinalIgnoreCase))
            && !string.IsNullOrWhiteSpace(field.Value));

    internal static string NormalizeDefenseText(string defenseText)
    {
        string trimmed = defenseText.Trim().TrimEnd('.');
        foreach (string defense in new[] { "AC", "Fortitude", "Reflex", "Will" })
        {
            if (trimmed.StartsWith(defense, StringComparison.OrdinalIgnoreCase)
                && (trimmed.Length == defense.Length || !char.IsLetter(trimmed[defense.Length])))
            {
                return defense;
            }
        }

        int end = trimmed.IndexOfAny([' ', ',', '.', '(']);
        return end > 0 ? trimmed[..end] : trimmed;
    }

    // Multi-attack powers (primary + secondary) use "Primary Attack" instead of "Attack".
    // Fall back through the conventional field names so the canonical attack line is found.
    private static readonly string[] AttackFieldFallback =
        ["Attack", "Primary Attack", "Secondary Attack", "Tertiary Attack"];

    // Sub-attack continuation field — used by powers like Avenging Charge where the
    // entire attack lives under the Effect block. Only treated as the primary attack
    // when the power has no Primary Target / Secondary Target / Primary Attack /
    // Secondary Attack fields (which would mark it as a multi-stage power where the
    // leading-space fields are secondary continuations, not the actual attack).
    private static bool HasMultiStageMarkers(RulesElement power)
        => power.Fields.ContainsKey("Primary Target")
           || power.Fields.ContainsKey(" Secondary Target")
           || power.Fields.ContainsKey("Secondary Target")
           || power.Fields.ContainsKey("Primary Attack")
           || power.Fields.ContainsKey("Secondary Attack");

    private static string? GetAttackLine(RulesElement power, ModifyOverlay? overlay)
    {
        foreach (var key in AttackFieldFallback)
        {
            string? line = overlay is null
                ? (power.Fields.TryGetValue(key, out var raw) ? raw : null)
                : overlay.GetField(power, key);
            if (!string.IsNullOrWhiteSpace(line))
                return line;
        }

        // Fall back to leading-space " Attack" only when this is NOT a multi-stage
        // power. Avenging Charge / similar single-attack-via-Effect powers store
        // their canonical attack here.
        string? subAttack = overlay is null
            ? (power.Fields.TryGetValue(" Attack", out var rawSub) ? rawSub : null)
            : overlay.GetField(power, " Attack");
        if (!HasMultiStageMarkers(power) && !string.IsNullOrWhiteSpace(subAttack))
            return subAttack;

        foreach (var (key, line) in power.Fields)
        {
            if (key.StartsWith("Attack (", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(line))
            {
                string? overlaid = overlay?.GetField(power, key);
                return !string.IsNullOrWhiteSpace(overlaid) ? overlaid : line;
            }
        }

        return null;
    }

    /// <summary>
    /// Extract keywords as a list (from comma-separated Keywords field).
    /// </summary>
    public static List<string> GetKeywords(RulesElement power)
    {
        if (!power.Fields.TryGetValue("Keywords", out var keywords) || string.IsNullOrWhiteSpace(keywords))
            return [];

        return keywords
            .Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    /// <summary>
    /// Extract the damage text from the Hit field.
    /// Strips trailing "damage." suffix and selects the appropriate tier-up
    /// clause ("Level NN: ...") for the supplied character level — those would
    /// otherwise be greedily parsed as additional dice/flat bonuses by
    /// DamageExpression.Parse, or under-report damage at high levels.
    /// </summary>
    public static string? GetDamageText(RulesElement power, int characterLevel = 1)
    {
        string? hit = GetHitLine(power);
        if (string.IsNullOrWhiteSpace(hit))
            return null;

        // Some Modify directives split a tier-up clause into a SEPARATE
        // "Level NN" field on the power instead of appending an "Increase
        // damage to X at NNth level" sentence to Hit. Example: Attack
        // Finesse (Hybrid Executioner) and Master of Shrouds (Executioner)
        // emit a Hit "1[W] + Dexterity modifier damage." plus a
        // Field="Level 21" (or " Level 21" with a leading space) carrying
        // the 2[W] epic-tier text. Pick the highest threshold not exceeding
        // characterLevel and use its value as Hit.
        string? leveledHit = FindFieldBasedLevelClause(power, characterLevel);
        if (leveledHit is not null)
            hit = leveledHit;

        string text = hit.Trim();

        if (text.StartsWith("You deal damage based on the level of the rage power", StringComparison.OrdinalIgnoreCase))
            return "As Above";

        // Normalize the rare "[NW]" data-typo into the canonical "N[W]"
        // (only one occurrence in the full rules dataset, but cheap to handle
        // and otherwise the dice would be silently dropped). Surfaced by the
        // PowerDamageSweep harness on Grasp of the Obsidian Tomb.
        text = BracketedWeaponDicePattern.Replace(text, "$1[W]");

        // Orcus writes weapon dice as "dW" / "NdW" — the same concept as 4e's
        // "[W]" / "N[W]" (the equipped weapon's damage die). Normalize so the
        // weapon-dice machinery substitutes the real die when a weapon is
        // equipped (and renders "N[W]" when none is).
        text = WeaponDieShorthandPattern.Replace(
            text, m => (m.Groups[1].Value.Length == 0 ? "1" : m.Groups[1].Value) + "[W]");

        // Powers with tier-up scaling stack clauses by newline:
        //   "1[W] + Str modifier damage."
        //   "Level 21: 2[W] + Str modifier damage."
        // Pick the single clause whose threshold is <= characterLevel (highest wins).
        text = SelectTierClause(text, characterLevel);

        // BEFORE we collapse to first line / truncate at "damage", scan the
        // entire (tier-selected) text for OCB-grounded literal extra-damage
        // phrases that live AFTER the main damage clause:
        //   "The attack deals extra damage equal to your X modifier."
        //   "The target takes extra damage equal to your X modifier."
        // These are unambiguously additive ability-mod damage. We capture
        // their abilities here, then re-append them to the truncated damage
        // text so DamageExpression.Parse picks them up. Conditional variants
        // ("If you are a dwarf...", "If you're wielding a hammer...") are
        // intentionally NOT auto-applied — they're feat triggers.
        var extraMods = ExtractExtraDamageMods(text);

        // Only consider the first sentence/line of the remaining text — secondary
        // sentences (e.g. "If the target is bloodied, it takes 1d6 extra damage")
        // would otherwise pollute the dice list.
        int newlineIdx = text.IndexOfAny(new[] { '\r', '\n' });
        if (newlineIdx >= 0)
            text = text[..newlineIdx].Trim();

        // Hit fields aren't always damage text — many at-wills (Bull Rush, Grab,
        // forced-movement attacks) describe an effect instead. Without a dice
        // expression, [W] token, "ongoing N damage", or modifier-style "+ Stat",
        // we shouldn't surface this as the Damage column. The original Hit text
        // is rendered verbatim in the card body.
        if (!HasDamageSignal(text))
            return null;
        if (IsOngoingOnlyDamage(text))
            return null;

        // Conditional / reactive damage: phrases like
        //   "the target takes a -2 penalty to attack rolls, and whenever the
        //    target misses with an attack, it takes 5 psychic damage."
        // OR
        //   "If the target is bloodied, it takes 2d6 extra damage."
        // do NOT represent direct on-hit damage. If a conditional connector
        // (when/whenever/if/while/until) appears before any damage signal that
        // isn't itself preceded by a direct-damage anchor ("X takes ... damage"
        // at the start of the clause), bail out — the power has no direct
        // damage to surface.
        if (IsPurelyConditionalDamage(text))
            return null;

        text = SelectDirectDamageSentence(text);

        // The damage clause typically ends with the literal word "damage" (optionally
        // preceded by a damage type like "force", "psychic", "necrotic"). Anything
        // after — "...and the target is dazed", "...and you push 1 square" — is
        // effect prose that DamageExpression.Parse would erroneously scrape numeric
        // tokens from. Cut after the damage-clause terminator so only the damage
        // portion is parsed.
        //
        // When multiple "damage" mentions exist, prefer the LAST one that has
        // explicit dice ([W]/NdM) somewhere before it — this rescues powers like
        // Warlock's Bargain whose Hit opens with non-damage prose ("You take
        // damage equal to your level, and the target takes 3d10 + Con damage...").
        text = TruncateAtDamageTerminator(text);

        // Strip trailing "damage." or "damage" suffix for cleaner parsing
        if (text.EndsWith("damage.", StringComparison.OrdinalIgnoreCase))
            text = text[..^"damage.".Length].Trim();
        else if (text.EndsWith("damage", StringComparison.OrdinalIgnoreCase))
            text = text[..^"damage".Length].Trim();
        text = DamageSubjectPrefix.Replace(text, string.Empty).Trim();

        // Re-append extra-damage modifiers captured before truncation. Each
        // becomes a "+ X modifier" clause that DamageExpression.Parse picks
        // up as an additional AbilityMod component.
        foreach (var ability in extraMods)
            text = string.IsNullOrWhiteSpace(text)
                ? $"{ability} modifier"
                : $"{text} + {ability} modifier";

        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    /// <summary>
    /// Walks newline-delimited clauses in a Hit/Effect field and returns the
    /// single clause whose "Level NN:" prefix is the highest threshold not
    /// exceeding <paramref name="characterLevel"/>. Clauses without a prefix
    /// are treated as the level-1 base.
    /// </summary>
    private static string SelectTierClause(string text, int characterLevel)
    {
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length <= 1)
        {
            var increase = SelectIncreaseDamageClause(text, characterLevel);
            if (increase is not null)
                return increase;

            var inline = SelectInlineTierClause(text, characterLevel);
            if (inline is not null)
                return inline;

            return StripIncreaseDamageClause(text).Trim();
        }

        string baseClause = StripIncreaseDamageClause(lines[0].Trim());
        string chosen = baseClause;
        int chosenThreshold = 1;
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            var m = TierUpClausePattern.Match(line);
            if (!m.Success || m.Index != 0)
                continue;
            int threshold = ExtractTierLevel(m.Value);
            if (threshold > chosenThreshold && threshold <= characterLevel)
            {
                chosen = line[(m.Index + m.Length)..].Trim();
                chosenThreshold = threshold;
            }
        }
        var increaseClause = SelectIncreaseDamageClause(text, characterLevel);
        if (increaseClause is not null)
            chosen = increaseClause;

        return chosen;
    }

    private static string? SelectInlineTierClause(string text, int characterLevel)
    {
        var matches = TierUpClausePattern.Matches(text);
        if (matches.Count == 0)
            return null;

        string chosen = StripIncreaseDamageClause(text[..matches[0].Index].Trim());
        int chosenThreshold = 1;

        for (int i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            int threshold = ExtractTierLevel(match.Value);
            int start = match.Index + match.Length;
            int end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
            string clause = StripIncreaseDamageClause(text[start..end].Trim());
            if (threshold > chosenThreshold && threshold <= characterLevel)
            {
                chosen = clause;
                chosenThreshold = threshold;
            }
        }

        return chosen.Trim();
    }

    private static string? SelectIncreaseDamageClause(string text, int characterLevel)
    {
        string? chosen = null;
        int chosenThreshold = 0;
        foreach (Match match in IncreaseDamageClausePattern.Matches(text))
        {
            if (!int.TryParse(match.Groups["level"].Value, out int threshold)
                || threshold <= chosenThreshold
                || threshold > characterLevel)
            {
                continue;
            }

            chosen = match.Groups["damage"].Value.Trim();
            chosenThreshold = threshold;
        }

        return chosen;
    }

    private static string StripIncreaseDamageClause(string text)
    {
        var match = IncreaseDamageClausePattern.Match(text);
        return match.Success ? text[..match.Index].Trim() : text;
    }

    private static int ExtractTierLevel(string anchor)
    {
        var digits = LevelDigitsPattern.Match(anchor);
        return digits.Success && int.TryParse(digits.Value, out int n) ? n : 0;
    }

    private static Regex LevelDigitsPattern => LevelDigitsRegex();

    // Some Modify directives place tier-up damage scaling in a separate
    // "Level NN" field on the power instead of appending an "Increase damage
    // to ... at NNth level" sentence to Hit. Field names may have a leading
    // space (e.g. " Level 21"); a few use "Level NN: extra damage" or the
    // unusual "Level NN damage" form. Returns the field value for the highest
    // NN <= characterLevel, or null if no such field applies.
    //
    // Many other Level NN fields hold non-damage prose (e.g. Level 13 Improved
    // Disruptive Shot adds a Field="Level 13" describing a blind/push rider),
    // so we ONLY accept values that begin with an N[W] or NdM dice expression —
    // i.e. values that are unambiguously damage replacements.
    private static string? FindFieldBasedLevelClause(RulesElement power, int characterLevel)
    {
        string? best = null;
        int bestThreshold = 0;
        foreach (var kvp in power.Fields)
        {
            var key = kvp.Key?.Trim();
            if (string.IsNullOrEmpty(key)) continue;
            if (!key.StartsWith("Level ", StringComparison.OrdinalIgnoreCase)) continue;

            var digits = LevelDigitsPattern.Match(key);
            if (!digits.Success) continue;
            if (!int.TryParse(digits.Value, out int threshold)) continue;
            if (threshold <= bestThreshold) continue;
            if (threshold > characterLevel) continue;
            if (string.IsNullOrWhiteSpace(kvp.Value)) continue;

            // Only treat the field as a damage replacement when its value
            // opens with a dice/[W] expression.
            string value = kvp.Value.TrimStart();
            if (!LevelClauseDamageOpener.IsMatch(value)) continue;

            best = kvp.Value;
            bestThreshold = threshold;
        }
        return best;
    }

    private static Regex LevelClauseDamageOpener => LevelClauseDamageOpenerRegex();

    // Heuristic gate words for damage-in-a-conditional-sub-clause detection.
    // NOTE: unlike most parser anchors here, these are NOT lifted from OCB's
    // binary string table — D20RulesEngine.dll does not expose literal
    // "whenever" / "if the target" anchors. OCB's NonDamagePower classification
    // is presumably structural (driven by Hit-field shape and power category).
    // We approximate it by treating a damage anchor that's preceded by a gating
    // connector — and isn't preceded by a direct-damage prefix — as conditional.
    private static Regex ConditionalGatePattern => ConditionalGateRegex();

    // Direct damage anchor at clause start. Mirrors the canonical OCB Hit-line
    // openings: bare "Ndice/N[W]/digits", or the prose form "the target takes/suffers N..."
    // (literal anchors like "The attack deals extra damage..." appear in OCB's
    // string table as full-sentence anchors).
    private static Regex DirectDamageAnchor => DirectDamageAnchorRegex();

    private static Regex DamageSubjectPrefix => DamageSubjectPrefixRegex();

    /// <summary>
    /// Returns true when the text's only damage signal sits inside a
    /// conditional sub-clause (e.g., "...whenever the target misses, it takes
    /// 5 damage."), meaning there is no direct on-hit damage to surface.
    ///
    /// </summary>
    private static bool IsPurelyConditionalDamage(string text)
    {
        // Find the first damage anchor.
        var dmg = DamageClauseTerminator.Match(text);
        if (!dmg.Success)
        {
            // No "damage" word — anything else is unambiguous direct damage.
            return false;
        }

        string preamble = text[..dmg.Index];

        // Direct damage almost always opens the Hit field. If a "[W]" or "NdM"
        // appears at clause start, treat as direct.
        if (DirectDamageAnchor.IsMatch(text))
            return false;

        string sentencePreamble = CurrentSentencePreamble(preamble);
        if (ConditionalGatePattern.IsMatch(sentencePreamble))
            return true;

        if (ExplicitDicePattern.IsMatch(text))
            return false;

        // Otherwise, if a conditional connector appears anywhere before the
        // damage anchor, the damage is gated.
        return ConditionalGatePattern.IsMatch(preamble);
    }

    private static string CurrentSentencePreamble(string preamble)
    {
        int sentenceStart = preamble.LastIndexOfAny(['.', '!', '?', '\r', '\n']);
        return sentenceStart >= 0 ? preamble[(sentenceStart + 1)..] : preamble;
    }

    private static string SelectDirectDamageSentence(string text)
    {
        foreach (Match match in DamageClauseTerminator.Matches(text))
        {
            string preamble = text[..match.Index];
            string sentencePreamble = CurrentSentencePreamble(preamble);
            if (ExplicitDicePattern.IsMatch(sentencePreamble) && DirectDamageAnchor.IsMatch(sentencePreamble))
            {
                int start = preamble.Length - sentencePreamble.Length;
                return text[start..].Trim();
            }
        }

        return text;
    }

    private static bool IsOngoingOnlyDamage(string text)
    {
        if (ExplicitDicePattern.IsMatch(text))
            return false;

        var dmg = DamageClauseTerminator.Match(text);
        if (!dmg.Success)
            return false;

        string preamble = text[..dmg.Index];
        return OngoingDamagePreamble.IsMatch(preamble);
    }

    // OCB binary anchors (verbatim from D20RulesEngine.dll string table):
    //   "The attack deals extra damage equal to your X modifier."
    //   "The target takes extra damage equal to your X modifier."
    // These are unambiguously additive on-hit damage. The conditional sister
    // anchors ("If you are a dwarf, the attack deals extra damage equal to
    // your X modifier.", "If you're wielding a hammer or a mace, ...") are
    // INTENTIONALLY excluded — they're feat triggers requiring separate
    // wielded-weapon / racial-trait gating logic.
    private static Regex ExtraDamageModifierPattern => ExtraDamageModifierRegex();

    // Sentences opening with "If " or "When " / "Whenever " gate the anchor
    // behind a condition (race, wielded weapon, target state) — skip those.
    private static Regex ConditionalSentencePrefix => ConditionalSentencePrefixRegex();

    private static IReadOnlyList<string> ExtractExtraDamageMods(string text)
    {
        // Split on sentence boundaries (period, exclamation, newline). Examine
        // each sentence in isolation so a conditional opener disqualifies its
        // own sentence without affecting siblings.
        var sentences = SentenceSplitPattern.Split(text);
        var found = new List<string>();
        foreach (var raw in sentences)
        {
            string sentence = raw.Trim();
            if (sentence.Length == 0) continue;
            if (ConditionalSentencePrefix.IsMatch(sentence)) continue;

            foreach (Match m in ExtraDamageModifierPattern.Matches(sentence))
            {
                string ability = m.Groups[1].Value;
                string normalized = char.ToUpperInvariant(ability[0]) + ability[1..].ToLowerInvariant();
                found.Add(normalized);
            }
        }
        return found;
    }

    private static Regex SentenceSplitPattern => SentenceSplitRegex();

    private static Regex OngoingDamagePreamble => OngoingDamagePreambleRegex();

    private static string TruncateAtDamageTerminator(string text)
    {
        var matches = DamageClauseTerminator.Matches(text);
        if (matches.Count == 0)
            return text;

        // Pick the FIRST "damage" terminator that has explicit dice somewhere
        // in its preamble — that's the one closing the actual damage clause.
        // (Subsequent "damage" mentions are extra-damage anchors that
        // ExtractExtraDamageMods already captured out-of-band.) Fall back to
        // the first match if no terminator is preceded by dice.
        Match chosen = matches[0];
        for (int i = 0; i < matches.Count; i++)
        {
            string preamble = text[..matches[i].Index];
            if (ExplicitDicePattern.IsMatch(preamble))
            {
                chosen = matches[i];
                break;
            }
        }

        return text[..(chosen.Index + chosen.Length)].Trim();
    }

    private static Regex ExplicitDicePattern => ExplicitDiceRegex();

    // Matches the rare "[NW]" data variant seen in Grasp of the Obsidian Tomb.
    // Captured group 1 is the dice count which we splice into "N[W]" form.
    private static Regex BracketedWeaponDicePattern => BracketedWeaponDiceRegex();

    // Orcus "dW"/"NdW" weapon-die shorthand → canonical "[W]"/"N[W]".
    private static Regex WeaponDieShorthandPattern => WeaponDieShorthandRegex();

    // Multi-attack powers (primary + secondary) use "Primary Hit" instead of "Hit".
    private static readonly string[] HitFieldFallback =
        ["Hit", "Primary Hit", "Secondary Hit", "Tertiary Hit"];

    private static string? GetHitLine(RulesElement power)
    {
        foreach (var key in HitFieldFallback)
        {
            if (power.Fields.TryGetValue(key, out var line) && !string.IsNullOrWhiteSpace(line))
                return line;
        }

        // Sub-attack continuation under Effect (e.g. Avenging Charge) — use only
        // when the power isn't a multi-stage primary/secondary attack power.
        if (!HasMultiStageMarkers(power)
            && power.Fields.TryGetValue(" Hit", out var subHit)
            && !string.IsNullOrWhiteSpace(subHit))
        {
            return subHit;
        }

        return null;
    }

    private static Regex DamageSignalPattern => DamageSignalRegex();

    // Matches the end of the damage clause — the literal word "damage" possibly
    // followed by terminating punctuation. Used to truncate effect prose that
    // follows a damage clause.
    private static Regex DamageClauseTerminator => DamageClauseTerminatorRegex();

    private static bool HasDamageSignal(string text)
        => !string.IsNullOrWhiteSpace(text) && DamageSignalPattern.IsMatch(text);

    private static Regex TierUpClausePattern => TierUpClauseRegex();

    private static Regex IncreaseDamageClausePattern => IncreaseDamageClauseRegex();

    /// <summary>
    /// Extract the power usage: "At-Will", "Encounter", "Daily".
    /// </summary>
    public static string? GetPowerUsage(RulesElement power)
    {
        return power.Fields.TryGetValue("Power Usage", out var usage) ? usage : null;
    }

    /// <summary>
    /// Check if this is a weapon power (Keywords contains "Weapon").
    /// Mirrors OCB's <c>IsWeaponPower</c> in
    /// <c>decompiled/D20RulesEngine/-Module-.cs:21038</c> — case-insensitive
    /// substring search of the power's <c>Keywords</c> field.
    /// The legacy `[W]`-in-Hit fallback is retained as a safety net for
    /// powers whose Keywords field is missing/malformed in CBLoader parts.
    /// </summary>
    public static bool IsWeaponPower(RulesElement power)
    {
        if (power.Fields.TryGetValue("Keywords", out var keywords)
            && keywords.Contains("Weapon", StringComparison.OrdinalIgnoreCase))
            return true;

        string? hit = GetHitLine(power);
        if (hit is not null && hit.Contains("[W]", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Check if this is an implement power (Keywords contains "Implement").
    /// Mirrors OCB's <c>IsImplementPower</c> in
    /// <c>decompiled/D20RulesEngine/-Module-.cs:21049</c>.
    /// </summary>
    public static bool IsImplementPower(RulesElement power)
        => power.Fields.TryGetValue("Keywords", out var keywords)
            && keywords.Contains("Implement", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Check if this power has melee delivery. Mirrors OCB's
    /// <c>IsMeleePower</c> in <c>decompiled/D20RulesEngine/-Module-.cs:22426</c>:
    /// case-insensitive substring "melee" on the <c>Attack Type</c> field,
    /// plus a Close-burst-weapon special case (any Close range counts as melee
    /// when the power is also a weapon power), plus a Swordmage thrown-weapon
    /// carveout: any power whose <c>Requirement</c> exactly equals
    /// <c>"You must throw your melee weapon at the target."</c> counts as
    /// melee even when its <c>Attack Type</c> is "Ranged N" — these are
    /// Swordmage powers (Whirling Blade, Blade Bolt, Falcon's Mark,
    /// Darksword Bolt, Sword Chaser Assault) where the character's MELEE
    /// weapon is what gets thrown, so melee weapons must populate the
    /// <c>&lt;Weapon&gt;</c> blocks.
    /// </summary>
    public static bool IsMeleePower(RulesElement power)
    {
        if (!power.Fields.TryGetValue("Attack Type", out var attackType)
            || string.IsNullOrWhiteSpace(attackType))
        {
            // Fall through — Requirement carveout below doesn't need
            // Attack Type. OCB reads Requirement separately at :22440.
            attackType = string.Empty;
        }

        if (attackType.Contains("melee", StringComparison.OrdinalIgnoreCase))
            return true;

        // Close-range weapon powers (e.g. cleave variants, weapon Close bursts)
        // are treated as melee for weapon-eligibility purposes.
        if (attackType.StartsWith("Close", StringComparison.OrdinalIgnoreCase)
            && IsWeaponPower(power))
            return true;

        // Swordmage thrown-melee-weapon carveout. OCB hardcodes this exact
        // Requirement string at -Module-.cs:22440 (global-212/213). Five
        // powers in the merged XML carry it; all are Swordmage attack
        // powers with "Ranged N" Attack Type.
        if (HasThrowMeleeWeaponRequirement(power))
            return true;

        return false;
    }

    private const string ThrowMeleeWeaponRequirement =
        "You must throw your melee weapon at the target.";

    /// <summary>
    /// Returns true if the power's <c>Requirement</c> field exactly
    /// matches OCB's hardcoded throw-melee-weapon string at
    /// <c>-Module-.cs:22440</c> / <c>:22464</c>. Matching is
    /// case-insensitive and trims surrounding whitespace, mirroring the
    /// effective semantics of <c>_wcsicmp</c> on field values that come
    /// from XML attributes (which preserve a leading/trailing space).
    /// </summary>
    private static bool HasThrowMeleeWeaponRequirement(RulesElement power)
    {
        if (!power.Fields.TryGetValue("Requirement", out var req)
            || string.IsNullOrWhiteSpace(req))
            return false;
        return string.Equals(
            req.Trim(),
            ThrowMeleeWeaponRequirement,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Check if this power has ranged / area delivery. Mirrors OCB's
    /// <c>IsRangedPower</c> in <c>decompiled/D20RulesEngine/-Module-.cs:22448</c>:
    /// IsClose first; substring "area burst" or "Area" on <c>Attack Type</c>;
    /// substring "ranged" with a thrown-melee-weapon Requirement carveout
    /// (Swordmage powers explicitly NOT counted as ranged so their melee
    /// weapons don't double-iterate as ranged); plus a hardcoded power-name
    /// fallback for "Stab and Shoot" (Sharpshooter paragon power whose
    /// Attack Type is "Melee 1" but whose secondary attack uses a bow/crossbow).
    /// </summary>
    public static bool IsRangedPower(RulesElement power)
    {
        if (!power.Fields.TryGetValue("Attack Type", out var attackType)
            || string.IsNullOrWhiteSpace(attackType))
        {
            // Fall through to the name-fallback at the bottom — OCB at
            // :22470 does the name compare regardless of the Attack Type
            // value (the read is into stringTableStr which may be empty).
            attackType = string.Empty;
        }

        if (attackType.StartsWith("Close", StringComparison.OrdinalIgnoreCase))
            return true;
        // Mirror OCB's substring at -Module-.cs:22457 (global-215 = "area burst").
        // We additionally accept the broader "Area" substring (matches "Area wall N"
        // attack types that OCB does not flag as ranged) — empirically required
        // for parity on wall-effect powers in the community corpus.
        if (attackType.Contains("Area", StringComparison.OrdinalIgnoreCase))
            return true;
        if (attackType.Contains("ranged", StringComparison.OrdinalIgnoreCase))
        {
            // Swordmage thrown-melee-weapon carveout (-Module-.cs:22464,
            // global-217/218). Powers like Whirling Blade / Blade Bolt /
            // Falcon's Mark have Attack Type = "Ranged 5" but the character
            // throws their MELEE weapon — they are NOT a ranged power.
            if (HasThrowMeleeWeaponRequirement(power))
                return false;
            return true;
        }

        // Hardcoded power-name fallback at -Module-.cs:22470 (global-219 =
        // "Stab and Shoot"). Sharpshooter Attack 11 has Attack Type "Melee 1"
        // (its primary stab) but its secondary attack uses a bow/crossbow;
        // OCB treats it as a ranged power so the bow/crossbow gets iterated.
        if (string.Equals(power.Name, "Stab and Shoot", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Whether the synthetic "Unarmed" weapon slot should be emitted for
    /// this power. Mirrors the combined effect of OCB's two gates in
    /// <c>WritePowerStat</c> at <c>-Module-.cs:11820</c>:
    ///   1. <c>IsValidPowerCombo(weapon=null, power)</c> →
    ///      <c>!IsRestricted(null, null, power)</c>
    ///      (decompile at <c>-Module-.cs:22517</c> /
    ///      <c>D20Workspace.cs:3973</c>).
    ///   2. <c>PowerStats(weapon=null, power, flags=4u) != null</c>
    ///      — the unarmed/null-mode stats computation; returns null when
    ///      no attack roll or healing payload is computable.
    ///
    /// Both gates were missing previously, producing ~565 spurious Unarmed
    /// blocks across the 549-file corpus. Empirical port:
    /// <list type="number">
    ///   <item>If the power's <c>Requirement</c> field contains
    ///   "wielding" → restricted (94/94 corpus matches with zero source
    ///   emits). The XML field is <c>Requirement</c>, not <c>Requires</c>;
    ///   OCB's <c>OtherRestrictions</c> at <c>-Module-.cs:21243</c>
    ///   special-cases exactly one Requirement string and otherwise
    ///   returns false, so finer parsing isn't needed for this case.</item>
    ///   <item>If the power has no <c>Attack</c> field AND lacks the
    ///   <c>Healing</c> keyword → no computable unarmed stats
    ///   (<c>PowerStats</c> returns null). Covers Effect-only powers
    ///   (Hellfire Heart, Power Strike, Mage Hand, Shield, Aura of Fear,
    ///   ...): 23/23 sampled extras correctly excluded; 15/15 sampled
    ///   source-emitting powers correctly admitted (8 with Attack,
    ///   3 Healing-only utility powers, the rest Attack+Healing).</item>
    /// </list>
    /// The fuller token-list parser for structured Requirement clauses
    /// (Group:X / Implement:X / monk weapon / dual shield / off-hand
    /// clauses) lives at <c>-Module-.cs:21472</c> and can be ported
    /// piecemeal if residuals appear.
    /// </summary>
    public static bool AllowUnarmed(RulesElement power)
    {
        if (power.Fields.TryGetValue("Requirement", out var requirement)
            && !string.IsNullOrWhiteSpace(requirement)
            && requirement.IndexOf("wielding", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        var hasAttack = HasNonEmptyField(power, "Attack")
            || HasNonEmptyField(power, "Primary Attack")
            || HasNonEmptyField(power, "Secondary Attack");
        if (hasAttack) return true;

        if (IsHealingPower(power)) return true;

        return false;
    }

    private static bool HasNonEmptyField(RulesElement power, string name)
    {
        return power.Fields.TryGetValue(name, out var v) && !string.IsNullOrWhiteSpace(v);
    }

    /// <summary>
    /// Check if this is a healing power (has "Healing" keyword or "heal" in Hit text).
    /// </summary>
    public static bool IsHealingPower(RulesElement power)
    {
        var keywords = GetKeywords(power);
        if (keywords.Any(k => k.Equals("Healing", StringComparison.OrdinalIgnoreCase)))
            return true;

        string? hit = GetHitLine(power);
        if (hit is not null && hit.Contains("heal", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex LevelDigitsRegex();

    [GeneratedRegex(@"^\s*\d+\s*(?:\[W\]|d\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex LevelClauseDamageOpenerRegex();

    [GeneratedRegex(@"\b(when|whenever|if|while|until)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ConditionalGateRegex();

    [GeneratedRegex(@"^\s*(?:the\s+target\s+(?:then\s+)?takes\s+|the\s+target\s+suffers\s+|the\s+attack\s+deals\s+)?(?:\d+\s*\[W\]|\d+d\d+|\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex DirectDamageAnchorRegex();

    [GeneratedRegex(@"^\s*(?:the\s+target\s+(?:then\s+)?takes\s+|the\s+target\s+suffers\s+|the\s+attack\s+deals\s+)", RegexOptions.IgnoreCase)]
    private static partial Regex DamageSubjectPrefixRegex();

    [GeneratedRegex(@"(?:the\s+attack\s+deals\s+extra\s+damage|the\s+target\s+takes\s+extra\s+damage)\s+equal\s+to\s+your\s+(Strength|Constitution|Dexterity|Intelligence|Wisdom|Charisma)\s+modifier", RegexOptions.IgnoreCase)]
    private static partial Regex ExtraDamageModifierRegex();

    [GeneratedRegex(@"^\s*(?:if|when|whenever|while|until)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ConditionalSentencePrefixRegex();

    [GeneratedRegex(@"(?<=[.!])\s+|[\r\n]+")]
    private static partial Regex SentenceSplitRegex();

    [GeneratedRegex(@"\bongoing\s+\d+\b", RegexOptions.IgnoreCase)]
    private static partial Regex OngoingDamagePreambleRegex();

    [GeneratedRegex(@"\[W\]|\b\d+d\d+\b", RegexOptions.IgnoreCase)]
    private static partial Regex ExplicitDiceRegex();

    [GeneratedRegex(@"\[(\d+)\s*W\]", RegexOptions.IgnoreCase)]
    private static partial Regex BracketedWeaponDiceRegex();

    // Orcus "dW" / "NdW" weapon-die shorthand (group 1 = optional multiplier).
    [GeneratedRegex(@"\b(\d*)dW\b", RegexOptions.IgnoreCase)]
    private static partial Regex WeaponDieShorthandRegex();

    [GeneratedRegex(@"\[W\]|\d+d\d+|\bongoing\s+\d+|\bdamage\b", RegexOptions.IgnoreCase)]
    private static partial Regex DamageSignalRegex();

    [GeneratedRegex(@"\bdamage\b\.?", RegexOptions.IgnoreCase)]
    private static partial Regex DamageClauseTerminatorRegex();

    [GeneratedRegex(@"\bLev(?:el|le)\s+\d+\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex TierUpClauseRegex();

    [GeneratedRegex(@"\bIncrease\s+(?:the\s+)?damage\s+to\s+(?<damage>.+?)\s+at\s+(?<level>\d+)(?:st|nd|rd|th)\s+level\b", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex IncreaseDamageClauseRegex();
}
