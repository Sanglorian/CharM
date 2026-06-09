using CharM.Engine.CharacterModel;
using CharM.Engine.Evaluation;
using CharM.Engine.Prerequisites;
using CharM.Engine.Rules;

namespace CharM.Engine.Orchestration;

public sealed partial class CharacterBuilder
{
    public int GetStatValue(string name)
    {
        var stat = Stats.TryGetStat(name);
        return stat?.ComputeValue(Stats) ?? 0;
    }

    /// <summary>
    /// Get all computed stat values as a dictionary.
    /// Includes stats explicitly created during build AND stats auto-created
    /// via lazy reference resolution (e.g., skill Misc/Trained zero-value stats).
    /// </summary>
    public Dictionary<string, int> GetAllStatValues()
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Iterate in rounds — ComputeValue can create new stats via lazy reference resolution.
        // Keep going until no new stats appear.
        int prevCount;
        do
        {
            prevCount = result.Count;
            foreach (var name in Stats.AllStatNames.ToList()) // snapshot to avoid mutation during iteration
            {
                if (result.ContainsKey(name))
                    continue;
                var stat = Stats.TryGetStat(name);
                if (stat is not null)
                    result[stat.Name] = stat.ComputeValue(Stats);
            }
        } while (result.Count > prevCount);

        return result;
    }

    /// <summary>
    /// Get all tracked stat names.
    /// </summary>
    public IReadOnlySet<string> KnownStatNames => _knownStatNames;

    private void TrackStat(string name) => _knownStatNames.Add(name);
}
