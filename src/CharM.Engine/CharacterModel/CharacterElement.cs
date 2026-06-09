using CharM.Engine.Rules;

namespace CharM.Engine.CharacterModel;

/// <summary>
/// A character's instance of a RulesElement. Forms a parent-child tree.
/// When an element is granted or selected, a CharacterElement is created as a 
/// child of the granting element. Each tracks its level, active state, and children.
/// </summary>
public sealed class CharacterElement
{
    public RulesElement? RulesElement { get; set; }
    public CharacterElement? Parent { get; set; }
    public List<CharacterElement> Children { get; } = [];

    /// <summary>Level at which this element was gained.</summary>
    public int Level { get; set; } = 1;

    /// <summary>Whether this element is currently active (met conditions).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>For select: the choice slot this element represents.</summary>
    public ChoiceSlot? Choice { get; set; }

    /// <summary>
    /// For elements that came from a user choice in a flat-import path
    /// (e.g. <see cref="Orchestration.CharacterBuilder"/> rebuilding from a
    /// .dnd4e file), the InternalId of the directive owner whose select
    /// produced this choice. Used to resolve modify-without-name implicit
    /// targets in phase 2 even when the slot itself isn't filled in the tree.
    /// </summary>
    public string? SlotOwnerInternalId { get; set; }

    /// <summary>
    /// When this element is the NEW side of a retraining swap, the
    /// charelem of the element it replaces. The writer emits this as the
    /// <c>replaces="..."</c> attribute on the corresponding
    /// <c>&lt;RulesElement&gt;</c> when the session has no source-file
    /// metadata to fall back to (UI-built characters where the user
    /// added the swap via <c>AddReplacement</c>). Source-imported
    /// characters still get their <c>replaces=</c> from
    /// <c>SourceMetadata</c> first; this is the fallback.
    /// </summary>
    public string? ReplacesCharelem { get; set; }

    /// <summary>Add a child element linked to the given RulesElement.</summary>
    public CharacterElement AddChild(RulesElement? element, int level = 1)
    {
        var child = new CharacterElement
        {
            RulesElement = element,
            Parent = this,
            Level = level,
        };
        Children.Add(child);
        return child;
    }

    /// <summary>Remove a child whose RulesElement matches the given internal ID.</summary>
    public bool RemoveChild(string internalId)
    {
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            if (string.Equals(Children[i].RulesElement?.InternalId, internalId, StringComparison.OrdinalIgnoreCase))
            {
                Children[i].Parent = null;
                Children.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    /// <summary>Find a descendant by RulesElement internal ID (depth-first).</summary>
    public CharacterElement? FindDescendant(string internalId)
    {
        foreach (var child in Children)
        {
            if (string.Equals(child.RulesElement?.InternalId, internalId, StringComparison.OrdinalIgnoreCase))
                return child;

            var found = child.FindDescendant(internalId);
            if (found is not null)
                return found;
        }
        return null;
    }

    /// <summary>Find a descendant by RulesElement name and type.</summary>
    public CharacterElement? FindDescendantByNameAndType(string name, string type)
    {
        foreach (var child in Children)
        {
            if (child.RulesElement is { } re
                && string.Equals(re.Name, name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(re.Type, type, StringComparison.OrdinalIgnoreCase))
                return child;

            var found = child.FindDescendantByNameAndType(name, type);
            if (found is not null)
                return found;
        }
        return null;
    }

    /// <summary>Yield all descendants (depth-first, flattened).</summary>
    public IEnumerable<CharacterElement> GetAllDescendants()
    {
        foreach (var child in Children)
        {
            yield return child;
            foreach (var desc in child.GetAllDescendants())
                yield return desc;
        }
    }

    public override string ToString()
        => RulesElement is not null
            ? $"CE: {RulesElement.Name} ({RulesElement.InternalId}) L{Level}"
            : $"CE: (no element) L{Level}";
}
