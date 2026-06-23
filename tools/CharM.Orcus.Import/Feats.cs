using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace CharM.Orcus.Import;

/// <summary>
/// Generates every feat in the "Feats" chapter of Orcus Player Options, verbatim.
/// One element per "###" feat heading; the chapter's "##" sub-headings (General
/// Feats, Martial Training Feats, …) are recorded as a "Feat Category" field. A
/// feat body is split into fields by its bold labels (Prerequisite, Benefit,
/// Special, Proficiency, Damage, Retraining, Range); any text before the first
/// label becomes the Flavor field. Every emitted field value is round-trip checked
/// (a subsequence of the feat's own source block) so nothing is reworded or
/// invented. A small curated rules overlay re-applies the mechanical bonuses for
/// the feats the engine can express (defenses incl. their 11th/21st scaling,
/// Perception, Heal, HP, initiative, and the Skill Training select); the rest
/// carry their text only. Do not hand-edit; regenerate instead.
/// </summary>
public static class Feats
{
    static readonly Regex H1 = new(@"^#\s+(.+?)\s*$", RegexOptions.Compiled);
    static readonly Regex H2 = new(@"^##\s+(.+?)\s*$", RegexOptions.Compiled);
    static readonly Regex H3 = new(@"^###\s+(.+?)\s*$", RegexOptions.Compiled);
    // A bold label that opens a field, e.g. "**Benefit:**" or "**Benefit:** text".
    static readonly Regex LabelRx = new(@"^\*\*(?<lbl>[A-Za-z][A-Za-z ]*?):\*\*\s*(?<rest>.*)$", RegexOptions.Compiled);

