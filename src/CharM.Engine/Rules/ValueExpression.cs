using System.Globalization;

namespace CharM.Engine.Rules;

/// <summary>
/// Parsed representation of a statadd value attribute.
/// Supports: literal integers, stat references, ability modifiers, and ABILITYMOD() function syntax.
/// 
/// Grammar:
///   value     := offset | absolute | stat-ref | abilmod-ref | abilmod-func
///   offset    := ('+' | '-') INTEGER
///   absolute  := INTEGER
///   stat-ref  := '+' STAT_NAME
///   abilmod-ref := '+' STAT_NAME ' modifier'
///   abilmod-func := ('+' | '-') 'ABILITYMOD(' STAT_NAME ')'
/// </summary>
public abstract record ValueExpression
{
    /// <summary>A literal integer value (e.g., "+2", "-1", "9").</summary>
    public sealed record Literal(int Value) : ValueExpression;

    /// <summary>
    /// Reference to another stat's computed value, with an optional scale factor.
    /// Result = CalculateStatValue(StatName) * ScaleFactor.
    /// If IsAbsolute, multiple references to the same stat don't stack (highest wins).
    /// Example: "+Toughness" → ScaleFactor=1, IsAbsolute=false (accumulates)
    /// Example: "Shield Bonus" → ScaleFactor=1, IsAbsolute=true (non-stacking)
    /// </summary>
    public sealed record StatReference(string StatName, int ScaleFactor = 1, bool IsAbsolute = false) : ValueExpression;

    /// <summary>
    /// Reference to a stat's value, then apply the ability modifier formula: floor(score/2) - 5.
    /// Example: "+Constitution modifier" → GetAbilityMod(GetCharStat("Constitution"))
    /// </summary>
    public sealed record AbilityModifier(string StatName) : ValueExpression;

    /// <summary>
    /// Explicit ABILITYMOD() function call with optional negation.
    /// Example: "+ABILITYMOD(Wisdom)" or "-ABILITYMOD(Constitution)"
    /// </summary>
    public sealed record AbilityModFunction(string StatName, bool Negate = false) : ValueExpression;

    /// <summary>
    /// Parse a value attribute string into a typed ValueExpression.
    /// </summary>
    public static ValueExpression Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new Literal(0);

        var trimmed = value.Trim();

        // +ABILITYMOD(StatName) or -ABILITYMOD(StatName)
        if (trimmed.Contains("ABILITYMOD(", StringComparison.Ordinal))
        {
            bool negate = trimmed.StartsWith('-');
            int start = trimmed.IndexOf("ABILITYMOD(", StringComparison.Ordinal) + "ABILITYMOD(".Length;
            int end = trimmed.IndexOf(')', start);
            if (end > start)
            {
                string statName = trimmed[start..end].Trim();
                return new AbilityModFunction(statName, negate);
            }
        }

        // +StatName modifier
        if (trimmed.StartsWith('+') && trimmed.EndsWith(" modifier", StringComparison.OrdinalIgnoreCase))
        {
            string statName = trimmed[1..^9].Trim(); // strip '+' and ' modifier'
            return new AbilityModifier(statName);
        }

        // Try parse as numeric: +N, -N, or bare N
        if (int.TryParse(trimmed, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int intVal))
        {
            return new Literal(intVal);
        }

        // +StatName (stat reference with implicit scale factor 1)
        if (trimmed.StartsWith('+'))
        {
            string statName = trimmed[1..].Trim();
            return new StatReference(statName, 1);
        }

        // -StatName (stat reference with scale factor -1)
        if (trimmed.StartsWith('-'))
        {
            string statName = trimmed[1..].Trim();
            return new StatReference(statName, -1);
        }

        // Bare string — stat reference without + prefix. Per original engine spec,
        // bare stat names (no +/- prefix) are "absolute" references that don't
        // accumulate like +StatName does. Multiple absolute refs to the same stat
        // from different sources take the highest (non-stacking).
        return new StatReference(trimmed, 1, IsAbsolute: true);
    }
}
