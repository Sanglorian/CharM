using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace CharM.Orcus.Import;

public sealed class ParsedPathFeature
{
    public string Name = "";
    public int Level = 11;
    public string Description = "";
}

public sealed class ParsedPath
{
    public string Name = "";
    public string Description = "";
    public string Requirements = "";
    public List<ParsedPathFeature> Features = new();
    public List<ParsedPower> Powers = new();
    public string RawSection = "";   // verbatim source lines (for the fidelity gate)
}

/// <summary>
/// Deterministic parser + YAML generator + verbatim gate for the prestige
/// (paragon-tier) paths. A path follows a fixed shape: a description, optional
/// requirements, a handful of class features tagged with their level, and up to
/// three powers (an 11th-level encounter, a 12th/16th-level power, a 20th-level
/// daily). The power blocks reuse the discipline power parser and its round-trip
/// gate; features/descriptions/requirements are checked as verbatim
/// subsequences of their own path's source section.
/// </summary>
public static class PathGen
{
    static readonly Regex H1 = new(@"^#\s+(.+?)\s*$", RegexOptions.Compiled);
    static readonly Regex H2 = new(@"^##\s+(.+?)\s*$", RegexOptions.Compiled);
    static readonly Regex H3 = new(@"^###\s+(.+?)\s*$", RegexOptions.Compiled);
    static readonly Regex H4 = new(@"^<h4 class=""Heading-4---[^""]*"">(.*?)</h4>\s*$", RegexOptions.Compiled);
    // **Name (11th level):** rest-of-line
    static readonly Regex FeatureStart = new(
        @"^\*\*(?<name>.+?)\s*\((?<lvl>\d+)(?:st|nd|rd|th)\s+level\)\s*:\s*\*\*\s*(?<rest>.*)$",
        RegexOptions.Compiled);
    static readonly Regex ReqLine = new(
        @"^\*\*(?:Requirements?|Prerequisite):\*\*\s*(?<val>.*)$", RegexOptions.Compiled);

    // Machine-readable prerequisites for the prestige paths whose requirement is
    // expressible in the PrereqParser grammar (a trained-skill element, or a named
    // feat). The verbatim Requirements field is left untouched (display); this
    // gates the level-11 Prestige Path selection. The other paths' requirements
    // (weapon/crossbow/garrote proficiency, "psi focus", "channel divinity",
    // "arcane class", "a power with the Fire/Martial tag") aren't expressible yet
    // and stay descriptive.
    static readonly Dictionary<string, string> PrereqOverlay = new()
    {
        ["ORCUS_PRESTIGE_BATTLEFIELD_HEALER"] = "Heal",          // Trained in Heal
        ["ORCUS_PRESTIGE_SHADOWSNEAK"] = "Stealth",              // Trained in Stealth
        ["ORCUS_PRESTIGE_SILVER_TONGUE"] = "Diplomacy",          // Trained in Diplomacy
        ["ORCUS_PRESTIGE_RING_FIGHTER"] = "Unarmed Combat",      // Unarmed Combat feat
        ["ORCUS_PRESTIGE_SPELLWRIGHT"] = "category:Arcane",      // Arcane class (Mageblade/Magician carry the Arcane category)
        ["ORCUS_PRESTIGE_WEAPON_MASTER"] = "keyword:Martial",    // One or more of your powers has the Martial tag
        // Weapon-group / single-weapon proficiency via the per-category Grants
        // bundle elements a class grants (or the per-weapon Proficiency element).
        ["ORCUS_PRESTIGE_ASSASSIN"] = "Simple Melee Weapon Proficiency, Simple Ranged Weapon Proficiency",
        ["ORCUS_PRESTIGE_DARKWOOD_ARCHER"] = "Martial Ranged Weapon Proficiency",   // "military ranged"
        ["ORCUS_PRESTIGE_DEADEYE_ARBALESTER"] = "Weapon Proficiency (Light crossbow), Weapon Proficiency (Heavy crossbow)",
        ["ORCUS_PRESTIGE_BREATHSTEALER"] = "Weapon Proficiency (Garrote)",   // garrote is a transcribed exotic weapon (equipment/garrote.yaml)
    };

