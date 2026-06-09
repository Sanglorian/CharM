namespace CharM.Web.Services;

public static class RulesDatabasePathResolver
{
    public static IEnumerable<string> GetStartupCandidates(
        string? configuredPath,
        IEnumerable<string> commandLineArgs,
        bool includeCurrentDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
            yield return Path.GetFullPath(configuredPath);

        foreach (var argPath in ParseCommandLinePaths(commandLineArgs))
            yield return Path.GetFullPath(argPath);

        if (includeCurrentDirectory)
            yield return Path.Combine(Directory.GetCurrentDirectory(), "rules.db");

        yield return Path.Combine(AppContext.BaseDirectory, "rules.db");

        foreach (var path in GetAncestorRulesDbCandidates(Directory.GetCurrentDirectory()))
            yield return path;

        foreach (var path in GetAncestorRulesDbCandidates(AppContext.BaseDirectory))
            yield return path;

        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localData))
            yield return Path.Combine(localData, "CharM", "rules.db");
    }

    public static string GetDefaultWorkingDirectory()
    {
        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localData))
            return Path.Combine(localData, "CharM");

        return Path.Combine(AppContext.BaseDirectory, "rules-data");
    }

    private static IEnumerable<string> ParseCommandLinePaths(IEnumerable<string> args)
    {
        string? pendingNamedPath = null;
        foreach (var arg in args)
        {
            if (pendingNamedPath is not null)
            {
                pendingNamedPath = null;
                if (!string.IsNullOrWhiteSpace(arg))
                    yield return arg;
                continue;
            }

            if (string.Equals(arg, "--rules", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "--rules-db", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "--rulesdb", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "--rules-db-path", StringComparison.OrdinalIgnoreCase))
            {
                pendingNamedPath = arg;
                continue;
            }

            var equalsIndex = arg.IndexOf('=');
            if (equalsIndex > 0)
            {
                var name = arg[..equalsIndex];
                if (string.Equals(name, "--rules", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "--rules-db", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "--rulesdb", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "--rules-db-path", StringComparison.OrdinalIgnoreCase))
                {
                    var value = arg[(equalsIndex + 1)..];
                    if (!string.IsNullOrWhiteSpace(value))
                        yield return value;
                    continue;
                }
            }

            if (arg.EndsWith(".db", StringComparison.OrdinalIgnoreCase)
                || arg.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase)
                || arg.EndsWith(".sqlite3", StringComparison.OrdinalIgnoreCase))
            {
                yield return arg;
            }
        }
    }

    private static IEnumerable<string> GetAncestorRulesDbCandidates(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        for (var depth = 0; directory is not null && depth < 6; depth++, directory = directory.Parent)
            yield return Path.Combine(directory.FullName, "rules.db");
    }
}
