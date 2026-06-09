using CharM.Engine.Rules;

namespace CharM.Engine.Evaluation;

/// <summary>
/// A single numeric contribution to a stat. Tracks source, type, conditions, and flags.
/// Supports lazy evaluation: if Expression is set, Value is resolved at compute time
/// against the StatBlock, enabling stat cross-references to resolve correctly regardless
/// of processing order.
/// </summary>
public sealed record StatContribution
{
    /// <summary>
    /// Pre-resolved numeric value. Used directly if Expression is null.
    /// If Expression is set, this field is ignored — the expression is evaluated lazily.
    /// </summary>
    public required int Value { get; init; }

    /// <summary>
    /// Optional deferred value expression. When set, the contribution's effective value
    /// is computed at stat evaluation time by resolving this expression against the StatBlock.
    /// This enables correct handling of stat cross-references regardless of processing order.
    /// </summary>
    public ValueExpression? Expression { get; init; }

    public string? BonusType { get; init; }
    public string? SourceElementId { get; init; }
    public string? Condition { get; init; }

    /// <summary>
    /// Verbatim <c>wearing=</c> predicate from the directive (e.g.
    /// <c>"armor:heavy"</c>, <c>"weapon:two-handed,polearm"</c>). Always
    /// preserved on the contribution for export, even when the predicate
    /// doesn't currently fire.
    /// </summary>
    public string? Wearing { get; init; }

    /// <summary>
    /// Verbatim <c>not-wearing=</c> predicate from the directive. Always
    /// preserved on the contribution for export.
    /// </summary>
    public string? NotWearing { get; init; }

    /// <summary>
    /// True when this contribution's gating predicates (requires / wearing /
    /// not-wearing) are currently satisfied. Inactive contributions are kept
    /// in the list so they round-trip on export but are ignored by
    /// <see cref="Stat.ComputeValue"/>. Defaults to true so existing call sites
    /// that build contributions without gating continue to work unchanged.
    /// </summary>
    public bool Active { get; init; } = true;

    /// <summary>
    /// Optional literal text payload (e.g. height/weight/size descriptions,
    /// class-name internal-ids, action-point feature blurbs). Source rules
    /// declare these via <c>&lt;textstring&gt;</c>; OCB serializes them as
    /// <c>&lt;statadd String="..." value="0"/&gt;</c>. Display-only —
    /// excluded from value summation.
    /// </summary>
    public string? StringPayload { get; init; }

    /// <summary>
    /// Character level at which this contribution was added. Used for export
    /// to round-trip the <c>Level=</c> attribute on <c>&lt;statadd&gt;</c>.
    /// Sourced from <c>directive.Level</c> when set, otherwise the level
    /// active when the parent rules-element was processed.
    /// </summary>
    public int? Level { get; init; }

    /// <summary>
    /// Verbatim <c>directive.Requires</c> text that the requires-evaluator
    /// matched at processing time. Preserved here only for export so the
    /// <c>requires=</c> attribute can be round-tripped on <c>&lt;statadd&gt;</c>.
    /// Mechanically already filtered — if a contribution is present, its
    /// requires gate already passed.
    /// </summary>
    public string? RequiresText { get; init; }

    /// <summary>When true, contribution goes into the "zero" bucket (applies even when base stat is 0).</summary>
    public bool Zero { get; init; }

    /// <summary>When true, contribution goes into the "non-zero" bucket (applies only when stat has non-zero base).</summary>
    public bool NonZero { get; init; }

    /// <summary>
    /// When true, this contribution participates in half-point rounding.
    /// Two interpretations depending on the value source:
    ///   • Literal value: the integer is the value rounded toward zero from a
    ///     true ½-step number (e.g., 2 represents 2.5). Two such contributions
    ///     to the same stat combine their implicit halves to add ±1.
    ///     Used by hybrid class HP/healing surges and Vampire surge penalties.
    ///   • AbilityModifier expression: the resulting modifier is halved
    ///     (legacy "half your &lt;ability&gt; modifier" semantics).
    /// </summary>
    public bool HalfPoint { get; init; }

    /// <summary>
    /// Get the effective value of this contribution, resolving the expression lazily if present.
    /// </summary>
    public int GetEffectiveValue(StatBlock block)
    {
        if (Expression is not null)
            return StatEvaluator.Evaluate(Expression, block);
        return Value;
    }
}
