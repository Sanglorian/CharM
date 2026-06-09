using CharM.Engine.Creation;
using CharM.RulesDb.Storage;

namespace CharM.ImportExport.SummaryText.Blocks;

/// <summary>
/// "TRAINED SKILLS\r\n&lt;Skill&gt; +N, ...\r\n" or
/// "UNTRAINED SKILLS\r\n&lt;Skill&gt; +N, ...\r\n".
/// See decompiled <c>SkillSummary.cs</c>.
/// </summary>
internal sealed class SkillSummaryBlock : SummaryBlock
{
    public const string TrainedHeader = "TRAINED SKILLS";
    public const string UntrainedHeader = "UNTRAINED SKILLS";

    private readonly bool _trained;

    public SkillSummaryBlock(bool trained) { _trained = trained; }

    public override string Write(CharacterSession session, IRulesDatabase database)
    {
        var snapshot = session.GetSnapshot() ?? session.GetPartialSnapshot();
        if (snapshot is null) return string.Empty;

        var allSkills = database.FindByType("Skill", includeRules: false)
            .Select(s => s.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        var trainedNames = new HashSet<string>(
            session.GetAllElementsOfType("Skill Training").Select(e => e.Name),
            StringComparer.OrdinalIgnoreCase);

        var stats = snapshot.Builder.Stats;

        IEnumerable<string> orderedSkills;
        if (_trained)
        {
            // OCB iterates Skill Training char-elements in tree order (auto
            // grants from race / theme come first, then user-picked Skill
            // Training). GetAllElementsOfType walks the tree in build order.
            orderedSkills = session.GetAllElementsOfType("Skill Training")
                .Select(e => e.Name)
                .Where(n => trainedNames.Contains(n))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            // OCB UNTRAINED order is alphabetical with one quirk: Athletics
            // appears LAST. Confirmed across all 3 OCB fixtures (deadfeather,
            // tower, themess). Hypothesis: Athletics was a late add in the
            // WotC StockTypeSkill table; OCB's IterateRulesElements walks in
            // that table order. We mirror by sorting then forcing Athletics
            // to the tail.
            orderedSkills = allSkills
                .Where(s => !trainedNames.Contains(s))
                .OrderBy(s => s, StringComparer.Ordinal)
                .OrderBy(s => s.Equals("Athletics", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
        }

        var entries = new List<(string Name, int Mod)>();
        foreach (var skill in orderedSkills)
        {
            var stat = stats.TryGetStat(skill);
            if (stat is null) continue;
            int mod = stat.ComputeValue(stats);
            entries.Add((skill, mod));
        }

        if (entries.Count == 0) return $"{(_trained ? TrainedHeader : UntrainedHeader)}{Newline}{Newline}";

        string header = _trained ? TrainedHeader : UntrainedHeader;
        var sb = new System.Text.StringBuilder();
        sb.Append(header).Append(Newline);
        for (int i = 0; i < entries.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(entries[i].Name).Append(' ').Append(entries[i].Mod >= 0 ? "+" : "").Append(entries[i].Mod);
        }
        sb.Append(Newline);
        return sb.ToString();
    }

    public override bool TryRead(CharacterSession session, IRulesDatabase database, ref string input)
    {
        string header = _trained ? TrainedHeader : UntrainedHeader;
        if (!input.StartsWith(header + Newline, StringComparison.Ordinal)) return false;
        SkipLine(ref input);

        // Read the data line.
        int nl = input.IndexOf(Newline, StringComparison.Ordinal);
        if (nl == -1) return true;

        if (_trained)
        {
            // Parse trained-skill picks and replay each Skill Training choice.
            string line = input[..nl];
            foreach (var raw in line.Split(','))
            {
                string entry = raw.Trim();
                int sp = entry.IndexOf(' ');
                string skillName = sp > 0 ? entry[..sp] : entry;
                var skillTraining = database.FindByNameAndType(skillName, "Skill Training");
                if (skillTraining is not null)
                    SessionReplayHelpers.PlacePositionally(session, skillTraining);
            }
        }

        input = input[(nl + Newline.Length)..];
        return true;
    }
}
