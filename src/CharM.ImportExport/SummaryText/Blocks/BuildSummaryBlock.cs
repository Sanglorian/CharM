using CharM.Engine.Creation;
using CharM.RulesDb.Storage;

namespace CharM.ImportExport.SummaryText.Blocks;

/// <summary>"Build: &lt;ClassBuild&gt;\r\n" or empty when no build picked.
/// See decompiled <c>BuildSummary.cs</c>.</summary>
internal sealed class BuildSummaryBlock : SummaryBlock
{
    public override string Write(CharacterSession session, IRulesDatabase database)
    {
        // DB stores class builds under type "Build". (OCB's StockTypeClassBuild
        // resolves to the same set.)
        var build = session.GetSelectedElement("Build")?.Name
                    ?? session.GetSelectedElement("Class Build")?.Name;
        if (string.IsNullOrWhiteSpace(build)) return string.Empty;
        return $"Build: {build}{Newline}";
    }

    public override bool TryRead(CharacterSession session, IRulesDatabase database, ref string input)
    {
        const string prefix = "Build: ";
        if (!input.StartsWith(prefix, StringComparison.Ordinal)) return false;
        int nl = input.IndexOf(Newline, StringComparison.Ordinal);
        if (nl == -1) return false;

        string buildName = input[prefix.Length..nl].Trim();
        var element = database.FindByNameAndType(buildName, "Build")
                   ?? database.FindByNameAndType(buildName, "Class Build");
        if (element is null) return false;

        SessionReplayHelpers.PlacePositionally(session, element);
        input = input[(nl + Newline.Length)..];
        return true;
    }
}
