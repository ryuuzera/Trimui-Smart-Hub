using Microsoft.Extensions.Logging;
using TrimuiSmartHub.Core.Games;
using TrimuiSmartHub.Core.Library;
using TrimuiSmartHub.Core.Platforms;
using TrimuiSmartHub.Core.Saves;
using TrimuiSmartHub.Core.Scraping;
using TrimuiSmartHub.Core.Settings;

namespace TrimuiSmartHub.Application.Infrastructure;

/// <summary>
/// Services bound to the current library folder. Recreated when the user picks another folder in the
/// settings, so pages always talk to the right library.
/// </summary>
public sealed class LibraryContext
{
    private readonly PlatformRegistry _platforms;
    private readonly GameDatabase _database;
    private readonly LibretroThumbnails _thumbnails;
    private readonly ILoggerFactory _loggerFactory;

    public LibraryContext(SettingsStore settings, PlatformRegistry platforms, GameDatabase database, LibretroThumbnails thumbnails, ILoggerFactory loggerFactory)
    {
        _platforms = platforms;
        _database = database;
        _thumbnails = thumbnails;
        _loggerFactory = loggerFactory;

        Load(settings.Current.LibraryPath);
        settings.Changed += s =>
        {
            if (!string.Equals(s.LibraryPath, Library.RootPath, StringComparison.OrdinalIgnoreCase)) Load(s.LibraryPath);
        };
    }

    public LibraryService Library { get; private set; } = null!;

    public RomIdentifier Identifier { get; private set; } = null!;

    public ScrapeService Scraper { get; private set; } = null!;

    public SaveBackupService Saves { get; private set; } = null!;

    /// <summary>Raised after the library folder changed.</summary>
    public event Action? Changed;

    private void Load(string path)
    {
        Library = new LibraryService(path, _platforms, _database);
        Identifier = new RomIdentifier(_database, Library.Index);
        Scraper = new ScrapeService(_thumbnails, Identifier, Library.Index, _loggerFactory.CreateLogger<ScrapeService>());
        Saves = new SaveBackupService(Library);
        Changed?.Invoke();
    }
}
