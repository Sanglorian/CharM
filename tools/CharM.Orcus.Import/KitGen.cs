using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace CharM.Orcus.Import;

public sealed class ParsedKitFeature
{
    public string Name = "";
    public int Level = 1;
    public string Description = "";
}

public sealed class ParsedKit
{
    public string Name = "";
    public string Description = "";
    public string Requirements = "";
    public List<ParsedKitFeature> Features = new();
    public List<string> DiscOptions = new();   // discipline display names
    public bool DiscChoice = false;            // "one of the following"
    public List<ParsedPower> Powers = new();
    public List<string> Warnings = new();
    public string RawSection = "";
}

/// <summary>
/// Deterministic parser + generator + verbatim gate for the "# Kits" chapter.
/// A kit becomes a <c>Theme</c> element granting the "Has Kit" marker, its
/// associated-discipline access (a single grant, or a select when the kit offers
/// "one of the following disciplines"), and its Level-1/5/10 features as
/// <c>Class Feature</c> elements; embedded power blocks become granted powers.
/// Companion/selection tables (e.g. the familiar list) are skipped and flagged.
/// Verbatim prose (description, requirements, feature/power text) is round-trip
/// checked against the kit's own source section. Kits already present in
/// kits.yaml are skipped. Powers reuse the discipline power parser.
/// </summary>
public static class KitGen
{
    static readonly Regex H1 = new(@"^#\s+(.+?)\s*$", RegexOptions.Compiled);
    static readonly Regex H2 = new(@"^##\s+(.+?)\s*$", RegexOptions.Compiled);
    static readonly Regex H4 = new(@"^<h4 class=""Heading-4---[^""]*"">(.*?)</h4>\s*$", RegexOptions.Compiled);
    static readonly Regex FeatureStart = new(
        @"^\*\*(?<name>.+?)\s*\(Level\s+(?<lvl>\d+)\)\s*:\*\*\s*(?<rest>.*)$", RegexOptions.Compiled);
    static readonly Regex ReqLine = new(@"^\*\*Requirements?:\*\*\s*(?<val>.*)$", RegexOptions.Compiled);
    static readonly Regex AssocLine = new(@"^\*\*Associated Discipline:\*\*\s*(?<val>.*)$", RegexOptions.Compiled);

