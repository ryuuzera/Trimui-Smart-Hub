using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrimuiSmartHub.Application.Infrastructure;
using TrimuiSmartHub.Application.Localization;
using TrimuiSmartHub.Core.Devices;
using TrimuiSmartHub.Core.Games;
using TrimuiSmartHub.Core.Library;
using TrimuiSmartHub.Core.Platforms;
using TrimuiSmartHub.Core.Scraping;
using TrimuiSmartHub.Core.Settings;
using TrimuiSmartHub.Core.Storage;

namespace TrimuiSmartHub.Application.ViewModels;

public enum ScrapeSource
{
    Library,
    Device,
}

public sealed partial class ScrapeTarget : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public required Platform Platform { get; init; }

    public required IFileStore RomStore { get; init; }

    public required IReadOnlyList<FileEntry> Roms { get; init; }

    public required IFileStore ImageStore { get; init; }

    public required string ImageDir { get; init; }

    public required int Missing { get; init; }
}

public sealed partial class ReviewItem : ObservableObject
{
    [ObservableProperty]
    private TitleMatch? _chosen;

    public required ScrapeRequest Request { get; init; }

    public required FileEntry Rom { get; init; }

    public required IReadOnlyList<TitleMatch> Suggestions { get; init; }

    public string PlatformName => Request.Platform.Name;
}

