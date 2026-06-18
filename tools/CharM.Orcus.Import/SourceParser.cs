using System.Text.RegularExpressions;
using System.Web;

namespace CharM.Orcus.Import;

public sealed record SourcePower(string Name, string Discipline, string UsageClass);

/// <summary>
/// Lightweight structural read of the source markdown: which power names appear,
/// and under which discipline. Used for coverage (omission) checks. Faithfulness
/// of the text itself is handled separately by the substring scan against the
/// full normalized source blob.
/// </summary>
public static class SourceParser
{
    static readonly Regex H4 = new(@"^<h4 class=""Heading-4---([^""]*)"">(.*?)</h4>\s*$", RegexOptions.Compiled);
    static readonly Regex H2 = new(@"^##\s+(.+?)\s*$", RegexOptions.Compiled);
    static readonly Regex H1 = new(@"^#\s+(.+?)\s*$", RegexOptions.Compiled);
    static readonly Regex KeyAbility = new(@"^\*\*Key Ability:\*\*", RegexOptions.Compiled);

    public static List<SourcePower> ParsePowers(IEnumerable<string> sourceFiles)
    {
        var powers = new List<SourcePower>();
        foreach (var file in sourceFiles)
        {
            var lines = File.ReadAllLines(file);
            string currentSection = "(none)";        // last ## or # heading
            bool currentIsDiscipline = false;        // does the section declare a Key Ability?
            // Pre-scan: a ## section is a discipline iff a **Key Ability:** line
            // follows it before the next heading. We do a single pass with a small
            // lookahead by tracking the most recent section and confirming.
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                var m2 = H2.Match(line);
                if (m2.Success)
                {
                    currentSection = HtmlDecode(m2.Groups[1].Value).Trim();
                    currentIsDiscipline = LooksLikeDiscipline(lines, i);
                    continue;
                }
                var m1 = H1.Match(line);
                if (m1.Success)
                {
                    currentSection = HtmlDecode(m1.Groups[1].Value).Trim();
                    currentIsDiscipline = false; // single # is a class, not a discipline
                    continue;
                }

                var mh = H4.Match(line);
                if (mh.Success)
                {
                    var usageClass = mh.Groups[1].Value.Trim();
                    var name = HtmlDecode(StripMd(mh.Groups[2].Value)).Trim();
                    if (name.Length == 0) continue;
                    var discipline = currentIsDiscipline ? currentSection : currentSection;
                    powers.Add(new SourcePower(name, discipline, usageClass));
                }
            }
        }
        return powers;
    }

    static bool LooksLikeDiscipline(string[] lines, int headingIndex)
    {
        for (int j = headingIndex + 1; j < lines.Length && j < headingIndex + 8; j++)
        {
            if (H2.IsMatch(lines[j]) || H1.IsMatch(lines[j])) return false;
            if (KeyAbility.IsMatch(lines[j])) return true;
        }
        return false;
    }

    static string StripMd(string s) => s.Replace("*", "").Replace("`", "");
    static string HtmlDecode(string s) => HttpUtility.HtmlDecode(s);
}
