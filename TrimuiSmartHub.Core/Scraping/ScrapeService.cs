using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TrimuiSmartHub.Core.Games;
using TrimuiSmartHub.Core.Jobs;
using TrimuiSmartHub.Core.Library;
using TrimuiSmartHub.Core.Platforms;
using TrimuiSmartHub.Core.Storage;

namespace TrimuiSmartHub.Core.Scraping;

public enum ScrapeStatus
{
    AlreadyHad,
    Downloaded,
    NeedsReview,
    NotFound,
    Failed,
}

/// <param name="Suggestions">Candidate titles for manual review, best first.</param>
public sealed record ScrapeOutcome(FileEntry Rom, ScrapeStatus Status, string? Title = null, IReadOnlyList<TitleMatch>? Suggestions = null, string? Error = null);

public sealed class ScrapeRequest
{
    public required Platform Platform { get; init; }

    /// <summary>Where the ROMs are (library or device).</summary>
    public required IFileStore RomStore { get; init; }

    public required IReadOnlyList<FileEntry> Roms { get; init; }

    /// <summary>Where images are written; can be another store than the ROMs.</summary>
    public required IFileStore ImageStore { get; init; }

    public required string ImageDir { get; init; }

    public MediaKind Kind { get; init; } = MediaKind.Boxart;

    public bool Overwrite { get; init; }
}

/// <summary>
/// Downloads images for ROMs. Order of precedence: a title the user picked before, the canonical name
/// from the database (CRC/file name), then fuzzy matching. Low-confidence fuzzy matches are never applied
/// automatically; they are returned for review.
/// </summary>
public sealed class ScrapeService
{
    private const int MaxParallelDownloads = 4;

    private readonly LibretroThumbnails _thumbnails;
    private readonly RomIdentifier _identifier;
    private readonly LibraryIndex? _index;
    private readonly ILogger<ScrapeService> _logger;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Catalog> _catalogs = new();

    public ScrapeService(LibretroThumbnails thumbnails, RomIdentifier identifier, LibraryIndex? index = null, ILogger<ScrapeService>? logger = null)
    {
        _thumbnails = thumbnails;
        _identifier = identifier;
        _index = index;
        _logger = logger ?? NullLogger<ScrapeService>.Instance;
    }