/// <summary>
/// Box art download. Works on the library (offline-first) or directly on the connected device, for
/// people who don't use the library. Uncertain matches are never applied blindly: they go to review.
/// </summary>
public sealed partial class ScrapeViewModel : PageViewModel
{
    private readonly LibraryContext _context;
    private readonly PlatformRegistry _platforms;
    private readonly JobsViewModel _jobs;
    private readonly DialogService _dialogs;
    private readonly SettingsStore _settings;
    private string? _pendingLibraryPlatform;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDeviceSource))]
    private ScrapeSource _source = ScrapeSource.Library;

    [ObservableProperty]
    private MediaKind _kind;

    [ObservableProperty]
    private bool _overwrite;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _lastResult;

    public ScrapeViewModel(DeviceMonitor monitor, LibraryContext context, PlatformRegistry platforms, JobsViewModel jobs, DialogService dialogs, SettingsStore settings)
    {
        Monitor = monitor;
        _context = context;
        _platforms = platforms;
        _jobs = jobs;
        _dialogs = dialogs;
        _settings = settings;
        _kind = settings.Current.ImageKind;

        monitor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(DeviceMonitor.Current)) return;
            if (Source == ScrapeSource.Device && monitor.Current == null) Source = ScrapeSource.Library;
            else if (Source == ScrapeSource.Device) LoadTargets();
        };
    }

    public DeviceMonitor Monitor { get; }

    public bool IsDeviceSource
    {
        get => Source == ScrapeSource.Device;
        set => Source = value ? ScrapeSource.Device : ScrapeSource.Library;
    }

    public IReadOnlyList<MediaKind> Kinds { get; } = Enum.GetValues<MediaKind>();

    public ObservableCollection<ScrapeTarget> Targets { get; } = new();

    public ObservableCollection<ReviewItem> Review { get; } = new();

    public override void OnNavigatedTo() => LoadTargets();

    partial void OnSourceChanged(ScrapeSource value) => LoadTargets();

    /// <summary>Opened from the library page: select that platform and start right away.</summary>
    public void StartForLibrary(string platformId)
    {
        _pendingLibraryPlatform = platformId;
        if (Source != ScrapeSource.Library) Source = ScrapeSource.Library;
        else LoadTargets();
    }

    [RelayCommand]
    private void LoadTargets()
    {
        var source = Source;
        var library = _context.Library;
        var device = Monitor.Current;
        IsLoading = true;

        Task.Run(() => source == ScrapeSource.Library ? LibraryTargets(library) : device == null ? new List<ScrapeTarget>() : DeviceTargets(device))
            .ContinueWith(t =>
            {
                IsLoading = false;
                Targets.Clear();
                if (t.IsFaulted) return;
                foreach (var target in t.Result) Targets.Add(target);

                if (_pendingLibraryPlatform != null && source == ScrapeSource.Library)
                {
                    var target = Targets.FirstOrDefault(x => x.Platform.Id.Equals(_pendingLibraryPlatform, StringComparison.OrdinalIgnoreCase));
                    _pendingLibraryPlatform = null;
                    if (target != null)
                    {
                        foreach (var other in Targets) other.IsSelected = other == target;
                        StartCommand.Execute(null);
                    }
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private List<ScrapeTarget> LibraryTargets(LibraryService library) =>
        library.GetPlatformIds()
            .Select(id =>
            {
                var roms = library.GetRoms(id);
                return new ScrapeTarget
                {
                    Platform = _platforms.GetOrUnknown(id),
                    RomStore = library.Store,
                    Roms = roms.Select(r => r.File).ToList(),
                    ImageStore = library.Store,
                    ImageDir = LibraryService.BoxartDir(id),
                    Missing = roms.Count(r => r.BoxartPath == null),
                };
            })
            .Where(t => t.Roms.Count > 0 && t.Platform.LibretroSystems.Count > 0)
            .ToList();

    private List<ScrapeTarget> DeviceTargets(DeviceInfo device) =>
        device.Layout.GetPlatforms(device.Store)
            .Where(p => p.ImagesDir != null)
            .Select(p =>
            {
                var roms = device.Store.EnumerateFiles(p.RomsDir, recursive: true)
                    .Where(f => !device.Layout.IsExcludedFromSync(f.Name) && !LibraryService.IsIgnoredFile(f.Name))
                    .ToList();
                var images = device.Store.EnumerateFiles(p.ImagesDir!, recursive: false)
                    .Select(f => RelPath.GetBaseName(f.Name))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                return new ScrapeTarget
                {
                    Platform = _platforms.GetOrUnknown(p.PlatformId),
                    RomStore = device.Store,
                    Roms = roms,
                    ImageStore = device.Store,
                    ImageDir = p.ImagesDir!,
                    Missing = roms.Count(r => !images.Contains(RelPath.GetBaseName(r.Name))),
                };
            })
            .Where(t => t.Roms.Count > 0 && t.Platform.LibretroSystems.Count > 0)
            .ToList();

    [RelayCommand]
    private void SelectAll()
    {
        var value = !Targets.All(t => t.IsSelected);
        foreach (var target in Targets) target.IsSelected = value;
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        var selected = Targets.Where(t => t.IsSelected).ToList();
        if (selected.Count == 0)
        {
            await _dialogs.ShowAsync(Loc.Get("NavBoxarts"), Loc.Get("ScrapeSelectPlatform"));
            return;
        }

        var scraper = _context.Scraper;
        var kind = Kind;
        var overwrite = Overwrite;
        Review.Clear();
        LastResult = null;

        var requests = selected.Select(t => new ScrapeRequest
        {
            Platform = t.Platform,
            RomStore = t.RomStore,
            Roms = t.Roms,
            ImageStore = t.ImageStore,
            ImageDir = t.ImageDir,
            Kind = kind,
            Overwrite = overwrite,
        }).ToList();

        var title = selected.Count == 1 ? Loc.Format("JobScrapeOne", selected[0].Platform.Name) : Loc.Format("JobScrapeMany", selected.Count);
        var job = _jobs.Run(title, async (progress, ct) =>
        {
            var results = new List<(ScrapeRequest Request, ScrapeOutcome Outcome)>();
            for (var i = 0; i < requests.Count; i++)
            {
                var index = i;
                var inner = new Core.Jobs.InlineProgress<Core.Jobs.JobProgress>(p =>
                    progress.Report(new Core.Jobs.JobProgress((index + (p.Fraction ?? 0)) / requests.Count, $"{requests[index].Platform.Name} · {p.Message}")));
                foreach (var outcome in await scraper.ScrapeAsync(requests[i], inner, ct)) results.Add((requests[i], outcome));
            }
            return results;
        });

        try
        {
            var results = (List<(ScrapeRequest Request, ScrapeOutcome Outcome)>)(await job.Completion)!;

            foreach (var (request, outcome) in results.Where(r => r.Outcome.Status == ScrapeStatus.NeedsReview))
            {
                Review.Add(new ReviewItem { Request = request, Rom = outcome.Rom, Suggestions = outcome.Suggestions!, Chosen = outcome.Suggestions![0] });
            }

            LastResult = Loc.Format("ScrapeResult",
                results.Count(r => r.Outcome.Status == ScrapeStatus.Downloaded),
                results.Count(r => r.Outcome.Status == ScrapeStatus.AlreadyHad),
                Review.Count,
                results.Count(r => r.Outcome.Status is ScrapeStatus.NotFound or ScrapeStatus.Failed));
        }
        catch (OperationCanceledException)
        {
            LastResult = Loc.Get("JobStatus_Canceled");
        }
        catch (Exception ex)
        {
            LastResult = ex.Message;
        }

        LoadTargets();
    }

    [RelayCommand]
    private async Task ApplyReviewAsync()
    {
        var chosen = Review.Where(r => r.Chosen != null).ToList();
        if (chosen.Count == 0) return;

        var scraper = _context.Scraper;
        var job = _jobs.Run(Loc.Format("JobScrapeReview", chosen.Count), async (progress, ct) =>
        {
            var applied = new List<ReviewItem>();
            for (var i = 0; i < chosen.Count; i++)
            {
                var item = chosen[i];
                if (await scraper.ApplyTitleAsync(item.Request, item.Rom, item.Chosen!.Candidate, ct)) applied.Add(item);
                progress.Report(new Core.Jobs.JobProgress((i + 1.0) / chosen.Count, item.Rom.Name));
            }
            return applied;
        });

        try
        {
            foreach (var item in (List<ReviewItem>)(await job.Completion)!) Review.Remove(item);
        }
        catch (Exception)
        {
            // Visible in the tasks panel.
        }
        LoadTargets();
    }

    [RelayCommand]
    private void SkipReview(ReviewItem? item)
    {
        if (item != null) Review.Remove(item);
    }
}
