using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace CharM.Orcus.Import;

public sealed class ParsedSpecies
{
    public string Name = "";
    public string TypeLine = "";     // verbatim "Medium natural humanoid"
    public string Size = "Medium";
    public string Vision = "Normal";
    public string Description = "";
    public string AbilityText = "";  // verbatim "Pick two of +2 …"
    public List<string> Trio = new();// e.g. STR, CON, CHA  (empty = "any one")
    public bool AbilityAny = false;
    public int Speed = 6;
    public string SpeedText = "";
    public string LanguagesText = "";
    public int LanguageCount = 1;
    public List<(string Skill, string Val)> Skills = new();
    public List<(string Label, string Text)> Traits = new();
    public List<ParsedPower> Powers = new();
    public bool PowerIsSelect = false;
    public string RawSection = "";
    public List<string> Warnings = new();
}

/// <summary>
/// Deterministic parser + YAML generator + verbatim gate for the Advanced Options
/// "# Species" roster. Verbatim prose (description, named traits, ability text,
/// speed/language text, power blocks) is round-trip checked against the species'
/// own source section; the mechanical scaffolding (size/vision grants, Speed and
/// Language Count statadds, per-skill Ancestry bonuses, the pick-two ability-trio
/// select, power grant/select) is derived mechanically — mirroring the
/// hand-authored species in ancestries-species.yaml. Species already present
/// there are skipped. Powers reuse the discipline power parser.
/// </summary>
public static class SpeciesGen
{
    static readonly Regex H1 = new(@"^#\s+(.+?)\s*$", RegexOptions.Compiled);
    static readonly Regex H2 = new(@"^##\s+(.+?)\s*$", RegexOptions.Compiled);
    static readonly Regex H3 = new(@"^###\s+(.+?)\s*$", RegexOptions.Compiled);
    static readonly Regex H4 = new(@"^<h4 class=""Heading-4---[^""]*"">(.*?)</h4>\s*$", RegexOptions.Compiled);
    static readonly Regex Label = new(@"^\*\*(?<lbl>[^*:]+?):\*\*\s*(?<val>.*)$", RegexOptions.Compiled);