    // Recognized field labels (normalized) -> output field name. Singular/plural
    // and a couple of one-off labels all map onto a stable field set.
    static readonly Dictionary<string, string> LabelMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Prerequisite"] = "Prerequisite",
        ["Prerequisites"] = "Prerequisite",
        ["Benefit"] = "Benefit",
        ["Special"] = "Special",
        ["Proficiency"] = "Proficiency",
        ["Damage"] = "Damage",
        ["Retraining"] = "Retraining",
        ["Range"] = "Range",
    };

    // Fixed emission order for the fields we recognize (Flavor handled separately).
    static readonly string[] FieldOrder =
        { "Prerequisite", "Benefit", "Special", "Proficiency", "Damage", "Range", "Retraining" };

    // Curated mechanical wiring for the feats whose bonus the engine can apply.
    // Each value is a `rules:` directive emitted verbatim under the feat. The
    // copied text fields are never touched. Scaling feats ("+2 … +3 at 11 … +4
    // at 21") emit one statadd per tier, level-gated; because same-type ("Feat")
    // bonuses don't stack (highest wins), the higher tier simply supersedes the
    // lower once the character reaches that level.
    // Element-level categories added to specific feats (beyond the per-feat
    // "Feat Category" field). ORCUS_BONUS_FEAT_CHOICE tags the two feats the
    // "Bonus Feats" variant (variants.yaml) lets a character pick between.
    static readonly Dictionary<string, string[]> CategoryOverlay = new()
    {
        ["ORCUS_FEAT_WEAPON_FOCUS"] = new[] { "ORCUS_BONUS_FEAT_CHOICE" },
        ["ORCUS_FEAT_FOCUSED_CASTER"] = new[] { "ORCUS_BONUS_FEAT_CHOICE" },
    };

    static readonly Dictionary<string, string[]> RulesOverlay = new()
    {
        ["ORCUS_FEAT_ALERTNESS"] = new[] { "{ statadd: Perception, value: 2, bonusType: Feat }" },
        ["ORCUS_FEAT_CANTRIP_MASTER"] = new[] { "{ select: { type: Power, number: 3, category: ORCUS_DISCIPLINE_CANTRIPS, name: Cantrip Master } }" },
        ["ORCUS_FEAT_ARMOR_PROFICIENCY"] = new[] { "{ select: { type: Proficiency, number: 1, category: ORCUS_ARMOR_PROFS, name: Armor Proficiency } }" },
        ["ORCUS_FEAT_GREAT_FORTITUDE"] = new[]
        {
            "{ statadd: Fortitude Defense, value: 2, bonusType: Feat }",
            "{ statadd: Fortitude Defense, value: 3, bonusType: Feat, level: 11 }",
            "{ statadd: Fortitude Defense, value: 4, bonusType: Feat, level: 21 }",
        },
        ["ORCUS_FEAT_IMPROVED_INITIATIVE"] = new[] { "{ statadd: Initiative, value: 4, bonusType: Feat }" },
        ["ORCUS_FEAT_IRON_WILL"] = new[]
        {
            "{ statadd: Will Defense, value: 2, bonusType: Feat }",
            "{ statadd: Will Defense, value: 3, bonusType: Feat, level: 11 }",
            "{ statadd: Will Defense, value: 4, bonusType: Feat, level: 21 }",
        },
        ["ORCUS_FEAT_KEEN_DEFENSES"] = new[]
        {
            "{ statadd: Fortitude Defense, value: 1, bonusType: Feat }",
            "{ statadd: Reflex Defense, value: 1, bonusType: Feat }",
            "{ statadd: Will Defense, value: 1, bonusType: Feat }",
            "{ statadd: Fortitude Defense, value: 2, bonusType: Feat, level: 11 }",
            "{ statadd: Reflex Defense, value: 2, bonusType: Feat, level: 11 }",
            "{ statadd: Will Defense, value: 2, bonusType: Feat, level: 11 }",
            "{ statadd: Fortitude Defense, value: 3, bonusType: Feat, level: 21 }",
            "{ statadd: Reflex Defense, value: 3, bonusType: Feat, level: 21 }",
            "{ statadd: Will Defense, value: 3, bonusType: Feat, level: 21 }",
        },
        ["ORCUS_FEAT_LIGHTNING_REFLEXES"] = new[]
        {
            "{ statadd: Reflex Defense, value: 2, bonusType: Feat }",
            "{ statadd: Reflex Defense, value: 3, bonusType: Feat, level: 11 }",
            "{ statadd: Reflex Defense, value: 4, bonusType: Feat, level: 21 }",
        },
        ["ORCUS_FEAT_SHIELD_PROFICIENCY"] = new[] { "{ select: { type: Proficiency, number: 1, category: ORCUS_SHIELD_PROFS, name: Shield Proficiency } }" },
        ["ORCUS_FEAT_SKILL_FOCUS"] = new[] { "{ select: { type: Skill Focus, number: 1, category: ORCUS_SKILL_FOCUS, name: Skill Focus } }" },
        ["ORCUS_FEAT_SKILL_TRAINING"] = new[] { "{ select: { type: Skill Training, number: 1, name: Skill Training } }" },
        ["ORCUS_FEAT_TALENTED_HEALER"] = new[] { "{ statadd: Heal, value: 2, bonusType: Feat }" },
        ["ORCUS_FEAT_WEAPON_FOCUS"] = new[] { "{ select: { type: Weapon Focus, number: 1, category: ORCUS_WEAPON_FOCUS, name: Weapon Focus } }" },
        ["ORCUS_FEAT_WEAPON_PROFICIENCY"] = new[] { "{ select: { type: Proficiency, number: 1, category: ORCUS_WEAPON_PROFS, name: Weapon Proficiency } }" },
        ["ORCUS_FEAT_WEAPON_SPECIALIZATION"] = new[] { "{ select: { type: Weapon Specialization, number: 1, category: ORCUS_WEAPON_SPEC, name: Weapon Specialization } }" },
        ["ORCUS_FEAT_TOUGHNESS"] = new[] { "{ statadd: Hit Points, value: { statref: Level }, bonusType: Feat }" },
    };

    // Curated machine-readable prerequisites for feats whose source Prerequisite
    // text is expressible in the engine's PrereqParser grammar (ability score,
    // "<n>th level", and named-element presence joined by ", " = AND). The
    // verbatim Prerequisite field is left untouched (display); this drives
    // selection legality. Requirements that can't be expressed yet (proficiency
    // with a specific weapon/shield — Orcus proficiency element names don't match
    // the parser's pattern; "psi focus power"; "you have a familiar"; low-light
    // vision; the Channel Divinity feature; choice-dependent ranks; etc.) are
    // intentionally omitted and stay descriptive in the Prerequisite text.
    static readonly Dictionary<string, string> PrereqOverlay = new()
    {
        ["ORCUS_FEAT_ARMOR_GRACE"] = "Armor Focus",
        ["ORCUS_FEAT_CROSSFIRE_IMPROVED"] = "21st level, Crossfire",
        ["ORCUS_FEAT_FOCUSED_CASTER"] = "2nd level",
        ["ORCUS_FEAT_MONSTER_EXPERT"] = "11th level",
        ["ORCUS_FEAT_ROLLING_KIP"] = "11th level",
        ["ORCUS_FEAT_TAG_TEAM"] = "11th level",
        ["ORCUS_FEAT_THE_PRESENCE"] = "11th level, Charisma 16",
        ["ORCUS_FEAT_TWO_WEAPON_DEFENSE"] = "Dex 13",   // "Two-Weapon Fighting" not in Orcus
        ["ORCUS_FEAT_WEAPON_FOCUS"] = "2nd level",
        ["ORCUS_FEAT_WEAPON_SPECIALIZATION"] = "2nd level",
        ["ORCUS_FEAT_BALANCE_AND_DIRECTION"] = "Unarmed Combat",
        ["ORCUS_FEAT_BEST_ON_THE_MAT"] = "Evolution of Pankration, Unarmed Combat",
        ["ORCUS_FEAT_BOUNCING_COMBO"] = "21st level, Unarmed Combat",
        ["ORCUS_FEAT_EARNED_THE_BELT"] = "11th level, Evolution of Pankration, Unarmed Combat",
        ["ORCUS_FEAT_EVOLUTION_OF_PANKRATION"] = "Unarmed Combat",
        ["ORCUS_FEAT_HAM_HANDS"] = "21st level, Unarmed Combat",
        ["ORCUS_FEAT_JUMPING_KNEE"] = "Unarmed Combat, Unarmed Expanded Profile",
        ["ORCUS_FEAT_KAYFABE_MANEUVER"] = "Superior Position",
        ["ORCUS_FEAT_MASTER_DEGREE_MARTIAL_ARTIST"] = "21st level, Unarmed Combat",
        ["ORCUS_FEAT_THE_RITUAL_OF_DANCE_AND_DAMAGE"] = "Unarmed Combat, Unarmed Expanded Profile",
        ["ORCUS_FEAT_UNARMED_COMBAT_IMPROVED"] = "Unarmed Combat",
        ["ORCUS_FEAT_UNARMED_COMBAT_MASTER"] = "11th level, Unarmed Combat, Unarmed Combat (Improved)",
        ["ORCUS_FEAT_UNARMED_EXPANDED_PROFILE"] = "Unarmed Combat",
        ["ORCUS_FEAT_AURA_SHARD"] = "Cha 13",
        ["ORCUS_FEAT_BLASTING_AURA"] = "Aura Shard, Cha 13",
        ["ORCUS_FEAT_EMPOWERING_AURA"] = "Aura Shard, Thieving Aura, Cha 13, 26th level",
        ["ORCUS_FEAT_EXTENDED_AURA"] = "Aura Shard, Cha 13",
        ["ORCUS_FEAT_FORCEFUL_AURA"] = "Aura Shard, Cha 13",
        ["ORCUS_FEAT_HEALING_AURA"] = "Aura Shard, Cha 13",
        ["ORCUS_FEAT_RESTORATIVE_AURA"] = "Aura Shard, Healing Aura, Cha 13",
        ["ORCUS_FEAT_SHIFTING_AURA"] = "Aura Shard, Cha 13",
        ["ORCUS_FEAT_SURGING_AURA"] = "Aura Shard, Cha 13",
        ["ORCUS_FEAT_THIEVING_AURA"] = "Aura Shard, Cha 13",
        ["ORCUS_FEAT_ACID_BLAST"] = "Blast Shard",
        ["ORCUS_FEAT_COLD_BLAST"] = "Blast Shard",
        ["ORCUS_FEAT_EMPOWERED_BLAST_SHARD"] = "Blast Shard",
        ["ORCUS_FEAT_FAR_BLAST"] = "Blast Shard",
        ["ORCUS_FEAT_FIRE_BLAST"] = "Blast Shard",
        ["ORCUS_FEAT_IMPROVED_BLAST_SHARD"] = "Empowered Blast Shard, 11th level",
        ["ORCUS_FEAT_LIGHTNING_BLAST"] = "Blast Shard",
        ["ORCUS_FEAT_MIND_BLAST"] = "Blast Shard",
        ["ORCUS_FEAT_FORCE_SHIELD"] = "Shield Shard, 11th level",
        ["ORCUS_FEAT_GREATER_SHIELD"] = "Shield Shard",
        ["ORCUS_FEAT_GROUNDING_SHARD"] = "Shield Shard",
        ["ORCUS_FEAT_IMMOVABLE_SHIELD"] = "Grounding Shard, Shield Shard",
        ["ORCUS_FEAT_OFFENSIVE_SHIELD"] = "Shield Shard",
        ["ORCUS_FEAT_REFRESHING_SHARD"] = "Shield Shard",
        ["ORCUS_FEAT_AS_ONE"] = "Weapon Shard",   // "+ a martial power" not expressible
        ["ORCUS_FEAT_ASSASSIN_S_WEAPON"] = "Slayer’s Weapon, Weapon Shard",
        ["ORCUS_FEAT_EXTENDED_WEAPON"] = "Weapon Shard",
        ["ORCUS_FEAT_GREATER_WEAPON_SHARD"] = "Improved Weapon Shard, Weapon Shard, 11th level",
        ["ORCUS_FEAT_IMPROVED_WEAPON_SHARD"] = "Weapon Shard",
        ["ORCUS_FEAT_MALLEABLE_WEAPON"] = "Weapon Shard",
        ["ORCUS_FEAT_SLAYER_S_WEAPON"] = "Weapon Shard",
        ["ORCUS_FEAT_STORMSHARD"] = "Weapon Shard",
        ["ORCUS_FEAT_TWIN_WEAPON"] = "Weapon Shard",
        ["ORCUS_FEAT_BATTLE_ADAPTATION_DUALCLASS"] = "Dualclass Recruit (Dualclass)",
        ["ORCUS_FEAT_FUNCTIONAL_ADAPTATION_DUALCLASS"] = "Dualclass Recruit (Dualclass)",
        ["ORCUS_FEAT_DAILY_ADAPTATION_DUALCLASS"] = "Dualclass Recruit (Dualclass)",
        ["ORCUS_FEAT_KIT_STUDY"] = "11th level",
        ["ORCUS_FEAT_KIT_STUDY_EXPERT"] = "15th level, Kit Study",
        ["ORCUS_FEAT_KIT_STUDY_ADVANCED"] = "20th level, Kit Study",
    };

    public static int Generate(string book, string outPath)
    {
        var lines = File.ReadAllLines(book);

        // Locate the "# Feats" chapter and its end (next "# " heading).
        int start = -1, end = lines.Length;
        for (int i = 0; i < lines.Length; i++)
        {
            var m = H1.Match(lines[i]);
            if (!m.Success) continue;
            if (m.Groups[1].Value.Trim().Equals("Feats", StringComparison.OrdinalIgnoreCase)) start = i;
            else if (start >= 0) { end = i; break; }
        }
        if (start < 0) { Console.Error.WriteLine("'# Feats' chapter not found"); return 1; }

        var sb = new StringBuilder();
        sb.AppendLine("# Feats (Orcus Player Options) — generated verbatim by");
        sb.AppendLine("# tools/CharM.Orcus.Import (generate-feats). One element per feat; text fields");
        sb.AppendLine("# (Flavor/Prerequisite/Benefit/Special/…) are copied from the source and round-");
        sb.AppendLine("# trip checked against it. \"Feat Category\" records the source sub-heading. A");
        sb.AppendLine("# small curated rules overlay re-applies the mechanical bonuses (defenses and");
        sb.AppendLine("# their 11th/21st scaling, Perception, Heal, HP, initiative, the Skill Training");
        sb.AppendLine("# select); other feats carry their text only. Characters gain a");
        sb.AppendLine("# feat per the level slots in _internal/level.yaml. Do not hand-edit; regenerate.");
        sb.AppendLine();

        int total = 0, fails = 0;
        var failMsgs = new List<string>();
        var usedIds = new HashSet<string>();
        string category = "";

        int p = start + 1;
        while (p < end)
        {
            var mCat = H2.Match(lines[p]);
            if (mCat.Success)
            {
                category = HttpUtility.HtmlDecode(mCat.Groups[1].Value).Trim();
                // Skip the "Variant: Bonus Feats" prose section (no ### feats of value).
                sb.AppendLine($"# === {category} ===");
                p++;
                continue;
            }
            var mh = H3.Match(lines[p]);
            if (!mh.Success) { p++; continue; }

            int q = p + 1;
            while (q < end && !H3.IsMatch(lines[q]) && !H2.IsMatch(lines[q])) q++;
            var bodyLines = lines.Skip(p + 1).Take(q - (p + 1))
                .Where(l => !l.TrimStart().StartsWith("<figure")).ToList();

            string name = HttpUtility.HtmlDecode(mh.Groups[1].Value).Trim();
            EmitFeat(sb, name, category, bodyLines, usedIds, ref total, ref fails, failMsgs);
            p = q;
        }

        File.WriteAllText(outPath, sb.ToString());
        Console.WriteLine($"Wrote {outPath}: {total} feats.");
        if (fails > 0)
        {
            Console.WriteLine($"\nROUND-TRIP FAILURES ({fails}): field text not found verbatim in source:");
            foreach (var m in failMsgs.Take(60)) Console.WriteLine("  " + m);
            return 2;
        }
        Console.WriteLine("All feat text verified verbatim against the source.");
        return 0;
    }

    static void EmitFeat(StringBuilder sb, string name, string category, List<string> bodyLines,
                         HashSet<string> usedIds, ref int total, ref int fails, List<string> failMsgs)
    {
        // Split the body into Flavor (pre-label text) + labeled field segments.
        var fields = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var flavor = new List<string>();
        string? current = null;
        foreach (var raw in bodyLines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var ml = LabelRx.Match(line);
            if (ml.Success && LabelMap.TryGetValue(ml.Groups["lbl"].Value.Trim(), out var field))
            {
                current = field;
                if (!fields.ContainsKey(field)) fields[field] = new List<string>();
                var rest = ml.Groups["rest"].Value.Trim();
                if (rest.Length > 0) fields[field].Add(rest);
            }
            else if (current is null) flavor.Add(line);
            else fields[current].Add(line);
        }

        string id = "ORCUS_FEAT_" + Slug(name);
        string baseId = id; int n = 2; while (!usedIds.Add(id)) id = $"{baseId}_{n++}";

        // Round-trip every value against the feat's own source block.
        string srcNorm = Normalizer.Norm(string.Join(" ", bodyLines));
        var emit = new List<(string Key, string Value)>();
        int localFails = 0;
        void Stage(string key, List<string>? raw)
        {
            if (raw is null || raw.Count == 0) return;
            string val = Clean(raw);
            if (val.Length == 0) return;
            if (!Phase2.TextIsFaithful(val, srcNorm))
            { localFails++; failMsgs.Add($"{name} — {key}: \"{Trunc(val)}\""); return; }
            emit.Add((key, val));
        }

        Stage("Flavor", flavor);
        foreach (var key in FieldOrder)
            if (fields.TryGetValue(key, out var v)) Stage(key, v);
        fails += localFails;

        sb.AppendLine($"- id: {id}");
        sb.AppendLine($"  name: {Q(name)}");
        sb.AppendLine($"  type: Feat");
        sb.AppendLine($"  source: \"Orcus Original\"");
        if (PrereqOverlay.TryGetValue(id, out var prereq))
            sb.AppendLine($"  prereqs: {Q(prereq)}");
        if (CategoryOverlay.TryGetValue(id, out var cats))
            sb.AppendLine($"  categories: [{string.Join(", ", cats)}]");
        sb.AppendLine($"  fields:");
        if (category.Length > 0) sb.AppendLine($"    \"Feat Category\": {Q(category)}");
        foreach (var (key, val) in emit)
            foreach (var l in Phase2.EmitFieldLines(key, val, 4)) sb.AppendLine(l);
        if (RulesOverlay.TryGetValue(id, out var rules))
        {
            sb.AppendLine($"  rules:");
            foreach (var r in rules) sb.AppendLine($"    - {r}");
        }
        sb.AppendLine();
        total++;
    }

    static string Clean(List<string> lines)
    {
        var parts = new List<string>();
        foreach (var raw in lines)
        {
            var l = raw.Trim();
            if (l.StartsWith("> ")) l = l.Substring(2).Trim();
            l = l.Replace("**", "").Replace("*", "").Replace("_", "").Replace("●", "").Replace("`", "");
            l = l.TrimStart('-', ' ').Trim();
            l = string.Join(' ', l.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
            if (l.Length > 0) parts.Add(l);
        }
        return string.Join(" ", parts).Trim();
    }

    static string Slug(string s)
    {
        var sb = new StringBuilder(); bool u = false;
        foreach (var ch in s.ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(ch)) { sb.Append(ch); u = false; }
            else if (!u) { sb.Append('_'); u = true; }
        }
        return sb.ToString().Trim('_');
    }

    static string Q(string v) => "\"" + v.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    static string Trunc(string s) => s.Length > 90 ? s[..90] + "…" : s;
}