    public static int Generate(string sourceFile, string outPath, string contentDir)
    {
        var paths = Parse(sourceFile);
        Console.WriteLine($"Parsed {paths.Count} prestige paths.");

        // Ids already used elsewhere, so generated ids don't collide.
        var existing = YamlLoader.LoadDir(contentDir)
            .Where(e => e.Id != null && Path.GetFileName(outPath) != Path.GetFileName(e.File))
            .Select(e => e.Id!).ToHashSet(StringComparer.OrdinalIgnoreCase);

        string yaml = Emit(paths, existing);
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        File.WriteAllText(outPath, yaml);
        Console.WriteLine($"Wrote {outPath}");

        // --- Verbatim gate -------------------------------------------------------
        var failures = new List<string>();
        int feats = 0, powers = 0;
        foreach (var path in paths)
        {
            string src = path.RawSection;
            if (path.Description.Length > 0 && !Phase2.TextIsFaithful(path.Description, src))
                failures.Add($"  ✗ {path.Name}: Description not verbatim in section");
            if (path.Requirements.Length > 0 && !Phase2.TextIsFaithful(path.Requirements, src))
                failures.Add($"  ✗ {path.Name}: Requirements not verbatim in section");
            foreach (var f in path.Features)
            {
                feats++;
                if (!Phase2.TextIsFaithful(f.Name, src))
                    failures.Add($"  ✗ {path.Name} / {f.Name}: feature name not in section");
                if (f.Description.Length > 0 && !Phase2.TextIsFaithful(f.Description, src))
                    failures.Add($"  ✗ {path.Name} / {f.Name}: feature text not verbatim");
            }
            foreach (var pw in path.Powers)
            {
                powers++;
                foreach (var problem in Phase2.CheckPower(pw))
                    failures.Add($"  ✗ {path.Name} / {pw.Name}: {problem}");
            }
        }

        Console.WriteLine($"\nVerbatim gate: {paths.Count} paths, {feats} features, {powers} powers.");
        if (failures.Count > 0)
        {
            Console.WriteLine($"FAILURES ({failures.Count}) — text not verbatim in source, or source words dropped:");
            foreach (var f in failures) Console.WriteLine(f);
            return 2;
        }
        Console.WriteLine("All paths, features and powers passed the verbatim gate (no fabrication, no omission).");
        return 0;
    }

    // ------------------------------------------------------------------ parsing
    static List<ParsedPath> Parse(string sourceFile)
    {
        var lines = File.ReadAllLines(sourceFile);

        // Bound the "# Prestige Paths" … next-"# " section.
        int secStart = -1, secEnd = lines.Length;
        for (int i = 0; i < lines.Length; i++)
        {
            var m = H1.Match(lines[i]);
            if (!m.Success) continue;
            string h = HttpUtility.HtmlDecode(m.Groups[1].Value).Trim();
            if (secStart < 0)
            {
                if (h.Equals("Prestige Paths", StringComparison.OrdinalIgnoreCase)) secStart = i;
            }
            else { secEnd = i; break; }
        }
        if (secStart < 0) throw new Exception("'# Prestige Paths' section not found in " + sourceFile);

        var h2s = new List<int>();
        for (int i = secStart + 1; i < secEnd; i++) if (H2.IsMatch(lines[i])) h2s.Add(i);

        var paths = new List<ParsedPath>();
        for (int k = 0; k < h2s.Count; k++)
        {
            int from = h2s[k];
            int to = (k + 1 < h2s.Count) ? h2s[k + 1] : secEnd;
            paths.Add(ParsePath(lines, from, to));
        }
        return paths;
    }

