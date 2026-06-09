using CharM.Engine.Creation;
using CharM.RulesDb.Storage;

namespace CharM.ImportExport.SummaryText.Blocks;

/// <summary>
/// "FEATS\r\n&lt;Source&gt;: &lt;Feat&gt;[ (replaces &lt;Old&gt; @ &lt;RetrainAt&gt;)]\r\n...".
/// See decompiled <c>FeatSummary.cs</c>. The source label is "Level N"
/// for normal level grants, "Free Feat" for UserEdit picks.
/// </summary>
internal sealed class FeatSummaryBlock : SummaryBlock
{
    public const string Header = "FEATS";

    public override string Write(CharacterSession session, IRulesDatabase database)
    {
        var feats = session.ChoiceHistory
            .Where(r => string.Equals(r.Element.Type, "Feat", StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.Level)
            .ThenBy(r => r.SequenceNumber)
            .ToList();
        if (feats.Count == 0) return $"{Header}{Newline}{Newline}";

        var sb = new System.Text.StringBuilder();
        sb.Append(Header).Append(Newline);
        foreach (var record in feats)
        {
            string source = ResolveFeatSource(session, record);
            sb.Append(source).Append(": ").Append(record.Element.Name).Append(Newline);
        }
        return sb.ToString();
    }

    private static string ResolveFeatSource(CharacterSession session, ChoiceRecord record)
    {
        if (record.Element.InternalId is { } id && session.UserEditPickIds.Contains(id))
            return "Free Feat";
        return $"Level {record.Level}";
    }

    public override bool TryRead(CharacterSession session, IRulesDatabase database, ref string input)
    {
        if (input.StartsWith(Header + Newline, StringComparison.Ordinal))
        {
            input = input[(Header.Length + Newline.Length)..];
            return true;
        }

        // Feat line: "<Source>: <FeatName>[ (replaces ... @ ...)]\r\n"
        int colon = input.IndexOf(": ", StringComparison.Ordinal);
        int nl = input.IndexOf(Newline, StringComparison.Ordinal);
        if (colon == -1 || nl == -1 || colon > nl) return false;

        string source = input[..colon];
        string rest = input[(colon + 2)..nl];

        // Source label must start with "Level " or be "Free Feat".
        bool isFree = source.Equals("Free Feat", StringComparison.Ordinal);
        if (!isFree && !source.StartsWith("Level ", StringComparison.Ordinal))
            return false;

        RemoveRetraining(ref rest, out _, out _);
        string featName = rest.Trim();

        var feat = database.FindByNameAndType(featName, "Feat");
        if (feat is null) return false;

        SessionReplayHelpers.PlacePositionally(session, feat);
        input = input[(nl + Newline.Length)..];
        return true;
    }
}
