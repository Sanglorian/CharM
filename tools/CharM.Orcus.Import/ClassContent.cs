using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace CharM.Orcus.Import;

/// <summary>
/// Extracts the verbatim *content* of a class section from the source book: the
/// prose description of each named feature/talent, and each feature power's stat
/// block. Engine rules are NOT touched here — the patcher keeps those.
/// </summary>
public sealed class ClassContent
{
    public Dictionary<string, string> FeatureText = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, ParsedPower> FeaturePowers = new(StringComparer.OrdinalIgnoreCase);

    static readonly Regex Heading = new(@"^(#{1,3})\s+(.+?)\s*$", RegexOptions.Compiled);
    static readonly Regex H4 = new(@"^<h4 class=""Heading-4---[^""]*"">(.*?)</h4>\s*$", RegexOptions.Compiled);
    static readonly Regex Bullet = new(@"^\*\s+\*\*(?<n>[^*]+?):\*\*\s*(?<r>.*)$", RegexOptions.Compiled);
    static readonly Regex BoldLed = new(@"^\*\*(?<n>[^*]+?):\*\*\s*(?<r>.*)$", RegexOptions.Compiled);

    static readonly HashSet<string> SkipHeadings = new(StringComparer.OrdinalIgnoreCase)
    {
        "Stats", "Proficiency and Training", "Features", "Powers", "Talents",
        "Dualclass", "Wild Gift", "Favored Terrain", "Wild Gifts", "Favored Terrains",
    };
    static readonly HashSet<string> SkipDefs = new(StringComparer.OrdinalIgnoreCase)
    {
        "Key Ability", "Secondary Ability", "Source", "Sources", "Note",
        "Talents and Secondary Abilities", "Hit Points at 1st Level",
        "Additional Hit Points at Higher Levels", "Recoveries per Long Rest",
        "Defenses", "Armor Proficiencies", "Weapon Proficiencies",
        "Focus Proficiencies", "Trained Skills", "Class Skills",
        "Class Disciplines", "Benefit", "Prerequisite",
    };

    public static ClassContent Parse(string sourceFile, string className)
    {
        var lines = File.ReadAllLines(sourceFile);
        int start = -1, end = lines.Length;
        for (int i = 0; i < lines.Length; i++)
        {
            var m = Heading.Match(lines[i]);
            if (!m.Success || m.Groups[1].Value.Length != 1) continue; // single '#'
            var name = HttpUtility.HtmlDecode(m.Groups[2].Value).Trim();
            if (start < 0 && Same(name, className)) start = i;
            else if (start >= 0) { end = i; break; }
        }
        if (start < 0) throw new Exception($"class '{className}' not found in {sourceFile}");

        var cc = new ClassContent();
        string? curName = null;
        var buf = new StringBuilder();

        void Flush()
        {
            if (curName != null)
                cc.FeatureText[curName] = CollapseSpaces(buf.ToString());
            curName = null; buf.Clear();
        }

        for (int i = start + 1; i < end; i++)
        {
            var raw = lines[i];
            if (raw.TrimStart().StartsWith("<figure")) continue;

            var mh4 = H4.Match(raw);
            if (mh4.Success)
            {
                Flush();
                string pname = HttpUtility.HtmlDecode(mh4.Groups[1].Value.Replace("*", "")).Trim();
                int j = i + 1;
                var block = new List<string>();
                for (; j < end; j++)
                {
                    if (H4.IsMatch(lines[j]) || Heading.IsMatch(lines[j])) break;
                    if (lines[j].TrimStart().StartsWith("<figure")) continue;
                    block.Add(lines[j]);
                }
                cc.FeaturePowers[pname] = Phase2.ParsePowerBlock(pname, block);
                i = j - 1;
                continue;
            }

            var mhead = Heading.Match(raw);
            if (mhead.Success)
            {
                Flush();
                var name = HttpUtility.HtmlDecode(mhead.Groups[2].Value).Trim();
                if (!SkipHeadings.Contains(name)) { curName = name; }
                continue;
            }

            var mb = Bullet.Match(raw);
            var ml = mb.Success ? mb : BoldLed.Match(raw);
            if (ml.Success)
            {
                var name = HttpUtility.HtmlDecode(ml.Groups["n"].Value).Trim();
                if (!SkipDefs.Contains(name))
                {
                    Flush();
                    curName = name;
                    buf.Append(Unwrap(ml.Groups["r"].Value));
                    continue;
                }
                // a structural stat line ends any open feature
                Flush();
                continue;
            }

            if (curName != null)
            {
                var t = Unwrap(raw);
                if (t.Length > 0) { if (buf.Length > 0) buf.Append(' '); buf.Append(t); }
            }
        }
        Flush();
        return cc;
    }

    /// <summary>Resolve a YAML element name (e.g. "Wild Gift: Skinchanger") to the
    /// source feature key ("Skinchanger").</summary>
    public string? ResolveFeatureKey(string yamlName)
    {
        if (FeatureText.ContainsKey(yamlName)) return yamlName;
        int idx = yamlName.LastIndexOf(": ", StringComparison.Ordinal);
        if (idx >= 0)
        {
            var tail = yamlName[(idx + 2)..].Trim();
            if (FeatureText.ContainsKey(tail)) return tail;
        }
        return null;
    }

    static bool Same(string a, string b)
    {
        static string K(string s) => string.Join(' ',
            s.Replace('’', '\'').Replace('‘', '\'').ToLowerInvariant()
             .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        return K(a) == K(b);
    }
    static string Unwrap(string s) => CollapseSpaces(s.Replace("**", "").Replace("*", "").Trim());
    static string CollapseSpaces(string s) =>
        string.Join(' ', s.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
}