    static readonly Dictionary<string, string> AbilAbbr = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Strength"] = "STR", ["Constitution"] = "CON", ["Dexterity"] = "DEX",
        ["Intelligence"] = "INT", ["Wisdom"] = "WIS", ["Charisma"] = "CHA",
    };
    static readonly string[] AbilOrder = { "STR", "CON", "DEX", "INT", "WIS", "CHA" };

    // Trait labels handled as structural fields rather than free-text traits.
    static readonly HashSet<string> StructuralLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Ability Score Increases", "Speed", "Languages", "Skill Bonuses", "Skill Bonus",
        "Low-Light Vision", "Darkvision",
    };

    public static int Generate(string book, string outPath, string contentDir)
    {
        var lines = File.ReadAllLines(book);

        // Bound the "# Species" … "# Designing Your Own Ancestries" section.
        int secStart = -1, secEnd = lines.Length;
        for (int i = 0; i < lines.Length; i++)
        {
            var m = H1.Match(lines[i]);
            if (!m.Success) continue;
            string h = HttpUtility.HtmlDecode(m.Groups[1].Value).Trim();
            if (secStart < 0) { if (h.Equals("Species", StringComparison.OrdinalIgnoreCase)) secStart = i; }
            else { secEnd = i; break; }
        }
        if (secStart < 0) throw new Exception("'# Species' section not found");

        // Existing species names (skip these).
        var existing = YamlLoader.LoadDir(contentDir)
            .Where(e => e.Type == "Race" && e.Name != null)
            .Select(e => e.Name!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingIds = YamlLoader.LoadDir(contentDir).Where(e => e.Id != null).Select(e => e.Id!).ToHashSet();

        var skipHeadings = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Ancestry Traits", "On this page", "The implied setting of Orcus" };

        var h2s = new List<int>();
        for (int i = secStart + 1; i < secEnd; i++)
        {
            var m = H2.Match(lines[i]);
            if (m.Success && !m.Groups[1].Value.Contains("Variant", StringComparison.OrdinalIgnoreCase))
                h2s.Add(i);
        }

        var species = new List<ParsedSpecies>();
        for (int k = 0; k < h2s.Count; k++)
        {
            int from = h2s[k], to = (k + 1 < h2s.Count) ? h2s[k + 1] : secEnd;
            string name = HttpUtility.HtmlDecode(H2.Match(lines[from]).Groups[1].Value).Trim();
            if (skipHeadings.Contains(name) || existing.Contains(name)) continue;
            species.Add(ParseSpecies(lines, from, to, name));
        }

        var neededTrios = new SortedSet<string>();
        bool needAny = false;
        foreach (var s in species)
        {
            if (s.AbilityAny) needAny = true;
            else if (s.Trio.Count == 3) neededTrios.Add("ORCUS_ABILTRIO_" + string.Join("_", s.Trio));
        }

        string yaml = Emit(species, existingIds);
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        File.WriteAllText(outPath, yaml);
        Console.WriteLine($"Wrote {outPath}: {species.Count} species.");

        // Verbatim gate.
        var failures = new List<string>();
        foreach (var s in species)
        {
            void Check(string field, string val)
            {
                if (!string.IsNullOrWhiteSpace(val) && !Phase2.TextIsFaithful(val, s.RawSection))
                    failures.Add($"  ✗ {s.Name} / {field}: \"{Short(val)}\"");
            }
            Check("Description", s.Description);
            Check("Ability Scores", s.AbilityText);
            Check("Speed", s.SpeedText);
            Check("Languages", s.LanguagesText);
            foreach (var (lbl, txt) in s.Traits) Check(lbl, txt);
            foreach (var pw in s.Powers)
                foreach (var p in Phase2.CheckPower(pw)) failures.Add($"  ✗ {s.Name} / {pw.Name}: {p}");
        }

        Console.WriteLine($"\nVerbatim gate: {species.Count} species, {species.Sum(s => s.Powers.Count)} powers.");
        if (neededTrios.Count > 0 || needAny)
        {
            Console.WriteLine("Ability-bonus categories these species need (register in reference.yaml):");
            foreach (var t in neededTrios) Console.WriteLine($"    {t}  ({TrioMembers(t)})");
            if (needAny) Console.WriteLine("    ORCUS_ABILANY  (all six) — for \"+2 to one ability of your choice\"");
        }
        var warns = species.SelectMany(s => s.Warnings.Select(w => $"  ! {s.Name}: {w}")).ToList();
        if (warns.Count > 0) { Console.WriteLine("Warnings (review):"); warns.ForEach(Console.WriteLine); }

        if (failures.Count > 0)
        {
            Console.WriteLine($"\nFAILURES ({failures.Count}):");
            failures.ForEach(Console.WriteLine);
            return 2;
        }
        Console.WriteLine("All species prose & powers passed the verbatim gate.");
        return 0;
    }

    static string TrioMembers(string cat)
    {
        var abbr = cat.Replace("ORCUS_ABILTRIO_", "").Split('_');
        var full = new Dictionary<string, string> { ["STR"] = "Strength", ["CON"] = "Constitution", ["DEX"] = "Dexterity", ["INT"] = "Intelligence", ["WIS"] = "Wisdom", ["CHA"] = "Charisma" };
        return string.Join(", ", abbr.Select(a => full[a]));
    }

    // ------------------------------------------------------------------ parsing
    static ParsedSpecies ParseSpecies(string[] lines, int from, int to, string name)
    {
        var s = new ParsedSpecies { Name = name };
        s.RawSection = string.Join("\n", lines.Skip(from).Take(to - from));

        // Variant subsections (e.g. "### Variant: True Grit") are optional and
        // formatted irregularly — bound the species content before the first one.
        int contentEnd = to;
        for (int i = from + 1; i < to; i++)
            if (H3.IsMatch(lines[i]) && lines[i].Contains("Variant", StringComparison.OrdinalIgnoreCase))
            { contentEnd = i; s.Warnings.Add("a Variant subsection was skipped (optional content)"); break; }
        to = contentEnd;

        // Type line: first bold line after the ##.
        var intro = new List<string>();
        int traitsHdr = to;
        for (int i = from + 1; i < to; i++)
        {
            if (H3.IsMatch(lines[i]) && lines[i].Contains("Traits")) { traitsHdr = i; break; }
        }
        for (int i = from + 1; i < (traitsHdr == to ? to : traitsHdr); i++)
        {
            string raw = lines[i].Trim();
            if (raw.Length == 0 || raw.StartsWith("<figure")) continue;
            string txt = Unwrap(raw);
            if (s.TypeLine.Length == 0) { s.TypeLine = txt; s.Size = txt.Split(' ')[0]; }
            intro.Add(txt);
        }
        s.Description = CollapseSpaces(string.Join(" ", intro));

        // Trait block + powers.
        int powStart = to;
        for (int i = (traitsHdr == to ? from + 1 : traitsHdr + 1); i < to; i++)
        {
            if (H4.IsMatch(lines[i])) { powStart = i; break; }
            if (H3.IsMatch(lines[i])) continue;  // sub-headers (Variant handled by skip)
            string raw = lines[i].Trim();
            if (raw.Length == 0 || raw.StartsWith("<figure")) continue;
            var m = Label.Match(raw);
            if (!m.Success)
            {
                // Continuation of the previous trait, if any (e.g. Automaton "You do not sleep…").
                if (s.Traits.Count > 0)
                {
                    var last = s.Traits[^1];
                    s.Traits[^1] = (last.Label, CollapseSpaces(last.Text + " " + Unwrap(raw)));
                }
                continue;
            }
            string lbl = m.Groups["lbl"].Value.Trim();
            string val = Unwrap(m.Groups["val"].Value);
            HandleLabel(s, lbl, val);
        }

        // Powers.
        if (powStart < to)
        {
            int q = powStart;
            while (q < to)
            {
                var mh = H4.Match(lines[q]);
                if (!mh.Success) { q++; continue; }
                string pname = HttpUtility.HtmlDecode(Strip(mh.Groups[1].Value)).Trim();
                int r = q + 1;
                while (r < to && !H4.IsMatch(lines[r])) r++;
                var block = lines.Skip(q + 1).Take(r - (q + 1)).Where(l => !l.TrimStart().StartsWith("<figure")).ToList();
                s.Powers.Add(Phase2.ParsePowerBlock(pname, block));
                q = r;
            }
        }
        return s;
    }

    static void HandleLabel(ParsedSpecies s, string lbl, string val)
    {
        if (lbl.Equals("Ability Score Increases", StringComparison.OrdinalIgnoreCase))
        {
            s.AbilityText = val.TrimEnd('.', ' ');
            ParseAbility(s, val);
        }
        else if (lbl.Equals("Speed", StringComparison.OrdinalIgnoreCase))
        {
            s.SpeedText = val;
            var mm = Regex.Match(val, @"(\d+)");
            if (mm.Success) s.Speed = int.Parse(mm.Groups[1].Value);
            if (Regex.IsMatch(val, @"swim|fly|climb|burrow", RegexOptions.IgnoreCase))
                s.Warnings.Add($"extra movement mode in Speed not modelled: \"{val}\"");
        }
        else if (lbl.Equals("Languages", StringComparison.OrdinalIgnoreCase))
        {
            s.LanguagesText = val;
            // "Common and one extra" or "Common, X" → 2 languages.
            s.LanguageCount = Regex.IsMatch(val, @"one extra|,", RegexOptions.IgnoreCase) ? 2 : 1;
        }
        else if (lbl.StartsWith("Skill Bonus", StringComparison.OrdinalIgnoreCase))
        {
            foreach (Match m in Regex.Matches(val, @"\+(\d+)\s+([A-Za-z][A-Za-z ]*?)(?=,|\.|$)"))
                s.Skills.Add((m.Groups[2].Value.Trim(), "+" + m.Groups[1].Value));
        }
        else if (lbl.Equals("Low-Light Vision", StringComparison.OrdinalIgnoreCase))
        {
            s.Vision = "Low-light"; s.Traits.Add((lbl, val));
        }
        else if (lbl.Equals("Darkvision", StringComparison.OrdinalIgnoreCase))
        {
            s.Vision = "Darkvision"; s.Traits.Add((lbl, val));
        }
        else if (lbl.EndsWith("Power", StringComparison.OrdinalIgnoreCase))
        {
            // "<Name> Power: …" — selects when it offers a choice.
            if (Regex.IsMatch(val, @"one of the following|\bor\b", RegexOptions.IgnoreCase))
                s.PowerIsSelect = true;
            s.Traits.Add((lbl, val));
        }
        else
        {
            s.Traits.Add((lbl, val));
        }
    }

    static void ParseAbility(ParsedSpecies s, string val)
    {
        if (Regex.IsMatch(val, @"one ability score of your choice|to one ability", RegexOptions.IgnoreCase))
        { s.AbilityAny = true; return; }
        var abil = new List<string>();
        foreach (Match m in Regex.Matches(val, @"\+2\s+(Strength|Constitution|Dexterity|Intelligence|Wisdom|Charisma)", RegexOptions.IgnoreCase))
            if (AbilAbbr.TryGetValue(m.Groups[1].Value, out var ab) && !abil.Contains(ab)) abil.Add(ab);
        s.Trio = abil.OrderBy(a => Array.IndexOf(AbilOrder, a)).ToList();
        if (s.Trio.Count != 3) s.Warnings.Add($"non-trio ability text not modelled: \"{val}\"");
    }

    // ------------------------------------------------------------------ generation
    static string Emit(List<ParsedSpecies> species, HashSet<string> existingIds)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Additional species (Orcus Advanced Options, # Species) — generated by");
        sb.AppendLine("# tools/CharM.Orcus.Import (generate-species). Prose is verbatim & round-trip");
        sb.AppendLine("# checked; size/vision/speed/language/skill/ability/power scaffolding is");
        sb.AppendLine("# derived mechanically, mirroring ancestries-species.yaml. Do not hand-edit;");
        sb.AppendLine("# regenerate. Species already in ancestries-species.yaml are not repeated.");
        sb.AppendLine();

        foreach (var s in species)
        {
            string raceId = MakeId("ORCUS_RACE_", s.Name, existingIds); existingIds.Add(raceId);
            string powCat = "ORCUS_" + Slug(s.Name) + "_POWER";

            sb.AppendLine($"# === {s.Name} " + new string('=', Math.Max(3, 74 - s.Name.Length)));
            sb.AppendLine($"- id: {raceId}");
            sb.AppendLine($"  name: {Scalar(s.Name)}");
            sb.AppendLine($"  type: Race");
            sb.AppendLine($"  source: \"Orcus Original\"");
            sb.AppendLine($"  categories: [{s.Size}]");
            sb.AppendLine($"  fields:");
            sb.AppendLine($"    Size: {s.Size}");
            sb.AppendLine($"    Vision: {s.Vision}");
            if (s.AbilityText.Length > 0) foreach (var l in Phase2.EmitFieldLines("Ability Scores", s.AbilityText, 4)) sb.AppendLine(l);
            if (s.Description.Length > 0) foreach (var l in Phase2.EmitFieldLines("Description", s.Description, 4)) sb.AppendLine(l);
            if (s.SpeedText.Length > 0) foreach (var l in Phase2.EmitFieldLines("Speed", s.SpeedText, 4)) sb.AppendLine(l);
            if (s.LanguagesText.Length > 0) foreach (var l in Phase2.EmitFieldLines("Languages", s.LanguagesText, 4)) sb.AppendLine(l);
            foreach (var (lbl, txt) in s.Traits)
                if (txt.Length > 0) foreach (var l in Phase2.EmitFieldLines(lbl, txt, 4)) sb.AppendLine(l);

            sb.AppendLine($"  rules:");
            sb.AppendLine($"    - {{ grant: ORCUS_SIZE_{s.Size.ToUpperInvariant()}, type: Size }}");
            sb.AppendLine($"    - {{ grant: ORCUS_VISION_{s.Vision.ToUpperInvariant()}, type: Vision }}");
            sb.AppendLine($"    - {{ statadd: Speed, value: {s.Speed} }}");
            sb.AppendLine($"    - {{ statadd: Language Count, value: {s.LanguageCount} }}");
            foreach (var (skill, vv) in s.Skills)
                sb.AppendLine($"    - {{ statadd: {skill}, value: {vv}, bonusType: Ancestry }}");
            if (s.AbilityAny)
                sb.AppendLine($"    - {{ select: {{ type: Race Ability Bonus, number: 1, category: ORCUS_ABILANY, name: {s.Name} Ability Score }} }}");
            else if (s.Trio.Count == 3)
                sb.AppendLine($"    - {{ select: {{ type: Race Ability Bonus, number: 2, category: ORCUS_ABILTRIO_{string.Join("_", s.Trio)}, name: {s.Name} Ability Scores }} }}");

            // Powers: select among them when the trait says "one of the following".
            var powIds = new List<string>();
            foreach (var pw in s.Powers) { string id = Phase2.MakeId(pw.Name, Slug(s.Name), existingIds); existingIds.Add(id); powIds.Add(id); }
            if (s.PowerIsSelect && s.Powers.Count > 1)
                sb.AppendLine($"    - {{ select: {{ type: Power, number: 1, category: {powCat}, name: {s.Name} Power }} }}");
            else
                foreach (var id in powIds) sb.AppendLine($"    - {{ grant: {id}, type: Power }}");
            sb.AppendLine();

            for (int i = 0; i < s.Powers.Count; i++)
            {
                var pw = s.Powers[i];
                var cats = new List<string> { "feature" };
                if (s.PowerIsSelect && s.Powers.Count > 1) cats.Add(powCat);
                sb.AppendLine($"- id: {powIds[i]}");
                sb.AppendLine($"  name: {Scalar(pw.Name)}");
                sb.AppendLine($"  type: Power");
                sb.AppendLine($"  source: \"Orcus Original\"");
                sb.AppendLine($"  categories: [{string.Join(", ", cats)}]");
                sb.AppendLine($"  fields:");
                foreach (var l in Phase2.EmitFieldLines(pw, 4)) sb.AppendLine(l);
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

    static string MakeId(string prefix, string name, HashSet<string> existing)
    {
        string baseId = prefix + Slug(name);
        string id = baseId; int n = 2;
        while (existing.Contains(id)) id = $"{baseId}_{n++}";
        return id;
    }

    static string Slug(string s)
    {
        var sb = new StringBuilder(); bool u = true;
        foreach (var ch in s.ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(ch)) { sb.Append(ch); u = false; }
            else if (!u) { sb.Append('_'); u = true; }
        }
        return sb.ToString().Trim('_');
    }

    static string Scalar(string v) => v.Contains('"') ? $"\"{v.Replace("\"", "\\\"")}\"" : $"\"{v}\"";
    static string Strip(string s) => s.Replace("*", "").Replace("`", "");
    static string Unwrap(string s) => CollapseSpaces(s.Replace("**", "").Replace("*", "").Trim());
    static string CollapseSpaces(string s) => string.Join(' ', s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
    static string Short(string s) => s.Length > 90 ? s[..90] + "…" : s;
}
