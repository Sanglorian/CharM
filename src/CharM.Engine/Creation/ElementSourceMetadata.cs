namespace CharM.Engine.Creation;

/// <summary>
/// Pass-through metadata for a single rules element identified by internal-id.
/// Captures the bits the .dnd4e file carries that our domain model doesn't
/// (yet) interpret natively (compendium URL, original CB charelem hash, the
/// retraining <c>replaces</c> hint, and child <c>&lt;specific&gt;</c> values
/// such as the power-card "Short Description" / "Power Usage" / "Action Type"
/// blocks). The exporter consults this dictionary to re-emit attributes and
/// power-card text for elements that came in via import and haven't been
/// touched since, so unmodified characters round-trip cleanly.
/// </summary>
public sealed class ElementSourceMetadata
{
    public string? InternalId { get; init; }
    public string? Url { get; init; }
    public string? Charelem { get; init; }
    public string? Replaces { get; init; }

    /// <summary>
    /// Verbatim <c>legality</c> attribute from the FIRST occurrence in source
    /// (i.e. the flat <c>&lt;RulesElementTally&gt;</c> row, since the file
    /// format puts tally before LevelTree). Captured so the exporter can
    /// re-emit the tally row's exact legality value even when other
    /// occurrences of the same internal-id are tagged differently — e.g. a
    /// retrained power's level-tree replacement node carries
    /// <c>legality="houserule"</c> while the flat tally row stays
    /// <c>legality="rules-legal"</c>. Without this we'd promote the tally
    /// row to houserule on round-trip because the file-wide houserule scan
    /// would include the level-tree node.
    /// </summary>
    public string? Legality { get; init; }

    /// <summary>
    /// Name attribute the source file used for this <c>&lt;RulesElement&gt;</c>.
    /// Preserved verbatim so the exporter can re-emit the same string the
    /// file originally carried, even when the local rules database has since
    /// renamed the element (e.g. <c>"Sensate"</c> → <c>"The Society of
    /// Sensation"</c>, or <c>"Corellon"</c> disambiguated to
    /// <c>"Corellon (Forgotten Realms)"</c>). Without this, a round-trip on
    /// a file authored against an older rules build silently mutates every
    /// element whose canonical name has drifted, breaking tally parity even
    /// though the IDs still match.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Child <c>&lt;specific name="..."&gt;…&lt;/specific&gt;</c> values keyed
    /// by the <c>name</c> attribute. Order is preserved by callers via
    /// dictionary iteration order (insertion-ordered on .NET).
    /// </summary>
    public Dictionary<string, string> Specifics { get; }
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Occurrence counts for each <c>&lt;specific&gt;</c> name across all
    /// source occurrences of this internal-id, accounting for in-source
    /// duplicates that <see cref="Specifics"/> collapses. Currently used by
    /// the tally exporter to determine how many times to re-emit a specific
    /// the source legitimately repeated — e.g. Heroes of the Feywild races
    /// (Hamadryad, Satyr) carry <c>&lt;specific name="Short Description"&gt;</c>
    /// twice and OCB faithfully serializes both copies with identical text.
    /// </summary>
    public Dictionary<string, int> SpecificCounts { get; }
        = new(StringComparer.OrdinalIgnoreCase);
}
