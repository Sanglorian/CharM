using CharM.Engine.Creation;
using CharM.RulesDb.Storage;

namespace CharM.ImportExport.SummaryText.Blocks;

/// <summary>OCB header line. See decompiled <c>Start.cs</c>.</summary>
public sealed class StartBlock : SummaryBlock
{
    public const string Header =
        "====== Created Using Wizards of the Coast D&D Character Builder ======\r\n";

    public override string Write(CharacterSession session, IRulesDatabase database) => Header;

    public override bool TryRead(CharacterSession session, IRulesDatabase database, ref string input)
    {
        if (!input.StartsWith(Header, StringComparison.Ordinal)) return false;
        input = input[Header.Length..];
        return true;
    }
}
