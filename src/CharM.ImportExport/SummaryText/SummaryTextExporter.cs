using CharM.Engine.Creation;
using CharM.RulesDb.Storage;

namespace CharM.ImportExport.SummaryText;

/// <summary>
/// Renders a populated <see cref="CharacterSession"/> as an OCB-compatible
/// SummaryText string suitable for pasting into a forum or the legacy
/// CharacterBuilder's clipboard import.
///
/// The existing OCB sections are emitted byte-identical to OCB output
/// (anchored by the user-supplied fixtures in <c>txt/</c>). A CharM-specific
/// extensions block may be appended after the OCB end marker (legacy OCB
/// stops reading there so extensions are forward-compatible).
/// </summary>
public static class SummaryTextExporter
{
    /// <summary>Render the character to OCB SummaryText.</summary>
    public static string Export(
        CharacterSession session,
        IRulesDatabase database,
        SummaryTextExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(database);
        options ??= SummaryTextExportOptions.Default;

        string body = SummaryTextDriver.Write(session, database);

        if (options.IncludeCharMExtensions)
        {
            body += Extensions.CharMExtensionsBlock.Write(session, database);
        }

        return body;
    }

    /// <summary>Write the rendered SummaryText to <paramref name="path"/> (UTF-8 no BOM).</summary>
    public static void ExportToFile(
        CharacterSession session,
        IRulesDatabase database,
        string path,
        SummaryTextExportOptions? options = null)
    {
        File.WriteAllText(path, Export(session, database, options), new System.Text.UTF8Encoding(false));
    }
}

/// <summary>Options controlling <see cref="SummaryTextExporter"/>.</summary>
public sealed record SummaryTextExportOptions
{
    /// <summary>
    /// When true (default) append a CharM Extensions v1 block after the
    /// OCB end marker. Set false for byte-equality testing against legacy
    /// OCB fixtures.
    /// </summary>
    public bool IncludeCharMExtensions { get; init; } = true;

    public static readonly SummaryTextExportOptions Default = new();
    public static readonly SummaryTextExportOptions LegacyOnly = new() { IncludeCharMExtensions = false };
}
