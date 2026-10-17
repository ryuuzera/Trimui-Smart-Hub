using Microsoft.Data.Sqlite;
using TrimuiSmartHub.Core.Games;
using TrimuiSmartHub.Core.Storage;

namespace TrimuiSmartHub.Core.Library;

public sealed record RenameRecord(long Id, string PlatformId, string OldBaseName, string NewBaseName, DateTime CreatedUtc);

/// <summary>
/// Library metadata kept in "&lt;library&gt;/.hub/library.db", so the library folder is portable.
/// Holds the CRC cache and the rename journal used to rename saves on the device during sync.
/// </summary>
public sealed class LibraryIndex : ICrcCache
{
    private readonly string _connectionString;

    public LibraryIndex(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = true }.ToString();
        Initialize();
    }

    private void Initialize()
    {
        using var connection = Open();
        Execute(connection, """
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS crc_cache (
                store TEXT NOT NULL,
                path  TEXT NOT NULL,
                size  INTEGER NOT NULL,
                mtime INTEGER NOT NULL,
                crc   INTEGER NOT NULL,
                PRIMARY KEY (store, path)
            );
            CREATE TABLE IF NOT EXISTS renames (
                id       INTEGER PRIMARY KEY AUTOINCREMENT,
                platform TEXT NOT NULL,
                old_base TEXT NOT NULL,
                new_base TEXT NOT NULL,
                created  INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS title_overrides (
                platform TEXT NOT NULL,
                base     TEXT NOT NULL,
                title    TEXT NOT NULL,
                PRIMARY KEY (platform, base)
            );
            """);
    }

    public bool TryGet(string key, FileEntry file, out uint crc32)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT crc FROM crc_cache WHERE store = $store AND path = $path AND size = $size AND mtime = $mtime;";
        command.Parameters.AddWithValue("$store", key);
        command.Parameters.AddWithValue("$path", file.Path);
        command.Parameters.AddWithValue("$size", file.Size);
        command.Parameters.AddWithValue("$mtime", file.LastWriteUtc.Ticks);

        var value = command.ExecuteScalar();
        crc32 = value is long l ? (uint)l : 0;
        return value is long;
    }

    public void Set(string key, FileEntry file, uint crc32)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO crc_cache (store, path, size, mtime, crc) VALUES ($store, $path, $size, $mtime, $crc)
            ON CONFLICT (store, path) DO UPDATE SET size = excluded.size, mtime = excluded.mtime, crc = excluded.crc;
            """;
        command.Parameters.AddWithValue("$store", key);
        command.Parameters.AddWithValue("$path", file.Path);
        command.Parameters.AddWithValue("$size", file.Size);
        command.Parameters.AddWithValue("$mtime", file.LastWriteUtc.Ticks);
        command.Parameters.AddWithValue("$crc", (long)crc32);
        command.ExecuteNonQuery();
    }

    public void AddRename(string platformId, string oldBaseName, string newBaseName)
    {
        if (oldBaseName == newBaseName) return;

        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO renames (platform, old_base, new_base, created) VALUES ($platform, $old, $new, $created);";
        command.Parameters.AddWithValue("$platform", platformId);
        command.Parameters.AddWithValue("$old", oldBaseName);
        command.Parameters.AddWithValue("$new", newBaseName);
        command.Parameters.AddWithValue("$created", DateTime.UtcNow.Ticks);
        command.ExecuteNonQuery();
    }

    /// <summary>Renames in the order they happened, so chains (A→B, then B→C) replay correctly.</summary>
    public IReadOnlyList<RenameRecord> GetRenames(string? platformId = null)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, platform, old_base, new_base, created FROM renames";
        if (platformId != null)
        {
            command.CommandText += " WHERE platform = $platform";
            command.Parameters.AddWithValue("$platform", platformId);
        }
        command.CommandText += " ORDER BY id;";

        var result = new List<RenameRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new RenameRecord(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), new DateTime(reader.GetInt64(4), DateTimeKind.Utc)));
        }
        return result;
    }

    public string? GetTitleOverride(string platformId, string baseName)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT title FROM title_overrides WHERE platform = $platform AND base = $base;";
        command.Parameters.AddWithValue("$platform", platformId);
        command.Parameters.AddWithValue("$base", baseName);
        return command.ExecuteScalar() as string;
    }

    public void SetTitleOverride(string platformId, string baseName, string title)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO title_overrides (platform, base, title) VALUES ($platform, $base, $title)
            ON CONFLICT (platform, base) DO UPDATE SET title = excluded.title;
            """;
        command.Parameters.AddWithValue("$platform", platformId);
        command.Parameters.AddWithValue("$base", baseName);
        command.Parameters.AddWithValue("$title", title);
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
