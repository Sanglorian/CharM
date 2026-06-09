using System.Text;

namespace CharM.Engine.Orchestration;

/// <summary>
/// Result of validating computed stats against expected values from a .dnd4e file.
/// </summary>
public sealed class ValidationResult
{
    public string CharacterName { get; init; } = "";
    public int CharacterLevel { get; init; }
    public int TotalStats { get; init; }
    public int MatchedStats { get; init; }
    public int MismatchedStats { get; init; }
    public int MissingStats { get; init; }
    public int ExtraStats { get; init; }

    public List<StatMismatch> Mismatches { get; } = [];

    public double MatchPercentage => TotalStats > 0 ? (double)MatchedStats / TotalStats * 100 : 0;
    public bool IsFullMatch => MismatchedStats == 0 && MissingStats == 0;

    /// <summary>Format a human-readable report.</summary>
    public string ToReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== {CharacterName} (Level {CharacterLevel}) ===");
        sb.AppendLine($"Stats: {MatchedStats}/{TotalStats} matched ({MatchPercentage:F1}%)");
        sb.AppendLine($"Mismatched: {MismatchedStats}");
        sb.AppendLine($"Missing: {MissingStats}");
        sb.AppendLine($"Extra: {ExtraStats}");

        if (Mismatches.Count > 0)
        {
            sb.AppendLine();
            foreach (var m in Mismatches)
            {
                string errata = m.IsKnownErrata ? " [KNOWN ERRATA]" : "";
                sb.AppendLine(
                    $"MISMATCH: {m.StatName} — Expected: {m.ExpectedValue}, " +
                    $"Computed: {m.ComputedValue}, Delta: {m.Delta:+0;-0;0}{errata}");

                if (m.ExpectedContributions.Count > 0)
                {
                    sb.AppendLine("  Expected contributions:");
                    foreach (var c in m.ExpectedContributions)
                        sb.AppendLine($"    {c}");
                }

                if (m.ComputedContributions.Count > 0)
                {
                    sb.AppendLine("  Computed contributions:");
                    foreach (var c in m.ComputedContributions)
                        sb.AppendLine($"    {c}");
                }
            }
        }

        return sb.ToString();
    }
}

/// <summary>
/// Details of a single stat mismatch between computed and expected values.
/// </summary>
public sealed class StatMismatch
{
    public required string StatName { get; init; }
    public int ExpectedValue { get; init; }
    public int ComputedValue { get; init; }
    public int Delta => ComputedValue - ExpectedValue;

    /// <summary>Expected contributions from the .dnd4e file.</summary>
    public List<string> ExpectedContributions { get; } = [];

    /// <summary>Computed contributions from our engine.</summary>
    public List<string> ComputedContributions { get; } = [];

    /// <summary>Whether this mismatch is marked as known errata.</summary>
    public bool IsKnownErrata { get; set; }
}
