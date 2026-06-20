using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace CharM.Orcus.Import;

/// <summary>
/// Generates the named magic-item "boosts" (Advanced Options) as the +1..+6
/// enchantment versions, verbatim. Property text is copied from the book; item
/// level comes from the Enchanted Item Progression table (level = 5*(X-1)+1 +
/// tier) and Cost from the Magic Item Prices table — both reproduced from the
/// Rulebook. Armor boosts add a +X AC statadd; cloak/neck boosts a +X
/// Fort/Ref/Will statadd; weapon/focus boosts carry the Enhancement field.
/// </summary>
public static class Boosts
{
    // Permanent magic item price (gp) by item level 1..30 (Rulebook price table).
    static readonly int[] Price =
    {
        0,
        360, 520, 680, 840, 1000, 1800, 2600, 3400, 4200, 5000,
        9000, 13000, 17000, 21000, 25000, 45000, 65000, 85000, 105000, 125000,
        225000, 325000, 425000, 525000, 625000, 1125000, 1625000, 2125000, 2625000, 3125000,
    };

    sealed class Category
    {
        public required string Heading;     // "Focus Boosts"
        public required string Slot;        // Item Slot
        public required string Type;        // Magic Item Type
        public required string NameSuffix;  // appended to the boost base name
        public required string IdCat;       // id category fragment
    }

    static readonly Category[] Categories =
    {
        new() { Heading = "Focus Boosts",  Slot = "Implement", Type = "Focus",  NameSuffix = " focus",  IdCat = "FOCUS" },
        new() { Heading = "Weapon Boosts", Slot = "Weapon",    Type = "Weapon", NameSuffix = " weapon", IdCat = "WEAPON" },
        new() { Heading = "Cloak Boosts",  Slot = "Neck",      Type = "Neck",   NameSuffix = "",         IdCat = "NECK" },
        new() { Heading = "Armor Boosts",  Slot = "Body",      Type = "Armor",  NameSuffix = " armor",  IdCat = "ARMOR" },
    };

    static readonly Regex Heading2 = new(@"^##\s+(.+?)\s*$", RegexOptions.Compiled);
    static readonly Regex Heading3 = new(@"^###\s+(.+?)\s*$", RegexOptions.Compiled);
    static readonly Regex Heading1 = new(@"^#\s+(.+?)\s*$", RegexOptions.Compiled);
    static readonly Regex TierRx = new(@"^(?<n>.+?)\s*\((?<t>IV|I{1,3})\)\s*$", RegexOptions.Compiled);
    static readonly Regex MinEnchRx = new(@"Minimum Enchantment:\s*\+(\d)", RegexOptions.Compiled);

    public static int Generate(string book, string outPath)
    {
        var lines = File.ReadAllLines(book);
        var sb = new StringBuilder();
        sb.AppendLine("# Named magic-item boosts (Orcus Advanced Options) — generated verbatim by");
        sb.AppendLine("# tools/CharM.Orcus.Import (generate-boosts). A boost is a named property on an");
        sb.AppendLine("# enchanted item; each is emitted as its +1..+6 enchantment versions (respecting");
        sb.AppendLine("# any minimum enchantment). Item Level = 5*(X-1)+1 + boost tier (Enchanted Item");
        sb.AppendLine("# Progression table); Cost from the Magic Item Prices table. Property text is");
        sb.AppendLine("# copied from the book. Armor -> +X AC; cloak/neck -> +X Fort/Ref/Will; weapon/");
        sb.AppendLine("# focus -> Enhancement field. Do not hand-edit; regenerate instead.");
        sb.AppendLine();

        int total = 0, fails = 0;
        var failMsgs = new List<string>();
        var usedIds = new HashSet<string>();

        foreach (var cat in Categories)
        {
            int start = -1, end = lines.Length;
            for (int i = 0; i < lines.Length; i++)
            {
                var m = Heading2.Match(lines[i]);
                if (m.Success && m.Groups[1].Value.Trim().Equals(cat.Heading, StringComparison.OrdinalIgnoreCase)) { start = i; }
                else if (start >= 0 && (Heading2.IsMatch(lines[i]) || Heading1.IsMatch(lines[i]))) { end = i; break; }
            }
            if (start < 0) { Console.Error.WriteLine($"section '{cat.Heading}' not found"); continue; }

            sb.AppendLine($"# === {cat.Heading} ===");
            int p = start + 1;
            while (p < end)
            {
                var mh = Heading3.Match(lines[p]);
                if (!mh.Success) { p++; continue; }
                int q = p + 1;
                while (q < end && !Heading3.IsMatch(lines[q])) q++;
                var bodyLines = lines.Skip(p + 1).Take(q - (p + 1))
                    .Where(l => !l.TrimStart().StartsWith("<figure")).ToList();

                string rawHeading = HttpUtility.HtmlDecode(mh.Groups[1].Value).Trim();
                string baseName = rawHeading; int tier = 0;
                var mt = TierRx.Match(rawHeading);
                if (mt.Success) { baseName = mt.Groups["n"].Value.Trim(); tier = Roman(mt.Groups["t"].Value); }

                string body = CleanBody(bodyLines);
                int minEnch = 1;
                var me = MinEnchRx.Match(body);
                if (me.Success) minEnch = int.Parse(me.Groups[1].Value);

                string srcNorm = Normalizer.Norm(string.Join(" ", bodyLines));
                EmitBoost(sb, cat, baseName, tier, minEnch, body, srcNorm, usedIds,
                          ref total, ref fails, failMsgs);
                p = q;
            }
            sb.AppendLine();
        }

        File.WriteAllText(outPath, sb.ToString());
        Console.WriteLine($"Wrote {outPath}: {total} boost versions across {Categories.Length} categories.");
        if (fails > 0)
        {
            Console.WriteLine($"\nROUND-TRIP FAILURES ({fails}): Property text not found in source:");
            foreach (var m in failMsgs.Take(40)) Console.WriteLine("  " + m);
            return 2;
        }
        Console.WriteLine("All boost Property text verified verbatim against the source.");
        return 0;
    }

