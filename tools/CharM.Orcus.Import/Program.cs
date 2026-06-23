using System.Text;
using CharM.Orcus.Import;

// Usage: orcus-import audit <repo-root> [report-path]
// Builds a faithful normalized text blob from every Orcus source book, then
// checks the hand-authored content/orcus YAML against it:
//   * COVERAGE  — which source powers are/aren't transcribed (omissions).
//   * FIDELITY  — every prose field whose text is NOT found verbatim (after
//                 formatting-only normalization) in any source book. These are
//                 paraphrases or fabrications.
//   * FLAVOR    — each Flavor field labelled FAITHFUL (in source) or INVENTED.

string mode = args.Length > 0 ? args[0] : "audit";

if (mode == "generate-discipline")
{
    // generate-discipline <repo-root> "<Discipline Name>" <DISCIPLINE_ID> <ID_SUFFIX> <out.yaml>
    string gRoot = args[1];
    string discName = args[2];
    string discId = args[3];
    string suffix = args[4];
    string outPath = args[5];

    string book = Directory.EnumerateFiles(gRoot, "Orcus Classes and Powers*.md").Single();
    var parsed = Phase2.Parse(book, discName);
    Console.WriteLine($"Parsed '{discName}': {parsed.Powers.Count} powers; key {parsed.KeyAbility}/{parsed.SecondaryAbility}.");

    // Collect ids already used elsewhere, so generated ids don't collide.
    var others = YamlLoader.LoadDir(Path.Combine(gRoot, "content/orcus"))
        .Where(e => e.Id != null && Path.GetFileName(outPath) != Path.GetFileName(e.File))
        .Select(e => e.Id!).ToHashSet();

    string yaml = Phase2.GenerateYaml(parsed, discId, suffix, others);
    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
    File.WriteAllText(outPath, yaml);
    Console.WriteLine($"Wrote {outPath}");

    var (pass, failures) = Phase2.RoundTrip(parsed);
    Console.WriteLine($"\nRound-trip gate: {pass}/{parsed.Powers.Count} powers verbatim & complete.");
    if (failures.Count > 0)
    {
        Console.WriteLine("FAILURES (text not verbatim in source, or source words dropped):");
        foreach (var f in failures) Console.WriteLine(f);
        return 2;
    }
    Console.WriteLine("All powers passed the round-trip gate (no fabrication, no omission).");
    return 0;
}

