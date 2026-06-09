using CharM.Engine.Rules;

namespace CharM.Engine.Selection;

/// <summary>
/// Evaluates category filter expressions from SelectDirective.Category.
///
/// Expression syntax:
///   - Terms separated by ',' -> AND (all must match)
///   - Terms separated by '|' within a position -> OR (any matches)
///   - A bare integer as the last term -> level filter (element level <= N)
///   - $$VARIABLE -> substituted from the variables dictionary
///
/// Example: "ID_FMP_CLASS_9,encounter|daily,7"
///   -> has category ID_FMP_CLASS_9 AND (encounter OR daily) AND level <= 7
/// </summary>
public static class CategoryMatcher
{
    public static bool Matches(string? categoryExpression, RulesElement element,
        Dictionary<string, string>? variables = null, int? elementLevel = null,
        Func<string, string?>? resolveNameToId = null)
    {
        if (string.IsNullOrWhiteSpace(categoryExpression))
            return true;

        var andTerms = categoryExpression.Split(',');

        for (int i = 0; i < andTerms.Length; i++)
        {
            var term = andTerms[i].Trim();
            if (term.Length == 0)
                continue;

            // Last term that is purely numeric -> level filter
            if (i == andTerms.Length - 1 && IsNumeric(term))
            {
                int maxLevel = int.Parse(term);
                int level = elementLevel ?? 0;
                if (level > maxLevel)
                    return false;
                continue;
            }

            // Special-case AND-terms that are negation/level variables. These shape the
            // semantics of the whole AND-term, so they must be handled before OR-splitting.
            if (variables is not null && IsSpecialVariableTerm(term, out var specialName))
            {
                if (!EvaluateSpecialVariableTerm(specialName, element, variables, elementLevel, resolveNameToId))
                    return false;
                continue;
            }

            // Split by '|' for OR alternatives
            var orAlternatives = term.Split('|');
            bool anyMatch = false;

            foreach (var alt in orAlternatives)
            {
                var resolved = alt.Trim();
                if (resolved.Length == 0)
                    continue;

                // Variable substitution
                if (resolved.StartsWith("$$") && variables is not null)
                {
                    if (variables.TryGetValue(resolved, out var substitution))
                    {
                        // Substitution may contain '|' (e.g., multi-class union)
                        if (substitution.Contains('|'))
                        {
                            foreach (var subAlt in substitution.Split('|'))
                            {
                                if (MatchesSingleTerm(subAlt.Trim(), element, resolveNameToId))
                                {
                                    anyMatch = true;
                                    break;
                                }
                            }
                            if (anyMatch) break;
                            continue;
                        }
                        resolved = substitution;
                    }
                    else
                    {
                        // Unknown $$VAR — character has no value for it (e.g. $$MULTICLASS
                        // when not multiclassed). The AND-term cannot be satisfied; treat
                        // as no match for this alternative and continue.
                        continue;
                    }
                }

                if (MatchesSingleTerm(resolved, element, resolveNameToId))
                {
                    anyMatch = true;
                    break;
                }
            }

            if (!anyMatch)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Detects AND-terms that are bare special variables whose semantics are
    /// not "match one of these IDs" (e.g. $$NOT_CLASS = exclusion, $$LEVEL = level cap).
    /// </summary>
    private static bool IsSpecialVariableTerm(string term, out string variableName)
    {
        variableName = term;
        if (term.StartsWith("$$", StringComparison.Ordinal)
            && (term.Equals("$$NOT_CLASS", StringComparison.OrdinalIgnoreCase)
                || term.Equals("$$LEVEL", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }
        return false;
    }

    private static bool EvaluateSpecialVariableTerm(
        string variableName,
        RulesElement element,
        Dictionary<string, string> variables,
        int? elementLevel,
        Func<string, string?>? resolveNameToId)
    {
        if (!variables.TryGetValue(variableName, out var value) || string.IsNullOrEmpty(value))
        {
            // No value: $$NOT_CLASS with no class is vacuously true; $$LEVEL with no level
            // means no level filter to apply.
            return true;
        }

        if (variableName.Equals("$$NOT_CLASS", StringComparison.OrdinalIgnoreCase))
        {
            // Element must NOT match any of the listed class IDs.
            foreach (var id in value.Split('|'))
            {
                if (MatchesSingleTerm(id.Trim(), element, resolveNameToId))
                    return false;
            }
            return true;
        }

        if (variableName.Equals("$$LEVEL", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(value, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out int cap))
                return true;
            int lvl = elementLevel ?? 0;
            return lvl <= cap;
        }

        return true;
    }

    /// <summary>Check if a single resolved term matches the element.</summary>
    private static bool MatchesSingleTerm(string resolved, RulesElement element,
        Func<string, string?>? resolveNameToId)
    {
        // Direct category match
        if (element.Categories.Contains(resolved, StringComparer.OrdinalIgnoreCase))
            return true;

        // InternalId match
        if (string.Equals(element.InternalId, resolved, StringComparison.OrdinalIgnoreCase))
            return true;

        // Name match
        if (string.Equals(element.Name, resolved, StringComparison.OrdinalIgnoreCase))
            return true;

        // ID_INTERNAL_CATEGORY_ prefix shorthand
        if (element.Categories.Contains(
            "ID_INTERNAL_CATEGORY_" + resolved.ToUpperInvariant().Replace(' ', '_'),
            StringComparer.OrdinalIgnoreCase))
            return true;

        // Negation prefix '!' - element must NOT have this category
        if (resolved.StartsWith('!'))
        {
            var negated = resolved[1..].Trim();
            return !element.Categories.Contains(negated, StringComparer.OrdinalIgnoreCase)
                && !element.Categories.Contains(
                    "ID_INTERNAL_CATEGORY_" + negated.ToUpperInvariant().Replace(' ', '_'),
                    StringComparer.OrdinalIgnoreCase)
                && !(element.Fields.TryGetValue("Keywords", out var negKw)
                    && negKw.Contains(negated, StringComparison.OrdinalIgnoreCase));
        }

        // Keywords field match (e.g., "bladespell")
        if (element.Fields.TryGetValue("Keywords", out var keywords)
            && keywords.Contains(resolved, StringComparison.OrdinalIgnoreCase))
            return true;

        // Resolve category name to element ID
        if (resolveNameToId is not null)
        {
            var resolvedId = resolveNameToId(resolved);
            if (resolvedId is not null
                && element.Categories.Contains(resolvedId, StringComparer.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Extract the level filter from a category expression (last numeric term), if any.
    /// </summary>
    public static int? ExtractLevelFilter(string? categoryExpression)
    {
        if (string.IsNullOrWhiteSpace(categoryExpression))
            return null;

        var andTerms = categoryExpression.Split(',');
        if (andTerms.Length == 0)
            return null;

        var lastTerm = andTerms[^1].Trim();
        if (IsNumeric(lastTerm) && int.TryParse(lastTerm, out int level))
            return level;

        return null;
    }

    private static bool IsNumeric(string value)
    {
        foreach (char c in value)
        {
            if (!char.IsAsciiDigit(c))
                return false;
        }
        return value.Length > 0;
    }
}
