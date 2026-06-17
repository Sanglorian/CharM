using CharM.Engine.CharacterModel;

namespace CharM.Engine.Selection;

/// <summary>
/// Extracts variable substitutions from the character element tree
/// for use in category filter expressions.
/// </summary>
public static class SelectVariables
{
    /// <summary>
    /// Resolve standard variables from the character's element tree.
    /// </summary>
    /// <param name="tree">Character element tree to scan.</param>
    /// <param name="characterLevel">Current character level (for $$LEVEL substitution).</param>
    /// <returns>Dictionary mapping variable names (e.g., "$$CLASS") to values.</returns>
    /// <remarks>
    /// Variable semantics:
    /// <list type="bullet">
    /// <item><c>$$CLASS</c>: primary <c>Class</c> + <c>Hybrid Class</c> InternalIds the character
    /// actually has. Does <b>not</b> include CountsAsClass — those are for prereq matching only,
    /// not power-slot expansion. Without this distinction, multiclass feats would unlock the
    /// entire power tree of the multiclassed class.</item>
    /// <item><c>$$MULTICLASS</c>: InternalIds of classes the character only <i>counts as</i>
    /// (CountsAsClass <c>_SupportsID</c> values). Used by power-swap multiclass feats and the
    /// Paragon Multiclassing path.</item>
    /// <item><c>$$CLASS_OR_MULTICLASS</c>: union of the two.</item>
    /// <item><c>$$NOT_CLASS</c>: same value as <c>$$CLASS</c>; <see cref="CategoryMatcher"/>
    /// inverts the match (used by Dilettante / Religious Dabbler / Versatile Channeler).</item>
    /// <item><c>$$HYBRID1</c>, <c>$$HYBRID2</c>: individual hybrid class IDs.
    /// <c>$$HYBRID</c>: union of both, for the bare-<c>$$HYBRID</c> uses in the data.</item>
    /// <item><c>$$LEVEL</c>: numeric character level. <see cref="CategoryMatcher"/> treats this
    /// as an element-level cap rather than substituting in place.</item>
    /// </list>
    /// </remarks>
    public static Dictionary<string, string> Resolve(CharacterElementTree tree, int? characterLevel = null)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        int hybridCount = 0;
        var primaryClassIds = new List<string>();
        var hybridIds = new List<string>();
        var inherentCountsAsIds = new List<string>();
        var multiclassIds = new List<string>();

        foreach (var node in tree.Root.GetAllDescendants())
        {
            if (!node.IsActive || node.RulesElement is not { } element)
                continue;

            if (string.Equals(element.Type, "Class", StringComparison.OrdinalIgnoreCase))
            {
                primaryClassIds.Add(element.InternalId);
                continue;
            }

            if (string.Equals(element.Type, "Hybrid Class", StringComparison.OrdinalIgnoreCase))
            {
                hybridCount++;
                hybridIds.Add(element.InternalId);
                if (hybridCount == 1)
                    variables["$$HYBRID1"] = element.InternalId;
                else if (hybridCount == 2)
                    variables["$$HYBRID2"] = element.InternalId;
                continue;
            }

            // Classify CountsAsClass by who granted it.
            // - Granted by a Class / Hybrid Class element => inherent class identity
            //   (e.g. Bladesinger inherently CountsAsClass Wizard to share Wizard's
            //   power-select infrastructure). Fold into $$CLASS so power slots
            //   filtered by $$CLASS still match the parent-class powers.
            // - Granted by a Feat (e.g. Arcane Initiate) => multiclass dabbling;
            //   feeds $$MULTICLASS only so power slots aren't widened.
            if (string.Equals(element.Type, "CountsAsClass", StringComparison.OrdinalIgnoreCase)
                && element.Fields.TryGetValue("_SupportsID", out var supportsId)
                && !string.IsNullOrEmpty(supportsId))
            {
                var origin = ClassifyCountsAsClassOrigin(node);
                if (origin == CountsAsOrigin.InherentClass)
                    inherentCountsAsIds.Add(supportsId);
                else
                    multiclassIds.Add(supportsId);
            }
        }

        primaryClassIds.AddRange(inherentCountsAsIds);

        var classMembers = new List<string>(primaryClassIds.Count + hybridIds.Count);
        var classMemberSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddDistinct(classMembers, classMemberSet, primaryClassIds);
        AddDistinct(classMembers, classMemberSet, hybridIds);

        if (classMembers.Count > 0)
        {
            var joined = string.Join("|", classMembers);
            variables["$$CLASS"] = joined;
            // $$NOT_CLASS resolves to the same set; CategoryMatcher inverts the match.
            variables["$$NOT_CLASS"] = joined;
        }