    static ParsedPath ParsePath(string[] lines, int from, int to)
    {
        var path = new ParsedPath
        {
            Name = HttpUtility.HtmlDecode(H2.Match(lines[from]).Groups[1].Value).Trim(),
            RawSection = string.Join("\n", lines.Skip(from).Take(to - from)),
        };

        // Locate the optional "### Class Features" / "### Powers" subsections.
        int featHdr = -1, powHdr = -1;
        for (int i = from + 1; i < to; i++)
        {
            var m = H3.Match(lines[i]);
            if (!m.Success) continue;
            string h = m.Groups[1].Value.Trim();
            if (h.StartsWith("Class Features", StringComparison.OrdinalIgnoreCase)) featHdr = i;
            else if (h.StartsWith("Powers", StringComparison.OrdinalIgnoreCase)) powHdr = i;
        }

        // Description + requirements: the intro before the first feature/power.
        var intro = new List<string>();
        for (int i = from + 1; i < to; i++)
        {
            string raw = lines[i].Trim();
            if (raw.Length == 0 || raw.StartsWith("<figure")) continue;
            if (H3.IsMatch(lines[i])) continue;
            var rq = ReqLine.Match(raw);
            if (rq.Success) { path.Requirements = Unwrap(rq.Groups["val"].Value); continue; }
            if (FeatureStart.IsMatch(raw) || H4.IsMatch(lines[i])) break;
            intro.Add(Unwrap(raw));
        }
        path.Description = CollapseSpaces(string.Join(" ", intro));

        // Features: between the Class-Features header (or the path start, for paths
        // with no headers like Devotee) and the Powers header (or path end).
        int featFrom = featHdr >= 0 ? featHdr + 1 : from + 1;
        int featTo = powHdr >= 0 ? powHdr : to;
        ParseFeatures(lines, featFrom, featTo, path);

        // Powers: the <h4> blocks under the Powers header.
        if (powHdr >= 0)
        {
            int p = powHdr + 1;
            while (p < to)
            {
                var mh = H4.Match(lines[p]);
                if (!mh.Success) { p++; continue; }
                string name = HttpUtility.HtmlDecode(Strip(mh.Groups[1].Value)).Trim();
                int q = p + 1;
                while (q < to && !H4.IsMatch(lines[q])) q++;
                var blockLines = lines.Skip(p + 1).Take(q - (p + 1))
                    .Where(l => !l.TrimStart().StartsWith("<figure")).ToList();
                path.Powers.Add(Phase2.ParsePowerBlock(name, blockLines));
                p = q;
            }
        }
        return path;
    }

    static void ParseFeatures(string[] lines, int from, int to, ParsedPath path)
    {
        ParsedPathFeature? cur = null;
        var buf = new List<string>();
        void Flush()
        {
            if (cur != null) { cur.Description = CollapseSpaces(string.Join(" ", buf)); path.Features.Add(cur); }
            cur = null; buf.Clear();
        }

        for (int i = from; i < to; i++)
        {
            if (H3.IsMatch(lines[i]) || H4.IsMatch(lines[i])) { Flush(); break; }
            string raw = lines[i].Trim();
            if (raw.Length == 0 || raw.StartsWith("<figure")) continue;

            var m = FeatureStart.Match(raw);
            if (m.Success)
            {
                Flush();
                cur = new ParsedPathFeature
                {
                    Name = Unwrap(m.Groups["name"].Value),
                    Level = int.Parse(m.Groups["lvl"].Value),
                };
                string rest = Unwrap(m.Groups["rest"].Value);
                if (rest.Length > 0) buf.Add(rest);
            }
            else if (cur != null)
            {
                buf.Add(Unwrap(raw));
            }
            // else: stray text before the first feature (the description) — ignore.
        }
        Flush();
    }

    // ------------------------------------------------------------------ generation
    static string Emit(List<ParsedPath> paths, HashSet<string> existing)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Prestige paths (Orcus Classes and Powers) — generated verbatim from the");
        sb.AppendLine("# source book by tools/CharM.Orcus.Import (do not hand-edit the text fields;");
        sb.AppendLine("# regenerate with `generate-paths`). Engine semantics (id/type/level gates)");
        sb.AppendLine("# are derived mechanically. At level 11 every character chooses a prestige");
        sb.AppendLine("# path on top of their class via the Prestige Path slot opened by");
        sb.AppendLine("# ID_INTERNAL_LEVEL_11; it grants class features and powers (an 11th-level");
        sb.AppendLine("# encounter, a 12th/16th-level power, a 20th-level daily) that do not count");
        sb.AppendLine("# against class power limits. Later-tier features/powers are gated with");
        sb.AppendLine("# `level:` so a character built below that level doesn't get them early.");
        sb.AppendLine("# Requirements are verbatim in the Requirements field; those expressible in");
        sb.AppendLine("# the engine grammar (trained skill / named feat) also gate selection via a");
        sb.AppendLine("# `prereqs:` field. All 20 published paths are transcribed.");
        sb.AppendLine();

