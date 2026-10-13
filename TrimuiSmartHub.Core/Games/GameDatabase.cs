using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TrimuiSmartHub.Core.Games;

public sealed record GameRecord(string Name, string System, string RomName, string? Crc32);

/// <summary>
/// The bundled libretro-based game database (~500k entries with CRC32). The shipped file has no indexes,
/// so on first use it is copied to the app data folder and indexed once; lookups then take milliseconds.
/// </summary>
public sealed class GameDatabase
{
    private const int SchemaVersion = 1;

    private readonly string _sourcePath;
    private readonly string _preparedPath;
    private readonly ILogger<GameDatabase> _logger;
    private readonly SemaphoreSlim _prepareLock = new(1, 1);
    private string? _connectionString;

    public GameDatabase(string sourcePath, string dataDirectory, ILogger<GameDatabase>? logger = null)
    {
        _sourcePath = sourcePath;
        _preparedPath = Path.Combine(dataDirectory, "games.db");
        _logger = logger ?? NullLogger<GameDatabase>.Instance;
    }

    public bool IsAvailable => File.Exists(_sourcePath) || File.Exists(_preparedPath);

    public async Task EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        if (_connectionString != null) return;

        await _prepareLock.WaitAsync(cancellationToken);
        try
        {
            if (_connectionString != null) return;

            if (!IsPrepared())
            {
                if (!File.Exists(_sourcePath))
                    throw new FileNotFoundException("Game database not found. Reinstall the application.", _sourcePath);

                await Task.Run(() => Prepare(cancellationToken), cancellationToken);
            }

            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = _preparedPath,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Shared,
            }.ToString();
        }
        finally
        {
            _prepareLock.Release();
        }
    }

    private bool IsPrepared()
    {
        if (!File.Exists(_preparedPath)) return false;

        // A newer database shipped with an update replaces the prepared copy.
        if (File.Exists(_sourcePath) && File.GetLastWriteTimeUtc(_sourcePath) > File.GetLastWriteTimeUtc(_preparedPath)) return false;

        try
        {
            using var connection = new SqliteConnection($"Data Source={_preparedPath};Mode=ReadOnly");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            return Convert.ToInt32(command.ExecuteScalar()) == SchemaVersion;
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    private void Prepare(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Preparing game database at {Path}", _preparedPath);

        Directory.CreateDirectory(Path.GetDirectoryName(_preparedPath)!);
        var temp = _preparedPath + ".tmp";
        File.Copy(_sourcePath, temp, overwrite: true);

        using (var connection = new SqliteConnection($"Data Source={temp};Pooling=False"))
        {
            connection.Open();
            using var transaction = connection.BeginTransaction();
            foreach (var sql in new[]
            {
                "UPDATE games SET crc32 = upper(crc32) WHERE crc32 IS NOT NULL;",
                "CREATE INDEX IF NOT EXISTS ix_games_crc ON games(crc32);",
                "CREATE INDEX IF NOT EXISTS ix_games_rom ON games(romName COLLATE NOCASE);",
                "CREATE INDEX IF NOT EXISTS ix_games_system_name ON games(system, name);",
                $"PRAGMA user_version = {SchemaVersion};",
            })
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
            transaction.Commit();
        }

        File.Move(temp, _preparedPath, overwrite: true);
        _logger.LogInformation("Game database ready");
    }

    public async Task<GameRecord?> FindByCrcAsync(uint crc32, IReadOnlyCollection<string>? systems = null, CancellationToken cancellationToken = default)
    {
        var matches = await QueryAsync(
            "SELECT name, system, romName, crc32 FROM games WHERE crc32 = $value",
            crc32.ToString("X8"), systems, cancellationToken);
        return PickBySystemOrder(matches, systems);
    }

    public async Task<GameRecord?> FindByRomNameAsync(string fileName, IReadOnlyCollection<string>? systems = null, CancellationToken cancellationToken = default)
    {
        var matches = await QueryAsync(
            "SELECT name, system, romName, crc32 FROM games WHERE romName = $value COLLATE NOCASE",
            fileName, systems, cancellationToken);
        return PickBySystemOrder(matches, systems);
    }

    public async Task<GameRecord?> FindByNameAsync(string name, IReadOnlyCollection<string>? systems = null, CancellationToken cancellationToken = default)
    {
        var matches = await QueryAsync(
            "SELECT name, system, romName, crc32 FROM games WHERE name = $value COLLATE NOCASE",
            name, systems, cancellationToken);
        return PickBySystemOrder(matches, systems);
    }

    /// <summary>All systems that contain a game with this CRC (used to guess the platform of an imported file).</summary>
    public Task<List<GameRecord>> FindAllByCrcAsync(uint crc32, CancellationToken cancellationToken = default) =>
        QueryAsync("SELECT name, system, romName, crc32 FROM games WHERE crc32 = $value", crc32.ToString("X8"), null, cancellationToken);

    private async Task<List<GameRecord>> QueryAsync(string sql, string value, IReadOnlyCollection<string>? systems, CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = sql;
        command.Parameters.AddWithValue("$value", value);

        if (systems is { Count: > 0 })
        {
            var names = systems.Select((_, i) => $"$s{i}").ToList();
            command.CommandText += $" AND system IN ({string.Join(',', names)})";
            var index = 0;
            foreach (var system in systems) command.Parameters.AddWithValue($"$s{index++}", system);
        }

        command.CommandText += " LIMIT 50;";

        var result = new List<GameRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new GameRecord(
                reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
        }

        return result;
    }

    private static GameRecord? PickBySystemOrder(List<GameRecord> matches, IReadOnlyCollection<string>? systems)
    {
        if (matches.Count == 0) return null;
        if (systems is not { Count: > 0 }) return matches[0];

        var order = systems.Select((s, i) => (s, i)).ToDictionary(x => x.s, x => x.i, StringComparer.OrdinalIgnoreCase);
        return matches.OrderBy(m => order.GetValueOrDefault(m.System, int.MaxValue)).First();
    }
}
