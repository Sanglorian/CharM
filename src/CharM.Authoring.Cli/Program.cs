using CharM.Engine.Creation;
using CharM.Engine.Orchestration;
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
    bool demoSwaps = false, featsAndKits = false;
    string? weaponName = null, armorName = null, shieldName = null, kitName = null, focusName = null;
    var magicNames = new List<string>();
    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--race": race = args[++i]; break;
            case "--class": cls = args[++i]; break;
            case "--swaps": demoSwaps = true; break;
            case "--weapon": weaponName = args[++i]; break;
            case "--armor": armorName = args[++i]; break;
            case "--shield": shieldName = args[++i]; break;
            case "--magic": magicNames.Add(args[++i]); break;
            case "--focus": focusName = args[++i]; break;
            case "--kit": kitName = args[++i]; break;
            case "--feats-and-kits": featsAndKits = true; break;
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
    Console.WriteLine($"  TryMakeChoice(class={cls}) -> {cOk}; pending: {Describe(session.GetAllPendingChoices())}");

    // House rule + kit: applied before auto-resolve so the heroic feat slots see
    // the "Has Kit" marker / "Feats and Kits" house rule when deciding to appear.
    if (featsAndKits)
    {
        var hr = db.FindByNameAndType("Feats and Kits", "House Rule");
        if (hr is not null) { session.AddUserEditPick(hr); Console.WriteLine("  house rule: Feats and Kits ON"); }
    }
    if (kitName is not null)
    {
        var kitEl = db.FindByNameAndType(kitName, "Theme");
        if (kitEl is null) Console.WriteLine($"  ! kit '{kitName}' not found");
        else Console.WriteLine($"  TryMakeChoice(kit={kitName}) -> {session.TryMakeChoice(kitEl)}");
    }
    Console.WriteLine();

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
            // Don't auto-pick the optional kit (Theme slot) — the default
            // character takes feats; a kit is selected explicitly via --kit.
            if (string.Equals(pc.Slot.ElementType, "Theme", StringComparison.OrdinalIgnoreCase))
                continue;
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

    // Any pending slots left with no candidates are the gaps. The optional Kit
    // (Theme) slot is intentionally not auto-filled, so it isn't a gap.
    foreach (var pc in session.GetAllPendingChoices())
    {
        if (string.Equals(pc.Slot.ElementType, "Theme", StringComparison.OrdinalIgnoreCase))
            continue;
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

    // --swaps: demonstrate the power-replacement (retraining) swap chain that the
    // classes declare via `replace` directives. The engine's headless builder
    // doesn't auto-generate candidates from a directive's PowerSwap (that wiring
    // is deferred), so we drive the working ElementReplacement API directly: at
    // each replacement level (encounter at 13/17/23/27, daily at 15/19/25/29) we
    // drop one class attack power of that frequency and grant a higher-level one
    // of the same frequency from the same discipline pool.
    if (demoSwaps && level >= 13)
    {
        Console.WriteLine("\nPower-replacement swaps (--swaps):");
        var allPowers = db.FindByType("Power").ToList();
        string? Field(RulesElement e, string f) => e.Fields.TryGetValue(f, out var v) ? v : null;
        bool IsAttack(RulesElement e) => string.Equals(Field(e, "Power Type"), "Attack", StringComparison.OrdinalIgnoreCase);
        bool FreqIs(RulesElement e, string freq) => string.Equals(Field(e, "Power Usage"), freq, StringComparison.OrdinalIgnoreCase);
        List<string> Disc(RulesElement e) => e.Categories.Where(c => c.StartsWith("ORCUS_DISCIPLINE_", StringComparison.Ordinal)).ToList();
        int Lvl(RulesElement e) => int.TryParse(Field(e, "Level"), out var n) ? n : 0;

        // Working set of the character's class attack powers; updated as we swap
        // (so a later level can re-swap a power gained from an earlier swap — the
        // engine applies chained replacements in level order).
        var held = session.GetSelectedElements("Power")
            .Select(p => db.FindByInternalId(p.InternalId) ?? p)
            .Where(p => IsAttack(p) && Disc(p).Count > 0)
            .ToList();

        foreach (var (swapLevel, freq) in new[]
        {
            (13, "Encounter"), (15, "Daily"), (17, "Encounter"), (19, "Daily"),
            (23, "Encounter"), (25, "Daily"), (27, "Encounter"), (29, "Daily"),
        })
        {
            if (swapLevel > level) continue;
            // Old: the lowest-level class attack power of this frequency we hold.
            var old = held.Where(p => FreqIs(p, freq)).OrderBy(Lvl).FirstOrDefault();
            if (old is null) { Console.WriteLine($"  Lv {swapLevel}: no {freq.ToLower()} attack power to swap"); continue; }
            // The character's disciplines (a swap may pull from any of them).
            var myDiscs = held.SelectMany(Disc).ToHashSet(StringComparer.Ordinal);
            var heldNames = held.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            // New: highest-level same-frequency attack from one of your disciplines,
            // <= the level being reached, that we don't already hold — and only if
            // it is a genuine upgrade over the power being given up.
            var rep = allPowers
                .Where(p => IsAttack(p) && FreqIs(p, freq) && Lvl(p) <= swapLevel
                    && Disc(p).Any(myDiscs.Contains) && !heldNames.Contains(p.Name))
                .OrderByDescending(Lvl).FirstOrDefault();
            if (rep is null || Lvl(rep) <= Lvl(old))
            {
                Console.WriteLine($"  Lv {swapLevel}: no higher-level {freq.ToLower()} power available to swap into");
                continue;
            }

            session.AddReplacement(swapLevel, new ElementReplacement(old.InternalId, rep.InternalId));
            held.Remove(old);
            held.Add(rep);
            Console.WriteLine($"  Lv {swapLevel}: {old.Name} (Lv {Lvl(old)}) -> {rep.Name} (Lv {Lvl(rep)})");
        }
    }

    // --weapon/--armor/--shield: equip gear and let it flow into the build, so
    // armor's AC/skill statadds apply and the equipped weapon supplies the [W]
    // die for power damage. (Heavy armor suppresses the Dex/Int AC bonus via the
    // bootstrap's `notWearing: armor:heavy` conditions.)
    RulesElement? equippedWeapon = null;
    if (weaponName is not null)
    {
        equippedWeapon = db.FindByNameAndType(weaponName, "Weapon");
        if (equippedWeapon is null) Console.WriteLine($"\n  ! weapon '{weaponName}' not found");
        else session.EquipItem("Main Hand", equippedWeapon);
    }
    if (armorName is not null)
    {
        var armorEl = db.FindByNameAndType(armorName, "Armor");
        if (armorEl is null) Console.WriteLine($"  ! armor '{armorName}' not found");
        else session.EquipItem("Body", armorEl);
    }
    if (shieldName is not null)
    {
        var shieldEl = db.FindByNameAndType(shieldName, "Armor");
        if (shieldEl is null) Console.WriteLine($"  ! shield '{shieldName}' not found");
        else session.EquipItem("Off-hand", shieldEl);
    }
    // --magic: enchanted items. Weapon/Focus enchantments stamp their `Enhancement`
    // onto the equipped weapon (the calculator reads it for attack/damage); other
    // enchantments (armor, cloak) equip into their slot so their statadds apply.
    foreach (var magicName in magicNames)
    {
        var mi = db.FindByNameAndType(magicName, "Magic Item");
        if (mi is null) { Console.WriteLine($"  ! magic item '{magicName}' not found"); continue; }
        var miType = mi.Fields.TryGetValue("Magic Item Type", out var mt) ? mt : "";
        if ((miType is "Weapon" or "Focus") && equippedWeapon is not null
            && mi.Fields.TryGetValue("Enhancement", out var enh))
        {
            // Composite pairing: the enchanted weapon is a base weapon + an
            // enchantment, modelled as a LootItem and re-equipped in the weapon
            // slot. The enhancement is also reflected on the power card below.
            session.EquipItem("Main Hand", new LootItem { Base = equippedWeapon, Enchantment = mi });
            equippedWeapon.Fields["Enhancement"] = enh;
        }
        else
        {
            var slot = mi.Fields.TryGetValue("Item Slot", out var s) && !string.IsNullOrWhiteSpace(s) ? s : "Body";
            session.EquipItem(slot, mi);
        }
    }
    // --focus: an implement (typically an "Enchanted Focus +X" magic item). Its
    // Enhancement feeds the attack and damage of Focus-keyword powers — the
    // implement analogue of an enchanted weapon. (A focus supplies no [W] die.)
    RulesElement? equippedFocus = null;
    if (focusName is not null)
    {
        equippedFocus = db.FindByNameAndType(focusName, "Magic Item") ?? db.FindByNameAndType(focusName, "Focus");
        if (equippedFocus is null) Console.WriteLine($"  ! focus '{focusName}' not found");
        else session.EquipItem("Implement", equippedFocus);
    }
    if (weaponName is not null || armorName is not null || shieldName is not null || focusName is not null || magicNames.Count > 0)
        Console.WriteLine($"\nEquipped: {string.Join(", ", new[] { weaponName, armorName, shieldName, focusName }.Concat(magicNames).Where(s => s is not null))}");

    var snapshot = session.GetSnapshot() ?? session.GetPartialSnapshot();
    if (snapshot is null)
    {
        Console.WriteLine("\nNo snapshot could be built.");
        return 0;
    }

    if (demoSwaps && level >= 13)
    {
        var finalAttacks = snapshot.ElementTree.GetActiveElements()
            .Where(e => string.Equals(e.Type, "Power", StringComparison.OrdinalIgnoreCase)
                && string.Equals(e.Fields.TryGetValue("Power Type", out var pt) ? pt : null, "Attack", StringComparison.OrdinalIgnoreCase)
                && e.Categories.Any(c => c.StartsWith("ORCUS_DISCIPLINE_", StringComparison.Ordinal)))
            .Select(e => e.Name).Distinct().OrderBy(n => n);
        Console.WriteLine($"  Post-swap class attack powers: {string.Join(", ", finalAttacks)}");
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
    // attack bonus, defense and damage. With no --weapon, none is supplied — that
    // proves the ability resolution is equipment-independent. With --weapon, the
    // equipped weapon supplies the [W] die (and proficiency, when trained).
    Console.WriteLine(equippedWeapon is null
        ? "\nPower cards (no weapon/implement; equipment-independent ability resolution):"
        : $"\nPower cards (weapon: {equippedWeapon.Name}, {(equippedWeapon.Fields.TryGetValue("Damage", out var wd) ? wd : "?")}):");
    foreach (var picked in session.GetSelectedElements("Power"))
    {
        var power = db.FindByInternalId(picked.InternalId) ?? picked;
        if (PowerFieldParser.GetAttackText(power) is null)
        {
            Console.WriteLine($"  {power.Name,-22} (no attack line)");
            continue;
        }
        // Focus-keyword powers draw on the equipped focus/implement; weapon
        // powers on the equipped weapon. (A power with neither just uses neither.)
        bool isFocusPower = power.Fields.TryGetValue("Keywords", out var kw)
            && kw.Contains("Focus", StringComparison.OrdinalIgnoreCase);
        var item = (isFocusPower && equippedFocus is not null) ? equippedFocus : equippedWeapon;
        var card = PowerStatCalculator.Calculate(power, snapshot.Builder.Stats, weapon: item, characterLevel: level,
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
          charm-authoring playtest <rules.db> [--race <name>] [--class <name>] [--level <n>] [--swaps]

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
