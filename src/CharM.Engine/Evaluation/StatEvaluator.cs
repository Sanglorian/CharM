using CharM.Engine.Rules;

namespace CharM.Engine.Evaluation;

/// <summary>
/// Evaluates ValueExpression instances against a StatBlock.
/// Handles stat cross-references, ability modifier formula, and scale factors.
/// </summary>
public static class StatEvaluator
{
    [ThreadStatic]
    private static HashSet<string>? t_evaluatingStats;

    /// <summary>
    /// Resolve a ValueExpression to an integer value.
    /// </summary>
    public static int Evaluate(ValueExpression expr, StatBlock block)
    {
        return expr switch
        {
            ValueExpression.Literal lit => lit.Value,
            ValueExpression.StatReference sr => EvaluateStatReference(sr, block),
            ValueExpression.AbilityModifier am => GetAbilityMod(ComputeStatValue(am.StatName, block)),
            ValueExpression.AbilityModFunction fn => EvaluateAbilityModFunction(fn, block),
            _ => 0,
        };
    }

    /// <summary>
    /// Ability modifier formula.
    /// GetAbilityMod(0) = 0 (zero-guard).
    /// GetAbilityMod(score) = floor(score / 2) - 5.
    /// </summary>
    public static int GetAbilityMod(int score)
    {
        if (score == 0)
            return 0;

        // Integer division in C# truncates toward zero. For correct floor division
        // with negative values, use Math.DivRem or explicit floor logic.
        // floor(score / 2) - 5
        return (int)Math.Floor(score / 2.0) - 5;
    }

    private static int EvaluateStatReference(ValueExpression.StatReference sr, StatBlock block)
    {
        if (TryParseMinOneMod(sr.StatName, out string abilityName))
        {
            int mod = GetAbilityMod(ComputeStatValue(abilityName, block));
            return Math.Max(1, mod) * sr.ScaleFactor;
        }

        int value = ComputeStatValue(sr.StatName, block);
        return value * sr.ScaleFactor;
    }

    private static int EvaluateAbilityModFunction(ValueExpression.AbilityModFunction fn, StatBlock block)
    {
        int mod = GetAbilityMod(ComputeStatValue(fn.StatName, block));
        return fn.Negate ? -mod : mod;
    }

    internal static bool TryParseMinOneMod(string text, out string abilityName)
    {
        abilityName = string.Empty;
        const string prefix = "MINONEMOD(";
        string trimmed = text.Trim();
        if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !trimmed.EndsWith(')'))
        {
            return false;
        }

        abilityName = trimmed[prefix.Length..^1].Trim();
        return abilityName.Length > 0;
    }

    /// <summary>
    /// Compute a stat's value with cycle detection. Throws if a circular reference is detected.
    /// </summary>
    private static int ComputeStatValue(string statName, StatBlock block)
    {
        var evaluating = t_evaluatingStats ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!evaluating.Add(statName))
            throw new InvalidOperationException(
                $"Circular stat reference detected: '{statName}' references itself through a chain.");

        try
        {
            var stat = block.GetOrCreateStat(statName);
            return stat.ComputeValue(block);
        }
        finally
        {
            evaluating.Remove(statName);
        }
    }
}
