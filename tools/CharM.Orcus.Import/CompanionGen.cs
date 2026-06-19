using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace CharM.Orcus.Import;

/// <summary>
/// Generates the animal companions (Classes and Powers "Animal Companions").
/// Numeric stats (abilities, base AC/defenses, speed) are extracted as values;
/// the attack powers, special traits, senses, skills and the "could also be used
/// for" note are copied verbatim. A round-trip gate confirms each prose field is
/// a subsequence of the source stat block.
/// </summary>
public static class CompanionGen
{
    static readonly Regex H3 = new(@"^###\s+(.+?)\s*$", RegexOptions.Compiled);
    static readonly Regex H2 = new(@"^##\s+(.+?)\s*$", RegexOptions.Compiled);
    static readonly Regex H1 = new(@"^#\s+(.+?)\s*$", RegexOptions.Compiled);
    static readonly Regex Abil1 = new(@"Str:\*\*\s*(\d+).*Con:\*\*\s*(\d+).*Dex:\*\*\s*(\d+)", RegexOptions.Compiled);
    static readonly Regex Abil2 = new(@"Int:\*\*\s*(\d+).*Wis:\*\*\s*(\d+).*Cha:\*\*\s*(\d+)", RegexOptions.Compiled);
    static readonly Regex Defs = new(@"AC:\*\*\s*(\d+).*Fort:\*\*\s*(\d+).*Ref:\*\*\s*(\d+).*Will:\*\*\s*(\d+)", RegexOptions.Compiled);

    const string Glyphs = "‡†⤢◊✦";

    public static int Generate(string book, string outPath)
    {
        var lines = File.ReadAllLines(book);
        int start = -1, end = lines.Length;
        for (int i = 0; i < lines.Length; i++)
        {
            var m = H2.Match(lines[i]);
            if (m.Success && m.Groups[1].Value.Trim().Equals("Animal Companions", StringComparison.OrdinalIgnoreCase)) start = i;
            else if (start >= 0 && (H2.IsMatch(lines[i]) || H1.IsMatch(lines[i]))) { end = i; break; }
        }
        if (start < 0) { Console.Error.WriteLine("Animal Companions section not found"); return 1; }

        var sb = new StringBuilder();
        sb.AppendLine("# Animal Companions (Orcus Classes and Powers) — generated verbatim by");
        sb.AppendLine("# tools/CharM.Orcus.Import (generate-companions). Engine-native `Companion`");
        sb.AppendLine("# elements tagged ORCUS_ANIMAL_COMPANION for the Sylvan's Animal Companion wild");
        sb.AppendLine("# gift. Numeric stats are extracted as values (AC/defenses are the base; the");
        sb.AppendLine("# companion adds your level); attacks, traits, senses, skills and the flavour");
        sb.AppendLine("# note are copied verbatim. Do not hand-edit; regenerate instead.");
        sb.AppendLine();

        int fails = 0; var failMsgs = new List<string>();
        int p = start + 1;
        while (p < end)
        {
            var mh = H3.Match(lines[p]);
            if (!mh.Success) { p++; continue; }
            string name = HttpUtility.HtmlDecode(mh.Groups[1].Value.Replace("*", "")).Trim();
            int q = p + 1;
            while (q < end && !H3.IsMatch(lines[q])) q++;
            var block = lines.Skip(p + 1).Take(q - (p + 1)).Where(l => !l.TrimStart().StartsWith("<figure")).ToList();
            if (name != "Playing with Kits" && name != "About Kits" && name != "Redundant Features"
                && name != "Powers and Associated Disciplines" && name != "Key and Secondary Abilities")
                EmitCompanion(sb, name, block, ref fails, failMsgs);
            p = q;
        }

        File.WriteAllText(outPath, sb.ToString());
        Console.WriteLine($"Wrote {outPath}.");
        if (fails > 0)
        {
            Console.WriteLine($"\nROUND-TRIP FAILURES ({fails}):");
            foreach (var m in failMsgs) Console.WriteLine("  " + m);
            return 2;
        }
        Console.WriteLine("All companion prose verified verbatim against the source.");
        return 0;
    }

    static void EmitCompanion(StringBuilder sb, string name, List<string> block, ref int fails, List<string> failMsgs)
    {
        string srcNorm = Normalizer.Norm(string.Join(" ", block));
        string type = "", senses = "", skills = "", speed = "", hp = "";
        string str = "", con = "", dex = "", intel = "", wis = "", cha = "";
        string ac = "", fort = "", refl = "", will = "";
        var attacks = new List<string>();
        var traits = new List<string>();
        var notes = new List<string>();

        // Strip a leading "> " and trailing whitespace helper.
        string Clean(string s) => string.Join(' ', s.Replace("**", "").Replace("*", "").Replace("●", "")
            .Replace("`", "").Trim().TrimStart(Glyphs.ToCharArray()).Trim()
            .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));

