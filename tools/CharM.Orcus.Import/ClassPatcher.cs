using System.Text.RegularExpressions;

namespace CharM.Orcus.Import;

/// <summary>
/// Rewrites only the verbatim-text fields of a class YAML file from the source
/// book — feature/talent Description prose and feature-power stat blocks — and
/// deletes invented Flavor. Every `rules:` block, id, category, and structural
/// field is preserved byte-for-byte. Reports a round-trip result per change.
/// </summary>
public static class ClassPatcher
{
    public static int Patch(string sourceFile, string classFile, string className) =>
        PatchWith(ClassContent.Parse(sourceFile, className), classFile);

    public static int PatchGlobal(string sourceFile, string classFile) =>
        PatchWith(ClassContent.ParseAll(sourceFile), classFile);

    static int PatchWith(ClassContent content, string classFile)
    {
        var lines = File.ReadAllLines(classFile).ToList();

        // Split into element blocks (each begins with a top-level "- " line).
        var blocks = new List<List<string>>();
        var preamble = new List<string>();
        List<string>? cur = null;
        foreach (var line in lines)
        {
            if (line.StartsWith("- "))
            {
                if (cur != null) blocks.Add(cur);
                cur = new List<string> { line };
            }
            else if (cur == null) preamble.Add(line);
            else cur.Add(line);
        }
        if (cur != null) blocks.Add(cur);

        int flavorsRemoved = 0, descsFixed = 0, powersFixed = 0;
        var problems = new List<string>();

        foreach (var block in blocks)
        {
            string? name = FieldValue(block, "name");
            string? type = FieldValue(block, "type");
            string cats = RawLine(block, "categories") ?? "";
            if (name == null) continue;

            // Inline flow-mapping form: `  fields: { Level: "21", Description: "..." }`.
            int inlineIdx = block.FindIndex(l => Regex.IsMatch(l, @"^\s*fields:\s*\{"));
            if (inlineIdx >= 0)
            {
                string before = block[inlineIdx];
                string after = RemoveInlineField(before, "Flavor");
                if (after != before) flavorsRemoved++;
                var ikey = content.ResolveFeatureKey(name);
                if (ikey != null && !string.IsNullOrWhiteSpace(content.FeatureText[ikey]))
                {
                    string fld = TargetField(type);
                    after = SetInlineField(after, fld, StripLabel(fld, content.FeatureText[ikey]));
                    descsFixed++;
                }
                block[inlineIdx] = after;
                continue;
            }

            if (RemoveField(block, "Flavor")) flavorsRemoved++;

            bool isFeaturePower = type == "Power" && cats.Contains("feature");
            if (isFeaturePower && (content.GetPower(name) is { } pw))
            {
                var probs = Phase2.CheckPower(pw);
                if (probs.Count > 0)
                    problems.Add($"  ✗ feature power {name}: {string.Join("; ", probs)}");
                else
                {
                    ReplaceFieldsBlock(block, Phase2.EmitFieldLines(pw, 4));
                    powersFixed++;
                }
                continue;
            }

            var key = content.ResolveFeatureKey(name);
            if (key != null)
            {
                var text = content.FeatureText[key];
                if (string.IsNullOrWhiteSpace(text)) continue;
                string fld = TargetField(type);
                SetField(block, fld, Phase2.EmitFieldLines(fld, StripLabel(fld, text), 4));
                descsFixed++;
            }
        }

        if (problems.Count > 0)
        {
            Console.WriteLine("Round-trip problems (no changes written):");
            foreach (var p in problems) Console.WriteLine(p);
            return 2;
        }

        var outLines = new List<string>();
        outLines.AddRange(preamble);
        foreach (var b in blocks) outLines.AddRange(b);
        File.WriteAllText(classFile, string.Join('\n', outLines).TrimEnd('\n') + "\n");

        Console.WriteLine($"Patched {classFile}: {descsFixed} feature descriptions, "
            + $"{powersFixed} feature powers regenerated, {flavorsRemoved} Flavor field(s) removed.");
        Console.WriteLine($"Source class features: {content.FeatureText.Count}; feature powers: {content.FeaturePowers.Count}.");
        return 0;
    }

