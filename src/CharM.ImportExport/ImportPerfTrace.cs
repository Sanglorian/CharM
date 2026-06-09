using System.Diagnostics;

namespace CharM.ImportExport;

/// <summary>
/// Lightweight per-thread timing harness for diagnosing import bottlenecks.
/// Disabled by default — call <see cref="Begin"/> to start a session, then
/// <see cref="Mark"/> at phase boundaries inside the importer. <see cref="End"/>
/// returns the per-phase totals (in milliseconds).
///
/// Usage:
/// <code>
///   ImportPerfTrace.Begin();
///   var result = Dnd4eImporter.Import(stream, db);
///   foreach (var (phase, ms) in ImportPerfTrace.End())
///       Console.WriteLine($"  {phase,-30} {ms}ms");
/// </code>
/// </summary>
public static class ImportPerfTrace
{
    [ThreadStatic] private static Stopwatch? _phase;
    [ThreadStatic] private static Dictionary<string, long>? _totals;
    [ThreadStatic] private static string? _lastLabel;

    public static bool Enabled => _totals is not null;

    public static void Begin()
    {
        _phase = Stopwatch.StartNew();
        _totals = new Dictionary<string, long>(StringComparer.Ordinal);
        _lastLabel = null;
    }

    public static void Mark(string label)
    {
        if (_phase is null || _totals is null) return;
        _phase.Stop();
        if (_lastLabel is not null)
        {
            _totals.TryGetValue(_lastLabel, out var prior);
            _totals[_lastLabel] = prior + _phase.ElapsedMilliseconds;
        }
        _lastLabel = label;
        _phase.Restart();
    }

    public static IReadOnlyList<(string Phase, long Ms)> End()
    {
        if (_phase is null || _totals is null) return Array.Empty<(string, long)>();
        if (_lastLabel is not null)
        {
            _phase.Stop();
            _totals.TryGetValue(_lastLabel, out var prior);
            _totals[_lastLabel] = prior + _phase.ElapsedMilliseconds;
        }
        var result = _totals.Select(kv => (kv.Key, kv.Value)).ToList();
        _phase = null;
        _totals = null;
        _lastLabel = null;
        return result;
    }
}
