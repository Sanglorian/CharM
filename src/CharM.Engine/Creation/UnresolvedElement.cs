using System.Xml.Linq;

namespace CharM.Engine.Creation;

/// <summary>
/// A <c>&lt;RulesElement&gt;</c> from an imported .dnd4e file whose
/// <c>internal-id</c> doesn't resolve against the local rules database.
///
/// <para>The character file may have been built with a different rules-content
/// set (CBLoader users routinely add .part files that extend the engine's
/// element catalog) or with houseruled elements the local DB never knew about
/// (custom alignments like Lawful Evil, third-party feats, etc.). Either way,
/// silently dropping these elements on import → export would lose data, so
/// the importer captures them here for verbatim passthrough.</para>
///
/// <para>The exporter re-emits each unresolved element at the same nesting
/// position it occupied in the source file (tally row goes back into the
/// flat tally; level-tree row goes back nested under its parent, replacing
/// the empty-slot <c>deadbeef</c> placeholder). The engine itself doesn't
/// evaluate any directives on these elements — they have no <see cref="Rules.RulesElement.Rules"/>
/// to fire — so they contribute zero stats and zero powers.</para>
///
/// <para>UI consumers can surface a list of these on the Details page,
/// optionally appending a ⚠ to the element's natural rendering position
/// so the user knows that part of the character won't behave correctly
/// without the missing content.</para>
/// </summary>
public sealed record UnresolvedElement(
    string InternalId,
    string Name,
    string Type,
    UnresolvedLocation Location,
    string? Legality = null,
    string? ParentInternalId = null,
    int? AtLevel = null,
    XElement? SourceXml = null);

/// <summary>
/// Where an <see cref="UnresolvedElement"/> appeared in the source file.
/// Drives both export re-emission and UI placement.
/// </summary>
public enum UnresolvedLocation
{
    /// <summary>Flat <c>&lt;RulesElementTally&gt;</c> row only.</summary>
    Tally,

    /// <summary>
    /// Nested inside the <c>&lt;Level&gt;</c> subtree under its
    /// <see cref="UnresolvedElement.ParentInternalId"/> at level
    /// <see cref="UnresolvedElement.AtLevel"/>. Almost all houseruled
    /// content lands here too (and is mirrored in the tally).
    /// </summary>
    LevelTree,

    /// <summary>Inside a UserEdit (Form A) houserule pick subtree.</summary>
    UserEdit,

    /// <summary>Inside an equipped loot composite.</summary>
    Loot,

    /// <summary>Inside an OCB "Grabbag" / D20CampaignSetting grant block.</summary>
    Grabbag,

    /// <summary>Target of a retrain (<c>replaces=</c> swap-new) that didn't resolve.</summary>
    RetrainTarget,

    /// <summary>A pick whose target slot never materialized in the wizard tree.</summary>
    DeferredPick,
}
