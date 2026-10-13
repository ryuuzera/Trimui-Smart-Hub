using TrimuiSmartHub.Core.Games;
using TrimuiSmartHub.Core.Platforms;
using TrimuiSmartHub.Core.Storage;

namespace TrimuiSmartHub.Core.Bios;

public enum BiosState
{
    Ok,
    Missing,
    /// <summary>File exists but its MD5 is not a known good dump.</summary>
    WrongHash,
}

public sealed record BiosFileStatus(BiosFile File, BiosState State, string? ActualMd5);

public sealed record PlatformBiosStatus(Platform Platform, IReadOnlyList<BiosFileStatus> Files)
{
    /// <summary>
    /// False when a required file (or every alternative of a required group) is missing or bad: the
    /// emulator won't start games. Optional files only improve compatibility.
    /// </summary>
    public bool IsReady
    {
        get
        {
            var required = Files.Where(f => f.File.Required).ToList();
            var groups = required.Where(f => f.File.Group != null).GroupBy(f => f.File.Group);
            var singles = required.Where(f => f.File.Group == null);

            return singles.All(f => f.State == BiosState.Ok) && groups.All(g => g.Any(f => f.State == BiosState.Ok));
        }
    }

    public bool HasRequired => Files.Any(f => f.File.Required);
}

public sealed class BiosChecker
{
    private readonly PlatformRegistry _platforms;

    public BiosChecker(PlatformRegistry platforms)
    {
        _platforms = platforms;
    }

    /// <param name="biosDir">BIOS folder in the store ("bios" in the library, "BIOS" on the device).</param>
    /// <param name="platformIds">Platforms to check (e.g. those installed on the device); null = all with BIOS.</param>
    public async Task<IReadOnlyList<PlatformBiosStatus>> CheckAsync(IFileStore store, string biosDir, IEnumerable<string>? platformIds = null, CancellationToken cancellationToken = default)
    {
        var wanted = platformIds?.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hashCache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var result = new List<PlatformBiosStatus>();

        foreach (var platform in _platforms.All.Where(p => p.Bios.Count > 0))
        {
            if (wanted != null && !wanted.Contains(platform.Id)) continue;

            var files = new List<BiosFileStatus>();
            foreach (var bios in platform.Bios)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = RelPath.Combine(biosDir, bios.File);

                if (!store.FileExists(path))
                {
                    files.Add(new BiosFileStatus(bios, BiosState.Missing, null));
                    continue;
                }

                if (bios.Md5.Count == 0)
                {
                    files.Add(new BiosFileStatus(bios, BiosState.Ok, null));
                    continue;
                }

                if (!hashCache.TryGetValue(path, out var md5))
                {
                    await using var stream = store.OpenRead(path);
                    md5 = await FileHasher.Md5Async(stream, cancellationToken);
                    hashCache[path] = md5;
                }

                var state = bios.Md5.Contains(md5, StringComparer.OrdinalIgnoreCase) ? BiosState.Ok : BiosState.WrongHash;
                files.Add(new BiosFileStatus(bios, state, md5));
            }

            result.Add(new PlatformBiosStatus(platform, files));
        }

        return result;
    }
}
