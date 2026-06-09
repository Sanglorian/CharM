using System.Text.RegularExpressions;
using System.Text.Json;
using System.Xml.Linq;
using CharM.Engine.Rules;
using CharM.RulesDb.Storage;
using Microsoft.Data.Sqlite;

namespace CharM.RulesDb.Import;

/// <summary>
/// Merges CBLoader .part files into an existing SQLite rules database.
/// Supports: RulesElement (create/overwrite), AppendNodes, DeleteElement, MassAppend.
/// </summary>
public static partial class PartMerger
{
    /// <summary>
    /// Download and merge all .part files from an index file Url
    /// </summary>
    /// <param name="dbPath"></param>
    /// <param name="indexFileUrl"></param>
    /// <param name="progress"></param>
    /// <returns></returns>
    public static MergeResult MergeFromIndex(string dbPath, string indexFileUrl, IProgress<string>? progress = null)
    {
        var tempPath = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
        _ = Directory.CreateDirectory(tempPath);
        try
        {
            using var downloadClient = new HttpClient();
            var indexUri = new Uri(indexFileUrl, UriKind.Absolute);

            progress?.Report($"Downloading part index {indexUri}");
            using var indexResp = downloadClient.GetAsync(indexUri).GetAwaiter().GetResult();
            indexResp.EnsureSuccessStatusCode();

            using var indexStream = indexResp.Content.ReadAsStream();
            var doc = XDocument.Load(indexStream);
            doc.Save(Path.Combine(tempPath, "WotC.index"));

            List<Task> tasks = new();
            foreach (var part in doc.Descendants("Part"))
            {
                string? filename = part.Element("Filename")?.Value?.Trim();
                string? address = part.Element("PartAddress")?.Value.Trim();
                if (string.IsNullOrWhiteSpace(filename) || string.IsNullOrWhiteSpace(address))
                    continue;

                var partUri = new Uri(indexUri, address);
                tasks.Add(DownloadPart(downloadClient, partUri, filename, tempPath, progress));
            }

            Task.WhenAll(tasks).GetAwaiter().GetResult();
            return Merge(dbPath, tempPath, progress);
        }
        finally
        {
            Directory.Delete(tempPath, recursive: true);
        }
    }

    private static async Task DownloadPart(
        HttpClient downloader,
        Uri url,
        string fileName,
        string tempDir,
        IProgress<string>? progress)
    {
        progress?.Report($"Downloading {fileName}");
        using var partResp = await downloader.GetAsync(url);
        partResp.EnsureSuccessStatusCode();

        var safeFileName = Path.GetFileName(fileName);
        await using var destination = File.Open(Path.Join(tempDir, safeFileName), FileMode.Create);
        await partResp.Content.CopyToAsync(destination);
    }
    /// <summary>
    /// Merge all .part files from a directory into the rules database.
    /// </summary>
    /// <param name="dbPath">Path to existing SQLite rules database.</param>
    /// <param name="partsDirectory">Directory containing .part files and optional WotC.index.</param>
    /// <param name="progress">Progress callback (status messages).</param>
    public static MergeResult Merge(string dbPath, string partsDirectory, IProgress<string>? progress = null)
    {
        var obsolete = LoadObsoleteSet(partsDirectory);

        var partFiles = Directory.GetFiles(partsDirectory, "*.part")
            .Where(f => !obsolete.Contains(Path.GetFileName(f)))
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .ToList();

        int filesProcessed = 0, added = 0, updated = 0, deleted = 0, appended = 0;

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        Execute(connection, "PRAGMA journal_mode = WAL");
        Execute(connection, "PRAGMA synchronous = NORMAL");

        RulesDbSchema.Create(connection);

        var jsonOptions = RulesDatabase.SharedJsonOptions;

        foreach (var partFile in partFiles)
        {
            string fileName = Path.GetFileName(partFile);
            progress?.Report($"  {fileName}");

            XDocument doc;
            try { doc = XDocument.Load(partFile); }
            catch { continue; }

            var root = doc.Root;
            if (root is null) continue;

            using var tx = connection.BeginTransaction();

            foreach (var el in root.Elements())
            {
                switch (el.Name.LocalName)
                {
                    case "RulesElement":
                    {
                        var parsed = ParseRulesElement(el);
                        if (parsed is null) continue;
                        bool exists = ElementExists(connection, tx, parsed.Element.InternalId);
                        UpsertElement(connection, tx, parsed, jsonOptions);
                        if (exists) updated++; else added++;
                        break;
                    }
                    case "AppendNodes":
                    {
                        string? id = Attr(el, "internal-id");
                        if (id is null) continue;
                        appended += AppendToElement(connection, tx, id, el, jsonOptions);
                        break;
                    }
                    case "DeleteElement":
                    {
                        string? id = Attr(el, "internal-id");
                        if (id is null) continue;
                        deleted += DeleteElement(connection, tx, id);
                        break;
                    }
                    case "MassAppend":
                    {
                        string? ids = Attr(el, "ids");
                        if (ids is null) continue;
                        foreach (var id in ids.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                            appended += AppendToElement(connection, tx, id, el, jsonOptions);
                        break;
                    }
                }
            }

            tx.Commit();
            filesProcessed++;
        }

        return new MergeResult(filesProcessed, added, updated, deleted, appended);
    }

    // ========================================================================
    // WotC.index parsing
    // ========================================================================

    private static HashSet<string> LoadObsoleteSet(string directory)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string indexPath = Path.Combine(directory, "WotC.index");
        if (!File.Exists(indexPath)) return set;

        try
        {
            var doc = XDocument.Load(indexPath);
            foreach (var obs in doc.Descendants("Obsolete"))
            {
                string? filename = obs.Element("Filename")?.Value?.Trim();
                if (filename is not null)
                    set.Add(filename);
            }
        }
        catch { /* index is optional */ }

        return set;
    }

