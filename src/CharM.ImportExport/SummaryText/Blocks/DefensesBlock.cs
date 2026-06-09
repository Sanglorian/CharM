using CharM.Engine.Creation;
using CharM.RulesDb.Storage;

namespace CharM.ImportExport.SummaryText.Blocks;

/// <summary>
/// "AC: N Fort: N Reflex: N Will: N\r\n".
/// Defenses are derived from the StatBlock — the OCB reader skips this line
/// (defenses are recomputed from rules), so our reader does the same.
/// See decompiled <c>Defenses.cs</c>.
///
/// CRITICAL: OCB uses single SPACES between values, not commas. Caught from
/// real fixture txt/thetower30.txt.
/// </summary>
internal sealed class DefensesBlock : SummaryBlock
{
    public override string Write(CharacterSession session, IRulesDatabase database)
    {
        var snapshot = session.GetSnapshot() ?? session.GetPartialSnapshot();
        if (snapshot is null) return string.Empty;
        var stats = snapshot.Builder.Stats;

        int ac = stats.TryGetStat("Armor Class")?.ComputeValue(stats) ?? 0;
        int fort = stats.TryGetStat("Fortitude Defense")?.ComputeValue(stats) ?? 0;
        int reflex = stats.TryGetStat("Reflex Defense")?.ComputeValue(stats) ?? 0;
        int will = stats.TryGetStat("Will Defense")?.ComputeValue(stats) ?? 0;

        return $"AC: {ac} Fort: {fort} Reflex: {reflex} Will: {will}{Newline}";
    }

    public override bool TryRead(CharacterSession session, IRulesDatabase database, ref string input)
    {
        if (!input.StartsWith("AC: ", StringComparison.Ordinal)) return false;
        SkipLine(ref input);
        return true;
    }
}