if (mode == "audit-all")
{
    // Comprehensive scan: every field (and name) of every element, vs the source.
    string aRoot = args.Length > 1 ? args[1] : Directory.GetCurrentDirectory();
    var books = Directory.EnumerateFiles(aRoot, "Orcus*.md", SearchOption.TopDirectoryOnly)
        .Where(f => !f.Contains("Open Game License", StringComparison.OrdinalIgnoreCase)).ToList();
    if (File.Exists(Path.Combine(aRoot, "Basic.html"))) books.Add(Path.Combine(aRoot, "Basic.html"));
    var blob2 = new System.Text.StringBuilder();
    foreach (var f in books) blob2.Append(' ').Append(File.ReadAllText(f));
    string src = Normalizer.Norm(blob2.ToString());
    var els = YamlLoader.LoadDir(Path.Combine(aRoot, "content/orcus"));

    // Structural/enum/numeric fields that are not copied prose.
    var skipKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Power Usage","Power Type","Action Type","Level","Item Level","Boost","Enhancement",
        "Cost","Key Ability","Secondary Ability","Role","Tradition","Magic Item Type","Item Slot",
        "Focus Type","Domain","Kit","Channel Divinity","Size","Speed","Proficiency Bonus","Group",
        "Armor Type","Shield Bonus","Damage","Price","Weight","_SupportsID","_Discipline","Sources",
        "Strength","Constitution","Dexterity","Intelligence","Wisdom","Charisma","Armor Class",
        "Fortitude Defense","Reflex Defense","Will Defense","HP","Senses",
    };
    bool FoundAll(string t) => src.Contains(Normalizer.Norm(t), StringComparison.Ordinal);
    var byType = new SortedDictionary<string, List<string>>();
    int scanned = 0, flagged = 0;
    foreach (var el in els)
    {
        var fields = new List<(string k, string v)>();
        if (el.Name != null) fields.Add(("name", el.Name));
        foreach (var (k, v) in el.Fields) if (!skipKeys.Contains(k)) fields.Add((k, v));
        foreach (var (k, v) in fields)
        {
            if (string.IsNullOrWhiteSpace(v)) continue;
            scanned++;
            if (FoundAll(v)) continue;
            var bad = Normalizer.Chunks(v).Where(c => Normalizer.WordCount(Normalizer.Norm(c)) >= 4 && !FoundAll(c)).ToList();
            if (bad.Count == 0) continue;
            flagged++;
            string t = el.Type ?? "(none)";
            var list = byType.TryGetValue(t, out var l) ? l : byType[t] = new();
            list.Add($"  [{el.File}] {el.Name} — {k}: " + string.Join(" / ", bad.Select(b => b.Length > 80 ? b[..80] + "…" : b)));
        }
    }
    Console.WriteLine($"audit-all: scanned {scanned} fields; flagged {flagged}.");
    foreach (var (t, l) in byType)
    {
        Console.WriteLine($"\n=== type: {t} ({l.Count}) ===");
        foreach (var line in l.Take(60)) Console.WriteLine(line);
    }
    return 0;
}

if (mode == "generate-paths")
{
    // generate-paths <repo-root> <out.yaml>
    string pRoot = args[1];
    string outP = args[2];
    string book = Directory.EnumerateFiles(pRoot, "Orcus Classes and Powers*.md").Single();
    return PathGen.Generate(book, outP, Path.Combine(pRoot, "content/orcus"));
}

if (mode == "generate-species")
{
    // generate-species <repo-root> <out.yaml>
    string sRoot = args[1];
    string outP = args[2];
    string book = Directory.EnumerateFiles(sRoot, "Orcus Advanced Options*.md").Single();
    return SpeciesGen.Generate(book, outP, Path.Combine(sRoot, "content/orcus"));
}

if (mode == "generate-kits")
{
    // generate-kits <repo-root> <out.yaml>
    string kRoot = args[1];
    string outP = args[2];
    string book = Directory.EnumerateFiles(kRoot, "Orcus Classes and Powers*.md").Single();
    return KitGen.Generate(book, outP, Path.Combine(kRoot, "content/orcus"));
}

if (mode == "generate-familiars")
{
    // generate-familiars <repo-root> <out.yaml>
    string fRoot = args[1];
    string outP = args[2];
    string book = Directory.EnumerateFiles(fRoot, "Orcus Classes and Powers*.md").Single();
    return FamiliarGen.Generate(book, outP);
}

if (mode == "generate-vehicles")
{
    // generate-vehicles <repo-root> <out.yaml>
    string vRoot = args[1];
    string outP = args[2];
    string book = Directory.EnumerateFiles(vRoot, "Orcus Advanced Options*.md").Single();
    return VehicleGen.Generate(book, outP);
}

if (mode == "generate-deities")
{
    // generate-deities <repo-root> <out.yaml>
    string dRoot = args[1];
    string outP = args[2];
    string book = Directory.EnumerateFiles(dRoot, "Orcus Player Options*.md").Single();
    return DeityGen.Generate(book, outP);
}

if (mode == "generate-backgrounds")
{
    // generate-backgrounds <repo-root> <out.yaml>
    string bRoot = args[1];
    string outP = args[2];
    string book = Directory.EnumerateFiles(bRoot, "Orcus Advanced Options*.md").Single();
    return BackgroundGen.Generate(book, outP);
}

