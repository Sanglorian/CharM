using CharM.Engine.CharacterModel;
using CharM.Engine.Creation;
using CharM.RulesDb.Storage;

namespace CharM.ImportExport.SummaryText.Blocks;

/// <summary>
/// "FINAL ABILITY SCORES\r\nStr 29, Con 18, ..., Cha 12.\r\n" or
/// "STARTING ABILITY SCORES\r\n...".
/// See decompiled <c>AbilityBlock.cs</c>.
/// </summary>
internal sealed class AbilityBlock : SummaryBlock
{
    public const string FinalHeader = "FINAL ABILITY SCORES";
    public const string StartingHeader = "STARTING ABILITY SCORES";

    private readonly bool _showFinal;

    public AbilityBlock(bool showFinal) { _showFinal = showFinal; }

    private static readonly (Ability Ability, string Abbrev)[] OrderedAbilities =
    {
        (Ability.Strength, "Str"),
        (Ability.Constitution, "Con"),
        (Ability.Dexterity, "Dex"),
        (Ability.Intelligence, "Int"),
        (Ability.Wisdom, "Wis"),
        (Ability.Charisma, "Cha"),
    };

    public override string Write(CharacterSession session, IRulesDatabase database)
    {
        string header = _showFinal ? FinalHeader : StartingHeader;
        var sb = new System.Text.StringBuilder();
        sb.Append(header).Append(Newline);

        var snapshot = session.GetSnapshot() ?? session.GetPartialSnapshot();

        for (int i = 0; i < OrderedAbilities.Length; i++)
        {
            var (ability, abbrev) = OrderedAbilities[i];
            int value = GetValue(session, snapshot, ability);
            if (i > 0) sb.Append(", ");
            sb.Append(abbrev).Append(' ').Append(value);
        }
        sb.Append('.').Append(Newline);
        return sb.ToString();
    }

    private int GetValue(CharacterSession session, CharacterSnapshot? snapshot, Ability ability)
    {
        if (_showFinal && snapshot is not null)
        {
            string statName = ability switch
            {
                Ability.Strength => "Strength",
                Ability.Constitution => "Constitution",
                Ability.Dexterity => "Dexterity",
                Ability.Intelligence => "Intelligence",
                Ability.Wisdom => "Wisdom",
                Ability.Charisma => "Charisma",
                _ => throw new ArgumentOutOfRangeException(),
            };
            var stat = snapshot.Builder.Stats.TryGetStat(statName);
            if (stat is not null) return stat.ComputeValue(snapshot.Builder.Stats);
        }
        return session.AbilityScores?[ability] ?? 10;
    }

    public override bool TryRead(CharacterSession session, IRulesDatabase database, ref string input)
    {
        string header = _showFinal ? FinalHeader : StartingHeader;
        if (!input.StartsWith(header + Newline, StringComparison.Ordinal)) return false;

        int afterHeader = header.Length + Newline.Length;
        int nl = input.IndexOf(Newline, afterHeader, StringComparison.Ordinal);
        if (nl == -1) return false;

        string line = input[afterHeader..nl].TrimEnd('.').Trim();

        if (_showFinal)
        {
            // Final scores are derived — skip without reading. OCB does the same.
            input = input[(nl + Newline.Length)..];
            return true;
        }

        // Starting scores — drive AbilityScoreSet.
        var scores = new Dictionary<Ability, int>();
        foreach (var tok in line.Split(','))
        {
            string entry = tok.Trim();
            int sp = entry.IndexOf(' ');
            if (sp < 1) return false;
            string abbrev = entry[..sp];
            if (!int.TryParse(entry.AsSpan(sp + 1), out int v)) return false;
            var ab = OrderedAbilities.FirstOrDefault(t => t.Abbrev == abbrev);
            if (ab.Abbrev is null) return false;
            scores[ab.Ability] = v;
        }
        if (scores.Count != 6) return false;

        var scoreSet = new AbilityScoreSet
        {
            [Ability.Strength] = scores[Ability.Strength],
            [Ability.Constitution] = scores[Ability.Constitution],
            [Ability.Dexterity] = scores[Ability.Dexterity],
            [Ability.Intelligence] = scores[Ability.Intelligence],
            [Ability.Wisdom] = scores[Ability.Wisdom],
            [Ability.Charisma] = scores[Ability.Charisma],
        };
        session.SetAbilityScores(scoreSet);

        input = input[(nl + Newline.Length)..];
        return true;
    }
}
