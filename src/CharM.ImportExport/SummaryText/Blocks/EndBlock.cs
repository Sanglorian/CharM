using CharM.Engine.Creation;
using CharM.RulesDb.Storage;

namespace CharM.ImportExport.SummaryText.Blocks;

/// <summary>OCB footer line. See decompiled <c>End.cs</c>.</summary>
public sealed class EndBlock : SummaryBlock
{
    public const string Footer =
        "====== Copy to Clipboard and Press the Import Button on the Summary Tab ======\r\n";

    public override string Write(CharacterSession session, IRulesDatabase database) => Footer;

    public override bool TryRead(CharacterSession session, IRulesDatabase database, ref string input)
    {
        if (!input.StartsWith(Footer, StringComparison.Ordinal)) return false;
        input = input[Footer.Length..];
        return true;
    }
}
