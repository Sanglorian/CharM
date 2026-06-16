using CharM.Engine.Creation;
using CharM.Engine.Powers;
using CharM.Engine.Rules;
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
            "playtest" => Playtest(rest),
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

// Headless character-build smoke test: build a level-1 character against a
// compiled database, auto-resolving every choice with its first candidate, then
// print the computed stats and any slots that had no candidates (the gaps that
// reveal engine-vocabulary misalignment).
static int Playtest(string[] args)
{
    string? dbPath = null, race = null, cls = null;
    int level = 1;
    int[]? scores = null;
    string? talentHint = null;
    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--race": race = args[++i]; break;
            case "--class": cls = args[++i]; break;
            case "--level": level = int.Parse(args[++i]); break;
            case "--scores": // STR,CON,DEX,INT,WIS,CHA
                scores = args[++i].Split(',').Select(int.Parse).ToArray();
                break;
            case "--talent":
            case "--pick": talentHint = args[++i]; break;
            default: dbPath ??= args[i]; break;
        }
    }
    if (dbPath is null) return Fail("playtest requires a database path.");
    race ??= "Humanity";
    cls ??= "Guardian";
    scores ??= [16, 14, 13, 10, 12, 8];

    using var db = new RulesDatabase(dbPath);
    db.Preload();

    var session = new CharacterSession(
        db.FindByInternalId,
        db.FindByNameAndType,
        db.FindByType,
        db.FindByTypeAndSource,
        level: level,
        autoFillSelectDefaults: false);

    // A plausible Strength-based defender array.
    session.SetAbilityScores(new AbilityScoreSet
    {
        [Ability.Strength] = scores[0],
        [Ability.Constitution] = scores[1],
        [Ability.Dexterity] = scores[2],
        [Ability.Intelligence] = scores[3],
        [Ability.Wisdom] = scores[4],
        [Ability.Charisma] = scores[5],
    });

    var raceEl = db.FindByNameAndType(race, "Race");
    var clsEl = db.FindByNameAndType(cls, "Class");
    if (raceEl is null) return Fail($"race '{race}' not found in database.");
    if (clsEl is null) return Fail($"class '{cls}' not found in database.");
    Console.WriteLine($"Building: level {level} {race} {cls} (base Str {scores[0]}, Con {scores[1]}, Dex {scores[2]}, Int {scores[3]}, Wis {scores[4]}, Cha {scores[5]})\n");
    Console.WriteLine($"  pending after scores: {Describe(session.GetAllPendingChoices())}");
    bool rOk = session.TryMakeChoice(raceEl);
    Console.WriteLine($"  TryMakeChoice(race={race}) -> {rOk}; pending: {Describe(session.GetAllPendingChoices())}");
    bool cOk = session.TryMakeChoice(clsEl);
    Console.WriteLine($"  TryMakeChoice(class={cls}) -> {cOk}; pending: {Describe(session.GetAllPendingChoices())}\n");

    // Auto-resolve choices: one pick per pass, re-fetching pending each time.
    var used = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    var gaps = new List<string>();
    for (int iter = 0; iter < 300; iter++)
    {
        var pending = session.GetAllPendingChoices();
        if (pending.Count == 0) break;

        bool progressed = false;
        foreach (var pc in pending)
        {
            var label = pc.Slot.Name ?? pc.Slot.DisplayLabel ?? pc.Slot.ElementType;
            var candidates = session.GetCandidatesForSlot(pc.Slot, skipPrereqs: true);
            if (candidates.Count == 0)
                continue;
            if (!used.TryGetValue(label, out var u)) { u = new(StringComparer.Ordinal); used[label] = u; }
            RulesElement? pick = null;
            if (talentHint is not null)
                pick = candidates.FirstOrDefault(c =>
                    c.Name.Contains(talentHint, StringComparison.OrdinalIgnoreCase) && !u.Contains(c.InternalId));
            pick ??= candidates.FirstOrDefault(c => !u.Contains(c.InternalId)) ?? candidates[0];
            try
            {
                session.MakeChoice(pc.Slot, pick);
                u.Add(pick.InternalId);
                Console.WriteLine($"  chose {pick.Name,-24} for [{label}]");
                progressed = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ! MakeChoice failed for [{label}]: {ex.Message}");
            }
            break;
        }
        if (!progressed) break;
    }

    // Any pending slots left with no candidates are the gaps.
    foreach (var pc in session.GetAllPendingChoices())
    {
        var label = pc.Slot.Name ?? pc.Slot.DisplayLabel ?? pc.Slot.ElementType;
        var n = session.GetCandidatesForSlot(pc.Slot, skipPrereqs: true).Count;
        gaps.Add($"{label} (type='{pc.Slot.ElementType}', category='{pc.Slot.Category}', remaining={pc.Slot.Remaining}, candidates={n})");
    }

    Console.WriteLine($"\nComplete: {session.IsComplete}");
    foreach (var t in new[] { "Race", "Crux", "Heritage", "Class", "Class Feature", "Skill Training", "Power", "Race Ability Bonus" })
    {
        var picks = session.GetSelectedElements(t);
        if (picks.Count > 0)
            Console.WriteLine($"  {t}: {string.Join(", ", picks.Select(p => p.Name))}");
    }

    if (gaps.Count > 0)
    {
        Console.WriteLine($"\nUnfillable slots ({gaps.Count}) — engine-alignment gaps:");
        foreach (var g in gaps) Console.WriteLine($"  - {g}");
    }

    var snapshot = session.GetSnapshot() ?? session.GetPartialSnapshot();
    if (snapshot is null)
    {
        Console.WriteLine("\nNo snapshot could be built.");
        return 0;
    }

    Console.WriteLine("\nComputed stats:");
    foreach (var stat in new[]
    {
        "Strength", "Constitution", "Dexterity", "Intelligence", "Wisdom", "Charisma",
        "Hit Points", "Healing Surges", "Speed", "AC",
        "Fortitude Defense", "Reflex Defense", "Will Defense",
    })
    {
        Console.WriteLine($"  {stat,-16} {snapshot.GetStat(stat)}");
    }

    var trained = snapshot.GetAllStats()
        .Where(kv => kv.Key.EndsWith(" Trained", StringComparison.Ordinal) && kv.Value != 0)
        .OrderBy(kv => kv.Key);
    var trainedList = string.Join(", ", trained.Select(kv => kv.Key.Replace(" Trained", "")));
    Console.WriteLine($"  Trained skills:  {(trainedList.Length > 0 ? trainedList : "(none)")}");

    // Power cards: compute the attack ability the engine actually resolves
    // (this is where the Orcus "use the higher ability" rule shows up) plus
    // attack bonus, defense and damage. NO weapon/implement is supplied, to
    // prove the ability resolution is equipment-independent (the substitution
    // rides on the character's ChosenAbilities, not on gear). Weapon dice /
    // proficiency are therefore omitted — the ability resolution is the point.
    Console.WriteLine("\nPower cards (no weapon/implement; equipment-independent ability resolution):");
    foreach (var picked in session.GetSelectedElements("Power"))
    {
        var power = db.FindByInternalId(picked.InternalId) ?? picked;
        if (PowerFieldParser.GetAttackText(power) is null)
        {
            Console.WriteLine($"  {power.Name,-22} (no attack line)");
            continue;
        }
        var card = PowerStatCalculator.Calculate(power, snapshot.Builder.Stats, weapon: null, characterLevel: level,
            sourceElementResolver: db.FindByInternalId);
        var dmg = string.IsNullOrWhiteSpace(card.DamageExpression) ? "-" : card.DamageExpression;
        var atk = card.ResolvedAttackStat.Length > 0 ? card.ResolvedAttackStat : "(none)";
        Console.WriteLine($"  {power.Name,-22} attack {atk} {card.AttackBonus:+0;-0} vs {card.Defense ?? "-"}; damage {dmg}");
    }
    return 0;
}

static string Describe(System.Collections.Generic.IReadOnlyList<PendingChoice> pending)
{
    if (pending.Count == 0) return "(none)";
    return string.Join("; ", pending.Select(p =>
        $"{p.Slot.ElementType}×{p.Slot.Remaining}"));
}

static int Usage()
{
    Console.WriteLine(
        """
        charm-authoring — compile authored YAML rules content into a CharM rules.db

        Usage:
          charm-authoring build    <content-path> -o <rules.db>
          charm-authoring lint     <content-path>
          charm-authoring playtest <rules.db> [--race <name>] [--class <name>]

        <content-path> may be a single .yaml file or a directory (searched recursively).
        playtest builds a level-1 character headlessly and prints computed stats.
        """);
    return 0;
}

static int Fail(string message)
{
    Console.Error.WriteLine($"error: {message}");
    return 1;
}
