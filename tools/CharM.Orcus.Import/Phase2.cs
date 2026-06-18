using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace CharM.Orcus.Import;

public sealed class ParsedPower
{
    public string Name = "";
    public string Usage = "";       // At-Will / Encounter / Daily
    public string Type = "";        // Attack / Utility
    public string Level = "";       // "1".."30" or "Feature"
    public string Action = "";      // e.g. Standard Action
    public string Keywords = "";
    public string Target = "";
    public string Flavor = "";
    public List<(string Label, string Value)> Body = new();
    public string RawBlock = "";    // verbatim source lines of this power (for round-trip)
}

public sealed class ParsedDiscipline
{
    public string Name = "";
    public string KeyAbility = "";
    public string SecondaryAbility = "";
    public string Sources = "";
    public string Description = "";
    public string Note = "";
    public List<ParsedPower> Powers = new();
}

/// <summary>
/// Phase 2: deterministic parser + YAML generator + round-trip gate for one
/// discipline. The parser copies text verbatim; the generator derives the engine
/// semantics (id/type/categories/level) mechanically; the gate proves the output
/// neither adds nor drops any source word.
/// </summary>
public static class Phase2
{
    static readonly HashSet<string> RangeWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "Melee", "Ranged", "Near", "Far", "Close", "Area", "Wall", "Personal",
        "Self", "Aura", "Unlimited", "Touch",
    };
    static readonly HashSet<string> BodyLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Attack", "Hit", "Miss", "Effect", "Trigger", "Special", "Requirements",
        "Requirement", "Prerequisite", "Sustain", "Aftereffect", "Target",
    };

    static readonly Regex H2 = new(@"^##\s+(.+?)\s*$", RegexOptions.Compiled);
    static readonly Regex H1 = new(@"^#\s+(.+?)\s*$", RegexOptions.Compiled);
    static readonly Regex H4 = new(@"^<h4 class=""Heading-4---[^""]*"">(.*?)</h4>\s*$", RegexOptions.Compiled);
    static readonly Regex Header = new(
        @"^\*\*(?<usage>[^*]+?)\*\*\s+\*\*(?<type>[^*]+?)\*\*\s+\*\*(?<level>[^*]+?)\*\*\s*\(\s*\*?\*?(?<action>[^*)]+?)\*?\*?\s*\)\s*(?:●\s*\*\*(?<kw>[^*]+?)\*\*)?\s*$",
        RegexOptions.Compiled);
    // Variant where the whole stat header is a single bold span:
    //   **Encounter Attack 1 (Standard Action)** ● **Martial, Weapon**
    static readonly Regex HeaderAll = new(
        @"^\*\*(?<body>[^*]+?)\*\*\s*(?:●\s*\*\*(?<kw>[^*]+?)\*\*\s*)?$", RegexOptions.Compiled);
    static readonly Regex HeaderInner = new(
        @"^(?<usage>At-Will|Encounter|Daily)\s+(?<type>Attack|Utility)\s+(?<level>Feature|\d+)\s*\(\s*(?<action>[^)]+?)\s*\)\s*$",
        RegexOptions.Compiled);

    // ------------------------------------------------------------------ parsing
    public static ParsedDiscipline Parse(string sourceFile, string disciplineName)
    {
        var lines = File.ReadAllLines(sourceFile);
        int start = -1, end = lines.Length;
        for (int i = 0; i < lines.Length; i++)
        {
            var m = H2.Match(lines[i]);
            if (m.Success && SameName(HttpUtility.HtmlDecode(m.Groups[1].Value), disciplineName))
            { start = i; }
            else if (start >= 0 && (H2.IsMatch(lines[i]) || H1.IsMatch(lines[i]))) { end = i; break; }
        }
        if (start < 0) throw new Exception($"discipline '{disciplineName}' not found in {sourceFile}");

        var disc = new ParsedDiscipline { Name = disciplineName };

        // Header block: from start until the first <h4>.
        int firstPower = end;
        for (int i = start + 1; i < end; i++) { if (H4.IsMatch(lines[i])) { firstPower = i; break; } }
        ParseDisciplineHeader(lines, start + 1, firstPower, disc);

        // Powers.
        int p = firstPower;
        while (p < end)
        {
            var mh = H4.Match(lines[p]);
            if (!mh.Success) { p++; continue; }
            string name = HttpUtility.HtmlDecode(Strip(mh.Groups[1].Value)).Trim();
            int q = p + 1;
            while (q < end && !H4.IsMatch(lines[q])) q++;
            var blockLines = lines.Skip(p + 1).Take(q - (p + 1))
                .Where(l => !l.TrimStart().StartsWith("<figure")).ToList();
            disc.Powers.Add(ParsePower(name, blockLines));
            p = q;
        }
        return disc;
    }

    static void ParseDisciplineHeader(string[] lines, int from, int to, ParsedDiscipline disc)
    {
        var intro = new List<string>();
        for (int i = from; i < to; i++)
        {
            var raw = lines[i].Trim();
            if (raw.Length == 0 || raw.StartsWith("<figure")) continue;
            if (TryField(raw, "Key Ability", out var ka)) { disc.KeyAbility = ka; continue; }
            if (TryField(raw, "Secondary Ability", out var sa)) { disc.SecondaryAbility = sa; continue; }
            if (TryField(raw, "Sources", out var ss)) { disc.Sources = ss; continue; }
            if (TryField(raw, "Source", out var s1)) { disc.Sources = s1; continue; }
            if (TryField(raw, "Note", out var nt)) { disc.Note = nt; continue; }
            intro.Add(Unwrap(raw));
        }
        disc.Description = string.Join(" ", intro).Trim();
    }

    static bool TryField(string raw, string label, out string value)
    {
        value = "";
        var prefix = $"**{label}:**";
        if (raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        { value = Unwrap(raw.Substring(prefix.Length)).Trim(); return true; }
        return false;
    }

    static ParsedPower ParsePower(string name, List<string> blockLines)
    {
        var pw = new ParsedPower { Name = name };
        pw.RawBlock = string.Join("\n", blockLines);
        bool headerSeen = false;
        string current = "";   // label of the field currently accumulating continuations
        var bodyMap = new List<(string Label, StringBuilder Sb)>();

        StringBuilder BodySb(string label)
        {
            var existing = bodyMap.FirstOrDefault(b => b.Label == label);
            if (existing.Sb != null) return existing.Sb;
            var sb = new StringBuilder();
            bodyMap.Add((label, sb));
            return sb;
        }

        foreach (var rawLine in blockLines)
        {
            var line = rawLine.TrimEnd();
            if (line.StartsWith("> ")) line = line.Substring(2);
            else if (line == ">") continue;
            line = line.Trim();
            if (line.Length == 0) continue;

            if (!headerSeen)
            {
                var mh = Header.Match(line);
                if (mh.Success)
                {
                    pw.Usage = mh.Groups["usage"].Value.Trim();
                    pw.Type = mh.Groups["type"].Value.Trim();
                    pw.Level = mh.Groups["level"].Value.Trim();
                    pw.Action = mh.Groups["action"].Value.Trim();
                    pw.Keywords = mh.Groups["kw"].Success ? mh.Groups["kw"].Value.Trim() : "";
                    headerSeen = true;
                    continue;
                }
                var ma = HeaderAll.Match(line);
                if (ma.Success)
                {
                    var mi = HeaderInner.Match(ma.Groups["body"].Value.Trim());
                    if (mi.Success)
                    {
                        pw.Usage = mi.Groups["usage"].Value.Trim();
                        pw.Type = mi.Groups["type"].Value.Trim();
                        pw.Level = mi.Groups["level"].Value.Trim();
                        pw.Action = mi.Groups["action"].Value.Trim();
                        pw.Keywords = ma.Groups["kw"].Success ? ma.Groups["kw"].Value.Trim() : "";
                        headerSeen = true;
                        continue;
                    }
                }
                // Pre-header italic line == flavor.
                if (line.StartsWith("*") && !line.StartsWith("**") && line.EndsWith("*"))
                { pw.Flavor = Unwrap(line); continue; }
                // Unrecognized pre-header line: ignore (kept in RawBlock for the gate).
                continue;
            }

            // After header.
            if (TryBoldLabel(line, out string label, out string rest))
            {
                string fw = label.Split(' ', 2)[0];
                // The range/target line: a range word, or the "Special range, ..."
                // form some powers use for unusual targeting.
                bool isRange = RangeWords.Contains(fw)
                    || (fw.Equals("Special", StringComparison.OrdinalIgnoreCase)
                        && rest.TrimStart().StartsWith("range", StringComparison.OrdinalIgnoreCase));
                if (isRange && pw.Target.Length == 0)
                { pw.Target = Unwrap(line); current = "Target"; continue; }

                if (label.Equals("Target", StringComparison.OrdinalIgnoreCase))
                { pw.Target = (pw.Target + " " + Unwrap(rest)).Trim(); current = "Target"; continue; }

                string first = label.Split(' ', 2)[0];
                if (BodyLabels.Contains(label) || BodyLabels.Contains(first)
                    || label.StartsWith("Maintain", StringComparison.OrdinalIgnoreCase)
                    || label.StartsWith("Boost", StringComparison.OrdinalIgnoreCase))
                {
                    string fieldName = NormalizeLabel(label);
                    string val = Unwrap(rest);
                    // For Maintain/Boost the descriptive qualifier lives in the label.
                    string extra = label.Substring(first.Length).Trim();
                    if ((fieldName == "Maintain" || fieldName == "Boost") && extra.Length > 0)
                        val = (extra + " " + val).Trim();
                    var sb = BodySb(fieldName);
                    if (sb.Length > 0) sb.Append(' ');
                    sb.Append(val);
                    current = fieldName;
                    continue;
                }
                // Bold-led but not a known label (e.g. "Form Attack") -> continuation.
            }

            // Continuation of the current field.
            if (current == "Target") { pw.Target = (pw.Target + " " + Unwrap(line)).Trim(); }
            else if (current.Length > 0) { var sb = BodySb(current); sb.Append(' ').Append(Unwrap(line)); }
            // else: stray text before any field; preserved only in RawBlock.
        }

        foreach (var (lbl, sb) in bodyMap)
            pw.Body.Add((lbl, CollapseSpaces(sb.ToString())));
        pw.Target = CollapseSpaces(pw.Target);
        return pw;
    }

    static bool TryBoldLabel(string line, out string label, out string rest)
    {
        label = ""; rest = "";
        if (!line.StartsWith("**")) return false;
        int close = line.IndexOf("**", 2, StringComparison.Ordinal);
        if (close < 0) return false;
        label = line.Substring(2, close - 2).Trim();
        rest = line.Substring(close + 2).Trim();
        return label.Length > 0;
    }

    static string NormalizeLabel(string label)
    {
        string first = label.Split(' ', 2)[0];
        if (label.StartsWith("Maintain", StringComparison.OrdinalIgnoreCase)) return "Maintain";
        if (label.StartsWith("Boost", StringComparison.OrdinalIgnoreCase)) return "Boost";
        return char.ToUpperInvariant(first[0]) + first.Substring(1).ToLowerInvariant();
    }

    static bool SameName(string a, string b)
    {
        static string K(string s) => string.Join(' ',
            s.Replace('’', '\'').Replace('‘', '\'').ToLowerInvariant()
             .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        return K(a) == K(b);
    }

    static string Strip(string s) => s.Replace("*", "").Replace("`", "");
    static string Unwrap(string s) => CollapseSpaces(s.Replace("**", "").Replace("*", "").Trim());
    static string CollapseSpaces(string s) =>
        string.Join(' ', s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)).Trim();

    // ------------------------------------------------------------------ generation
    public static string GenerateYaml(ParsedDiscipline disc, string disciplineId, string idSuffix,
                                      HashSet<string> existingIds)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {disc.Name} discipline — generated verbatim from the source book by");
        sb.AppendLine($"# tools/CharM.Orcus.Import (do not hand-edit the text fields). Engine");
        sb.AppendLine($"# semantics (id/type/categories/level) are derived mechanically.");
        sb.AppendLine();
        sb.AppendLine($"- id: {disciplineId}");
        sb.AppendLine($"  name: {Scalar(disc.Name)}");
        sb.AppendLine($"  type: Discipline");
        sb.AppendLine($"  source: \"Orcus (Outlaw Kingdoms)\"");
        sb.AppendLine($"  fields:");
        if (disc.KeyAbility.Length > 0) sb.AppendLine($"    \"Key Ability\": {Scalar(disc.KeyAbility)}");
        if (disc.SecondaryAbility.Length > 0) sb.AppendLine($"    \"Secondary Ability\": {Scalar(disc.SecondaryAbility)}");
        if (disc.Sources.Length > 0) sb.AppendLine($"    Sources: {Scalar(disc.Sources)}");
        if (disc.Description.Length > 0) AppendField(sb, "Description", disc.Description, 4);
        if (disc.Note.Length > 0) AppendField(sb, "Note", disc.Note, 4);
        sb.AppendLine();

        foreach (var pw in disc.Powers)
        {
            string id = MakeId(pw.Name, idSuffix, existingIds);
            existingIds.Add(id);
            string tag = pw.Type.Equals("Utility", StringComparison.OrdinalIgnoreCase)
                ? "utility" : pw.Usage.ToLowerInvariant();
            var cats = new List<string> { disciplineId, tag };
            if (pw.Type.Equals("Attack", StringComparison.OrdinalIgnoreCase)) cats.Add("ability-swap");

            sb.AppendLine($"- id: {id}");
            sb.AppendLine($"  name: {Scalar(pw.Name)}");
            sb.AppendLine($"  type: Power");
            sb.AppendLine($"  source: \"Orcus (Outlaw Kingdoms)\"");
            sb.AppendLine($"  categories: [{string.Join(", ", cats)}]");
            sb.AppendLine($"  fields:");
            sb.AppendLine($"    Level: \"{pw.Level}\"");
            sb.AppendLine($"    \"Power Usage\": {Scalar(pw.Usage)}");
            sb.AppendLine($"    \"Power Type\": {Scalar(pw.Type)}");
            if (pw.Action.Length > 0) sb.AppendLine($"    \"Action Type\": {Scalar(pw.Action)}");
            if (pw.Keywords.Length > 0) AppendField(sb, "Keywords", pw.Keywords, 4);
            if (pw.Target.Length > 0) AppendField(sb, "Target", pw.Target, 4);
            foreach (var (label, value) in pw.Body)
                if (value.Length > 0) AppendField(sb, label, value, 4);
            if (pw.Flavor.Length > 0) AppendField(sb, "Flavor", pw.Flavor, 4);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    static void AppendField(StringBuilder sb, string key, string value, int indent)
    {
        string pad = new string(' ', indent);
        string keyOut = NeedsQuote(key) ? $"\"{key}\"" : key;
        value = value.Trim();
        if (value.Length <= 66 && !value.Contains('"') && !value.Contains(": ") && !value.Contains(" #"))
        {
            sb.AppendLine($"{pad}{keyOut}: \"{value}\"");
            return;
        }
        // Folded block scalar: reads back as a single space-joined string.
        sb.AppendLine($"{pad}{keyOut}: >-");
        string cpad = new string(' ', indent + 2);
        foreach (var wrapped in Wrap(value, 74))
            sb.AppendLine($"{cpad}{wrapped}");
    }

    static bool NeedsQuote(string key) => key.Contains(' ');

    static IEnumerable<string> Wrap(string text, int width)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = new StringBuilder();
        foreach (var w in words)
        {
            if (line.Length > 0 && line.Length + 1 + w.Length > width)
            { yield return line.ToString(); line.Clear(); }
            if (line.Length > 0) line.Append(' ');
            line.Append(w);
        }
        if (line.Length > 0) yield return line.ToString();
    }

    static string Scalar(string v)
    {
        v = v.Trim();
        if (v.Length == 0) return "\"\"";
        if (v.Contains('"')) return $"\"{v.Replace("\"", "\\\"")}\"";
        return $"\"{v}\"";
    }

    public static string MakeId(string name, string suffix, HashSet<string> existing)
    {
        var sb = new StringBuilder("ORCUS_POWER_");
        bool lastUnderscore = false;
        foreach (var ch in name.ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(ch)) { sb.Append(ch); lastUnderscore = false; }
            else if (!lastUnderscore) { sb.Append('_'); lastUnderscore = true; }
        }
        string id = sb.ToString().Trim('_');
        if (existing.Contains(id)) id = $"{id}_{suffix}";
        return id;
    }

    // ------------------------------------------------------------------ round-trip gate
    // Field-label / structural words that become YAML keys (not value text) and so
    // legitimately "cover" the corresponding bold labels in the source block.
    static readonly string[] StructuralKeyWords =
    {
        "level", "power", "usage", "type", "action", "keywords", "target", "flavor",
    };

    public static (int pass, List<string> failures) RoundTrip(ParsedDiscipline disc)
    {
        var failures = new List<string>();
        int pass = 0;
        foreach (var pw in disc.Powers)
        {
            var problems = CheckPower(pw);
            if (problems.Count == 0) pass++;
            else failures.Add($"  ✗ {pw.Name} (L{pw.Level} {pw.Usage} {pw.Type})\n        "
                              + string.Join("\n        ", problems));
        }
        return (pass, failures);
    }

    /// <summary>Per-power round-trip check: forward (no fabrication/rewording) +
    /// backward (no omission). Returns an empty list when faithful.</summary>
    public static List<string> CheckPower(ParsedPower pw)
    {
        var blockSeq = Tokens(pw.RawBlock);
        var blockTokens = blockSeq.ToHashSet();

        var covered = new HashSet<string>(StructuralKeyWords);
        foreach (var t in Tokens(pw.Name)) covered.Add(t);
        foreach (var t in Tokens(pw.Usage)) covered.Add(t);
        foreach (var t in Tokens(pw.Type)) covered.Add(t);
        foreach (var t in Tokens(pw.Level)) covered.Add(t);
        foreach (var t in Tokens(pw.Action)) covered.Add(t);
        foreach (var t in Tokens(pw.Keywords)) covered.Add(t);
        foreach (var t in Tokens(pw.Target)) covered.Add(t);
        foreach (var t in Tokens(pw.Flavor)) covered.Add(t);
        foreach (var (lbl, v) in pw.Body)
        {
            foreach (var t in Tokens(lbl)) covered.Add(t);
            foreach (var t in Tokens(v)) covered.Add(t);
        }

        var problems = new List<string>();
        void Fwd(string fieldName, string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return;
            var seq = Tokens(val);
            if (seq.Count > 0 && !IsSubsequence(seq, blockSeq))
                problems.Add($"FABRICATED/REWORDED in {fieldName}: \"{Short(val)}\"");
        }
        Fwd("Keywords", pw.Keywords);
        Fwd("Target", pw.Target);
        foreach (var (lbl, v) in pw.Body) Fwd(lbl, v);
        if (pw.Flavor.Length > 0) Fwd("Flavor", pw.Flavor);

        var missing = blockTokens.Where(t => !covered.Contains(t)).ToList();
        if (missing.Count > 0)
            problems.Add($"OMITTED source words: {string.Join(", ", missing.Take(30))}");
        return problems;
    }

    /// <summary>Verify a verbatim prose value (e.g. a feature Description) is a
    /// subsequence of the given source text — no rewording, no fabrication.</summary>
    public static bool TextIsFaithful(string value, string sourceText) =>
        IsSubsequence(Tokens(value), Tokens(sourceText));

    /// <summary>Parse a single power's blockquote lines (public wrapper).</summary>
    public static ParsedPower ParsePowerBlock(string name, List<string> blockLines) =>
        ParsePower(name, blockLines);

    /// <summary>Emit the `fields:` child lines (Level..Flavor) for a power, at the
    /// given indent — reused by the class patcher.</summary>
    public static List<string> EmitFieldLines(ParsedPower pw, int indent)
    {
        string pad = new string(' ', indent);
        var sb = new StringBuilder();
        sb.AppendLine($"{pad}Level: \"{pw.Level}\"");
        sb.AppendLine($"{pad}\"Power Usage\": {Scalar(pw.Usage)}");
        sb.AppendLine($"{pad}\"Power Type\": {Scalar(pw.Type)}");
        if (pw.Action.Length > 0) sb.AppendLine($"{pad}\"Action Type\": {Scalar(pw.Action)}");
        if (pw.Keywords.Length > 0) AppendField(sb, "Keywords", pw.Keywords, indent);
        if (pw.Target.Length > 0) AppendField(sb, "Target", pw.Target, indent);
        foreach (var (label, value) in pw.Body)
            if (value.Length > 0) AppendField(sb, label, value, indent);
        if (pw.Flavor.Length > 0) AppendField(sb, "Flavor", pw.Flavor, indent);
        return sb.ToString().TrimEnd('\n').Split('\n').ToList();
    }

    /// <summary>Emit a single field (key: value) at the given indent as lines.</summary>
    public static List<string> EmitFieldLines(string key, string value, int indent)
    {
        var sb = new StringBuilder();
        AppendField(sb, key, value, indent);
        return sb.ToString().TrimEnd('\n').Split('\n').ToList();
    }

    static List<string> Tokens(string? s) =>
        Normalizer.Norm(s).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

    static bool IsSubsequence(List<string> needle, List<string> haystack)
    {
        int i = 0;
        foreach (var h in haystack) { if (i < needle.Count && needle[i] == h) i++; }
        return i == needle.Count;
    }

    static string Short(string s) => s.Length > 110 ? s[..110] + "…" : s;
}
