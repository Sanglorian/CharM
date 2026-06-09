using CharM.Engine.Creation;
using CharM.RulesDb.Storage;

namespace CharM.ImportExport.SummaryText.Blocks;

/// <summary>Bare blank line between sections. See decompiled <c>BlankLine.cs</c>.</summary>
internal sealed class BlankLineBlock : SummaryBlock
{
    public override string Write(CharacterSession session, IRulesDatabase database) => Newline;

    public override bool TryRead(CharacterSession session, IRulesDatabase database, ref string input)
    {
        if (!input.StartsWith(Newline, StringComparison.Ordinal)) return false;
        input = input[Newline.Length..];
        return true;
    }
}
