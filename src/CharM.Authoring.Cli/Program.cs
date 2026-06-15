using CharM.RulesDb.Authoring;
using CharM.RulesDb.Storage;

// charm-authoring — compile human-authored YAML rules content into a CharM rules.db.
//
//   charm-authoring build <content-path> -o <rules.db>   build a database (then self-check it)
//   charm-authoring lint  <content-path>                  parse + validate only, no output db
//
// <content-path> may be a single .yaml file or a directory (searched recursively).

return Run(args);

static int Run(string[] args)
{
    if (args.Length == 0)
        return Usage();

    var command = args[0];
    var rest = args[1..];

    try
    {
        return command switch
        {
            "build" => Build(rest),
            "lint" => Lint(rest),
            "-h" or "--help" or "help" => Usage(),
            _ => Fail($"unknown command '{command}'."),
        };
    }
    catch (AuthoringException ex)
    {
        Console.Error.WriteLine($"error: {ex.Message}");
        return 1;
    }
}

static int Build(string[] args)
{
    string? content = null;
    string? output = null;
    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "-o" or "--output":
                if (++i >= args.Length) return Fail("-o requires a path.");
                output = args[i];
                break;
            default:
                content ??= args[i];
                break;
        }
    }

    if (content is null) return Fail("build requires a content path.");
    output ??= "rules.db";

    var result = AuthoringCompiler.Compile(content, output);
    Console.WriteLine($"Compiled {result.ElementCount} element(s) -> {output}");

    foreach (var warning in result.Warnings)
        Console.WriteLine($"  warning: {warning}");
    if (result.Warnings.Count > 0)
        Console.WriteLine($"  ({result.Warnings.Count} warning(s))");

    // Self-check: reopen the database through the same reader the app uses and
    // confirm it loads, so a successful build is proof the format round-trips.
    using var db = new RulesDatabase(output);
    var types = db.GetDistinctTypes();
    Console.WriteLine($"Verified: {db.Count} element(s) across {types.Count} type(s) load cleanly.");
    return 0;
}

static int Lint(string[] args)
{
    if (args.Length == 0) return Fail("lint requires a content path.");
    var result = AuthoringCompiler.Lint(args[0]);
    foreach (var warning in result.Warnings)
        Console.WriteLine($"  warning: {warning}");
    Console.WriteLine($"OK: {result.ElementCount} element(s) parsed, {result.Warnings.Count} warning(s).");
    return 0;
}

static int Usage()
{
    Console.WriteLine(
        """
        charm-authoring — compile authored YAML rules content into a CharM rules.db

        Usage:
          charm-authoring build <content-path> -o <rules.db>
          charm-authoring lint  <content-path>

        <content-path> may be a single .yaml file or a directory (searched recursively).
        """);
    return 0;
}

static int Fail(string message)
{
    Console.Error.WriteLine($"error: {message}");
    return 1;
}
