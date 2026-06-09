using CharM.Engine.Creation;
using CharM.RulesDb.Storage;

namespace CharM.ImportExport.SummaryText.Extensions;

/// <summary>
/// Optional CharM-specific section appended AFTER the OCB end marker.
/// Legacy OCB stops reading at the end marker, so anything we write here is
/// forward-compatible — older tools see and ignore it.
///
/// Currently emits a simple "key: value" line block headed by the
/// versioned banner. Reserved for things OCB didn't carry (portrait,
/// money tracking, persisted current HP / surges / power-points, deity
/// when not surfaced by ClassChoices, etc.).
/// </summary>
internal static class CharMExtensionsBlock
{
    public const string Header = "====== CharM Extensions v1 ======\r\n";

    public static string Write(CharacterSession session, IRulesDatabase database)
    {
        var lines = new List<string>();

        foreach (var (k, v) in session.Details.OrderBy(d => d.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(v)) continue;
            lines.Add($"{k}: {v}");
        }

        var skip = new HashSet<string>(StringComparer.Ordinal) { "Name" };
        foreach (var (k, v) in session.TextStrings.OrderBy(d => d.Key, StringComparer.Ordinal))
        {
            if (skip.Contains(k)) continue;
            if (string.IsNullOrWhiteSpace(v)) continue;
            lines.Add($"TextString[{k}]: {v}");
        }

        if (lines.Count == 0) return string.Empty;
        return Header + string.Join(SummaryBlock.Newline, lines) + SummaryBlock.Newline;
    }

    /// <summary>
    /// Split the extensions section off the tail of <paramref name="text"/>.
    /// On return, <paramref name="text"/> is the OCB portion (up to and
    /// including the end marker); the captured extensions body is returned
    /// for parsing by the importer.
    /// </summary>
    public static string? SplitExtensions(ref string text)
    {
        int idx = text.IndexOf(Header, StringComparison.Ordinal);
        if (idx == -1) return null;

        string ext = text[(idx + Header.Length)..];
        text = text[..idx];
        return ext;
    }
}