        for (int i = 0; i < block.Count; i++)
        {
            var raw = block[i].TrimEnd();
            var t = raw.Trim();
            if (t.Length == 0 || t == "**Animal Companion**") continue;

            if (t.StartsWith("**Senses:**") || t.StartsWith("**Skills:**"))
            {
                var c = Clean(t);
                int si = c.IndexOf("Skills:", StringComparison.OrdinalIgnoreCase);
                if (c.StartsWith("Senses:", StringComparison.OrdinalIgnoreCase))
                {
                    if (si >= 0) { senses = c.Substring(7, si - 7).Trim().TrimEnd(';').Trim(); skills = c.Substring(si + 7).Trim(); }
                    else senses = c.Substring(7).Trim();
                }
                else skills = c.Substring(7).Trim();
                continue;
            }
            var a1 = Abil1.Match(t); if (a1.Success) { str = a1.Groups[1].Value; con = a1.Groups[2].Value; dex = a1.Groups[3].Value; continue; }
            var a2 = Abil2.Match(t); if (a2.Success) { intel = a2.Groups[1].Value; wis = a2.Groups[2].Value; cha = a2.Groups[3].Value; continue; }
            if (t.StartsWith("**Speed:**")) { speed = Clean(t).Substring(6).Trim(); continue; }
            var df = Defs.Match(t); if (df.Success) { ac = df.Groups[1].Value; fort = df.Groups[2].Value; refl = df.Groups[3].Value; will = df.Groups[4].Value; continue; }
            if (t.StartsWith("**HP:**")) { hp = Clean(t).Substring(3).Trim(); continue; }
            if (t.StartsWith("**Type:**")) { type = Clean(t).Substring(5).Trim(); continue; }

            // Attack: a glyph-led power line; its detail is on the following line(s).
            if (Glyphs.IndexOf(t[0]) >= 0)
            {
                var head = Clean(t);
                var detail = new List<string>();
                while (i + 1 < block.Count)
                {
                    var nx = block[i + 1].Trim();
                    if (nx.Length == 0) { i++; continue; }
                    if (Glyphs.IndexOf(nx.Length > 0 ? nx[0] : ' ') >= 0 || nx.StartsWith("**")) break;
                    detail.Add(Clean(nx)); i++;
                    break; // attack detail is a single line in this source
                }
                attacks.Add((head + " " + string.Join(" ", detail)).Trim());
                continue;
            }
            // Trait: a bold-only name line followed by its text.
            if (t.StartsWith("**"))
            {
                var tn = Clean(t);
                var txt = new List<string>();
                while (i + 1 < block.Count)
                {
                    var nx = block[i + 1].Trim();
                    if (nx.Length == 0 || Glyphs.IndexOf(nx.Length > 0 ? nx[0] : ' ') >= 0 || nx.StartsWith("**")) break;
                    txt.Add(Clean(nx)); i++;
                }
                traits.Add((tn + ": " + string.Join(" ", txt)).Trim());
                continue;
            }
            // The type line (plain, appears before stats) or a trailing flavour note.
            if (type.Length == 0 && str.Length == 0) type = Clean(t);
            else notes.Add(Clean(t));
        }

        string attack = string.Join(" ", attacks);
        string trait = string.Join(" ", traits);
        string note = string.Join(" ", notes);

        // Round-trip every verbatim prose field.
        var probs = new List<string>();
        void Check(string field, string val)
        {
            if (val.Length > 0 && !Phase2.TextIsFaithful(val, srcNorm))
                probs.Add($"{name} [{field}]: \"{(val.Length > 80 ? val[..80] + "…" : val)}\"");
        }
        Check("Attack", attack); Check("Traits", trait); Check("Note", note);
        Check("Senses", senses); Check("Skills", skills); Check("HP", hp);
        fails += probs.Count; failMsgs.AddRange(probs);

        string size = type.Split(' ', 2)[0];
        sb.AppendLine($"- id: ORCUS_COMPANION_{Slug(name)}");
        sb.AppendLine($"  name: {Q(name)}");
        sb.AppendLine($"  type: Companion");
        sb.AppendLine($"  source: \"Orcus (Outlaw Kingdoms)\"");
        sb.AppendLine($"  categories: [ORCUS_ANIMAL_COMPANION]");
        sb.AppendLine($"  fields:");
        if (type.Length > 0) sb.AppendLine($"    Type: {Q(type)}");
        if (size.Length > 0) sb.AppendLine($"    Size: {Q(size)}");
        Stat(sb, "Strength", str); Stat(sb, "Constitution", con); Stat(sb, "Dexterity", dex);
        Stat(sb, "Intelligence", intel); Stat(sb, "Wisdom", wis); Stat(sb, "Charisma", cha);
        if (speed.Length > 0) sb.AppendLine($"    Speed: {Q(speed)}");
        Stat(sb, "Armor Class", ac); Stat(sb, "Fortitude Defense", fort);
        Stat(sb, "Reflex Defense", refl); Stat(sb, "Will Defense", will);
        if (senses.Length > 0) sb.AppendLine($"    Senses: {Q(senses)}");
        if (skills.Length > 0) foreach (var l in Phase2.EmitFieldLines("Skills", skills, 4)) sb.AppendLine(l);
        if (hp.Length > 0) sb.AppendLine($"    HP: {Q(hp)}");
        if (attack.Length > 0) foreach (var l in Phase2.EmitFieldLines("Attack", attack, 4)) sb.AppendLine(l);
        if (trait.Length > 0) foreach (var l in Phase2.EmitFieldLines("Traits", trait, 4)) sb.AppendLine(l);
        if (note.Length > 0) foreach (var l in Phase2.EmitFieldLines("Note", note, 4)) sb.AppendLine(l);
        sb.AppendLine();
    }

    static void Stat(StringBuilder sb, string key, string v)
    {
        if (v.Length == 0) return;
        string k = key.Contains(' ') ? $"\"{key}\"" : key;
        sb.AppendLine($"    {k}: \"{v}\"");
    }

    static string Slug(string s)
    {
        var sb = new StringBuilder(); bool u = false;
        foreach (var ch in s.ToUpperInvariant())
        { if (char.IsLetterOrDigit(ch)) { sb.Append(ch); u = false; } else if (!u) { sb.Append('_'); u = true; } }
        return sb.ToString().Trim('_');
    }
    static string Q(string v) => "\"" + v.Replace("\"", "\\\"") + "\"";
}