        foreach (var path in paths)
        {
            string pathId = UniqueId("ORCUS_PRESTIGE_", path.Name, existing);
            existing.Add(pathId);

            // Pre-assign feature + power ids so the grant list can reference them.
            var featIds = new List<string>();
            foreach (var f in path.Features)
            {
                string id = UniqueId("ORCUS_FEATURE_", f.Name, existing);
                existing.Add(id); featIds.Add(id);
            }
            var powIds = new List<string>();
            foreach (var pw in path.Powers)
            {
                string id = Phase2.MakeId(pw.Name, Suffix(path.Name), existing);
                existing.Add(id); powIds.Add(id);
            }

            sb.AppendLine(Banner(path.Name));
            sb.AppendLine($"- id: {pathId}");
            sb.AppendLine($"  name: {Scalar(path.Name)}");
            sb.AppendLine($"  type: Prestige Path");
            sb.AppendLine($"  source: \"Orcus Original\"");
            if (PrereqOverlay.TryGetValue(pathId, out var prereq))
                sb.AppendLine($"  prereqs: {Scalar(prereq)}");
            sb.AppendLine($"  fields:");
            if (path.Requirements.Length > 0)
                foreach (var l in Phase2.EmitFieldLines("Requirements", path.Requirements, 4)) sb.AppendLine(l);
            if (path.Description.Length > 0)
                foreach (var l in Phase2.EmitFieldLines("Description", path.Description, 4)) sb.AppendLine(l);
            sb.AppendLine($"  rules:");
            for (int i = 0; i < path.Features.Count; i++)
            {
                string lvl = path.Features[i].Level > 11 ? $", level: {path.Features[i].Level}" : "";
                sb.AppendLine($"    - {{ grant: {featIds[i]}, type: Class Feature{lvl} }}");
            }
            for (int i = 0; i < path.Powers.Count; i++)
            {
                string lvl = int.TryParse(path.Powers[i].Level, out var pl) && pl > 11 ? $", level: {pl}" : "";
                sb.AppendLine($"    - {{ grant: {powIds[i]}, type: Power{lvl} }}");
            }
            sb.AppendLine();

            for (int i = 0; i < path.Features.Count; i++)
            {
                var f = path.Features[i];
                sb.AppendLine($"- id: {featIds[i]}");
                sb.AppendLine($"  name: {Scalar(f.Name)}");
                sb.AppendLine($"  type: Class Feature");
                sb.AppendLine($"  source: \"Orcus Original\"");
                sb.AppendLine($"  fields:");
                sb.AppendLine($"    Level: \"{f.Level}\"");
                if (f.Description.Length > 0)
                    foreach (var l in Phase2.EmitFieldLines("Description", f.Description, 4)) sb.AppendLine(l);
            }
            if (path.Features.Count > 0) sb.AppendLine();

            for (int i = 0; i < path.Powers.Count; i++)
            {
                var pw = path.Powers[i];
                sb.AppendLine($"- id: {powIds[i]}");
                sb.AppendLine($"  name: {Scalar(pw.Name)}");
                sb.AppendLine($"  type: Power");
                sb.AppendLine($"  source: \"Orcus Original\"");
                sb.AppendLine($"  categories: [feature]");
                sb.AppendLine($"  fields:");
                foreach (var l in Phase2.EmitFieldLines(pw, 4)) sb.AppendLine(l);
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

    static string Banner(string name)
    {
        string head = $"# === {name} ";
        int pad = 80 - head.Length;
        return head + new string('=', pad > 0 ? pad : 3);
    }

    static string UniqueId(string prefix, string name, HashSet<string> existing)
    {
        var sb = new StringBuilder(prefix);
        bool lastUnderscore = true;   // suppress a separator right after the prefix '_'
        foreach (var ch in name.ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(ch)) { sb.Append(ch); lastUnderscore = false; }
            else if (!lastUnderscore) { sb.Append('_'); lastUnderscore = true; }
        }
        string baseId = sb.ToString().TrimEnd('_');
        string id = baseId;
        int n = 2;
        while (existing.Contains(id)) id = $"{baseId}_{n++}";
        return id;
    }

    static string Suffix(string pathName)
    {
        var sb = new StringBuilder();
        foreach (var ch in pathName.ToUpperInvariant())
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
        return sb.Length > 0 ? sb.ToString() : "PATH";
    }

    static string Scalar(string v)
    {
        v = v.Trim();
        if (v.Length == 0) return "\"\"";
        if (v.Contains('"')) return $"\"{v.Replace("\"", "\\\"")}\"";
        return $"\"{v}\"";
    }

    static string Strip(string s) => s.Replace("*", "").Replace("`", "");
    static string Unwrap(string s) => CollapseSpaces(s.Replace("**", "").Replace("*", "").Trim());
    static string CollapseSpaces(string s) =>
        string.Join(' ', s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
}
