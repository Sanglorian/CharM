namespace CharM.Engine.Evaluation;

/// <summary>
/// Holds all computed stats for a character. Stats are auto-created on first access.
/// Each stat tracks its contributions and cached computed value.
/// </summary>
public sealed class StatBlock
{
    private readonly Dictionary<string, Stat> _stats = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Get an existing stat or create a new one. Resolves aliases transparently.
    /// </summary>
    public Stat GetOrCreateStat(string name)
    {
        string normalized = NormalizeCompoundStatName(name);
        string resolved = ResolveAlias(normalized);

        if (!_stats.TryGetValue(resolved, out var stat))
        {
            stat = new Stat(resolved);
            _stats[resolved] = stat;
        }

        return stat;
    }

    /// <summary>
    /// Try to get an existing stat by name (resolves aliases). Returns null if the stat doesn't exist.
    /// </summary>
    public Stat? TryGetStat(string name)
    {
        string normalized = NormalizeCompoundStatName(name);
        string resolved = ResolveAlias(normalized);
        return _stats.TryGetValue(resolved, out var stat) ? stat : null;
    }

    /// <summary>
    /// Normalize comma-joined compound stat names (e.g. "implement,staff implement:attack") so
    /// that tokens which contain another token as a substring sort BEFORE the less-specific
    /// token. This mirrors OCB's stat-alias emission ordering — "staff implement,implement:attack",
    /// "holy symbol implement,implement:attack", etc. Disjoint tokens preserve their input order
    /// (stable). Names without a comma in the prefix portion are returned unchanged.
    /// </summary>
    public static string NormalizeCompoundStatName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.IndexOf(',') < 0)
            return name;

        int colon = name.LastIndexOf(':');
        string prefix = colon >= 0 ? name[..colon] : name;
        string suffix = colon >= 0 ? name[colon..] : string.Empty;

        if (prefix.IndexOf(',') < 0)
            return name;

        var tokens = prefix.Split(',');
        // Stable sort: bubble-sort with the partial-order comparator (n is tiny, almost always 2).
        for (int i = 1; i < tokens.Length; i++)
        {
            for (int j = i; j > 0; j--)
            {
                string a = tokens[j - 1], b = tokens[j];
                // If b strictly contains a (and they differ), b is more specific → move b before a.
                bool bContainsA = !a.Equals(b, StringComparison.OrdinalIgnoreCase)
                    && b.Contains(a, StringComparison.OrdinalIgnoreCase);
                if (bContainsA)
                {
                    tokens[j - 1] = b;
                    tokens[j] = a;
                }
                else
                {
                    break;
                }
            }
        }
        return string.Concat(string.Join(',', tokens), suffix);
    }

    /// <summary>
    /// Create an alias so that looking up <paramref name="alias"/> resolves to <paramref name="primaryName"/>.
    /// </summary>
    public void AddAlias(string primaryName, string alias)
    {
        // Resolve the primary in case it's itself an alias
        string resolved = ResolveAlias(primaryName);
        _aliases[alias] = resolved;

        // If there's an existing stat under the alias name, merge it into the primary
        if (_stats.Remove(alias, out var existingAliasStat))
        {
            var primary = GetOrCreateStat(resolved);
            foreach (var c in existingAliasStat.Contributions)
                primary.AddContribution(c);
        }
    }

    /// <summary>
    /// Reset all stats for re-evaluation.
    /// </summary>
    public void Clear()
    {
        _stats.Clear();
        _aliases.Clear();
        TrainedWeapons.Clear();
        TrainedImplements.Clear();
        ChosenAbilities.Clear();
        KeyAbilitySwaps.Clear();
        ClassEquivalents.Clear();
    }

    /// <summary>
    /// Get all stat names that have been created (explicitly or via lazy reference resolution).
    /// </summary>
    public IEnumerable<string> AllStatNames => _stats.Keys;

    /// <summary>
    /// Names of weapons the character is trained with (extracted from active
    /// "Weapon Proficiency (X)" Proficiency elements). Consumed by
    /// PowerStatCalculator to gate the weapon's "Proficiency Bonus" field —
    /// untrained wielders should not get it credited to attack rolls.
    /// </summary>
    public HashSet<string> TrainedWeapons { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Names of implements the character is trained with (extracted from active
    /// "Implement Proficiency (X)" Proficiency elements). Reserved for future
    /// implement-bonus gating; currently informational.
    /// </summary>
    public HashSet<string> TrainedImplements { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Ability names selected by the character via active "Ability Choice"
    /// category elements (e.g., the warlock <c>Eldritch Blast Charisma</c> /
    /// <c>Eldritch Blast Constitution</c> pact-ability selector). Powers whose
    /// damage / attack text reads "X or Y modifier" — a player choice in 4e —
    /// resolve to whichever candidate ability is in this set. Falls back to
    /// "highest modifier" only when no candidate matches a chosen ability.
    /// Populated by <c>CharacterBuilder.IndexAbilityChoices</c> at end of build.
    /// </summary>
    public HashSet<string> ChosenAbilities { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Ability names that may be SUBSTITUTED in for a power's printed key
    /// ability when they are higher — the Orcus "use your class key ability
    /// instead" rule. Populated from active "Key Ability Swap" category
    /// elements (name ends in the ability, e.g. "Priest Key Wisdom"). Unlike
    /// <see cref="ChosenAbilities"/> (which forces a player's pick), these are
    /// ADDED to a power's candidate abilities and the highest is taken, so a
    /// character never does worse than the printed ability. Applied only to
    /// powers tagged with the "ability-swap" category (a class-discipline
    /// power), so WotC content — which has neither the elements nor the tag —
    /// is unaffected. See docs/orcus-mapping.md.
    /// </summary>
    public HashSet<string> KeyAbilitySwaps { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// InternalIds of every class the character has, has via a Hybrid Class,
    /// or counts as via an INHERENT CountsAsClass element (granted by a
    /// Class / Hybrid Class — NOT by a multiclass feat). Lets callers answer
    /// "is this power one of mine for attack-ability purposes?"
    /// Populated by <c>CharacterBuilder.IndexClassEquivalents</c> at end of build from
    /// <see cref="CharM.Engine.Selection.SelectVariables.GetActiveClassIds"/>.
    /// </summary>
    public HashSet<string> ClassEquivalents { get; } = new(StringComparer.OrdinalIgnoreCase);

    private string ResolveAlias(string name)
    {
        // Follow alias chain (defensive against multi-level aliases)
        int depth = 0;
        while (_aliases.TryGetValue(name, out var target) && depth < 10)
        {
            name = target;
            depth++;
        }
        return name;
    }
}
