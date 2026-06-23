using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace CharM.Orcus.Import;

/// <summary>
/// Generates the single (non-boost) magic items: the slot items (head/waist/
/// arms/hands/ring/feet/wondrous) and the consumables. One element per source
/// entry, Property copied verbatim. A clean single "*Level N*" yields Item Level
/// + Cost (permanent or consumable price table); ambiguous levels ("*Level X*",
/// "*Level 9, 19 or 29*", scaling oils) are left in the Property verbatim with no
/// derived Level/Cost — better blank than wrong.
/// </summary>
public static class MiscItems
{
    static readonly int[] Permanent =
    {
        0, 360, 520, 680, 840, 1000, 1800, 2600, 3400, 4200, 5000,
        9000, 13000, 17000, 21000, 25000, 45000, 65000, 85000, 105000, 125000,
        225000, 325000, 425000, 525000, 625000, 1125000, 1625000, 2125000, 2625000, 3125000,
    };
    static readonly int[] Consumable =
    {
        0, 14, 21, 27, 34, 40, 72, 104, 136, 168, 200,
        360, 520, 680, 840, 1000, 1800, 2600, 3400, 4200, 5000,
        9000, 13000, 17000, 21000, 25000, 45000, 65000, 85000, 105000, 125000,
    };

    static readonly Regex H2 = new(@"^##\s+(.+?)\s*$", RegexOptions.Compiled);
    static readonly Regex H1 = new(@"^#\s+(.+?)\s*$", RegexOptions.Compiled);
    static readonly Regex H3 = new(@"^###\s+(.+?)\s*$", RegexOptions.Compiled);
    static readonly Regex LevelSingle = new(@"^\*Level (\d+)\*\s*$", RegexOptions.Compiled);
    static readonly Regex LevelAny = new(@"^\*Level\b.*\*\s*,?\s*$", RegexOptions.Compiled);

    public static int Generate(string book, string wondrousOut, string consumablesOut)
    {
        var lines = File.ReadAllLines(book);
        int fails = 0; var failMsgs = new List<string>();

        var slotSections = new (string Heading, string Slot)[]
        {
            ("Head Items", "Head"), ("Waist Items", "Waist"), ("Arms Items", "Arms"),
            ("Hands Items", "Hands"), ("Ring Items", "Ring"), ("Feet Items", "Feet"),
            ("Wondrous Items", "Wondrous"),
        };

        var wsb = Header("slot items (head/waist/arms/hands/ring/feet/wondrous)");
        var usedW = new HashSet<string>();
        foreach (var (heading, slot) in slotSections)
        {
            wsb.AppendLine($"# === {heading} ===");
            EmitSection(lines, book, heading, slot, slot, Permanent, wsb, usedW, ref fails, failMsgs);
            wsb.AppendLine();
        }
        File.WriteAllText(wondrousOut, wsb.ToString());

        var csb = Header("consumables (oils, potions, tonics, scrolls, …)");
        var usedC = new HashSet<string>();
        EmitSection(lines, book, "Consumable Items", "Consumable", "Consumable", Consumable, csb, usedC, ref fails, failMsgs);
        csb.AppendLine("# === Poisons (masterwork consumables — double the consumable price) ===");
        EmitPoisons(lines, csb, usedC, ref fails, failMsgs);
        File.WriteAllText(consumablesOut, csb.ToString());

        Console.WriteLine($"Wrote {wondrousOut} ({usedW.Count} items) and {consumablesOut} ({usedC.Count} items).");
        if (fails > 0)
        {
            Console.WriteLine($"\nROUND-TRIP FAILURES ({fails}):");
            foreach (var m in failMsgs.Take(40)) Console.WriteLine("  " + m);
            return 2;
        }
        Console.WriteLine("All Property text verified verbatim against the source.");
        return 0;
    }

