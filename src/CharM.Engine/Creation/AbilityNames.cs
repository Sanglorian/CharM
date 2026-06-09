namespace CharM.Engine.Creation;

public static class AbilityNames
{
    public static readonly Ability[] StandardOrder =
    [
        Ability.Strength,
        Ability.Constitution,
        Ability.Dexterity,
        Ability.Intelligence,
        Ability.Wisdom,
        Ability.Charisma,
    ];

    public static readonly string[] FullNames =
        StandardOrder.Select(GetFullName).ToArray();

    public static bool TryParse(string? text, out Ability ability)
    {
        switch (text?.Trim().ToLowerInvariant())
        {
            case "strength":
            case "str":
                ability = Ability.Strength;
                return true;
            case "constitution":
            case "con":
                ability = Ability.Constitution;
                return true;
            case "dexterity":
            case "dex":
                ability = Ability.Dexterity;
                return true;
            case "intelligence":
            case "int":
                ability = Ability.Intelligence;
                return true;
            case "wisdom":
            case "wis":
                ability = Ability.Wisdom;
                return true;
            case "charisma":
            case "cha":
                ability = Ability.Charisma;
                return true;
            default:
                ability = default;
                return false;
        }
    }

    public static string GetFullName(Ability ability) => ability switch
    {
        Ability.Strength => "Strength",
        Ability.Constitution => "Constitution",
        Ability.Dexterity => "Dexterity",
        Ability.Intelligence => "Intelligence",
        Ability.Wisdom => "Wisdom",
        Ability.Charisma => "Charisma",
        _ => ability.ToString(),
    };

    public static string GetAbbreviation(Ability ability) => ability switch
    {
        Ability.Strength => "Str",
        Ability.Constitution => "Con",
        Ability.Dexterity => "Dex",
        Ability.Intelligence => "Int",
        Ability.Wisdom => "Wis",
        Ability.Charisma => "Cha",
        _ => ability.ToString(),
    };

    public static string Normalize(string text)
        => TryParse(text, out var ability) ? GetFullName(ability) : text;
}
