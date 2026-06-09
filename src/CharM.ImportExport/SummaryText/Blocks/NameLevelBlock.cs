using CharM.Engine.Creation;
using CharM.RulesDb.Storage;

namespace CharM.ImportExport.SummaryText.Blocks;

/// <summary>
/// "&lt;Name&gt;, level &lt;N&gt;\r\n". See decompiled <c>NameLevel.cs</c>.
/// Name is optional; the comma separator collapses if absent.
/// </summary>
internal sealed class NameLevelBlock : SummaryBlock
{
    public override string Write(CharacterSession session, IRulesDatabase database)
    {
        // OCB reads the character name from text-string "Name" (decompile
        // string-table opcode 23930). Our CharacterSession exposes Name
        // directly; TextStrings is fallback for round-tripped fluff that
        // didn't make the move to a typed property.
        string name = session.Name;
        if (string.IsNullOrWhiteSpace(name) || name == "New Character")
        {
            if (session.TextStrings.TryGetValue("Name", out var ts) && !string.IsNullOrWhiteSpace(ts))
                name = ts;
            else
                name = string.Empty;
        }

        string prefix = name.Length > 0 ? name + ", " : string.Empty;
        return $"{prefix}level {session.Level}{Newline}";
    }

    public override bool TryRead(CharacterSession session, IRulesDatabase database, ref string input)
    {
        int nl = input.IndexOf(Newline, StringComparison.Ordinal);
        if (nl == -1) return false;

        string line = input[..nl];

        // Try "<Name>, level N" first; fall back to bare "level N".
        string? name = null;
        string levelPart = line;
        int comma = line.LastIndexOf(", ", StringComparison.Ordinal);
        if (comma > 0)
        {
            name = line[..comma];
            levelPart = line[(comma + 2)..];
        }

        if (!levelPart.StartsWith("level ", StringComparison.OrdinalIgnoreCase)
            && !levelPart.StartsWith("Level ", StringComparison.Ordinal))
            return false;

        if (!int.TryParse(levelPart.AsSpan("level ".Length), out int level) || level < 1)
            return false;

        if (!string.IsNullOrWhiteSpace(name))
            session.Name = name;

        if (level > session.Level)
            session.SetLevel(level);

        input = input[(nl + Newline.Length)..];
        return true;
    }
}