if (mode == "generate-companions")
{
    // generate-companions <repo-root>
    string cRoot = args[1];
    string cp = Directory.EnumerateFiles(cRoot, "Orcus Classes and Powers*.md").Single();
    return CompanionGen.Generate(cp, Path.Combine(cRoot, "content/orcus/companions.yaml"));
}

if (mode == "generate-misc")
{
    // generate-misc <repo-root>
    string mRoot = args[1];
    string adv = Directory.EnumerateFiles(mRoot, "Orcus Advanced Options*.md").Single();
    return MiscItems.Generate(adv,
        Path.Combine(mRoot, "content/orcus/equipment/magic-items-wondrous.yaml"),
        Path.Combine(mRoot, "content/orcus/equipment/magic-items-consumables.yaml"));
}

if (mode == "generate-feats")
{
    // generate-feats <repo-root>
    string fRoot = args[1];
    string playerOpts = Directory.EnumerateFiles(fRoot, "Orcus Player Options*.md").Single();
    return Feats.Generate(playerOpts, Path.Combine(fRoot, "content/orcus/feats.yaml"));
}

if (mode == "generate-boosts")
{
    // generate-boosts <repo-root> <out.yaml>
    string bRoot = args[1];
    string outP = args[2];
    string advBook = Directory.EnumerateFiles(bRoot, "Orcus Advanced Options*.md").Single();
    return Boosts.Generate(advBook, outP);
}

if (mode == "patch-global")
{
    // patch-global <repo-root> <file.yaml> <bookGlob>
    string gRoot2 = args[1];
    string file2 = args[2];
    string bookGlob = args[3];
    string book2 = Directory.EnumerateFiles(gRoot2, bookGlob).First();
    return ClassPatcher.PatchGlobal(book2, file2);
}

if (mode == "patch-class")
{
    // patch-class <repo-root> <classFile.yaml> "<Class Name>" [bookGlob]
    string pRoot = args[1];
    string classFile = args[2];
    string className = args[3];
    string glob = args.Length > 4 ? args[4] : "Orcus Classes and Powers*.md";
    string book = Directory.EnumerateFiles(pRoot, glob).First();
    return ClassPatcher.Patch(book, classFile, className);
}

string root = args.Length > 1 ? args[1] : Directory.GetCurrentDirectory();
string reportPath = args.Length > 2 ? args[2] : Path.Combine(root, "tools/CharM.Orcus.Import/audit-report.md");

if (mode != "audit") { Console.Error.WriteLine("unknown mode; use 'audit', 'generate-discipline' or 'patch-class'"); return 1; }

var sourceFiles = Directory.EnumerateFiles(root, "Orcus*.md", SearchOption.TopDirectoryOnly)
    .Where(f => !f.Contains("Open Game License", StringComparison.OrdinalIgnoreCase)).ToList();
if (File.Exists(Path.Combine(root, "Basic.html"))) sourceFiles.Add(Path.Combine(root, "Basic.html"));
sourceFiles = sourceFiles.OrderBy(f => f).ToList();
if (sourceFiles.Count == 0) { Console.Error.WriteLine($"no Orcus*.md books found in {root}"); return 1; }

string contentDir = Path.Combine(root, "content/orcus");
if (!Directory.Exists(contentDir)) { Console.Error.WriteLine($"no content dir at {contentDir}"); return 1; }

// --- Build the normalized source blob -------------------------------------------------
var blob = new StringBuilder();
foreach (var f in sourceFiles) blob.Append(' ').Append(File.ReadAllText(f));
string sourceNorm = Normalizer.Norm(blob.ToString());
Console.WriteLine($"Source books: {sourceFiles.Count}; normalized length {sourceNorm.Length:n0} chars.");

// --- Load YAML content ----------------------------------------------------------------
var elements = YamlLoader.LoadDir(contentDir);
Console.WriteLine($"YAML elements loaded: {elements.Count}");

