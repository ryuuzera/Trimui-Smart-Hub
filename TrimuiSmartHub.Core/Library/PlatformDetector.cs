using TrimuiSmartHub.Core.Games;
using TrimuiSmartHub.Core.Platforms;
using TrimuiSmartHub.Core.Storage;

namespace TrimuiSmartHub.Core.Library;

public sealed record PlatformDetection(Platform? Platform, IReadOnlyList<Platform> Candidates)
{
    public static readonly PlatformDetection None = new(null, Array.Empty<Platform>());
}

/// <summary>
/// Guesses the platform of a file being imported: first by unambiguous extension, then by the
/// extensions inside a zip, then by looking up the CRC in the game database.
/// </summary>
public sealed class PlatformDetector
{
    private readonly PlatformRegistry _platforms;
    private readonly GameDatabase? _database;

    public PlatformDetector(PlatformRegistry platforms, GameDatabase? database)
    {
        _platforms = platforms;
        _database = database;
    }

    public async Task<PlatformDetection> DetectAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var extension = RelPath.GetExtension(filePath);

        var byExtension = _platforms.FindByExtension(extension);
        if (byExtension != null) return new PlatformDetection(byExtension, new[] { byExtension });

        var crcs = new List<uint>();
        try
        {
            if (extension == "zip")
            {
                await using var stream = File.OpenRead(filePath);
                var entries = FileHasher.ReadZipEntries(stream);

                var inner = entries
                    .Select(e => _platforms.FindByExtension(RelPath.GetExtension(e.Name)))
                    .OfType<Platform>()
                    .Distinct()
                    .ToList();
                if (inner.Count == 1) return new PlatformDetection(inner[0], inner);

                crcs.AddRange(entries.OrderByDescending(e => e.Size).Take(3).Select(e => e.Crc32));
            }
            else if (extension != "7z" && new FileInfo(filePath).Length <= FileHasher.MaxHashSize)
            {
                await using var stream = File.OpenRead(filePath);
                crcs.Add(await FileHasher.Crc32Async(stream, cancellationToken));
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return PlatformDetection.None;
        }

        if (_database == null || crcs.Count == 0) return PlatformDetection.None;

        var candidates = new List<Platform>();
        foreach (var crc in crcs)
        {
            foreach (var record in await _database.FindAllByCrcAsync(crc, cancellationToken))
            {
                foreach (var platform in _platforms.All.Where(p => p.DbSystems.Contains(record.System, StringComparer.OrdinalIgnoreCase)))
                {
                    if (!candidates.Contains(platform)) candidates.Add(platform);
                }
            }
            if (candidates.Count > 0) break;
        }

        // Arcade sets exist in several arcade platforms (MAME, FBNeo, CPS...): let the user choose.
        return new PlatformDetection(candidates.Count == 1 ? candidates[0] : null, candidates);
    }
}