    // --- block field helpers ---------------------------------------------------
    static int Indent(string l) { int n = 0; while (n < l.Length && l[n] == ' ') n++; return n; }

    static (int start, int end) FieldsRegion(List<string> block)
    {
        int fi = block.FindIndex(l => l.TrimEnd() == "  fields:");
        if (fi < 0) return (-1, -1);
        int end = block.Count;
        for (int i = fi + 1; i < block.Count; i++)
        {
            var l = block[i];
            if (l.Trim().Length == 0) continue;
            if (Indent(l) <= 2) { end = i; break; }
        }
        return (fi + 1, end);
    }

    static string? KeyOf(string line)
    {
        if (Indent(line) != 4) return null;
        var t = line.Trim();
        if (t.StartsWith("\""))
        {
            int q = t.IndexOf('"', 1);
            return q > 0 ? t.Substring(1, q - 1) : null;
        }
        int c = t.IndexOf(':');
        return c > 0 ? t.Substring(0, c) : null;
    }

    static bool RemoveField(List<string> block, string key)
    {
        var (s, e) = FieldsRegion(block);
        if (s < 0) return false;
        for (int i = s; i < e && i < block.Count; i++)
        {
            if (KeyOf(block[i]) == key)
            {
                int j = i + 1;
                while (j < block.Count && Indent(block[j]) >= 5) j++; // folded continuations
                block.RemoveRange(i, j - i);
                return true;
            }
        }
        return false;
    }

    static void SetField(List<string> block, string key, List<string> newLines)
    {
        RemoveField(block, key);
        var (s, e) = FieldsRegion(block);
        if (s < 0) return;
        // Insert at the end of the fields region.
        int at = e;
        block.InsertRange(at, newLines);
    }

    static void ReplaceFieldsBlock(List<string> block, List<string> newFieldLines)
    {
        var (s, e) = FieldsRegion(block);
        if (s < 0) return;
        block.RemoveRange(s, e - s);
        block.InsertRange(s, newFieldLines);
    }

    static string? RawLine(List<string> block, string key)
    {
        var l = block.FirstOrDefault(x => x.TrimStart().StartsWith(key + ":"));
        return l;
    }

    static string Esc(string v) => v.Replace("\\", "\\\\").Replace("\"", "\\\"");

    static string RemoveInlineField(string line, string key)
    {
        // Drop `key: "..."` (with an adjacent comma) from an inline flow map.
        string s = Regex.Replace(line, @"\s*,?\s*" + Regex.Escape(key) + @":\s*""(?:[^""\\]|\\.)*""", "");
        s = s.Replace("{ ,", "{").Replace("{,", "{");
        return s;
    }

    static string SetInlineField(string line, string field, string text)
    {
        string q = "\"" + Esc(text) + "\"";
        if (Regex.IsMatch(line, @"\b" + field + @":\s*""(?:[^""\\]|\\.)*"""))
            return Regex.Replace(line, @"(\b" + field + @":\s*)""(?:[^""\\]|\\.)*""", "$1" + q.Replace("$", "$$"));
        // Insert before the closing brace.
        int brace = line.LastIndexOf('}');
        if (brace < 0) return line;
        string head = line.Substring(0, brace).TrimEnd();
        string sep = head.TrimEnd().EndsWith("{") ? " " : ", ";
        return head + sep + field + ": " + q + " }";
    }

    static string TargetField(string? type) => type == "Feat" ? "Benefit" : "Description";
    static string StripLabel(string field, string text) =>
        field == "Benefit" ? Regex.Replace(text, @"^Benefit:\s*", "") : text;

    static string? FieldValue(List<string> block, string topKey)
    {
        // top-level element key like "  name: \"X\"" (indent 2)
        var l = block.FirstOrDefault(x => Indent(x) == 2 && x.Trim().StartsWith(topKey + ":"));
        if (l == null) return null;
        var v = l.Trim().Substring(topKey.Length + 1).Trim();
        if (v.StartsWith("\"") && v.EndsWith("\"") && v.Length >= 2) v = v[1..^1];
        return v;
    }
}
