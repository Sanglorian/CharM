using System.Xml.Linq;

namespace CharM.Serialization;

/// <summary>
/// Captures the OCB houserule system data for a character, in the four storage
/// forms reverse-engineered from sample files.
///
/// <para>Forms:</para>
/// <list type="table">
///   <item>
///     <term>A — &lt;UserEdit&gt; (per-Level)</term>
///     <description>Wrapper RulesElement holding picks (and their cascade-tagged
///     grant subtree), plus a sibling <c>&lt;rules&gt;&lt;select/&gt;</c> block
///     positionally paired to the picks. Lives inside <c>&lt;Level&gt;</c>.</description>
///   </item>
///   <item>
///     <term>B — Legacy inline tally row</term>
///     <description>RulesElement with <c>legality="houserule"</c> directly under
///     <c>RulesElementTally</c> not paired to any UserEdit. Pre-houserule-system
///     artifact, found alongside or instead of Form A.</description>
///   </item>
///   <item>
///     <term>C — Tally mirror of Form A</term>
///     <description>The same picks from Form A also serialize as flat tally
///     rows (same charelem). Re-emitted by writing the Form A subtree contents
///     into the tally output.</description>
///   </item>
///   <item>
///     <term>D — &lt;D20CampaignSetting&gt;&lt;Houserules&gt;</term>
///     <description>Definition-only; no observed engine effect across the
///     community pack. Preserved verbatim through <c>RawSections</c>.</description>
///   </item>
/// </list>
///
/// <para>When any Form A or Form B picks are present, <c>legality="houserule"</c>
/// cascades to the root <c>D20Character</c> and to <c>AbilityScores</c>.</para>
/// </summary>
public sealed class HouseruleOverlay
{
    /// <summary>True if any Form A or Form B picks present — triggers legality cascade.</summary>
    public bool IsCharacterHouseruled { get; set; }

    /// <summary>
    /// Raw <c>&lt;UserEdit&gt;</c> elements captured per character level (key =
    /// level number). Re-emitted verbatim inside the matching <c>&lt;Level&gt;</c>
    /// container during export, preserving wrapper attributes, full pick subtree
    /// (including cascade-tagged grant chains), and the sibling
    /// <c>&lt;rules&gt;&lt;select/&gt;</c> block.
    /// </summary>
    public Dictionary<int, List<XElement>> LevelUserEdits { get; }
        = new();

    /// <summary>
    /// Form A picks flattened for tally re-emission (Form C). Each entry is the
    /// raw <c>&lt;RulesElement&gt;</c> as it appeared inside the UserEdit
    /// subtree (descendants of the wrapper, not the empty wrapper itself).
    /// Re-emitted verbatim into <c>RulesElementTally</c>.
    /// </summary>
    public List<XElement> FormATallyMirror { get; } = new();

    /// <summary>
    /// Raw legacy inline tally rows (Form B) preserved verbatim. These appear
    /// in the source's <c>RulesElementTally</c> with <c>legality="houserule"</c>
    /// but are not paired to any UserEdit.
    /// </summary>
    public List<XElement> LegacyTallyRows { get; } = new();

    /// <summary>
    /// Internal-ids of every <c>&lt;RulesElement&gt;</c> in the source file
    /// tagged with <c>legality="houserule"</c>, regardless of where it appeared
    /// (Level tree, RulesElementTally, UserEdit subtree). Used at export time
    /// to re-emit per-element legality so round-trips preserve the OCB
    /// classification of houseruled feats / powers / class features that the
    /// user took without meeting prereqs.
    /// </summary>
    public HashSet<string> HouseruledElementIds { get; }
        = new(StringComparer.OrdinalIgnoreCase);
}