    static void EmitBoost(StringBuilder sb, Category cat, string baseName, int tier, int minEnch,
                          string property, string srcNorm, HashSet<string> usedIds,
                          ref int total, ref int fails, List<string> failMsgs)
    {
        // Round-trip: the (cleaned) property must be a subsequence of the source.
        if (!Phase2.TextIsFaithful(property, srcNorm))
        {
            fails++; failMsgs.Add($"{baseName} ({cat.Heading}): \"{Trunc(property)}\"");
            return;
        }

        string display = baseName + cat.NameSuffix;
        string slug = Slug(baseName) + "_" + cat.IdCat;
        string roman = tier > 0 ? Roman(tier) : "";

        for (int x = minEnch; x <= 6; x++)
        {
            int level = 5 * (x - 1) + 1 + tier;
            if (level < 1 || level > 30) continue;
            string id = $"ORCUS_MAGIC_{slug}_PLUS{x}";
            if (!usedIds.Add(id)) id += "_X";
            string cost = $"{Price[level]:n0} gp";

            sb.AppendLine($"- id: {id}");
            sb.AppendLine($"  name: {Q("+" + x + " " + display.ToLowerInvariant())}");
            sb.AppendLine($"  type: Magic Item");
            sb.AppendLine($"  source: \"Orcus Original\"");
            sb.AppendLine($"  fields:");
            sb.AppendLine($"    \"Item Slot\": {cat.Slot}");
            sb.AppendLine($"    \"Magic Item Type\": {cat.Type}");
            sb.AppendLine($"    Enhancement: \"+{x}\"");
            sb.AppendLine($"    \"Item Level\": \"{level}\"");
            if (roman.Length > 0) sb.AppendLine($"    Boost: {roman}");
            sb.AppendLine($"    Cost: {Q(cost)}");
            foreach (var l in Phase2.EmitFieldLines("Property", property, 4)) sb.AppendLine(l);
            // Functional wiring: armor -> AC; neck/cloak -> defenses; weapon/focus -> Enhancement field only.
            if (cat.Type == "Armor")
            {
                sb.AppendLine($"  rules:");
                sb.AppendLine($"    - {{ statadd: AC, value: {x}, bonusType: Enhancement }}");
            }
            else if (cat.Type == "Neck")
            {
                sb.AppendLine($"  rules:");
                sb.AppendLine($"    - {{ statadd: Fortitude Defense, value: {x}, bonusType: Enhancement }}");
                sb.AppendLine($"    - {{ statadd: Reflex Defense, value: {x}, bonusType: Enhancement }}");
                sb.AppendLine($"    - {{ statadd: Will Defense, value: {x}, bonusType: Enhancement }}");
            }
            total++;
        }
    }

    static string CleanBody(List<string> lines)
    {
        var parts = new List<string>();
        foreach (var raw in lines)
        {
            var l = raw.Trim();
            if (l.StartsWith("> ")) l = l.Substring(2).Trim();
            l = l.Replace("**", "").Replace("*", "").Replace("●", "").Replace("`", "");
            l = l.TrimStart('-', ' ').Trim();
            l = string.Join(' ', l.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
            if (l.Length > 0) parts.Add(l);
        }
        return string.Join(" ", parts).Trim();
    }

    static int Roman(string r) => r switch { "I" => 1, "II" => 2, "III" => 3, "IV" => 4, _ => 0 };
    static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", _ => "" };

    static string Slug(string s)
    {
        var sb = new StringBuilder();
        bool u = false;
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
