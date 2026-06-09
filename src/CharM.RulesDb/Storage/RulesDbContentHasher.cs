using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace CharM.RulesDb.Storage;

/// <summary>
/// Computes a deterministic content-based SHA-256 fingerprint over a rules
/// database. Unlike a file-byte hash, this is stable across SQLite versions,
/// page layouts, insertion order, and WAL checkpointing — the same set of
/// rules elements always produces the same hash regardless of how the DB
/// file was assembled.
/// </summary>
/// <remarks>
/// <para>
/// Algorithm: for each user table in alphabetical order, SELECT all rows
/// ordered by primary key (or by all columns when there is no PK), serialize
/// every column with a type tag and length-prefix, and feed the byte stream
/// into <see cref="IncrementalHash"/> using SHA-256.
/// </para>
/// <para>
/// Table boundary marker (<c>0xFF 0xFE 0xFD 0xFC</c>) and column type tags
/// (<c>0..4</c>) keep the encoding unambiguous so equivalent content never
/// collides across schemas. SQLite system tables (<c>sqlite_*</c>) are
/// skipped — they hold autoincrement counters and other layout state that
/// has nothing to do with rules content.
/// </para>
/// <para>
/// Cost is roughly one full table scan; on a ~50 MB rules DB with ~38k
/// elements this completes in 1–3 seconds. Run on a background thread when
/// called from UI code.
/// </para>
/// </remarks>
public static class RulesDbContentHasher
{
    private const byte TypeNull = 0;
    private const byte TypeInteger = 1;
    private const byte TypeReal = 2;
    private const byte TypeText = 3;
    private const byte TypeBlob = 4;

    private static readonly byte[] TableSeparator = [0xFF, 0xFE, 0xFD, 0xFC];

    /// <summary>
    /// Compute the content hash of the rules database file at the given path.
    /// </summary>
    public static string ComputeContentHash(string dbPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
            throw new ArgumentException("Database path is required.", nameof(dbPath));
        if (!File.Exists(dbPath))
            throw new FileNotFoundException("Rules database not found.", dbPath);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly,
        }.ToString();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        return ComputeContentHash(connection, cancellationToken);
    }

    /// <summary>
    /// Compute the content hash over an already-open SQLite connection.
    /// </summary>
    public static string ComputeContentHash(SqliteConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var firstTable = true;

        foreach (var table in EnumerateUserTables(connection))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!firstTable)
                hasher.AppendData(TableSeparator);
            firstTable = false;

            // Feed the table name so two tables with identical column content
            // can never collide.
            AppendString(hasher, table);
            AppendByte(hasher, TableSeparator[0]);

            HashTable(connection, table, hasher, cancellationToken);
        }

        var bytes = hasher.GetHashAndReset();
        return Convert.ToHexString(bytes);
    }

    private static IEnumerable<string> EnumerateUserTables(SqliteConnection connection)
    {
        var tables = new List<string>();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            tables.Add(reader.GetString(0));
        return tables;
    }

    private static void HashTable(
        SqliteConnection connection,
        string tableName,
        IncrementalHash hasher,
        CancellationToken cancellationToken)
    {
        var (columns, primaryKey) = GetColumns(connection, tableName);
        if (columns.Count == 0)
            return;

        var orderBy = primaryKey.Count > 0
            ? primaryKey
            : columns;

        // Use parameterized table/column names is not supported by SQLite —
        // table names come from sqlite_master so they are trusted system input,
        // not user input. We still quote them with double quotes to handle any
        // table name edge cases.
        var orderByClause = string.Join(", ", orderBy.Select(c => $"\"{c}\""));
        var sql = $"SELECT * FROM \"{tableName}\" ORDER BY {orderByClause}";

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();

        var fieldCount = reader.FieldCount;
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var i = 0; i < fieldCount; i++)
                AppendColumnValue(hasher, reader, i);
        }
    }

    private static (List<string> Columns, List<string> PrimaryKey) GetColumns(SqliteConnection connection, string tableName)
    {
        var columns = new List<string>();
        var pkOrder = new List<(string Name, int PkIndex)>();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{tableName}\")";
        using var reader = cmd.ExecuteReader();

        // table_info columns: cid, name, type, notnull, dflt_value, pk
        // pk = 0 for non-PK columns, 1..N for PK columns indicating position
        while (reader.Read())
        {
            var name = reader.GetString(1);
            columns.Add(name);
            var pk = reader.GetInt32(5);
            if (pk > 0)
                pkOrder.Add((name, pk));
        }

        var primaryKey = pkOrder
            .OrderBy(p => p.PkIndex)
            .Select(p => p.Name)
            .ToList();

        return (columns, primaryKey);
    }

    private static void AppendColumnValue(IncrementalHash hasher, SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            AppendByte(hasher, TypeNull);
            return;
        }

        var value = reader.GetValue(ordinal);
        switch (value)
        {
            case long l:
                AppendByte(hasher, TypeInteger);
                Span<byte> intBuf = stackalloc byte[8];
                BitConverter.TryWriteBytes(intBuf, l);
                if (!BitConverter.IsLittleEndian)
                    intBuf.Reverse();
                hasher.AppendData(intBuf);
                break;

            case double d:
                AppendByte(hasher, TypeReal);
                Span<byte> realBuf = stackalloc byte[8];
                BitConverter.TryWriteBytes(realBuf, d);
                if (!BitConverter.IsLittleEndian)
                    realBuf.Reverse();
                hasher.AppendData(realBuf);
                break;

            case byte[] blob:
                AppendByte(hasher, TypeBlob);
                AppendLength(hasher, blob.Length);
                hasher.AppendData(blob);
                break;

            default:
                AppendByte(hasher, TypeText);
                AppendString(hasher, Convert.ToString(value) ?? string.Empty);
                break;
        }
    }

    private static void AppendString(IncrementalHash hasher, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        AppendLength(hasher, bytes.Length);
        hasher.AppendData(bytes);
    }

    private static void AppendLength(IncrementalHash hasher, int length)
    {
        Span<byte> buf = stackalloc byte[4];
        BitConverter.TryWriteBytes(buf, length);
        if (!BitConverter.IsLittleEndian)
            buf.Reverse();
        hasher.AppendData(buf);
    }

    private static void AppendByte(IncrementalHash hasher, byte value)
    {
        Span<byte> single = stackalloc byte[1];
        single[0] = value;
        hasher.AppendData(single);
    }
}
