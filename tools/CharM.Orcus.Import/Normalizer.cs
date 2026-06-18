using System.Text;

namespace CharM.Orcus.Import;

/// <summary>
/// Text normalization shared by the source side and the YAML side, so that
/// comparison ignores formatting (markdown emphasis, smart quotes, blockquote
/// markers, bullet glyphs, punctuation, whitespace, case) but preserves the
/// actual words and numbers. A faithful copy survives normalization intact; a
/// reworded or fabricated one does not.
/// </summary>
public static class Normalizer
{
    public static string Norm(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            char c = ch switch
            {
                '’' or '‘' or 'ʼ' => '\'',
                '“' or '”' => '"',
                '–' or '—' or '−' => '-',
                ' ' => ' ',
                _ => ch,
            };
            // Keep only letters and digits; everything else becomes a space.
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            else sb.Append(' ');
        }
        // Collapse runs of whitespace.
        var parts = sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts);
    }

    /// <summary>Number of whitespace-delimited tokens in a normalized string.</summary>
    public static int WordCount(string norm) =>
        string.IsNullOrEmpty(norm) ? 0 : norm.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>
    /// Split a raw field value into sentence-like chunks for pinpointing which
    /// part of a field diverges. Splits on sentence and clause boundaries.
    /// </summary>
    public static IEnumerable<string> Chunks(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) yield break;
        foreach (var piece in s.Split(new[] { '.', ';', '\n', '•', '●' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var t = piece.Trim();
            if (t.Length > 0) yield return t;
        }
    }
}
