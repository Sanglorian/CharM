using CharM.Engine.Rules;

namespace CharM.Engine.Creation;

public enum RitualPracticeAlchemyKind
{
    Ritual,
    RitualScroll,
    AlchemicalFormula,
    AlchemicalItem,
    PoisonRecipe,
    MartialPractice,
}

public sealed record RitualPracticeAlchemyEntry(
    RulesElement Element,
    RitualPracticeAlchemyKind Kind,
    int Quantity,
    bool IsInventoryItem);

public static class RitualPracticeAlchemyClassifier
{
    public static RitualPracticeAlchemyKind? GetKind(RulesElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (IsRitualScroll(element))
            return RitualPracticeAlchemyKind.RitualScroll;

        if (IsAlchemicalItem(element))
            return RitualPracticeAlchemyKind.AlchemicalItem;

        if (IsPoisonRecipe(element))
            return RitualPracticeAlchemyKind.PoisonRecipe;

        if (IsMartialPractice(element))
            return RitualPracticeAlchemyKind.MartialPractice;

        if (IsRitual(element))
            return IsAlchemicalFormula(element)
                ? RitualPracticeAlchemyKind.AlchemicalFormula
                : RitualPracticeAlchemyKind.Ritual;

        return null;
    }

    public static bool IsRitualPracticeAlchemyElement(RulesElement element)
        => GetKind(element) is not null;

    public static bool IsInventoryAddCandidate(RulesElement element)
    {
        var kind = GetKind(element);
        return kind is RitualPracticeAlchemyKind.Ritual
            or RitualPracticeAlchemyKind.RitualScroll
            or RitualPracticeAlchemyKind.AlchemicalFormula
            or RitualPracticeAlchemyKind.AlchemicalItem;
    }

    public static bool IsAlchemicalItem(RulesElement element)
        => string.Equals(element.Type, "Magic Item", StringComparison.OrdinalIgnoreCase)
           && element.Fields.TryGetValue("Magic Item Type", out var magicItemType)
           && string.Equals(magicItemType.Trim(), "Alchemical", StringComparison.OrdinalIgnoreCase);

    public static int KindSortKey(RitualPracticeAlchemyKind kind)
        => kind switch
        {
            RitualPracticeAlchemyKind.Ritual => 0,
            RitualPracticeAlchemyKind.AlchemicalFormula => 1,
            RitualPracticeAlchemyKind.AlchemicalItem => 2,
            RitualPracticeAlchemyKind.RitualScroll => 3,
            RitualPracticeAlchemyKind.PoisonRecipe => 4,
            RitualPracticeAlchemyKind.MartialPractice => 5,
            _ => 99,
        };

    private static bool IsRitual(RulesElement element)
        => string.Equals(element.Type, "Ritual", StringComparison.OrdinalIgnoreCase);

    private static bool IsRitualScroll(RulesElement element)
        => string.Equals(element.Type, "Ritual Scroll", StringComparison.OrdinalIgnoreCase);

    private static bool IsPoisonRecipe(RulesElement element)
        => string.Equals(element.Type, "Class Feature", StringComparison.OrdinalIgnoreCase)
           && (element.Categories.Any(IsAssassinPoisonCategory)
               || element.Name.EndsWith(" Recipe", StringComparison.OrdinalIgnoreCase));

    private static bool IsMartialPractice(RulesElement element)
        => ContainsAny(element.Name, "Martial Practice")
           || element.Categories.Any(c => c.Contains("MARTIAL_PRACTICE", StringComparison.OrdinalIgnoreCase))
           || element.Fields.Values.Any(v => ContainsAny(v, "Martial Practice"));

    private static bool IsAlchemicalFormula(RulesElement element)
    {
        if (!IsRitual(element))
            return false;

        return element.Categories.Any(IsAlchemyCategory)
            || ContainsAny(element.Name, "Alchemist", "Alchemical", "Formula", "Poultice", "Powder")
            || element.Fields.Values.Any(v => ContainsAny(v, "Alchemy", "Alchemical", "alchemical item"));
    }

    private static bool IsAssassinPoisonCategory(string category)
        => category.Contains("Assassin Poison", StringComparison.OrdinalIgnoreCase);

    private static bool IsAlchemyCategory(string category)
        => category.Contains("ALCHEM", StringComparison.OrdinalIgnoreCase)
           || category.Contains("Assassin Poison", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAny(string? value, params string[] needles)
        => !string.IsNullOrWhiteSpace(value)
           && needles.Any(n => value.Contains(n, StringComparison.OrdinalIgnoreCase));
}
