using Microsoft.Data.Sqlite;

namespace CharM.RulesDb.Storage;

/// <summary>
/// Creates the SQLite schema for the rules database.
/// </summary>
public static class RulesDbSchema
{
    public static void Create(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS rules_elements (
                internal_id TEXT PRIMARY KEY,
                name        TEXT NOT NULL,
                type        TEXT NOT NULL,
                source      TEXT,
                prereqs     TEXT,
                fields_json TEXT,
                rules_json  TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_re_name_type ON rules_elements(name, type);
            CREATE INDEX IF NOT EXISTS idx_re_type ON rules_elements(type);
            CREATE INDEX IF NOT EXISTS idx_re_source ON rules_elements(source);
            CREATE INDEX IF NOT EXISTS idx_re_type_source ON rules_elements(type, source);

            CREATE TABLE IF NOT EXISTS element_categories (
                internal_id TEXT NOT NULL,
                category    TEXT NOT NULL,
                PRIMARY KEY (internal_id, category)
            );

            CREATE INDEX IF NOT EXISTS idx_ec_category ON element_categories(category);
            """;
        cmd.ExecuteNonQuery();
    }
}