// --- Coverage: source powers vs transcribed powers ------------------------------------
var sourcePowers = SourceParser.ParsePowers(sourceFiles);
var sourceByDisc = sourcePowers.GroupBy(p => p.Discipline)
    .ToDictionary(g => g.Key, g => g.Select(p => p.Name).ToList());
var sourceNameSet = sourcePowers.Select(p => Normalizer.Norm(p.Name)).ToHashSet();

var discIdToName = elements.Where(e => e.Type == "Discipline" && e.Id != null && e.Name != null)
    .ToDictionary(e => e.Id!, e => e.Name!);

// --- Field scanning config ------------------------------------------------------------
var proseFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "Hit", "Miss", "Effect", "Trigger", "Special", "Requirements", "Maintain",
    "Sustain", "Boost", "Attack", "Target", "Range", "Keywords",
    "Description", "Flavor", "Note", "Benefit", "Property", "Prerequisite",
};
var skipTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "Grants", "Skill", "Skill Training", "Proficiency", "Key Ability Swap",
    "Secondary Ability Swap", "Marker", "House Rule", "Level1Rules", "Level",
    "Level Ability Bonus", "Race Ability Bonus", "Discipline Access",
};

bool Found(string text) => sourceNorm.Contains(Normalizer.Norm(text), StringComparison.Ordinal);

// --- FIDELITY + FLAVOR scan -----------------------------------------------------------
var fidelityByFile = new SortedDictionary<string, List<string>>();
var flavorFaithful = new List<string>();
var flavorInvented = new List<string>();
int fieldsScanned = 0, fieldsFlagged = 0;

foreach (var el in elements)
{
    if (el.Type != null && skipTypes.Contains(el.Type)) continue;
    foreach (var (key, val) in el.Fields)
    {
        if (!proseFields.Contains(key)) continue;
        if (string.IsNullOrWhiteSpace(val)) continue;
        fieldsScanned++;

        bool wholeFound = Found(val);
        if (key.Equals("Flavor", StringComparison.OrdinalIgnoreCase))
        {
            string label = $"{el.File} :: {el.Name} :: \"{Trim(val)}\"";
            if (wholeFound) flavorFaithful.Add(label); else flavorInvented.Add(label);
            if (!wholeFound) { fieldsFlagged++; }
            continue;
        }

        if (wholeFound) continue;

        // Whole field not found verbatim — pinpoint the offending chunks.
        var badChunks = Normalizer.Chunks(val)
            .Where(c => Normalizer.WordCount(Normalizer.Norm(c)) >= 3 && !Found(c))
            .ToList();
        if (badChunks.Count == 0) continue; // differs only in tiny fragments/formatting

        fieldsFlagged++;
        var list = fidelityByFile.TryGetValue(el.File, out var l) ? l : fidelityByFile[el.File] = new();
        list.Add($"  [{el.Type}] {el.Name} — field '{key}':");
        foreach (var c in badChunks) list.Add($"      ✗ {Trim(c)}");
    }
}

// --- COVERAGE computation -------------------------------------------------------------
var yamlPowersByDisc = new Dictionary<string, List<string>>();
var yamlPowerNames = new HashSet<string>();
foreach (var el in elements.Where(e => e.Type == "Power"))
{
    if (el.Name != null) yamlPowerNames.Add(Normalizer.Norm(el.Name));
    var discCat = el.Categories.FirstOrDefault(c => c.StartsWith("ORCUS_DISCIPLINE_"));
    if (discCat == null || el.Name == null) continue;
    string dname = discIdToName.TryGetValue(discCat, out var n) ? n : discCat;
    (yamlPowersByDisc.TryGetValue(dname, out var lst) ? lst : yamlPowersByDisc[dname] = new()).Add(el.Name);
}

