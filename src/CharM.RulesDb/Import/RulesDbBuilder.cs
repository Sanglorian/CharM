using System.Text.Json;
using CharM.RulesDb.Storage;
using Microsoft.Data.Sqlite;

namespace CharM.RulesDb.Import;

/// <summary>
/// Import pipeline: D20Rules XML → SQLite database.
/// </summary>
public static class RulesDbBuilder
{
    private const int BatchSize = 1000;

    /// <summary>
    /// Import a D20Rules XML file into a new SQLite database.
    /// </summary>
    public static void Import(string xmlPath, string dbPath, IProgress<int>? progress = null)
        => Build(RulesXmlReader.ReadAll(xmlPath), dbPath, progress);

    /// <summary>
    /// Write a sequence of parsed elements into a new SQLite database.
    /// Shared by the XML importer (<see cref="Import"/>) and any other producer
    /// of <see cref="ParsedElement"/>s (e.g. the YAML authoring compiler), so the
    /// on-disk format is guaranteed identical regardless of source.
    /// </summary>
    public static void Build(IEnumerable<ParsedElement> elements, string dbPath, IProgress<int>? progress = null)
    {
        if (File.Exists(dbPath))
            File.Delete(dbPath);

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        // Pragmas for bulk-insert performance
        Execute(connection, "PRAGMA journal_mode = WAL");
        Execute(connection, "PRAGMA synchronous = NORMAL");

        RulesDbSchema.Create(connection);

        int count = 0;
        SqliteTransaction? tx = null;
        SqliteCommand? insertElement = null;
        SqliteCommand? insertCategory = null;

        try
        {
            tx = connection.BeginTransaction();
            insertElement = CreateInsertElementCommand(connection, tx);
            insertCategory = CreateInsertCategoryCommand(connection, tx);

            foreach (var parsed in elements)
            {
                InsertElement(insertElement, insertCategory, parsed);
                count++;

                if (count % BatchSize == 0)
                {
                    tx.Commit();
                    progress?.Report(count);
                    tx.Dispose();
                    insertElement.Dispose();
                    insertCategory.Dispose();

                    tx = connection.BeginTransaction();
                    insertElement = CreateInsertElementCommand(connection, tx);
                    insertCategory = CreateInsertCategoryCommand(connection, tx);
                }
            }

            // Commit remaining rows
            tx.Commit();
            progress?.Report(count);
        }
        finally
        {
            insertElement?.Dispose();
            insertCategory?.Dispose();
            tx?.Dispose();
        }

        // Fold the write-ahead log back into the main database file and switch the
        // journal mode to DELETE, so the produced .db is a single, self-contained,
        // portable file (no -wal/-shm sidecars carrying the actual data). Without
        // this, connection pooling can leave the WAL un-checkpointed and the .db a
        // ~4 KB stub with the real data stranded in the -wal file.
        Execute(connection, "PRAGMA wal_checkpoint(TRUNCATE)");
        Execute(connection, "PRAGMA journal_mode = DELETE");
    }

    private static SqliteCommand CreateInsertElementCommand(SqliteConnection connection, SqliteTransaction tx)
    {
        var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT OR REPLACE INTO rules_elements (internal_id, name, type, source, prereqs, fields_json, rules_json)
            VALUES ($id, $name, $type, $source, $prereqs, $fields, $rules)
            """;
        cmd.Parameters.Add(new SqliteParameter("$id", SqliteType.Text));
        cmd.Parameters.Add(new SqliteParameter("$name", SqliteType.Text));
        cmd.Parameters.Add(new SqliteParameter("$type", SqliteType.Text));
        cmd.Parameters.Add(new SqliteParameter("$source", SqliteType.Text));
        cmd.Parameters.Add(new SqliteParameter("$prereqs", SqliteType.Text));
        cmd.Parameters.Add(new SqliteParameter("$fields", SqliteType.Text));
        cmd.Parameters.Add(new SqliteParameter("$rules", SqliteType.Text));
        return cmd;
    }

    private static SqliteCommand CreateInsertCategoryCommand(SqliteConnection connection, SqliteTransaction tx)
    {
        var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT OR IGNORE INTO element_categories (internal_id, category)
            VALUES ($id, $cat)
            """;
        cmd.Parameters.Add(new SqliteParameter("$id", SqliteType.Text));
        cmd.Parameters.Add(new SqliteParameter("$cat", SqliteType.Text));
        return cmd;
    }

    private static void InsertElement(SqliteCommand insertElement, SqliteCommand insertCategory, ParsedElement parsed)
    {
        var element = parsed.Element;
        var jsonOptions = RulesDatabase.SharedJsonOptions;

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

        insertElement.Parameters["$id"].Value = element.InternalId;
        insertElement.Parameters["$name"].Value = element.Name;
        insertElement.Parameters["$type"].Value = element.Type;
        insertElement.Parameters["$source"].Value = (object?)element.Source ?? DBNull.Value;
        insertElement.Parameters["$prereqs"].Value = (object?)element.Prereqs ?? DBNull.Value;
        insertElement.Parameters["$fields"].Value = (object?)fieldsJson ?? DBNull.Value;
        insertElement.Parameters["$rules"].Value = (object?)rulesJson ?? DBNull.Value;
        insertElement.ExecuteNonQuery();

        foreach (var category in parsed.Categories)
        {
            insertCategory.Parameters["$id"].Value = element.InternalId;
            insertCategory.Parameters["$cat"].Value = category;
            insertCategory.ExecuteNonQuery();
        }
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
