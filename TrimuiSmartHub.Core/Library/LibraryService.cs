using System.IO.Compression;
using TrimuiSmartHub.Core.Games;
using TrimuiSmartHub.Core.Platforms;
using TrimuiSmartHub.Core.Storage;

namespace TrimuiSmartHub.Core.Library;

public sealed record LibraryRom(string PlatformId, FileEntry File, string? BoxartPath)
{
    public string BaseName => RelPath.GetBaseName(File.Name);
}

public sealed record PlatformSummary(Platform Platform, int RomCount, long TotalSize, int WithBoxart)
{
    public int MissingBoxart => RomCount - WithBoxart;
}

public enum ImportStatus
{
    Imported,
    AlreadyExists,
    Unresolved,
    Failed,
}

public sealed record ImportOutcome(string SourcePath, ImportStatus Status, Platform? Platform = null, IReadOnlyList<Platform>? Candidates = null, string? Error = null);

/// <summary>
/// The library on the PC is the source of truth for ROMs, box arts and BIOS. Layout (independent of
/// the firmware; device layouts map it to device paths):
/// <code>
/// roms/&lt;PLATFORM&gt;/...            media/&lt;PLATFORM&gt;/boxart/&lt;rom base name&gt;.png
/// bios/...                       backups/saves/&lt;device id&gt;/&lt;timestamp&gt;/...
/// .hub/library.db
/// </code>
/// </summary>
public sealed class LibraryService
{
    public const string RomsRoot = "roms";
    public const string MediaRoot = "media";
    public const string BiosRoot = "bios";
    public const string BackupsRoot = "backups/saves";
    public const string HubRoot = ".hub";

    private readonly PlatformRegistry _platforms;
    private readonly PlatformDetector _detector;

    public LocalFileStore Store { get; }

    public LibraryIndex Index { get; }

    public string RootPath => Store.RootPath;

    public LibraryService(string rootPath, PlatformRegistry platforms, GameDatabase? database)
    {
        Directory.CreateDirectory(rootPath);
        Store = new LocalFileStore(rootPath, "library");
        _platforms = platforms;
        _detector = new PlatformDetector(platforms, database);
        Index = new LibraryIndex(Path.Combine(Store.RootPath, HubRoot, "library.db"));

        foreach (var dir in new[] { RomsRoot, MediaRoot, BiosRoot, BackupsRoot }) Store.CreateDirectory(dir);
    }

    public static string RomsDir(string platformId) => RelPath.Combine(RomsRoot, platformId);

    public static string BoxartDir(string platformId) => RelPath.Combine(MediaRoot, platformId, "boxart");

    public static string BoxartPathFor(string platformId, string romFileName) =>
        RelPath.Combine(BoxartDir(platformId), RelPath.GetBaseName(romFileName) + ".png");

    /// <summary>Files that live in ROM folders but are not ROMs (OS metadata, caches, temp files).</summary>
    public static bool IsIgnoredFile(string fileName)
    {
        if (string.IsNullOrEmpty(fileName) || fileName.StartsWith('.')) return true;
        var lower = fileName.ToLowerInvariant();
        return lower is "thumbs.db" or "desktop.ini"
            || lower.EndsWith(".db")
            || lower.EndsWith(".db-journal")
            || lower.EndsWith(LocalFileStore.TempSuffix);
    }

