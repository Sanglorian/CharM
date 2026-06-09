using System.Text.RegularExpressions;

namespace CharM.Engine.Prerequisites;

/// <summary>
/// Parsed prerequisite tree node. Forms a binary tree with AND/OR connectors.
/// 
/// Prereqs syntax (from &lt;Prereqs&gt; element text):
///   - Semicolons ';' and commas ',' are AND separators
///   - 'or' keyword is OR separator
///   - '~' prefix is negation (must NOT have element)
///   - '!' prefix is absence check (same as ~ in many contexts)
///   - Plain text is element name presence check
///   - "Str 13" pattern is ability score threshold
///   - "21st level" pattern is level requirement
/// </summary>
public abstract record PrereqNode
{
    /// <summary>AND/OR compound node with left and right children.</summary>
    public sealed record Compound(PrereqNode Left, PrereqNode Right, bool IsAnd) : PrereqNode;

    /// <summary>Check if character has a named element.</summary>
    public sealed record HasElement(string Name, bool Negate = false) : PrereqNode;

    /// <summary>Check if character meets a level requirement.</summary>
    public sealed record LevelCheck(int MinLevel) : PrereqNode;

    /// <summary>Check if an ability score meets a threshold.</summary>
    public sealed record AbilityCheck(string AbilityName, int MinScore) : PrereqNode;

    /// <summary>Check if character has any class/role matching a keyword (e.g., "any martial class").</summary>
    public sealed record AnyClassCheck(string Keyword) : PrereqNode;

    /// <summary>Check if character has a matching weapon/implement/armor/shield proficiency.</summary>
    public sealed record ProficiencyCheck(string Target) : PrereqNode;
}

public static partial class PrereqParser
{
    // Pattern: "21st level", "11th level", "21st-level wizard", etc.
    // Captures the number and optional trailing text after "level"
    private static Regex LevelPattern => LevelRegex();

    // Pattern: "Str 13", "Dexterity 15"
    private static Regex AbilityPattern => AbilityRegex();

    // Pattern: "proficient with bastard sword", "proficient with a shield"
    private static Regex ProficientWithPattern => ProficientWithRegex();

    // Pattern: "any martial class", "Any divine class", "Any arcane or divine class"
    private static Regex AnyClassPattern => AnyClassRegex();

    // Pattern: "Any defender class", "Any controller class"
    private static readonly HashSet<string> PowerSources = new(StringComparer.OrdinalIgnoreCase)
    {
        "martial", "arcane", "divine", "primal", "psionic", "shadow"
    };