    // ========================================================================
    // SQLite operations
    // ========================================================================

    private static bool ElementExists(SqliteConnection conn, SqliteTransaction tx, string internalId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT 1 FROM rules_elements WHERE internal_id = $id LIMIT 1";
        cmd.Parameters.AddWithValue("$id", internalId);
        return cmd.ExecuteScalar() is not null;
    }

    private static void UpsertElement(
        SqliteConnection conn, SqliteTransaction tx, ParsedElement parsed, JsonSerializerOptions jsonOptions)
    {
        var element = parsed.Element;

        // Serialize the ordered list-of-pairs view so duplicates (e.g. two
        // <specific name="Hit"> children for primary/secondary attack) round-
        // trip through the DB. Fall back to Fields if FieldEntries wasn't
        // populated by the caller. Reader accepts both legacy object format
        // and the array-of-pairs format.
        IReadOnlyList<KeyValuePair<string, string>> entries = element.FieldEntries.Count > 0
            ? element.FieldEntries
            : element.Fields.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)).ToList();
        string? fieldsJson = entries.Count > 0
            ? JsonSerializer.Serialize(entries)
            : null;
        string? rulesJson = element.Rules.Count > 0
            ? JsonSerializer.Serialize(element.Rules, jsonOptions)
            : null;

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT OR REPLACE INTO rules_elements (internal_id, name, type, source, prereqs, fields_json, rules_json)
            VALUES ($id, $name, $type, $source, $prereqs, $fields, $rules)
            """;
        cmd.Parameters.AddWithValue("$id", element.InternalId);
        cmd.Parameters.AddWithValue("$name", element.Name);
        cmd.Parameters.AddWithValue("$type", element.Type);
        cmd.Parameters.AddWithValue("$source", (object?)element.Source ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$prereqs", (object?)element.Prereqs ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$fields", (object?)fieldsJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$rules", (object?)rulesJson ?? DBNull.Value);
        cmd.ExecuteNonQuery();

        foreach (var cat in parsed.Categories)
        {
            using var catCmd = conn.CreateCommand();
            catCmd.Transaction = tx;
            catCmd.CommandText = "INSERT OR IGNORE INTO element_categories (internal_id, category) VALUES ($id, $cat)";
            catCmd.Parameters.AddWithValue("$id", element.InternalId);
            catCmd.Parameters.AddWithValue("$cat", cat);
            catCmd.ExecuteNonQuery();
        }
    }

    private static int AppendToElement(
        SqliteConnection conn, SqliteTransaction tx, string internalId, XElement appendEl, JsonSerializerOptions jsonOptions)
    {
        int count = 0;

        var newDirectives = new List<RuleDirective>();
        var newCategories = new List<string>();

        foreach (var child in appendEl.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "rules":
                    ParseRulesBlock(child, newDirectives);
                    break;
                case "Category":
                    ParseCategories(child, newCategories);
                    break;
            }
        }

        if (newDirectives.Count > 0)
        {
            string? existingJson = ReadRulesJson(conn, tx, internalId);
            var existing = existingJson is not null
                ? JsonSerializer.Deserialize<List<RuleDirective>>(existingJson, jsonOptions) ?? []
                : new List<RuleDirective>();

            existing.AddRange(newDirectives);

            // Deduplicate: remove identical directives that may have been appended
            // from both the base DB and a .part file (same statadd appearing twice)
            var deduplicated = DeduplicateDirectives(existing, jsonOptions);
            string updatedJson = JsonSerializer.Serialize(deduplicated, jsonOptions);

            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "UPDATE rules_elements SET rules_json = $rules WHERE internal_id = $id";
            cmd.Parameters.AddWithValue("$rules", updatedJson);
            cmd.Parameters.AddWithValue("$id", internalId);
            if (cmd.ExecuteNonQuery() > 0)
                count += newDirectives.Count;
        }

        foreach (var cat in newCategories)
        {
            using var catCmd = conn.CreateCommand();
            catCmd.Transaction = tx;
            catCmd.CommandText = "INSERT OR IGNORE INTO element_categories (internal_id, category) VALUES ($id, $cat)";
            catCmd.Parameters.AddWithValue("$id", internalId);
            catCmd.Parameters.AddWithValue("$cat", cat);
            catCmd.ExecuteNonQuery();
            count++;
        }

        return count;
    }

    private static string? ReadRulesJson(SqliteConnection conn, SqliteTransaction tx, string internalId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT rules_json FROM rules_elements WHERE internal_id = $id";
        cmd.Parameters.AddWithValue("$id", internalId);
        var result = cmd.ExecuteScalar();
        return result is DBNull or null ? null : (string)result;
    }

    private static int DeleteElement(SqliteConnection conn, SqliteTransaction tx, string internalId)
    {
        using var catCmd = conn.CreateCommand();
        catCmd.Transaction = tx;
        catCmd.CommandText = "DELETE FROM element_categories WHERE internal_id = $id";
        catCmd.Parameters.AddWithValue("$id", internalId);
        catCmd.ExecuteNonQuery();

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "DELETE FROM rules_elements WHERE internal_id = $id";
        cmd.Parameters.AddWithValue("$id", internalId);
        return cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Remove duplicate directives by comparing their JSON serialization.
    /// This handles cases where a .part file AppendNodes adds a directive
    /// that already exists on the element (from the base DB or a previous merge).
    /// </summary>
    private static List<RuleDirective> DeduplicateDirectives(List<RuleDirective> directives, JsonSerializerOptions jsonOptions)
    {
        var seen = new HashSet<string>();
        var result = new List<RuleDirective>();
        foreach (var d in directives)
        {
            string key = JsonSerializer.Serialize(d, d.GetType(), jsonOptions);
            if (seen.Add(key))
                result.Add(d);
        }
        return result;
    }

    // ========================================================================
    // XML → domain model parsing (mirrors RulesXmlReader but uses XElement)
    // ========================================================================

    private static ParsedElement? ParseRulesElement(XElement el)
    {
        string? name = Attr(el, "name");
        string? type = Attr(el, "type");
        string? internalId = Attr(el, "internal-id");
        string? source = Attr(el, "source");

        if (name is null || type is null || internalId is null)
            return null;

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var fieldEntries = new List<KeyValuePair<string, string>>();
        string? prereqs = null;
        var categories = new List<string>();
        var rules = new List<RuleDirective>();

        foreach (var child in el.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "specific":
                    string? fieldName = Attr(child, "name");
                    if (fieldName is not null)
                    {
                        string value = child.Value.Trim();
                        // Always record the raw entry so duplicates are
                        // recoverable (e.g. Ravening Thought emits two
                        // <specific name="Hit"> children). The lookup
                        // Dictionary keeps the FIRST occurrence to match
                        // OCB's RulesElementField behavior.
                        fieldEntries.Add(new(fieldName, value));
                        if (!fields.ContainsKey(fieldName))
                            fields[fieldName] = value;
                    }
                    break;
                case "Prereqs":
                    prereqs = child.Value.Trim();
                    break;
                case "Category":
                    ParseCategories(child, categories);
                    break;
                case "rules":
                    ParseRulesBlock(child, rules);
                    break;
            }
        }

        // Capture mixed-content text (description body) that sits as direct XText
        // children of the RulesElement, e.g. body description after </rules>.
        var descBuilder = new System.Text.StringBuilder();
        foreach (var node in el.Nodes().OfType<System.Xml.Linq.XText>())
            descBuilder.Append(node.Value);
        string descText = NormalizeDescription(descBuilder.ToString());
        if (descText.Length > 0 && !fields.ContainsKey("Description"))
        {
            fields["Description"] = descText;
            fieldEntries.Add(new("Description", descText));
        }

        var element = new RulesElement
        {
            InternalId = internalId,
            Name = name,
            Type = type,
            Source = source,
            Prereqs = prereqs,
            Fields = fields,
            FieldEntries = fieldEntries,
            Rules = rules,
        };

        return new ParsedElement(element, categories);
    }

    private static void ParseCategories(XElement categoryEl, List<string> categories)
    {
        string content = categoryEl.Value.Trim();
        if (string.IsNullOrWhiteSpace(content)) return;
        foreach (var cat in content.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            categories.Add(cat);
    }

    private static void ParseRulesBlock(XElement rulesEl, List<RuleDirective> rules)
    {
        foreach (var child in rulesEl.Elements())
        {
            RuleDirective? directive = child.Name.LocalName switch
            {
                "statadd" => ParseStatAdd(child),
                "grant" => ParseGrant(child),
                "modify" => ParseModify(child),
                "select" => ParseSelect(child),
                "replace" => ParseReplace(child),
                "drop" => ParseDrop(child),
                "suggest" => ParseSuggest(child),
                "textstring" => ParseTextString(child),
                "statalias" => ParseStatAlias(child),
                _ => null,
            };
            if (directive is not null)
                rules.Add(directive);
        }
    }

    // ---- Individual directive parsers (match RulesXmlReader logic exactly) ----

    private static StatAddDirective? ParseStatAdd(XElement el)
    {
        string? name = Attr(el, "name");
        string? valueStr = Attr(el, "value");
        if (name is null || valueStr is null) return null;

        return new StatAddDirective
        {
            Name = name,
            Value = ValueExpression.Parse(valueStr),
            BonusType = Attr(el, "type"),
            Level = ParseIntOrNull(AttrCI(el, "Level", "level")),
            Requires = Attr(el, "requires"),
            Condition = Attr(el, "condition"),
            Wearing = Attr(el, "wearing"),
            NotWearing = Attr(el, "not-wearing"),
            Zero = ParseBool(Attr(el, "zero")),
            NonZero = ParseBool(Attr(el, "non-zero")),
            HalfPoint = ParseBool(Attr(el, "half-point")),
            StatMin = Attr(el, "statmin"),
        };
    }

    private static GrantDirective? ParseGrant(XElement el)
    {
        string? name = Attr(el, "name");
        string? type = Attr(el, "type");
        if (name is null || type is null) return null;

        return new GrantDirective
        {
            Name = name,
            ElementType = type,
            Level = ParseIntOrNull(AttrCI(el, "Level", "level")),
            Requires = Attr(el, "requires"),
        };
    }

    private static ModifyDirective? ParseModify(XElement el)
    {
        string? field = Attr(el, "Field") ?? Attr(el, "field");
        if (field is null) return null;

        return new ModifyDirective
        {
            Field = field,
            Name = Attr(el, "name"),
            ElementType = Attr(el, "type"),
            Value = Attr(el, "value"),
            Level = ParseIntOrNull(AttrCI(el, "Level", "level")),
            Requires = Attr(el, "requires"),
            ListAddition = Attr(el, "list-addition"),
            SelectSlot = Attr(el, "select"),
            Wearing = Attr(el, "wearing"),
            DieIncrease = ParseIntOrNull(Attr(el, "die-increase")),
        };
    }

    private static SelectDirective? ParseSelect(XElement el)
    {
        string? type = Attr(el, "type");
        if (type is null) return null;

        // The attribute is the stable slot IDENTIFIER (referenced by
        // modify/replace directives). The inner text is a separate UI
        // DISPLAY LABEL. Keep them distinct — never let inner text
        // overwrite a present attribute.
        string? innerText = null;
        if (!el.HasElements)
        {
            string trimmed = el.Value.Trim();
            if (trimmed.Length > 0) innerText = trimmed;
        }

        return new SelectDirective
        {
            ElementType = type,
            Number = ParseIntOrNull(Attr(el, "number")) ?? 1,
            Category = AttrCI(el, "Category", "category"),
            Name = Attr(el, "name") ?? innerText,
            DisplayLabel = innerText,
            Level = ParseIntOrNull(AttrCI(el, "Level", "level")),
            Requires = Attr(el, "requires"),
            Prepare = AttrCI(el, "Prepare", "prepare"),
            Spellbook = Attr(el, "spellbook"),
            Optional = ParseBool(Attr(el, "optional")),
            Existing = ParseBool(Attr(el, "existing")),
            Default = Attr(el, "default"),
            Grant = Attr(el, "grant"),
        };
    }

    private static ReplaceDirective ParseReplace(XElement el)
    {
        return new ReplaceDirective
        {
            Name = Attr(el, "name"),
            Level = ParseIntOrNull(AttrCI(el, "Level", "level")),
            Multiclass = Attr(el, "multiclass"),
            PowerSwap = Attr(el, "powerswap"),
            PowerReplace = Attr(el, "power-replace"),
            Optional = ParseBool(Attr(el, "optional")),
            Requires = Attr(el, "requires"),
        };
    }

    private static DropDirective ParseDrop(XElement el)
    {
        return new DropDirective
        {
            SelectSlot = Attr(el, "select"),
            Name = Attr(el, "name"),
            ElementType = Attr(el, "type"),
            Level = ParseIntOrNull(AttrCI(el, "Level", "level")),
            Requires = Attr(el, "requires"),
        };
    }

    private static SuggestDirective? ParseSuggest(XElement el)
    {
        string? name = Attr(el, "name");
        string? type = Attr(el, "type");
        if (name is null || type is null) return null;

        return new SuggestDirective
        {
            Name = name,
            ElementType = type,
            Level = ParseIntOrNull(AttrCI(el, "Level", "level")),
            Requires = Attr(el, "requires"),
        };
    }

    private static TextStringDirective? ParseTextString(XElement el)
    {
        string? name = Attr(el, "name");
        string? value = Attr(el, "value");
        if (name is null || value is null) return null;

        return new TextStringDirective
        {
            Name = name,
            Value = value,
            Level = ParseIntOrNull(AttrCI(el, "Level", "level")),
            Requires = Attr(el, "requires"),
            Condition = Attr(el, "condition"),
        };
    }

    private static StatAliasDirective? ParseStatAlias(XElement el)
    {
        string? name = Attr(el, "name");
        string? alias = Attr(el, "alias");
        if (name is null || alias is null) return null;

        return new StatAliasDirective
        {
            Name = name,
            Alias = alias,
            Level = ParseIntOrNull(AttrCI(el, "Level", "level")),
            Requires = Attr(el, "requires"),
        };
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    private static string? Attr(XElement el, string name) => el.Attribute(name)?.Value;

    private static string? AttrCI(XElement el, string upper, string lower) =>
        el.Attribute(upper)?.Value ?? el.Attribute(lower)?.Value;

    private static int? ParseIntOrNull(string? value) =>
        int.TryParse(value, out int result) ? result : null;

    private static bool ParseBool(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private static void Execute(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static string NormalizeDescription(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var lines = raw.Replace('\t', ' ')
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => WhitespaceRegex().Replace(l, " ").Trim())
            .Where(l => l.Length > 0);
        return string.Join("\n", lines);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}

/// <summary>
/// Result of merging .part files into the rules database.
/// </summary>
public sealed record MergeResult(
    int FilesProcessed,
    int ElementsAdded,
    int ElementsUpdated,
    int ElementsDeleted,
    int NodesAppended);
