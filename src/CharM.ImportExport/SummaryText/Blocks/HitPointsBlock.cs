using CharM.Engine.Creation;
using CharM.RulesDb.Storage;

namespace CharM.ImportExport.SummaryText.Blocks;

/// <summary>
/// "HP: N Surges: N Surge Value: N\r\n".
/// HP / Surges are derived — OCB reader skips. See decompiled <c>HitPoints.cs</c>.
///
/// CRITICAL: 3 fields, no "Bloodied", no "Surges/Day". Caught from real
/// fixture txt/thetower30.txt (`HP: 207 Surges: 13 Surge Value: 55`).
/// </summary>
internal sealed class HitPointsBlock : SummaryBlock
{
    public override string Write(CharacterSession session, IRulesDatabase database)
    {
        var snapshot = session.GetSnapshot() ?? session.GetPartialSnapshot();
        if (snapshot is null) return string.Empty;
        var stats = snapshot.Builder.Stats;

        int hp = stats.TryGetStat("Hit Points")?.ComputeValue(stats) ?? 0;
        int surges = stats.TryGetStat("Healing Surges")?.ComputeValue(stats) ?? 0;

        // OCB formula: SurgeValue = HP/4 + (modifier stat). Empirically the
        // modifier stat is Con bonus for most characters, but PCs with surge
        // value items carry a separate "Healing Surge Value" contribution.
        // See decompiled HitPoints.cs line 42: num = charStat / 4 + GetCharStat("...");
        int surgeValueBonus = stats.TryGetStat("Healing Surge Value")?.ComputeValue(stats) ?? 0;
        int surgeValue = (hp / 4) + surgeValueBonus;

        return $"HP: {hp} Surges: {surges} Surge Value: {surgeValue}{Newline}";
    }

    public override bool TryRead(CharacterSession session, IRulesDatabase database, ref string input)
    {
        if (!input.StartsWith("HP: ", StringComparison.Ordinal)) return false;
        SkipLine(ref input);
        return true;
    }
}
