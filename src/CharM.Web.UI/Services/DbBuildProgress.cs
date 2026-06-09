namespace CharM.Web.Services;

/// <summary>
/// Structured progress update emitted by long-running rules-database
/// operations (import + part merge). Consumers render a determinate
/// progress bar when both <see cref="Current"/> and <see cref="Total"/>
/// are set, indeterminate otherwise.
/// </summary>
/// <param name="Phase">Human-readable phase name, e.g. "Importing rules".</param>
/// <param name="Current">Current item count, or null for indeterminate phases.</param>
/// <param name="Total">Total item count, or null when unknown.</param>
/// <param name="Detail">Optional secondary detail, e.g. the current file name.</param>
public sealed record DbBuildProgress(
    string Phase,
    int? Current = null,
    int? Total = null,
    string? Detail = null);