        var distinctMulticlass = new List<string>(multiclassIds.Count);
        var multiclassSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in multiclassIds)
        {
            if (!classMemberSet.Contains(id) && multiclassSet.Add(id))
                distinctMulticlass.Add(id);
        }

        if (distinctMulticlass.Count > 0)
            variables["$$MULTICLASS"] = string.Join("|", distinctMulticlass);

        if (classMembers.Count + distinctMulticlass.Count > 0)
            variables["$$CLASS_OR_MULTICLASS"] = string.Join("|", classMembers.Concat(distinctMulticlass));

        if (hybridIds.Count > 0)
        {
            var distinctHybridIds = new List<string>(hybridIds.Count);
            AddDistinct(distinctHybridIds, new HashSet<string>(StringComparer.OrdinalIgnoreCase), hybridIds);
            variables["$$HYBRID"] = string.Join("|", distinctHybridIds);
        }

        if (characterLevel is int lvl && lvl > 0)
            variables["$$LEVEL"] = lvl.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // $$KITDISC: disciplines the character can select powers from via a kit's
        // "associated discipline" (Orcus). A kit grants a "Discipline Access"
        // element whose `_Discipline` field is the discipline's InternalId; a
        // class power-select category that ORs in $$KITDISC then accepts that
        // discipline's powers as class powers. Absent when no such access exists,
        // in which case the OR alternative simply doesn't match (see CategoryMatcher).
        var kitDisciplineIds = new List<string>();
        var kitDisciplineSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in tree.Root.GetAllDescendants())
        {
            if (!node.IsActive || node.RulesElement is not { } element)
                continue;
            if (!string.Equals(element.Type, "Discipline Access", StringComparison.OrdinalIgnoreCase))
                continue;
            if (element.Fields.TryGetValue("_Discipline", out var disc)
                && !string.IsNullOrWhiteSpace(disc)
                && kitDisciplineSeen.Add(disc))
            {
                kitDisciplineIds.Add(disc.Trim());
            }
        }
        if (kitDisciplineIds.Count > 0)
            variables["$$KITDISC"] = string.Join("|", kitDisciplineIds);

        return variables;
    }

    private static void AddDistinct(List<string> target, HashSet<string> seen, IEnumerable<string> source)
    {
        foreach (var id in source)
        {
            if (seen.Add(id))
                target.Add(id);
        }
    }

    /// <summary>
    /// Returns the union of all class InternalIds the character has, has via
    /// a Hybrid Class, or counts as via an inherent CountsAsClass element
    /// (granted by a Class / Hybrid Class — not by a multiclass feat). For
    /// each <c>CountsAsClass</c> node, the base-class id comes from its
    /// <c>_SupportsID</c> field — that is the same id a power's <c>Class</c>
    /// field would list, so callers can answer "is this power one of mine
    /// for attack-ability override purposes?" via set membership.
    ///
    /// Multiclass-feat-granted CountsAsClass entries are intentionally
    /// excluded: a Monk/Warlock who multiclasses into Wizard via Arcane
    /// Initiate still treats Wizard powers as foreign (their
    /// <c>multiclass:key ability</c> override should fire on Fire Shroud
    /// rather than the power's native Intelligence).
    ///
    /// Mirrors the membership of <c>$$CLASS</c> in <see cref="Resolve"/>.
    /// </summary>
    public static HashSet<string> GetActiveClassIds(CharacterElementTree tree)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in tree.Root.GetAllDescendants())
        {
            if (!node.IsActive || node.RulesElement is not { } element)
                continue;

            if (string.Equals(element.Type, "Class", StringComparison.OrdinalIgnoreCase)
                || string.Equals(element.Type, "Hybrid Class", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(element.InternalId))
                    result.Add(element.InternalId);
                continue;
            }

            if (string.Equals(element.Type, "CountsAsClass", StringComparison.OrdinalIgnoreCase)
                && element.Fields.TryGetValue("_SupportsID", out var supportsId)
                && !string.IsNullOrWhiteSpace(supportsId)
                && ClassifyCountsAsClassOrigin(node) == CountsAsOrigin.InherentClass)
            {
                result.Add(supportsId);
            }
        }

        return result;
    }

    private enum CountsAsOrigin { InherentClass, Multiclass }

    /// <summary>
    /// Walk up the tree from a CountsAsClass node to determine whether it was
    /// granted by a Class / Hybrid Class (inherent identity) or by a Feat /
    /// other source (multiclass dabbling).
    /// </summary>
    private static CountsAsOrigin ClassifyCountsAsClassOrigin(CharacterElement node)
    {
        var cur = node.Parent;
        while (cur is not null)
        {
            if (cur.RulesElement is { } re)
            {
                if (string.Equals(re.Type, "Class", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(re.Type, "Hybrid Class", StringComparison.OrdinalIgnoreCase))
                    return CountsAsOrigin.InherentClass;
                if (string.Equals(re.Type, "Feat", StringComparison.OrdinalIgnoreCase))
                    return CountsAsOrigin.Multiclass;
            }
            cur = cur.Parent;
        }
        // No identifying ancestor found — be conservative and treat as multiclass
        // so we don't accidentally widen $$CLASS for orphaned grants.
        return CountsAsOrigin.Multiclass;
    }
}
