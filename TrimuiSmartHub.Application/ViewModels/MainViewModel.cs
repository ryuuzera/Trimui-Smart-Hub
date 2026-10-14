using System.IO;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MahApps.Metro.IconPacks;
using Microsoft.Extensions.Logging;
using TrimuiSmartHub.Application.Infrastructure;
using TrimuiSmartHub.Application.Localization;
using TrimuiSmartHub.Core.Devices;
using TrimuiSmartHub.Core.Games;
using TrimuiSmartHub.Core.Saves;
using TrimuiSmartHub.Core.Settings;
using TrimuiSmartHub.Core.Updates;

namespace TrimuiSmartHub.Application.ViewModels;

public sealed record NavItem(string Label, PackIconMaterialKind Icon, PageViewModel Page);

public sealed partial class MainViewModel : ObservableObject
{
    private readonly SettingsStore _settings;
    private readonly LibraryContext _library;
    private readonly UpdateChecker _updates;
    private readonly GameDatabase _database;
    private readonly ILogger<MainViewModel> _logger;

    [ObservableProperty]
    private NavItem? _selected;

    [ObservableProperty]
    private PageViewModel? _currentPage;

    [ObservableProperty]
    private AvailableUpdate? _update;

    public MainViewModel(
        DeviceMonitor monitor,
        JobsViewModel jobs,
        SettingsStore settings,
        LibraryContext library,
        UpdateChecker updates,
        GameDatabase database,
        Navigator navigator,
        ILogger<MainViewModel> logger,
        DeviceViewModel device,
        LibraryViewModel libraryPage,
        ScrapeViewModel scrape,
        DownloadsViewModel downloads,
        SyncViewModel sync,
        SavesViewModel saves,
        BiosViewModel bios,
        StorageViewModel storage,
        SettingsViewModel settingsPage)
    {
        Monitor = monitor;
        Jobs = jobs;
        _settings = settings;
        _library = library;
        _updates = updates;
        _database = database;
        _logger = logger;

        Items = new[]
        {
            new NavItem(Loc.Get("NavDevice"), PackIconMaterialKind.GamepadVariantOutline, device),
            new NavItem(Loc.Get("NavLibrary"), PackIconMaterialKind.Bookshelf, libraryPage),
            new NavItem(Loc.Get("NavBoxarts"), PackIconMaterialKind.ImageMultipleOutline, scrape),
            new NavItem(Loc.Get("NavDownloads"), PackIconMaterialKind.CloudDownloadOutline, downloads),
            new NavItem(Loc.Get("NavSync"), PackIconMaterialKind.Sync, sync),
            new NavItem(Loc.Get("NavSaves"), PackIconMaterialKind.ContentSaveOutline, saves),
            new NavItem(Loc.Get("NavBios"), PackIconMaterialKind.Chip, bios),
            new NavItem(Loc.Get("NavStorage"), PackIconMaterialKind.ChartDonut, storage),
            new NavItem(Loc.Get("NavJobs"), PackIconMaterialKind.ProgressClock, jobs),
            new NavItem(Loc.Get("NavSettings"), PackIconMaterialKind.CogOutline, settingsPage),
        };

        monitor.Connected += OnDeviceConnected;
        navigator.NavigationRequested += type => Selected = Items.FirstOrDefault(i => i.Page.GetType() == type) ?? Selected;
        Selected = Items[0];
    }

    public DeviceMonitor Monitor { get; }

    public JobsViewModel Jobs { get; }

    public IReadOnlyList<NavItem> Items { get; }

    public string Version { get; } = "v" + (Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0");

    partial void OnSelectedChanged(NavItem? value)
    {
        if (value == null) return;
        CurrentPage = value.Page;
        value.Page.OnNavigatedTo();
    }

    /// <summary>Runs once the window is shown.</summary>
    public async Task StartAsync()
    {
        if (_database.IsAvailable && _database.NeedsPreparation)
        {
            Jobs.Run(Loc.Get("JobPrepareDatabase"), (_, ct) => _database.EnsureReadyAsync(ct));
        }

        Monitor.Start();

        if (_settings.Current.CheckForUpdates)
        {
            try
            {
                var current = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);
                Update = await _updates.CheckAsync(current);
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "Update check failed");
            }
        }
    }

    private void OnDeviceConnected(DeviceInfo device)
    {
        if (!_settings.Current.AutoBackupSaves) return;

        var saves = _library.Saves;
        var retention = new SaveRetention { KeepLatest = _settings.Current.BackupKeepLatest, KeepMonthly = _settings.Current.BackupKeepMonthly };

        Jobs.Run(Loc.Format("JobBackupSaves", device.Name), async (progress, ct) =>
        {
            var backup = await saves.BackupAsync(device, force: false, progress, ct);
            saves.ApplyRetention(device.Id, retention, path => Directory.Delete(path, recursive: true));
            return backup;
        });
    }

    [RelayCommand]
    private void OpenUpdate()
    {
        if (Update != null) ShellService.OpenUrl(Update.Page);
    }

    [RelayCommand]
    private void DismissUpdate() => Update = null;

    [RelayCommand]
    private void ShowJobs() => Selected = Items.First(i => i.Page is JobsViewModel);
}
