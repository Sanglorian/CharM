using CharM.Engine.Creation;
using CharM.ImportExport.SummaryText.Blocks;
using CharM.RulesDb.Storage;

namespace CharM.ImportExport.SummaryText;

/// <summary>
/// Holds the ordered list of <see cref="SummaryBlock"/> instances that
/// together form the OCB SummaryText format. Drives both <c>Write</c>
/// (concatenate every block's output) and <c>Read</c> (try every block
/// against the head of the input, rotate on no-match).
/// </summary>
internal static class SummaryTextDriver
{
    /// <summary>OCB block ordering — matches the constructor in
    /// <c>decompiled/CharacterBuilder/Character_Builder/SummaryText.cs</c>.</summary>
    public static IReadOnlyList<SummaryBlock> Blocks { get; } = new SummaryBlock[]
    {
        new StartBlock(),
        new NameLevelBlock(),
        new RaceClassBlock(),
        new BuildSummaryBlock(),
        new ClassChoicesBlock(),
        new BackgroundBlock(),
        new BlankLineBlock(),
        new AbilityBlock(showFinal: true),
        new BlankLineBlock(),
        new AbilityBlock(showFinal: false),
        new BlankLineBlock(),
        new BlankLineBlock(),
        new DefensesBlock(),
        new HitPointsBlock(),
        new BlankLineBlock(),
        new SkillSummaryBlock(trained: true),
        new BlankLineBlock(),
        new SkillSummaryBlock(trained: false),
        new BlankLineBlock(),
        new FeatSummaryBlock(),
        new BlankLineBlock(),
        new PowerSummaryBlock(),
        new BlankLineBlock(),
        new ItemSummaryBlock(),
        new EndBlock(),
    };

    public static string Write(CharacterSession session, IRulesDatabase database)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var block in Blocks)
            sb.Append(block.Write(session, database));
        return sb.ToString();
    }

    /// <summary>
    /// Replay <paramref name="input"/> into <paramref name="session"/>.
    /// Mirrors OCB's <c>SummaryText.Read</c> rotation loop: try every block
    /// against the head; on no-match rotate the first line to the tail; on a
    /// full cycle without progress, give up and surface the unread tail as
    /// <see cref="UnconsumedLines"/>.
    /// </summary>
    public static IReadOnlyList<string> Replay(CharacterSession session, IRulesDatabase database, string input)
    {
        // Ensure trailing newline so the final block always has a complete line.
        if (!input.EndsWith(SummaryBlock.Newline, StringComparison.Ordinal))
            input += SummaryBlock.Newline;

        int safetyValve = 0;
        int initialLen = input.Length;
        string cycleAnchor = string.Empty;

        while (input.Length > 0)
        {
            if (++safetyValve > initialLen * 4)
                break; // pathological input — bail rather than infinite-loop

            bool matched = false;
            foreach (var block in Blocks)
            {
                if (block.TryRead(session, database, ref input))
                {
                    matched = true;
                    cycleAnchor = string.Empty;
                    break;
                }
            }

            if (matched) continue;

            // No block recognized the head. Rotate the head line to the tail
            // and try again. If we cycle back to a line we already failed to
            // parse, treat the remaining tail as unparseable.
            int nl = input.IndexOf(SummaryBlock.Newline, StringComparison.Ordinal);
            if (nl == -1) break;

            string headLine = input[..(nl + SummaryBlock.Newline.Length)];
            if (cycleAnchor.Length == 0)
                cycleAnchor = headLine;
            else if (string.Equals(cycleAnchor, headLine, StringComparison.Ordinal))
                break;

            input = input[(nl + SummaryBlock.Newline.Length)..] + headLine;
        }

        if (input.Length == 0) return Array.Empty<string>();

        return input
            .Split(new[] { SummaryBlock.Newline }, StringSplitOptions.RemoveEmptyEntries)
            .ToArray();
    }
}