// Powers present in YAML whose name is in NO source book (possible fabrication / rename).
// Familiars (ORCUS_FAMILIAR) are table-derived companion data carried on a
// "Familiar: <creature>" Power (the engine's familiar shape), not transcribed
// powers — their constructed names are expected not to appear verbatim.
var yamlPowersNotInSource = elements.Where(e => e.Type == "Power" && e.Name != null)
    .Where(e => !e.Categories.Contains("ORCUS_FAMILIAR"))
    .Where(e => !sourceNameSet.Contains(Normalizer.Norm(e.Name!)))
    .Select(e => $"{e.File} :: {e.Name}").Distinct().OrderBy(s => s).ToList();

// --- Write the report -----------------------------------------------------------------
var r = new StringBuilder();
r.AppendLine("# Orcus content audit (machine-generated)");
r.AppendLine();
r.AppendLine($"- Source books scanned: {sourceFiles.Count}");
r.AppendLine($"- YAML elements loaded: {elements.Count}");
r.AppendLine($"- Prose fields scanned: {fieldsScanned}; flagged: {fieldsFlagged}");
r.AppendLine($"- Flavor fields: {flavorFaithful.Count} faithful, {flavorInvented.Count} INVENTED");
r.AppendLine();
r.AppendLine("A field is flagged when its text (after stripping markdown, smart quotes,");
r.AppendLine("bullets, punctuation, case and whitespace) is **not found verbatim** in any");
r.AppendLine("source book. Such text was reworded, fabricated, or otherwise altered.");
r.AppendLine();

r.AppendLine("## Invented Flavor fields (not in any source) — to be removed");
r.AppendLine();
if (flavorInvented.Count == 0) r.AppendLine("_none_");
else foreach (var f in flavorInvented.OrderBy(s => s)) r.AppendLine($"- {f}");
r.AppendLine();

r.AppendLine("## Fidelity flags — reworded / fabricated prose, by file");
r.AppendLine();
if (fidelityByFile.Count == 0) r.AppendLine("_none_");
else foreach (var (file, lines) in fidelityByFile)
{
    r.AppendLine($"### {file}");
    r.AppendLine("```");
    foreach (var line in lines) r.AppendLine(line);
    r.AppendLine("```");
}
r.AppendLine();

r.AppendLine("## Coverage — transcribed vs source, by discipline");
r.AppendLine();
r.AppendLine("| Discipline | source | transcribed | missing (omitted) |");
r.AppendLine("|---|--:|--:|---|");
foreach (var (disc, srcNames) in sourceByDisc.OrderBy(k => k.Key))
{
    if (!yamlPowersByDisc.TryGetValue(disc, out var yamlNames)) continue; // discipline not started
    var yamlNorm = yamlNames.Select(Normalizer.Norm).ToHashSet();
    var missing = srcNames.Where(n => !yamlNorm.Contains(Normalizer.Norm(n))).ToList();
    string miss = missing.Count == 0 ? "—" : string.Join(", ", missing);
    r.AppendLine($"| {disc} | {srcNames.Count} | {yamlNames.Count} | {miss} |");
}
r.AppendLine();

r.AppendLine("## YAML powers whose name is in NO source book (check for rename/fabrication)");
r.AppendLine();
if (yamlPowersNotInSource.Count == 0) r.AppendLine("_none_");
else foreach (var p in yamlPowersNotInSource) r.AppendLine($"- {p}");
r.AppendLine();

Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
File.WriteAllText(reportPath, r.ToString());

Console.WriteLine();
Console.WriteLine($"Prose fields scanned: {fieldsScanned}; flagged: {fieldsFlagged}");
Console.WriteLine($"Invented Flavor fields: {flavorInvented.Count}");
Console.WriteLine($"YAML powers not found in any source book: {yamlPowersNotInSource.Count}");
Console.WriteLine($"Report written to {reportPath}");
return 0;

static string Trim(string s)
{
    s = s.Replace("\n", " ").Trim();
    return s.Length > 200 ? s[..200] + "…" : s;
}
