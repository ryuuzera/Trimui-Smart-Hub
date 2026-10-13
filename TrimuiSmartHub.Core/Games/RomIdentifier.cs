using TrimuiSmartHub.Core.Platforms;
using TrimuiSmartHub.Core.Storage;

namespace TrimuiSmartHub.Core.Games;

public enum IdentificationSource
{
    None,
    Crc,
    RomName,
    Name,
}

public sealed record RomIdentification(string? Name, IdentificationSource Source, uint? Crc32)
{
    public static readonly RomIdentification Unknown = new(null, IdentificationSource.None, null);

    public bool IsIdentified => Source != IdentificationSource.None && !string.IsNullOrEmpty(Name);
}

/// <summary>Remembers CRCs of files that didn't change, so rescans don't re-read every ROM.</summary>
public interface ICrcCache
{
    bool TryGet(string key, FileEntry file, out uint crc32);

    void Set(string key, FileEntry file, uint crc32);
}

/// <summary>
/// Finds the canonical (No-Intro / MAME) name of a ROM. This is what turns "mario3.zip" into
/// "Super Mario Bros. 3 (USA)", which maps 1:1 to a libretro thumbnail.
/// </summary>
public sealed class RomIdentifier
{
    private readonly GameDatabase _database;
    private readonly ICrcCache? _cache;

    public RomIdentifier(GameDatabase database, ICrcCache? cache = null)
    {
        _database = database;
        _cache = cache;
    }

    public async Task<RomIdentification> IdentifyAsync(IFileStore store, FileEntry file, Platform platform, CancellationToken cancellationToken = default)
    {
        var systems = platform.DbSystems;
        if (systems.Count == 0) return RomIdentification.Unknown;

        if (platform.Arcade)
        {
            // Arcade sets are identified by their zip name ("sf2.zip"); the CRC of the zip itself means nothing.
            var arcade = await _database.FindByRomNameAsync(file.Name, systems, cancellationToken);
            return arcade == null ? RomIdentification.Unknown : new RomIdentification(arcade.Name, IdentificationSource.RomName, null);
        }

        uint? crc = await TryGetCrcAsync(store, file, cancellationToken);
        if (crc.HasValue)
        {
            var byCrc = await _database.FindByCrcAsync(crc.Value, systems, cancellationToken);
            if (byCrc != null) return new RomIdentification(byCrc.Name, IdentificationSource.Crc, crc);
        }

        var byRomName = await _database.FindByRomNameAsync(file.Name, systems, cancellationToken);
        if (byRomName != null) return new RomIdentification(byRomName.Name, IdentificationSource.RomName, crc);

        var byName = await _database.FindByNameAsync(RelPath.GetBaseName(file.Name), systems, cancellationToken);
        if (byName != null) return new RomIdentification(byName.Name, IdentificationSource.Name, crc);

        return new RomIdentification(null, IdentificationSource.None, crc);
    }

    /// <summary>
    /// CRC of the ROM content. For zips the CRC of the largest entry is read from the zip directory,
    /// which costs a few bytes of I/O instead of decompressing the file.
    /// </summary>
    public async Task<uint?> TryGetCrcAsync(IFileStore store, FileEntry file, CancellationToken cancellationToken = default)
    {
        var cacheKey = store.DisplayName;
        if (_cache != null && _cache.TryGet(cacheKey, file, out var cached)) return cached;

        var extension = RelPath.GetExtension(file.Name);
        uint? crc = null;

        try
        {
            if (extension == "zip")
            {
                await using var stream = store.OpenRead(file.Path);
                var entries = FileHasher.ReadZipEntries(stream);
                if (entries.Count > 0) crc = entries.OrderByDescending(e => e.Size).First().Crc32;
            }
            else if (extension != "7z" && file.Size <= FileHasher.MaxHashSize)
            {
                await using var stream = store.OpenRead(file.Path);
                crc = await FileHasher.Crc32Async(stream, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return null;
        }

        if (crc.HasValue) _cache?.Set(cacheKey, file, crc.Value);
        return crc;
    }
}