    public static int Generate(string book, string outPath, string contentDir)
    {
        var lines = File.ReadAllLines(book);

        int secStart = -1, secEnd = lines.Length;
        for (int i = 0; i < lines.Length; i++)
        {
            var m = H1.Match(lines[i]);
            if (!m.Success) continue;
            string h = HttpUtility.HtmlDecode(m.Groups[1].Value).Trim();
            if (secStart < 0) { if (h.Equals("Kits", StringComparison.OrdinalIgnoreCase)) secStart = i; }
            else { secEnd = i; break; }
        }
        if (secStart < 0) throw new Exception("'# Kits' section not found");

        // Load existing content, excluding this generator's own output file so a
        // regenerate doesn't treat previously-emitted kits as "already present".
        string outName = Path.GetFileName(outPath);
        var els = YamlLoader.LoadDir(contentDir).Where(e => Path.GetFileName(e.File) != outName).ToList();
        var existingKits = els.Where(e => e.Type == "Theme" && e.Name != null)
            .Select(e => e.Name!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingIds = els.Where(e => e.Id != null).Select(e => e.Id!).ToHashSet();
        // discipline display-name (normalized) → id
        var discByName = new Dictionary<string, string>();
        foreach (var e in els.Where(e => e.Type == "Discipline" && e.Id != null && e.Name != null))
            discByName[NormName(e.Name!)] = e.Id!;
        // existing Discipline Access: discipline-id → access-id (reuse, don't duplicate)
        var accessByDisc = new Dictionary<string, string>();
        foreach (var e in els.Where(e => e.Type == "Discipline Access" && e.Id != null))
            if (e.Fields.TryGetValue("_Discipline", out var d)) accessByDisc[d] = e.Id!;

        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Playing with Kits", "About Kits", "Redundant Features", "Powers and Associated Disciplines", "Feat Paths" };

        var h2s = new List<int>();
        for (int i = secStart + 1; i < secEnd; i++) if (H2.IsMatch(lines[i])) h2s.Add(i);

        var kits = new List<ParsedKit>();
        for (int k = 0; k < h2s.Count; k++)
        {
            int from = h2s[k], to = (k + 1 < h2s.Count) ? h2s[k + 1] : secEnd;
            string name = HttpUtility.HtmlDecode(H2.Match(lines[from]).Groups[1].Value).Trim();
            if (skip.Contains(name) || existingKits.Contains(name)) continue;
            kits.Add(ParseKit(lines, from, to, name));
        }

        // Resolve disciplines and allocate any new shared Discipline Access elements.
        var newShared = new List<(string accId, string discId, string discName)>();
        string GetSharedAccess(string discId, string discName)
        {
            if (accessByDisc.TryGetValue(discId, out var a)) return a;
            string accId = "ORCUS_DISCACCESS_" + discId.Replace("ORCUS_DISCIPLINE_", "");
            accessByDisc[discId] = accId; existingIds.Add(accId);
            newShared.Add((accId, discId, discName));
            return accId;
        }
        foreach (var kit in kits)
            foreach (var dn in kit.DiscOptions)
            {
                if (discByName.TryGetValue(NormName(dn), out var did)) { if (!kit.DiscChoice) GetSharedAccess(did, dn); }
                else kit.Warnings.Add($"associated discipline not found: \"{dn}\"");
            }

        string yaml = Emit(kits, newShared, discByName, accessByDisc, existingIds);
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        File.WriteAllText(outPath, yaml);
        Console.WriteLine($"Wrote {outPath}: {kits.Count} kits, {newShared.Count} new discipline-access elements.");

        // Verbatim gate.
        var failures = new List<string>();
        foreach (var kit in kits)
        {
            void Check(string f, string v) { if (!string.IsNullOrWhiteSpace(v) && !Phase2.TextIsFaithful(v, kit.RawSection)) failures.Add($"  ✗ {kit.Name} / {f}: \"{Short(v)}\""); }
            Check("Description", kit.Description);
            Check("Requirements", kit.Requirements);
            foreach (var f in kit.Features) Check(f.Name, f.Description);
            foreach (var pw in kit.Powers) foreach (var p in Phase2.CheckPower(pw)) failures.Add($"  ✗ {kit.Name} / {pw.Name}: {p}");
        }

        Console.WriteLine($"\nVerbatim gate: {kits.Count} kits, {kits.Sum(k => k.Features.Count)} features, {kits.Sum(k => k.Powers.Count)} powers.");
        var warns = kits.SelectMany(k => k.Warnings.Select(w => $"  ! {k.Name}: {w}")).ToList();
        if (warns.Count > 0) { Console.WriteLine("Warnings (review):"); warns.ForEach(Console.WriteLine); }
        if (failures.Count > 0) { Console.WriteLine($"\nFAILURES ({failures.Count}):"); failures.ForEach(Console.WriteLine); return 2; }
        Console.WriteLine("All kit prose, features and powers passed the verbatim gate.");
        return 0;
    }

    // ------------------------------------------------------------------ parsing
    static ParsedKit ParseKit(string[] lines, int from, int to, string name)
    {
        var kit = new ParsedKit { Name = name, RawSection = string.Join("\n", lines.Skip(from).Take(to - from)) };
        var intro = new List<string>();
        ParsedKitFeature? cur = null;
        var buf = new List<string>();
        bool sawStructure = false;
        void Flush() { if (cur != null) { cur.Description = CollapseSpaces(string.Join(" ", buf)); kit.Features.Add(cur); } cur = null; buf.Clear(); }

        int i = from + 1;
        while (i < to)
        {
            string line = lines[i];
            string raw = line.Trim();

            if (H4.IsMatch(line))
            {
                Flush(); sawStructure = true;
                int q = i + 1;
                while (q < to && !H4.IsMatch(lines[q]) && !FeatureStart.IsMatch(lines[q].Trim())
                       && !AssocLine.IsMatch(lines[q].Trim()) && !H2.IsMatch(lines[q])) q++;
                string pname = HttpUtility.HtmlDecode(Strip(H4.Match(line).Groups[1].Value)).Trim();
                var block = lines.Skip(i + 1).Take(q - (i + 1))
                    .Where(l => !l.TrimStart().StartsWith("<figure") && !l.TrimStart().StartsWith("|")).ToList();
                kit.Powers.Add(Phase2.ParsePowerBlock(pname, block));
                i = q; continue;
            }
            if (raw.Length == 0 || raw.StartsWith("<figure")) { i++; continue; }
            if (raw.StartsWith("|") || raw.StartsWith("**Table"))
            {
                if (!kit.Warnings.Any(w => w.Contains("table"))) kit.Warnings.Add("a table (companion/selection list) was skipped");
                i++; continue;
            }

            var rq = ReqLine.Match(raw);
            if (rq.Success) { Flush(); kit.Requirements = Unwrap(rq.Groups["val"].Value); sawStructure = true; i++; continue; }

            var am = AssocLine.Match(raw);
            if (am.Success)
            {
                Flush(); sawStructure = true;
                string val = Unwrap(am.Groups["val"].Value);
                if (val.Contains("one of the following", StringComparison.OrdinalIgnoreCase))
                {
                    kit.DiscChoice = true;
                    for (int j = i + 1; j < to; j++)
                    {
                        string b = lines[j].Trim();
                        if (b.Length == 0) continue;
                        if (b.StartsWith("*") || b.StartsWith("●") || b.StartsWith("-"))
                            kit.DiscOptions.Add(Unwrap(b).TrimEnd('.', ' '));
                        else break;
                    }
                }
                else if (val.Length > 0) kit.DiscOptions.Add(val.TrimEnd('.', ' '));
                i++; continue;
            }

            var fm = FeatureStart.Match(raw);
            if (fm.Success)
            {
                Flush(); sawStructure = true;
                cur = new ParsedKitFeature { Name = Unwrap(fm.Groups["name"].Value), Level = int.Parse(fm.Groups["lvl"].Value) };
                string rest = Unwrap(fm.Groups["rest"].Value);
                if (rest.Length > 0) buf.Add(rest);
                i++; continue;
            }

            if (cur != null) buf.Add(Unwrap(raw));
            else if (!sawStructure) intro.Add(Unwrap(raw));
            i++;
        }
        Flush();
        kit.Description = CollapseSpaces(string.Join(" ", intro));
        return kit;
    }

    // ------------------------------------------------------------------ generation
    static string Emit(List<ParsedKit> kits, List<(string accId, string discId, string discName)> newShared,
                       Dictionary<string, string> discByName, Dictionary<string, string> accessByDisc,
                       HashSet<string> existingIds)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Additional kits (Orcus Classes and Powers, # Kits) — generated by");
        sb.AppendLine("# tools/CharM.Orcus.Import (generate-kits). Each kit is a Theme granting the");
        sb.AppendLine("# Has Kit marker, its associated-discipline access (a grant, or a select for");
        sb.AppendLine("# \"one of the following\"), and its Level-1/5/10 features as Class Features;");
        sb.AppendLine("# embedded powers are granted. Prose is verbatim & round-trip checked.");
        sb.AppendLine("# Companion tables (e.g. familiars) are not modelled. Do not hand-edit;");
        sb.AppendLine("# regenerate. Kits already in kits.yaml are not repeated.");
        sb.AppendLine();

        if (newShared.Count > 0)
        {
            sb.AppendLine("# --- Discipline Access elements newly referenced by these kits ---------------");
            foreach (var (accId, discId, discName) in newShared)
                sb.AppendLine($"- {{ id: {accId}, name: \"Discipline Access ({discName})\", type: Discipline Access, source: \"Orcus Original\", fields: {{ _Discipline: {discId} }} }}");
            sb.AppendLine();
        }

        foreach (var kit in kits)
        {
            string kitId = MakeId("ORCUS_KIT_", kit.Name, existingIds); existingIds.Add(kitId);
            var featIds = new List<string>();
            foreach (var f in kit.Features) { string id = MakeId("ORCUS_KITF_", f.Name, existingIds); existingIds.Add(id); featIds.Add(id); }
            var powIds = new List<string>();
            foreach (var pw in kit.Powers) { string id = Phase2.MakeId(pw.Name, Slug(kit.Name), existingIds); existingIds.Add(id); powIds.Add(id); }

            // Resolve discipline access wiring.
            var resolved = kit.DiscOptions.Where(d => discByName.ContainsKey(NormName(d))).ToList();
            string choiceCat = "ORCUS_KITDISC_" + Slug(kit.Name);

            sb.AppendLine(Banner(kit.Name));
            sb.AppendLine($"- id: {kitId}");
            sb.AppendLine($"  name: {Scalar(kit.Name)}");
            sb.AppendLine($"  type: Theme");
            sb.AppendLine($"  source: \"Orcus Original\"");
            if (kit.Requirements.Length > 0 || kit.Description.Length > 0)
            {
                sb.AppendLine($"  fields:");
                if (kit.Requirements.Length > 0) foreach (var l in Phase2.EmitFieldLines("Requirements", kit.Requirements, 4)) sb.AppendLine(l);
                if (kit.Description.Length > 0) foreach (var l in Phase2.EmitFieldLines("Description", kit.Description, 4)) sb.AppendLine(l);
            }
            sb.AppendLine($"  rules:");
            sb.AppendLine($"    - {{ grant: ORCUS_MARKER_HAS_KIT, type: Marker }}");
            // Binds Familiar: the Spirit Friend feature picks one familiar from the
            // table (generated into familiars.yaml as ORCUS_FAMILIAR Powers).
            if (kit.Name.Equals("Binds Familiar", StringComparison.OrdinalIgnoreCase))
                sb.AppendLine($"    - {{ select: {{ type: Power, number: 1, category: ORCUS_FAMILIAR, name: Familiar }} }}");
            if (kit.DiscChoice && resolved.Count > 1)
                sb.AppendLine($"    - {{ select: {{ type: Discipline Access, number: 1, category: {choiceCat}, name: {kit.Name} Discipline }} }}");
            else if (resolved.Count >= 1)
                sb.AppendLine($"    - {{ grant: {accessByDisc[discByName[NormName(resolved[0])]]}, type: Discipline Access }}");
            for (int j = 0; j < kit.Features.Count; j++)
            {
                string lvl = kit.Features[j].Level > 1 ? $", level: {kit.Features[j].Level}" : "";
                sb.AppendLine($"    - {{ grant: {featIds[j]}, type: Class Feature{lvl} }}");
            }
            foreach (var id in powIds) sb.AppendLine($"    - {{ grant: {id}, type: Power }}");
            sb.AppendLine();

            // Per-kit Discipline Access options for a "one of the following" choice.
            if (kit.DiscChoice && resolved.Count > 1)
            {
                foreach (var dn in resolved)
                {
                    string discId = discByName[NormName(dn)];
                    string optId = MakeId("ORCUS_DISCACCESS_" + Slug(kit.Name) + "_", dn, existingIds); existingIds.Add(optId);
                    sb.AppendLine($"- {{ id: {optId}, name: \"Discipline Access ({dn})\", type: Discipline Access, source: \"Orcus Original\", categories: [{choiceCat}], fields: {{ _Discipline: {discId} }} }}");
                }
                sb.AppendLine();
            }

            for (int j = 0; j < kit.Features.Count; j++)
            {
                var f = kit.Features[j];
                sb.AppendLine($"- id: {featIds[j]}");
                sb.AppendLine($"  name: {Scalar(f.Name)}");
                sb.AppendLine($"  type: Class Feature");
                sb.AppendLine($"  source: \"Orcus Original\"");
                sb.AppendLine($"  fields:");
                sb.AppendLine($"    Level: \"{f.Level}\"");
                if (f.Description.Length > 0) foreach (var l in Phase2.EmitFieldLines("Description", f.Description, 4)) sb.AppendLine(l);
            }
            if (kit.Features.Count > 0) sb.AppendLine();

            for (int j = 0; j < kit.Powers.Count; j++)
            {
                var pw = kit.Powers[j];
                sb.AppendLine($"- id: {powIds[j]}");
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

    static string Banner(string name) { string h = $"# === {name} "; int pad = 80 - h.Length; return h + new string('=', pad > 0 ? pad : 3); }
    static string MakeId(string prefix, string name, HashSet<string> existing)
    {
        string baseId = prefix + Slug(name); string id = baseId; int n = 2;
        while (existing.Contains(id)) id = $"{baseId}_{n++}";
        return id;
    }
    static string Slug(string s)
    {
        var sb = new StringBuilder(); bool u = true;
        foreach (var ch in s.ToUpperInvariant()) { if (char.IsLetterOrDigit(ch)) { sb.Append(ch); u = false; } else if (!u) { sb.Append('_'); u = true; } }
        return sb.ToString().Trim('_');
    }
    static string Title(string s) => string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()));
    static string NormName(string s) => new string(s.ToLowerInvariant().Replace('’', '\'').Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '\'').ToArray()).Trim();
    static string Scalar(string v) => v.Contains('"') ? $"\"{v.Replace("\"", "\\\"")}\"" : $"\"{v}\"";
    static string Strip(string s) => s.Replace("*", "").Replace("`", "");
    static string Unwrap(string s) => CollapseSpaces(s.Replace("**", "").Replace("*", "").Trim());
    static string CollapseSpaces(string s) => string.Join(' ', s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
    static string Short(string s) => s.Length > 90 ? s[..90] + "…" : s;
}
