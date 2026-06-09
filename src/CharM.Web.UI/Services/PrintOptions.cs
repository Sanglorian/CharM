namespace CharM.Web.Services;

/// <summary>
/// User-selected options from the Print Options modal. Marshalled into
/// query-string params on the /print/character URL so the print page is
/// stateless and can be bookmarked / refreshed.
/// </summary>
public sealed record PrintOptions(
    string Color,
    string Size,
    bool Inventory,
    bool Blanks,
    bool CutMarks)
{
    public string ToQueryString()
        => $"?color={Color}&size={Size}"
         + $"&inventory={(Inventory ? 1 : 0)}"
         + $"&blanks={(Blanks ? 1 : 0)}"
         + $"&cuts={(CutMarks ? 1 : 0)}";

    public static PrintOptions FromQuery(
        string? color, string? size, string? inventory, string? blanks, string? cuts)
        => new(
            Color: NormalizeColor(color),
            Size: NormalizeSize(size),
            Inventory: ParseBool(inventory),
            Blanks: ParseBool(blanks, defaultValue: true),
            CutMarks: ParseBool(cuts, defaultValue: true));

    private static string NormalizeColor(string? value) => value?.ToLowerInvariant() switch
    {
        "bw" or "muted" => value.ToLowerInvariant(),
        _ => "color",
    };

    private static string NormalizeSize(string? value) => value?.ToLowerInvariant() switch
    {
        "a4" => "a4",
        _ => "letter",
    };

    private static bool ParseBool(string? value, bool defaultValue = false)
        => value switch
        {
            "1" or "true" or "yes" or "on" => true,
            "0" or "false" or "no" or "off" => false,
            _ => defaultValue,
        };
}
