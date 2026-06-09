using System.Diagnostics.CodeAnalysis;

namespace CharM.Engine.Prerequisites;

/// <summary>
/// Evaluates <c>requires</c> condition expressions against a character's element set.
/// 
/// Grammar:
///   requires     := '!' negated_expr | or_expr | and_expr
///   negated_expr := or_expr | and_expr
///   or_expr      := token ('|' token)*
///   and_expr     := token ('&amp;' token)*   (& is XML-escaped in source)
///   token        := '(' requires ')'
///                  | type ':' category
///                  | ELEMENT_NAME
///
/// Evaluation:
///   1. Leading '!' inverts the entire result
///   2. If string contains '|' → OR mode (short-circuit: first true wins)
///   3. Otherwise '&amp;' splits into AND (short-circuit: first false fails)
///   4. Parenthesized sub-expressions recurse
///   5. 'Type:CategoryMatch' checks character has element of type matching category
///   6. Plain names check element existence
/// </summary>
public sealed class RequiresEvaluator
{
    /// <summary>
    /// Evaluate a requires expression against a set of character elements.
    /// </summary>
    /// <param name="requires">The requires expression string.</param>
    /// <param name="hasElement">Callback that checks if the character has a named element.</param>
    /// <param name="hasElementOfTypeAndCategory">Callback for Type:Category checks.</param>
    /// <param name="characterLevel">Current character level (for "N level" checks).</param>
    /// <returns>True if the requirements are met.</returns>
    public static bool Evaluate(
        string? requires,
        Func<string, bool> hasElement,
        Func<string, string, bool>? hasElementOfTypeAndCategory = null,
        int characterLevel = 30)
    {
        if (string.IsNullOrWhiteSpace(requires))
            return true;

        return EvaluateExpression(requires.AsSpan().Trim(), hasElement, hasElementOfTypeAndCategory, characterLevel);
    }

    private static bool EvaluateExpression(
        ReadOnlySpan<char> expr,
        Func<string, bool> hasElement,
        Func<string, string, bool>? hasElementOfTypeAndCategory,
        int characterLevel)
    {
        expr = expr.Trim();
        if (expr.IsEmpty)
            return true;

        // Leading '!' negates the entire expression
        if (expr[0] == '!')
        {
            return !EvaluateExpression(expr[1..], hasElement, hasElementOfTypeAndCategory, characterLevel);
        }

        // Check for top-level OR (pipe outside parentheses)
        if (TrySplitTopLevel(expr, '|', out var orParts))
        {
            foreach (var part in orParts)
            {
                if (EvaluateExpression(part.AsSpan(), hasElement, hasElementOfTypeAndCategory, characterLevel))
                    return true;
            }
            return false;
        }

        // Check for top-level AND (& outside parentheses)
        if (TrySplitTopLevel(expr, '&', out var andParts))
        {
            foreach (var part in andParts)
            {
                if (!EvaluateExpression(part.AsSpan(), hasElement, hasElementOfTypeAndCategory, characterLevel))
                    return false;
            }
            return true;
        }

        // Single token: could be parenthesized, Type:Category, level check, or plain name
        return EvaluateToken(expr, hasElement, hasElementOfTypeAndCategory, characterLevel);
    }

    private static bool EvaluateToken(
        ReadOnlySpan<char> token,
        Func<string, bool> hasElement,
        Func<string, string, bool>? hasElementOfTypeAndCategory,
        int characterLevel)
    {
        token = token.Trim();
        if (token.IsEmpty)
            return true;

        // Parenthesized sub-expression
        if (token[0] == '(' && token[^1] == ')')
        {
            return EvaluateExpression(token[1..^1], hasElement, hasElementOfTypeAndCategory, characterLevel);
        }

        string tokenStr = token.ToString();

        // Level check: "N level" pattern (e.g., "11 level", "21 level")
        if (tokenStr.EndsWith(" level", StringComparison.OrdinalIgnoreCase))
        {
            var numPart = tokenStr[..^6].Trim(); // strip " level"
            if (int.TryParse(numPart, out int requiredLevel))
                return characterLevel >= requiredLevel;
        }

        // Level check: "level N" pattern (reversed, rare — 1 instance in data)
        if (tokenStr.StartsWith("level ", StringComparison.OrdinalIgnoreCase))
        {
            var numPart = tokenStr[6..].Trim(); // strip "level "
            if (int.TryParse(numPart, out int requiredLevel))
                return characterLevel >= requiredLevel;
        }

        if (tokenStr.Equals("Heroic Tier", StringComparison.OrdinalIgnoreCase))
            return characterLevel >= 1;
        if (tokenStr.Equals("Paragon Tier", StringComparison.OrdinalIgnoreCase))
            return characterLevel >= 11;
        if (tokenStr.Equals("Epic Tier", StringComparison.OrdinalIgnoreCase))
            return characterLevel >= 21;

        // Type:Category check (e.g., "Power:encounter")
        int colonIdx = tokenStr.IndexOf(':');
        if (colonIdx > 0 && colonIdx < tokenStr.Length - 1)
        {
            string type = tokenStr[..colonIdx].Trim();
            string category = tokenStr[(colonIdx + 1)..].Trim();

            if (hasElementOfTypeAndCategory is not null)
                return hasElementOfTypeAndCategory(type, category);

            // Fallback: treat as plain element name
            return hasElement(tokenStr);
        }

        return hasElement(tokenStr);
    }

    /// <summary>
    /// Split an expression at top-level occurrences of <paramref name="separator"/>,
    /// respecting parenthesized sub-expressions.
    /// Returns false if the separator doesn't appear at top level.
    /// </summary>
    private static bool TrySplitTopLevel(
        ReadOnlySpan<char> expr,
        char separator,
        [NotNullWhen(true)] out List<string>? parts)
    {
        parts = null;
        int depth = 0;
        bool found = false;

        // First pass: check if separator exists at top level
        for (int i = 0; i < expr.Length; i++)
        {
            char c = expr[i];
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == separator && depth == 0)
            {
                found = true;
                break;
            }
        }

        if (!found)
            return false;

        // Second pass: split
        parts = [];
        depth = 0;
        int start = 0;

        for (int i = 0; i < expr.Length; i++)
        {
            char c = expr[i];
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == separator && depth == 0)
            {
                parts.Add(expr[start..i].ToString().Trim());
                start = i + 1;
            }
        }

        parts.Add(expr[start..].ToString().Trim());
        return true;
    }
}
