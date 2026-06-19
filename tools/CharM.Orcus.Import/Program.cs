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

if (mode == "generate-boosts")
{
    // generate-boosts <repo-root> <out.yaml>
    string bRoot = args[1];
    string outP = args[2];
    string advBook = Directory.EnumerateFiles(bRoot, "Orcus Advanced Options*.md").Single();
    return Boosts.Generate(advBook, outP);
}

if (mode == "patch-class")
{
    // patch-class <repo-root> <classFile.yaml> "<Class Name>"
    string pRoot = args[1];
    string classFile = args[2];
    string className = args[3];
    string book = Directory.EnumerateFiles(pRoot, "Orcus Classes and Powers*.md").Single();
    return ClassPatcher.Patch(book, classFile, className);
}

string root = args.Length > 1 ? args[1] : Directory.GetCurrentDirectory();
string reportPath = args.Length > 2 ? args[2] : Path.Combine(root, "tools/CharM.Orcus.Import/audit-report.md");

if (mode != "audit") { Console.Error.WriteLine("unknown mode; use 'audit', 'generate-discipline' or 'patch-class'"); return 1; }

var sourceFiles = Directory.EnumerateFiles(root, "Orcus*.md", SearchOption.TopDirectoryOnly)
    .Where(f => !f.Contains("Open Game License", StringComparison.OrdinalIgnoreCase))
    .OrderBy(f => f).ToList();
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
var yamlPowersNotInSource = elements.Where(e => e.Type == "Power" && e.Name != null)
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