    private static readonly HashSet<string> ClassRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "defender", "striker", "leader", "controller"
    };

    /// <summary>
    /// Parse a &lt;Prereqs&gt; text string into a PrereqNode tree.
    /// Returns null if the string is empty or unparseable.
    /// </summary>
    public static PrereqNode? Parse(string? prereqText)
    {
        if (string.IsNullOrWhiteSpace(prereqText))
            return null;

        return ParseAndChain(prereqText.Trim());
    }

    /// <summary>
    /// Parse semicolon-separated AND clauses. Semicolons are the highest-level separator.
    /// </summary>
    private static PrereqNode? ParseAndChain(string text)
    {
        // Split on semicolons first (highest-precedence AND separator)
        var semicolonParts = SplitAndTrim(text, ';');

        if (semicolonParts.Count == 0)
            return null;

        PrereqNode? result = null;
        foreach (var part in semicolonParts)
        {
            var node = ParseCommaChain(part);
            if (node is null) continue;

            result = result is null ? node : new PrereqNode.Compound(result, node, IsAnd: true);
        }

        return result;
    }

    /// <summary>
    /// Parse comma-separated AND clauses. Commas are medium-precedence AND.
    /// Parsed BEFORE 'or' so that "A or B, C" = "(A or B) AND C".
    /// </summary>
    private static PrereqNode? ParseCommaChain(string text)
    {
        var commaParts = SplitAndTrim(text, ',');

        if (commaParts.Count == 0)
            return null;

        PrereqNode? result = null;
        foreach (var part in commaParts)
        {
            var node = ParseOrChain(part);
            if (node is null) continue;

            result = result is null ? node : new PrereqNode.Compound(result, node, IsAnd: true);
        }

        return result;
    }

    /// <summary>
    /// Parse 'or'-separated OR clauses within a comma segment.
    /// The 'or' keyword must be a standalone word (word boundary match).
    /// </summary>
    private static PrereqNode? ParseOrChain(string text)
    {
        // Split on ' or ' as a word boundary separator
        var orParts = SplitOnOrKeyword(text);

        if (orParts.Count <= 1)
        {
            // No 'or' found — parse as single atom
            return ParseAtom(text);
        }

        PrereqNode? result = null;
        foreach (var part in orParts)
        {
            var node = ParseAtom(part);
            if (node is null) continue;

            result = result is null ? node : new PrereqNode.Compound(result, node, IsAnd: false);
        }

        return result;
    }

    /// <summary>
    /// Parse a single atom: level check, ability check, negated element, or plain element name.
    /// </summary>
    private static PrereqNode? ParseAtom(string text)
    {
        text = text.Trim();
        if (string.IsNullOrEmpty(text))
            return null;

        // Level requirement: "21st level", "11th level", "21st-level wizard"
        var levelMatch = LevelPattern.Match(text);
        if (levelMatch.Success)
        {
            int level = int.Parse(levelMatch.Groups[1].Value);
            var levelNode = new PrereqNode.LevelCheck(level);

            // If there's trailing text (e.g., "wizard" in "21st-level wizard"),
            // combine as AND with a HasElement check
            if (levelMatch.Groups[2].Success)
            {
                string trailing = levelMatch.Groups[2].Value.Trim();
                if (!string.IsNullOrEmpty(trailing))
                {
                    var elementNode = new PrereqNode.HasElement(trailing);
                    return new PrereqNode.Compound(levelNode, elementNode, IsAnd: true);
                }
            }

            return levelNode;
        }

        // Ability score check: "Str 13", "Dexterity 15"
        var abilityMatch = AbilityPattern.Match(text);
        if (abilityMatch.Success)
        {
            return new PrereqNode.AbilityCheck(
                abilityMatch.Groups[1].Value,
                int.Parse(abilityMatch.Groups[2].Value));
        }

        // Proficiency requirement: "proficient with rapier", "proficient with a shield"
        var proficiencyMatch = ProficientWithPattern.Match(text);
        if (proficiencyMatch.Success)
        {
            string target = proficiencyMatch.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(target))
                return new PrereqNode.ProficiencyCheck(target);
        }

        // "Any X class" check: "any martial class", "Any arcane or divine class"
        var anyClassMatch = AnyClassPattern.Match(text);
        if (anyClassMatch.Success)
        {
            string keyword = anyClassMatch.Groups[1].Value.Trim();
            // Handle "arcane or divine" → OR of two checks
            if (keyword.Contains(" or ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = KeywordOrSplitRegex().Split(keyword);
                PrereqNode? result = null;
                foreach (var part in parts)
                {
                    var node = new PrereqNode.AnyClassCheck(part.Trim());
                    result = result is null ? node : new PrereqNode.Compound(result, node, IsAnd: false);
                }
                return result;
            }
            return new PrereqNode.AnyClassCheck(keyword);
        }

        // Negation with ~ or !
        if (text[0] is '~' or '!')
        {
            string name = text[1..].Trim();
            if (!string.IsNullOrEmpty(name))
                return new PrereqNode.HasElement(name, Negate: true);
        }

        // Plain element name
        return new PrereqNode.HasElement(text);
    }

    /// <summary>
    /// Split on the 'or' keyword at word boundaries, avoiding matching 'or' inside words
    /// like "Dragonborn" or "Sorcerer".
    /// </summary>
    private static List<string> SplitOnOrKeyword(string text)
    {
        // Use regex to split on ' or ' with word boundaries
        var parts = OrSplitRegex().Split(text);

        var result = new List<string>();
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 0)
                result.Add(trimmed);
        }

        return result;
    }

    private static List<string> SplitAndTrim(string text, char separator)
    {
        var result = new List<string>();
        foreach (var part in text.Split(separator))
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 0)
                result.Add(trimmed);
        }
        return result;
    }

    [GeneratedRegex(@"^(\d+)(?:st|nd|rd|th)[\s-]+level(?:\s+(.+))?$", RegexOptions.IgnoreCase)]
    private static partial Regex LevelRegex();

    [GeneratedRegex(@"^(Str|Dex|Con|Int|Wis|Cha|Strength|Dexterity|Constitution|Intelligence|Wisdom|Charisma)\s+(\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex AbilityRegex();

    [GeneratedRegex(@"^proficient\s+with\s+(?:(?:a|an|the)\s+)?(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ProficientWithRegex();

    [GeneratedRegex(@"^any\s+(.+?)\s+class$", RegexOptions.IgnoreCase)]
    private static partial Regex AnyClassRegex();

    [GeneratedRegex(@"\s+or\s+", RegexOptions.IgnoreCase)]
    private static partial Regex KeywordOrSplitRegex();

    [GeneratedRegex(@"\bor\b", RegexOptions.IgnoreCase)]
    private static partial Regex OrSplitRegex();
}
