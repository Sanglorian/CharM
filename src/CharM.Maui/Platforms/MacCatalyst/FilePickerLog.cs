using System;

namespace CharM.Maui;

/// <summary>
/// Lightweight logging helper for the Mac Catalyst file-picker debug
/// pass. <see cref="Console.WriteLine"/> on Mac Catalyst routes through
/// stderr into the unified log at <em>default</em> level, so messages
/// appear in <c>Console.app</c> and <c>log stream --predicate
/// 'process == "CharM.Maui"'</c> without needing <c>--level debug</c>.
/// All messages share a fixed prefix so they can be filtered cheaply
/// (e.g. <c>log stream ... | grep CharM.FilePicker</c>).
/// </summary>
internal static class FilePickerLog
{
    public const string Tag = "[CharM.FilePicker]";

    public static void Info(string message)
        => Console.WriteLine($"{Tag} {message}");

    public static void Info(string scope, string message)
        => Console.WriteLine($"{Tag} [{scope}] {message}");

    public static void Warn(string scope, string message)
        => Console.WriteLine($"{Tag} [{scope}] WARN: {message}");

    public static void Error(string scope, string message, Exception? ex = null)
        => Console.WriteLine(ex is null
            ? $"{Tag} [{scope}] ERROR: {message}"
            : $"{Tag} [{scope}] ERROR: {message} :: {ex.GetType().Name}: {ex.Message}");
}