    public IReadOnlyList<string> GetPlatformIds() =>
        Store.EnumerateDirectories(RomsRoot)
            .Select(RelPath.GetFileName)
            .OrderBy(id => _platforms.GetOrUnknown(id).Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public IReadOnlyList<PlatformSummary> GetPlatformSummaries() =>
        GetPlatformIds()
            .Select(id =>
            {
                var roms = GetRoms(id);
                return new PlatformSummary(_platforms.GetOrUnknown(id), roms.Count, roms.Sum(r => r.File.Size), roms.Count(r => r.BoxartPath != null));
            })
            .ToList();

    public IReadOnlyList<LibraryRom> GetRoms(string platformId)
    {
        var boxarts = Store.EnumerateFiles(BoxartDir(platformId), recursive: false)
            .ToDictionary(f => RelPath.GetBaseName(f.Name), f => f.Path, StringComparer.OrdinalIgnoreCase);

        return Store.EnumerateFiles(RomsDir(platformId), recursive: true)
            .Where(f => !IsIgnoredFile(f.Name))
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(f => new LibraryRom(platformId, f, boxarts.GetValueOrDefault(RelPath.GetBaseName(f.Name))))
            .ToList();
    }

    /// <summary>Box arts whose ROM no longer exists.</summary>
    public IReadOnlyList<FileEntry> GetOrphanBoxarts(string platformId)
    {
        var romBases = GetRoms(platformId).Select(r => r.BaseName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Store.EnumerateFiles(BoxartDir(platformId), recursive: false)
            .Where(f => !romBases.Contains(RelPath.GetBaseName(f.Name)))
            .ToList();
    }

    /// <summary>Groups of ROMs with the same content (same CRC), largest groups first.</summary>
    public async Task<IReadOnlyList<IReadOnlyList<LibraryRom>>> FindDuplicatesAsync(string platformId, RomIdentifier identifier, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var platform = _platforms.GetOrUnknown(platformId);
        if (platform.Arcade) return Array.Empty<IReadOnlyList<LibraryRom>>();

        var roms = GetRoms(platformId);
        var byCrc = new Dictionary<uint, List<LibraryRom>>();
        for (var i = 0; i < roms.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var crc = await identifier.TryGetCrcAsync(Store, roms[i].File, cancellationToken);
            if (crc.HasValue)
            {
                if (!byCrc.TryGetValue(crc.Value, out var list)) byCrc[crc.Value] = list = new List<LibraryRom>();
                list.Add(roms[i]);
            }
            progress?.Report((i + 1.0) / roms.Count);
        }

        return byCrc.Values.Where(g => g.Count > 1).OrderByDescending(g => g.Count).Select(g => (IReadOnlyList<LibraryRom>)g).ToList();
    }

    public async Task<IReadOnlyList<ImportOutcome>> ImportAsync(IEnumerable<string> sourcePaths, string? platformHint = null, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var files = ExpandSources(sourcePaths).ToList();
        var hint = platformHint == null ? null : _platforms.GetOrUnknown(platformHint);
        var outcomes = new List<ImportOutcome>();

        for (var i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = files[i];

            var detection = await _detector.DetectAsync(file, cancellationToken);
            var platform = detection.Platform
                ?? (hint != null && (detection.Candidates.Count == 0 || detection.Candidates.Contains(hint)) ? hint : null);

            outcomes.Add(platform == null
                ? new ImportOutcome(file, ImportStatus.Unresolved, Candidates: detection.Candidates)
                : await ImportFileAsync(file, platform, cancellationToken));

            progress?.Report((i + 1.0) / files.Count);
        }

        return outcomes;
    }

    public async Task<ImportOutcome> ImportFileAsync(string sourcePath, Platform platform, CancellationToken cancellationToken = default)
    {
        try
        {
            var extension = RelPath.GetExtension(sourcePath);
            if (platform.ExtractZip && extension == "zip")
                return await ExtractZipAsync(sourcePath, platform, cancellationToken);

            var destination = RelPath.Combine(RomsDir(platform.Id), RelPath.SanitizeFileName(Path.GetFileName(sourcePath)));
            if (Store.FileExists(destination)) return new ImportOutcome(sourcePath, ImportStatus.AlreadyExists, platform);

            await using var input = File.OpenRead(sourcePath);
            await Store.WriteAtomicAsync(destination, input, File.GetLastWriteTimeUtc(sourcePath), cancellationToken: cancellationToken);
            return new ImportOutcome(sourcePath, ImportStatus.Imported, platform);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return new ImportOutcome(sourcePath, ImportStatus.Failed, platform, Error: ex.Message);
        }
    }

    private async Task<ImportOutcome> ExtractZipAsync(string zipPath, Platform platform, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var entries = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToList();

        var targets = new List<(ZipArchiveEntry Entry, string Destination)>();
        foreach (var entry in entries)
        {
            // Resolve() rejects "../" entries, so a malicious zip can't write outside the ROM folder.
            var relative = RelPath.Resolve(entry.FullName)
                ?? throw new InvalidDataException($"Unsafe path in zip: {entry.FullName}");
            var sanitized = string.Join('/', relative.Split('/').Select(RelPath.SanitizeFileName));
            targets.Add((entry, RelPath.Combine(RomsDir(platform.Id), sanitized)));
        }

        if (targets.Count > 0 && targets.All(t => Store.FileExists(t.Destination)))
            return new ImportOutcome(zipPath, ImportStatus.AlreadyExists, platform);

        foreach (var (entry, destination) in targets)
        {
            await using var input = entry.Open();
            await Store.WriteAtomicAsync(destination, input, entry.LastWriteTime.UtcDateTime, cancellationToken: cancellationToken);
        }

        return new ImportOutcome(zipPath, ImportStatus.Imported, platform);
    }

    private static IEnumerable<string> ExpandSources(IEnumerable<string> sourcePaths)
    {
        foreach (var path in sourcePaths)
        {
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    if (!IsIgnoredFile(Path.GetFileName(file))) yield return file;
                }
            }
            else if (File.Exists(path))
            {
                yield return path;
            }
        }
    }

    /// <summary>
    /// Renames a ROM and its box art, and records the rename so the next sync renames the ROM and its
    /// saves on the device too. Without this, the game would lose its save progress on the device.
    /// </summary>
    public LibraryRom Rename(LibraryRom rom, string newBaseName)
    {
        newBaseName = RelPath.SanitizeFileName(newBaseName);
        if (string.IsNullOrWhiteSpace(newBaseName)) throw new ArgumentException("Invalid name.", nameof(newBaseName));
        if (newBaseName == rom.BaseName) return rom;

        var extension = Path.GetExtension(rom.File.Name);
        var newRomPath = RelPath.Combine(RelPath.GetDirectory(rom.File.Path), newBaseName + extension);
        if (Store.FileExists(newRomPath)) throw new IOException($"A ROM named \"{newBaseName}{extension}\" already exists.");

        Store.Move(rom.File.Path, newRomPath);

        string? newBoxart = null;
        if (rom.BoxartPath != null)
        {
            newBoxart = BoxartPathFor(rom.PlatformId, newBaseName + extension);
            Store.Move(rom.BoxartPath, newBoxart, overwrite: true);
        }

        Index.AddRename(rom.PlatformId, rom.BaseName, newBaseName);

        return new LibraryRom(rom.PlatformId, Store.GetFile(newRomPath)!, newBoxart);
    }

    /// <param name="delete">Deletes one full path. The app sends files to the recycle bin.</param>
    public void Delete(IEnumerable<LibraryRom> roms, Action<string> delete)
    {
        foreach (var rom in roms)
        {
            delete(Store.GetFullPath(rom.File.Path));
            if (rom.BoxartPath != null && Store.FileExists(rom.BoxartPath)) delete(Store.GetFullPath(rom.BoxartPath));
        }
    }
}