    static StringBuilder Header(string what)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Magic {what} (Orcus Advanced Options) — generated verbatim by");
        sb.AppendLine($"# tools/CharM.Orcus.Import (generate-misc). Property is copied from the book;");
        sb.AppendLine($"# Item Level/Cost are filled only for a clean single \"*Level N*\" (Cost from the");
        sb.AppendLine($"# Magic Item Prices table). Ambiguous levels are left verbatim in Property with");
        sb.AppendLine($"# no derived Level/Cost. Do not hand-edit; regenerate instead.");
        sb.AppendLine();
        return sb;
    }

    static void EmitSection(string[] lines, string book, string heading, string slot, string type,
                            int[] price, StringBuilder sb, HashSet<string> usedIds,
                            ref int fails, List<string> failMsgs)
    {
        int start = -1, end = lines.Length;
        for (int i = 0; i < lines.Length; i++)
        {
            var m = H2.Match(lines[i]);
            if (m.Success && m.Groups[1].Value.Trim().Equals(heading, StringComparison.OrdinalIgnoreCase)) start = i;
            else if (start >= 0 && (H2.IsMatch(lines[i]) || H1.IsMatch(lines[i]))) { end = i; break; }
        }
        if (start < 0) { Console.Error.WriteLine($"section '{heading}' not found"); return; }

        int p = start + 1;
        while (p < end)
        {
            var mh = H3.Match(lines[p]);
            if (!mh.Success) { p++; continue; }
            int q = p + 1;
            while (q < end && !H3.IsMatch(lines[q])) q++;
            var bodyRaw = lines.Skip(p + 1).Take(q - (p + 1))
                .Where(l => !l.TrimStart().StartsWith("<figure")).ToList();
            string name = HttpUtility.HtmlDecode(mh.Groups[1].Value.Replace("*", "")).Trim();

            // Pull a clean single level; otherwise leave level lines in the body.
            int level = 0;
            var kept = new List<string>();
            foreach (var l in bodyRaw)
            {
                var t = l.Trim();
                var ms = LevelSingle.Match(t);
                if (ms.Success && level == 0) { level = int.Parse(ms.Groups[1].Value); continue; }
                kept.Add(l);
            }

            string property = CleanBody(kept);
            string srcNorm = Normalizer.Norm(string.Join(" ", bodyRaw));
            if (property.Length > 0 && !Phase2.TextIsFaithful(property, srcNorm))
            { fails++; failMsgs.Add($"{name} ({heading}): \"{Trunc(property)}\""); p = q; continue; }

            string id = "ORCUS_MAGIC_" + Slug(name);
            string baseId = id; int n = 2; while (!usedIds.Add(id)) id = $"{baseId}_{n++}";

            sb.AppendLine($"- id: {id}");
            sb.AppendLine($"  name: {Q(name)}");
            sb.AppendLine($"  type: Magic Item");
            sb.AppendLine($"  source: \"Orcus Original\"");
            sb.AppendLine($"  fields:");
            sb.AppendLine($"    \"Item Slot\": {Q(slot)}");
            sb.AppendLine($"    \"Magic Item Type\": {Q(type)}");
            if (level >= 1 && level <= 30)
            {
                sb.AppendLine($"    \"Item Level\": \"{level}\"");
                sb.AppendLine($"    Cost: {Q($"{price[level]:n0} gp")}");
            }
            if (property.Length > 0)
                foreach (var ln in Phase2.EmitFieldLines("Property", property, 4)) sb.AppendLine(ln);
            p = q;
        }
    }

    static readonly Regex H4 = new(@"^<h4 class=""Heading-4---[^""]*"">(.*?)</h4>\s*$", RegexOptions.Compiled);
    // Poison stat line: "**Consumable** **Attack** **5** (**Swift Action**) ● **Poison**"
    static readonly Regex PoisonHeader = new(@"^>?\s*\*\*Consumable\*\*\s+\*\*(?:Attack|Utility)\*\*\s+\*\*(\d+)\*\*", RegexOptions.Compiled);

    /// <summary>The "## Poisons" section: each <h4> block is a Consumable magic
    /// item used as a power. Level comes from the stat line; cost is double the
    /// consumable price (poisons are "masterwork consumables"). The effect/special
    /// prose is copied verbatim into Property; the stat line itself is dropped.</summary>
    static void EmitPoisons(string[] lines, StringBuilder sb, HashSet<string> usedIds,
                            ref int fails, List<string> failMsgs)
    {
        int start = -1, end = lines.Length;
        for (int i = 0; i < lines.Length; i++)
        {
            var m = H2.Match(lines[i]);
            if (m.Success && m.Groups[1].Value.Trim().Equals("Poisons", StringComparison.OrdinalIgnoreCase)) start = i;
            else if (start >= 0 && (H2.IsMatch(lines[i]) || H1.IsMatch(lines[i]))) { end = i; break; }
        }
        if (start < 0) { Console.Error.WriteLine("section 'Poisons' not found"); return; }

        int p = start + 1;
        while (p < end)
        {
            var mh = H4.Match(lines[p]);
            if (!mh.Success) { p++; continue; }
            int q = p + 1;
            while (q < end && !H4.IsMatch(lines[q])) q++;
            var body = lines.Skip(p + 1).Take(q - (p + 1)).Where(l => !l.TrimStart().StartsWith("<figure")).ToList();
            string name = HttpUtility.HtmlDecode(mh.Groups[1].Value.Replace("*", "")).Trim();

            int level = 0;
            var kept = new List<string>();
            foreach (var l in body)
            {
                var hm = PoisonHeader.Match(l.Trim());
                if (hm.Success && level == 0) { level = int.Parse(hm.Groups[1].Value); continue; }
                kept.Add(l);
            }

            string property = CleanBody(kept);
            string srcNorm = Normalizer.Norm(string.Join(" ", body));
            if (property.Length > 0 && !Phase2.TextIsFaithful(property, srcNorm))
            { fails++; failMsgs.Add($"{name} (Poisons): \"{Trunc(property)}\""); p = q; continue; }

            string id = "ORCUS_MAGIC_" + Slug(name);
            string baseId = id; int n = 2; while (!usedIds.Add(id)) id = $"{baseId}_{n++}";

            sb.AppendLine($"- id: {id}");
            sb.AppendLine($"  name: {Q(name)}");
            sb.AppendLine($"  type: Magic Item");
            sb.AppendLine($"  source: \"Orcus Original\"");
            sb.AppendLine($"  fields:");
            sb.AppendLine($"    \"Item Slot\": \"Consumable\"");
            sb.AppendLine($"    \"Magic Item Type\": \"Consumable\"");
            if (level >= 1 && level <= 30)
            {
                sb.AppendLine($"    \"Item Level\": \"{level}\"");
                sb.AppendLine($"    Cost: {Q($"{Consumable[level] * 2:n0} gp")}");
            }
            sb.AppendLine($"    Keywords: \"Poison\"");
            if (property.Length > 0)
                foreach (var ln in Phase2.EmitFieldLines("Property", property, 4)) sb.AppendLine(ln);
            p = q;
        }
        sb.AppendLine();
    }

    static string CleanBody(List<string> lines)
    {
        var parts = new List<string>();
        foreach (var raw in lines)
        {
            var l = raw.Trim();
            if (l == ">") continue;                       // lone blockquote marker
            if (l.StartsWith(">")) l = l.Substring(1).Trim();
            l = l.Replace("**", "").Replace("*", "").Replace("●", "").Replace("`", "")
                 .Replace("|", " ").Replace("#", "");
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
    static string Q(string v) => "\"" + v.Replace("\"", "\\\"") + "\"";
    static string Trunc(string s) => s.Length > 90 ? s[..90] + "…" : s;
}