    public async Task<IReadOnlyList<ScrapeOutcome>> ScrapeAsync(ScrapeRequest request, IProgress<JobProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var platform = request.Platform;
        if (platform.LibretroSystems.Count == 0)
            return request.Roms.Select(r => new ScrapeOutcome(r, ScrapeStatus.NotFound, Error: "No image source for this platform.")).ToList();

        var catalog = await LoadCatalogAsync(platform, request.Kind, cancellationToken);
        var outcomes = new ScrapeOutcome[request.Roms.Count];
        var done = 0;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, request.Roms.Count),
            new ParallelOptions { MaxDegreeOfParallelism = MaxParallelDownloads, CancellationToken = cancellationToken },
            async (i, ct) =>
            {
                var rom = request.Roms[i];
                outcomes[i] = await ScrapeOneAsync(request, catalog, rom, ct);
                var count = Interlocked.Increment(ref done);
                progress?.Report(new JobProgress((double)count / request.Roms.Count, rom.Name));
            });

        return outcomes;
    }

    /// <summary>Applies a title chosen by the user during review and remembers it for next time.</summary>
    public async Task<bool> ApplyTitleAsync(ScrapeRequest request, FileEntry rom, string title, CancellationToken cancellationToken = default)
    {
        var catalog = await LoadCatalogAsync(request.Platform, request.Kind, cancellationToken);
        if (!catalog.Entries.TryGetValue(LibretroThumbnails.SanitizeTitle(title), out var entry)) return false;

        var saved = await DownloadAndSaveAsync(request, rom, entry, cancellationToken);
        if (saved) _index?.SetTitleOverride(request.Platform.Id, RelPath.GetBaseName(rom.Name), title);
        return saved;
    }

    private async Task<ScrapeOutcome> ScrapeOneAsync(ScrapeRequest request, Catalog catalog, FileEntry rom, CancellationToken cancellationToken)
    {
        try
        {
            var imagePath = RelPath.Combine(request.ImageDir, RelPath.GetBaseName(rom.Name) + ".png");
            if (!request.Overwrite && request.ImageStore.FileExists(imagePath))
                return new ScrapeOutcome(rom, ScrapeStatus.AlreadyHad);

            var baseName = RelPath.GetBaseName(rom.Name);

            var overridden = _index?.GetTitleOverride(request.Platform.Id, baseName);
            if (overridden != null && catalog.Entries.TryGetValue(LibretroThumbnails.SanitizeTitle(overridden), out var chosen))
                return await SaveAsync(request, rom, chosen);

            var identification = await _identifier.IdentifyAsync(request.RomStore, rom, request.Platform, cancellationToken);
            foreach (var candidate in new[] { identification.Name, baseName })
            {
                if (candidate != null && catalog.Entries.TryGetValue(LibretroThumbnails.SanitizeTitle(candidate), out var exact))
                    return await SaveAsync(request, rom, exact);
            }

            var query = identification.Name ?? baseName;
            var matches = catalog.Titles.FindBest(LibretroThumbnails.SanitizeTitle(query));
            if (matches.Count > 0 && matches[0].Score >= TitleMatcher.AutoAcceptScore)
                return await SaveAsync(request, rom, catalog.Entries[matches[0].Candidate]);

            return new ScrapeOutcome(rom, matches.Count > 0 ? ScrapeStatus.NeedsReview : ScrapeStatus.NotFound, Suggestions: matches);

            async Task<ScrapeOutcome> SaveAsync(ScrapeRequest r, FileEntry f, CatalogEntry entry) =>
                await DownloadAndSaveAsync(r, f, entry, cancellationToken)
                    ? new ScrapeOutcome(f, ScrapeStatus.Downloaded, entry.Title)
                    : new ScrapeOutcome(f, ScrapeStatus.NotFound, entry.Title);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Scrape failed for {Rom}", rom.Name);
            return new ScrapeOutcome(rom, ScrapeStatus.Failed, Error: ex.Message);
        }
    }

    private async Task<bool> DownloadAndSaveAsync(ScrapeRequest request, FileEntry rom, CatalogEntry entry, CancellationToken cancellationToken)
    {
        var bytes = await _thumbnails.DownloadAsync(LibretroThumbnails.BuildUrl(entry.System, request.Kind, entry.Title), cancellationToken);
        if (bytes == null) return false;

        var imagePath = RelPath.Combine(request.ImageDir, RelPath.GetBaseName(rom.Name) + ".png");
        await using var content = new MemoryStream(bytes);
        await request.ImageStore.WriteAtomicAsync(imagePath, content, cancellationToken: cancellationToken);
        return true;
    }

    /// <param name="Title">Title as named on the server (already sanitized).</param>
    private sealed record CatalogEntry(string System, string Title);

    private sealed record Catalog(Dictionary<string, CatalogEntry> Entries, TitleIndex Titles);

    /// <summary>Union of the platform's libretro systems; earlier systems win on duplicate titles.</summary>
    private async Task<Catalog> LoadCatalogAsync(Platform platform, MediaKind kind, CancellationToken cancellationToken)
    {
        var key = $"{platform.Id}|{kind}";
        if (_catalogs.TryGetValue(key, out var cached)) return cached;

        var entries = new Dictionary<string, CatalogEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var system in platform.LibretroSystems)
        {
            try
            {
                foreach (var title in await _thumbnails.GetIndexAsync(system, kind, cancellationToken))
                {
                    entries.TryAdd(title, new CatalogEntry(system, title));
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Could not load thumbnail index for {System}", system);
            }
        }

        if (entries.Count == 0) throw new HttpRequestException("Could not reach thumbnails.libretro.com. Check your internet connection.");

        var catalog = new Catalog(entries, new TitleIndex(entries.Keys));
        _catalogs[key] = catalog;
        return catalog;
    }
}
