using CharM.Engine.Rules;

namespace CharM.Engine.Selection;

public sealed record RulesElementPickerFieldColumn(string FieldName, string Header);

public sealed record RulesElementPickerGroup(
    string Key,
    string Name,
    IReadOnlyList<RulesElement> Candidates,
    bool IsCategoryBacked);

public static class RulesElementPickerDisplay
{
    private const string AllOptionsKey = "__all";
    private const string UngroupedKey = "__ungrouped";

    private static readonly string[] PreferredFieldColumns =
    [
        "Tier",
        "Level",
        "Power Usage",
        "Power Type",
        "Action Type",
        "Attack Type",
        "Keywords",
        "Group",
        "Weapon Category",
        "Armor Type",
        "Armor Category",
        "Magic Item Type",
        "Item Slot",
        "Role",
        "Power Source",
    ];

    private static readonly HashSet<string> SuppressedFieldColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "Description",
        "Short Description",
        "Flavor",
        "Special",
        "Supplemental",
        "Creating",
        "Powers",
        "Class Features",
        "Associated Powers",
        "Associated Power Info",
        "print-prereqs",
    };

    public static ISet<string> ExtractExplicitCategoryIds(string? categoryExpression)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(categoryExpression))
            return result;

        foreach (var token in categoryExpression.Split([',', '|'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var bare = token.Trim().TrimStart('!');
            if (bare.StartsWith("ID_", StringComparison.OrdinalIgnoreCase))
                result.Add(bare);
        }

        return result;
    }

    public static IReadOnlyList<RulesElementPickerFieldColumn> GetFieldColumns(
        IReadOnlyList<RulesElement> candidates,
        int maxColumns = 4,
        ISet<string>? excludedFieldNames = null)
    {
        if (candidates.Count == 0 || maxColumns <= 0)
            return [];

        excludedFieldNames ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<RulesElementPickerFieldColumn>();
        var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in PreferredFieldColumns)
        {
            TryAddField(field);
            if (result.Count >= maxColumns)
                return result;
        }

        foreach (var field in candidates
                     .SelectMany(candidate => candidate.Fields.Keys)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(field => field, StringComparer.OrdinalIgnoreCase))
        {
            if (!IsDisplayableFieldColumn(field))
                continue;

            TryAddField(field);
            if (result.Count >= maxColumns)
                break;
        }

        return result;

        void TryAddField(string fieldName)
        {
            if (added.Contains(fieldName)
                || excludedFieldNames.Contains(fieldName)
                || !HasAnyDisplayValue(candidates, fieldName))
            {
                return;
            }

            added.Add(fieldName);
            result.Add(new RulesElementPickerFieldColumn(fieldName, FormatHeader(fieldName)));
        }
    }

    public static IReadOnlyList<RulesElementPickerGroup> GroupByCategory(
        IReadOnlyList<RulesElement> candidates,
        Func<string, string?> resolveCategoryName,
        ISet<string>? excludedCategoryIds = null)
    {
        if (candidates.Count == 0)
            return [];

        excludedCategoryIds ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var commonCategoryIds = GetCommonCategoryIds(candidates);
        var groups = new Dictionary<string, (string Name, List<RulesElement> Candidates, int Priority)>(
            StringComparer.OrdinalIgnoreCase);
        var ungrouped = new List<RulesElement>();

        foreach (var candidate in candidates)
        {
            var best = FindBestCategory(candidate, commonCategoryIds, excludedCategoryIds, resolveCategoryName);
            if (best is null)
            {
                ungrouped.Add(candidate);
                continue;
            }

            if (!groups.TryGetValue(best.Value.Id, out var group))
            {
                group = (best.Value.Name, [], GetCategoryPriority(best.Value.Id));
                groups.Add(best.Value.Id, group);
            }

            group.Candidates.Add(candidate);
        }

        if (groups.Count == 0)
        {
            return
            [
                new RulesElementPickerGroup(
                    AllOptionsKey,
                    "All options",
                    SortCandidates(candidates),
                    IsCategoryBacked: false),
            ];
        }

        var result = groups
            .OrderBy(group => group.Value.Priority)
            .ThenBy(group => group.Value.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => new RulesElementPickerGroup(
                group.Key,
                group.Value.Name,
                SortCandidates(group.Value.Candidates),
                IsCategoryBacked: true))
            .ToList();

        if (ungrouped.Count > 0)
        {
            result.Add(new RulesElementPickerGroup(
                UngroupedKey,
                "Other",
                SortCandidates(ungrouped),
                IsCategoryBacked: false));
        }

        if (result.Count == 1)
        {
            return
            [
                new RulesElementPickerGroup(
                    AllOptionsKey,
                    "All options",
                    SortCandidates(candidates),
                    IsCategoryBacked: false),
            ];
        }

        return result;
    }

    private static IReadOnlyList<RulesElement> SortCandidates(IReadOnlyList<RulesElement> candidates)
    {
        if (HasAnyDisplayValue(candidates, "Level"))
        {
            return candidates
                .OrderBy(candidate => ParseLevelSortKey(candidate))
                .ThenBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return candidates
            .OrderBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int ParseLevelSortKey(RulesElement candidate)
    {
        return candidate.Fields.TryGetValue("Level", out var raw)
            && int.TryParse(raw, out var level)
            ? level
            : int.MaxValue;
    }

    private static bool HasAnyDisplayValue(IReadOnlyList<RulesElement> candidates, string fieldName)
    {
        return candidates.Any(candidate =>
            candidate.Fields.TryGetValue(fieldName, out var value)
            && !string.IsNullOrWhiteSpace(value));
    }

    private static bool IsDisplayableFieldColumn(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName)
            || fieldName.StartsWith('_')
            || fieldName.StartsWith(' ')
            || SuppressedFieldColumns.Contains(fieldName))
        {
            return false;
        }

        return fieldName.Contains("Category", StringComparison.OrdinalIgnoreCase)
            || fieldName.EndsWith(" Type", StringComparison.OrdinalIgnoreCase)
            || fieldName.EndsWith(" Source", StringComparison.OrdinalIgnoreCase)
            || fieldName.EndsWith(" Usage", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatHeader(string fieldName)
    {
        if (fieldName.StartsWith('_'))
            fieldName = fieldName.TrimStart('_');
        return fieldName.Trim();
    }

    private static HashSet<string> GetCommonCategoryIds(IReadOnlyList<RulesElement> candidates)
    {
        var common = new HashSet<string>(GetUsefulCategoryIds(candidates[0]), StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates.Skip(1))
            common.IntersectWith(GetUsefulCategoryIds(candidate));

        return common;
    }

    private static IEnumerable<string> GetUsefulCategoryIds(RulesElement candidate)
        => candidate.Categories
            .Select(category => category.Trim())
            .Where(IsUsefulCategoryId);

    private static (string Id, string Name)? FindBestCategory(
        RulesElement candidate,
        HashSet<string> commonCategoryIds,
        ISet<string> excludedCategoryIds,
        Func<string, string?> resolveCategoryName)
    {
        (string Id, string Name, int Priority, int Index)? best = null;

        for (int i = 0; i < candidate.Categories.Count; i++)
        {
            var categoryId = candidate.Categories[i].Trim();
            if (!IsUsefulCategoryId(categoryId)
                || commonCategoryIds.Contains(categoryId)
                || excludedCategoryIds.Contains(categoryId))
            {
                continue;
            }

            var name = resolveCategoryName(categoryId);
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var priority = GetCategoryPriority(categoryId);
            if (best is null
                || priority < best.Value.Priority
                || (priority == best.Value.Priority && i < best.Value.Index))
            {
                best = (categoryId, name, priority, i);
            }
        }

        return best is null ? null : (best.Value.Id, best.Value.Name);
    }

    private static bool IsUsefulCategoryId(string category)
        => category.StartsWith("ID_", StringComparison.OrdinalIgnoreCase);

    private static int GetCategoryPriority(string categoryId)
    {
        if (categoryId.StartsWith("ID_FMP_CATEGORY_", StringComparison.OrdinalIgnoreCase))
            return 0;
        if (categoryId.StartsWith("ID_INTERNAL_CATEGORY_", StringComparison.OrdinalIgnoreCase))
            return 1;
        if (categoryId.StartsWith("ID_FMP_POWER_SOURCE_", StringComparison.OrdinalIgnoreCase)
            || categoryId.StartsWith("ID_FMP_ROLE_", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }
        if (categoryId.StartsWith("ID_FMP_CLASS_", StringComparison.OrdinalIgnoreCase)
            || categoryId.StartsWith("ID_FMP_RACE_", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        return 4;
    }
}
