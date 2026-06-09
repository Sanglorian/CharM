using CharM.Engine.Rules;

namespace CharM.Engine.Evaluation;

/// <summary>
/// Processes StatAddDirective rules and populates a StatBlock with contributions.
/// Handles value expression evaluation, condition checking, and half-point rounding.
/// </summary>
public static class StatAddProcessor
{
    /// <summary>
    /// Process a StatAddDirective and add its contribution to the stat block.
    /// Skips requires/wearing/not-wearing conditions (those need the prerequisite evaluator).
    /// Processes: value evaluation, bonus type, zero/non-zero flags, half-point.
    /// </summary>
    public static void Process(StatAddDirective directive, StatBlock block, string? sourceElementId = null, int? characterLevel = null, bool active = true)
    {
        // For literal values, resolve eagerly. For stat references and ability modifiers,
        // store the expression for lazy evaluation at ComputeValue() time.
        // This ensures stat cross-references resolve correctly regardless of processing order.
        int value;
        ValueExpression? deferredExpr = null;

        if (directive.Value is ValueExpression.Literal lit)
        {
            value = lit.Value;
        }
        else
        {
            // Defer evaluation — store the expression, use 0 as placeholder
            value = 0;
            deferredExpr = directive.Value;
        }

        // For absolute stat references (bare stat name without + prefix, like "Shield Bonus"),
        // the bonus type is derived from the referenced stat name. Per written rules,
        // "your shield bonus also applies to Fortitude" means the Shield-typed bonus
        // extends to that defense — it's not a new untyped bonus. "Shield Bonus" → type "Shield".
        // OCB only emits the derived type when the stat name has the " Bonus" suffix —
        // for absolute references to "Initiative Misc", "Perception", "Insight", etc.
        // the resulting statadd is untyped (no type= attribute on the wire).
        string? bonusType = directive.BonusType;
        if (bonusType is null && directive.Value is ValueExpression.StatReference { IsAbsolute: true } absRef)
        {
            string refName = absRef.StatName;
            int bonusSuffix = refName.LastIndexOf(" Bonus", StringComparison.OrdinalIgnoreCase);
            if (bonusSuffix > 0)
                bonusType = refName[..bonusSuffix];
        }

        var contribution = new StatContribution
        {
            Value = value,
            Expression = deferredExpr,
            BonusType = bonusType,
            SourceElementId = sourceElementId,
            Condition = directive.Condition,
            Zero = directive.Zero,
            NonZero = directive.NonZero,
            HalfPoint = directive.HalfPoint,
            Level = directive.Level ?? characterLevel,
            RequiresText = directive.Requires,
            Wearing = directive.Wearing,
            NotWearing = directive.NotWearing,
            Active = active,
        };

        var stat = block.GetOrCreateStat(directive.Name);
        stat.AddContribution(contribution);
    }
}
