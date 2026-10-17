using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrimuiSmartHub.Application.Infrastructure;
using TrimuiSmartHub.Application.Localization;
using TrimuiSmartHub.Core.Library;
using TrimuiSmartHub.Core.Platforms;
using TrimuiSmartHub.Core.Storage;

namespace TrimuiSmartHub.Application.ViewModels;

public sealed partial class RomRow : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public RomRow(LibraryRom rom, string? boxartFullPath)
    {
        Rom = rom;
        BoxartFullPath = boxartFullPath;
    }

    public LibraryRom Rom { get; }

    public string Name => Rom.File.Name;

    public string Folder => RelPath.GetDirectory(Rom.File.Path).Split('/').Length > 2 ? RelPath.GetDirectory(Rom.File.Path) : string.Empty;

    public long Size => Rom.File.Size;

    public string? BoxartFullPath { get; }

    public bool HasBoxart => BoxartFullPath != null;
}

public sealed partial class PendingImport : ObservableObject
{
    [ObservableProperty]
    private Platform? _chosen;

    public PendingImport(string path, IReadOnlyList<Platform> candidates)
    {
        Path = path;
        Candidates = candidates;
        _chosen = candidates.Count == 1 ? candidates[0] : null;
    }

    public string Path { get; }

    public string FileName => System.IO.Path.GetFileName(Path);

    public IReadOnlyList<Platform> Candidates { get; }
}

public sealed partial class LibraryViewModel : PageViewModel
{
    private readonly LibraryContext _context;
    private readonly PlatformRegistry _platforms;
    private readonly JobsViewModel _jobs;
    private readonly DialogService _dialogs;
    private readonly Navigator _navigator;
    private readonly ScrapeViewModel _scrape;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScrapeMissingCommand), nameof(RenameToCanonicalCommand), nameof(FindDuplicatesCommand), nameof(CleanOrphansCommand), nameof(OpenFolderCommand))]
    private PlatformSummary? _selectedPlatform;

    [ObservableProperty]
    private string _filter = string.Empty;

    [ObservableProperty]
    private bool _onlyMissingBoxart;

    [ObservableProperty]
    private bool _isLoading;

    public LibraryViewModel(LibraryContext context, PlatformRegistry platforms, JobsViewModel jobs, DialogService dialogs, Navigator navigator, ScrapeViewModel scrape)
    {
        _context = context;
        _platforms = platforms;
        _jobs = jobs;
        _dialogs = dialogs;
        _navigator = navigator;
        _scrape = scrape;

        RomsView = CollectionViewSource.GetDefaultView(Roms);
        RomsView.Filter = o => o is RomRow row
            && (!OnlyMissingBoxart || !row.HasBoxart)
            && (string.IsNullOrWhiteSpace(Filter) || row.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase));

        context.Changed += () => System.Windows.Application.Current.Dispatcher.BeginInvoke(LoadPlatforms);
    }

    public string LibraryPath => _context.Library.RootPath;

    public ObservableCollection<PlatformSummary> Platforms { get; } = new();

    public ObservableCollection<RomRow> Roms { get; } = new();

    public ICollectionView RomsView { get; }

    public ObservableCollection<PendingImport> PendingImports { get; } = new();

    public IReadOnlyList<Platform> AllPlatforms => _platforms.All.OrderBy(p => p.Name).ToList();

    public override void OnNavigatedTo() => LoadPlatforms();

    partial void OnFilterChanged(string value) => RomsView.Refresh();

    partial void OnOnlyMissingBoxartChanged(bool value) => RomsView.Refresh();

    partial void OnSelectedPlatformChanged(PlatformSummary? value) => LoadRoms();

    [RelayCommand]
    private void LoadPlatforms()
    {
        OnPropertyChanged(nameof(LibraryPath));
        var selectedId = SelectedPlatform?.Platform.Id;
        var library = _context.Library;
        IsLoading = true;

        Task.Run(() => library.GetPlatformSummaries()).ContinueWith(t =>
        {
            IsLoading = false;
            if (t.IsFaulted) return;

            Platforms.Clear();
            foreach (var summary in t.Result) Platforms.Add(summary);
            SelectedPlatform = Platforms.FirstOrDefault(p => p.Platform.Id == selectedId) ?? Platforms.FirstOrDefault();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void LoadRoms()
    {
        Roms.Clear();
        var platform = SelectedPlatform;
        if (platform == null) return;

        var library = _context.Library;
        Task.Run(() => library.GetRoms(platform.Platform.Id)
                .Select(r => new RomRow(r, r.BoxartPath == null ? null : library.Store.GetFullPath(r.BoxartPath)))
                .ToList())
            .ContinueWith(t =>
            {
                if (t.IsFaulted || SelectedPlatform != platform) return;
                foreach (var row in t.Result) Roms.Add(row);
            }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private List<RomRow> SelectedRoms() => Roms.Where(r => r.IsSelected).ToList();

    [RelayCommand]
    private void ImportFiles()
    {
        var files = _dialogs.PickFiles(Loc.Get("ImportPickFiles"));
        if (files.Count > 0) Import(files);
    }

    [RelayCommand]
    private void ImportFolder()
    {
        var folder = _dialogs.PickFolder(Loc.Get("ImportPickFolder"));
        if (folder != null) Import(new[] { folder });
    }

    /// <summary>Drag &amp; drop target. Files dropped while a platform is open default to that platform when ambiguous.</summary>
    public void Import(IReadOnlyList<string> paths)
    {
        var library = _context.Library;
        var hint = SelectedPlatform?.Platform.Id;

        var job = _jobs.Run(Loc.Format("JobImport", paths.Count), (progress, ct) =>
            library.ImportAsync(paths, hint, new Progress<double>(p => progress.Report(new Core.Jobs.JobProgress(p))), ct));

        _ = ReportImportAsync(job);
    }

    private async Task ReportImportAsync(Core.Jobs.Job job)
    {
        IReadOnlyList<ImportOutcome> outcomes;
        try
        {
            outcomes = (IReadOnlyList<ImportOutcome>)(await job.Completion)!;
        }
        catch (Exception)
        {
            return;
        }

        foreach (var unresolved in outcomes.Where(o => o.Status == ImportStatus.Unresolved))
        {
            var candidates = unresolved.Candidates is { Count: > 0 } ? unresolved.Candidates : AllPlatforms;
            PendingImports.Add(new PendingImport(unresolved.SourcePath, candidates));
        }

        LoadPlatforms();

        var failed = outcomes.Where(o => o.Status == ImportStatus.Failed).ToList();
        var message = Loc.Format("ImportResult",
            outcomes.Count(o => o.Status == ImportStatus.Imported),
            outcomes.Count(o => o.Status == ImportStatus.AlreadyExists),
            outcomes.Count(o => o.Status == ImportStatus.Unresolved),
            failed.Count);
        if (failed.Count > 0) message += Environment.NewLine + string.Join(Environment.NewLine, failed.Take(5).Select(f => $"{Path.GetFileName(f.SourcePath)}: {f.Error}"));

        await _dialogs.ShowAsync(Loc.Get("ImportTitle"), message);
    }

    [RelayCommand]
    private void ImportPending()
    {
        var ready = PendingImports.Where(p => p.Chosen != null).ToList();
        if (ready.Count == 0) return;

        var library = _context.Library;
        var job = _jobs.Run(Loc.Format("JobImport", ready.Count), async (progress, ct) =>
        {
            var outcomes = new List<ImportOutcome>();
            for (var i = 0; i < ready.Count; i++)
            {
                outcomes.Add(await library.ImportFileAsync(ready[i].Path, ready[i].Chosen!, ct));
                progress.Report(new Core.Jobs.JobProgress((i + 1.0) / ready.Count));
            }
            return (IReadOnlyList<ImportOutcome>)outcomes;
        });

        foreach (var item in ready) PendingImports.Remove(item);
        _ = ReportImportAsync(job);
    }

    [RelayCommand]
    private void DiscardPending() => PendingImports.Clear();

    private bool HasPlatform() => SelectedPlatform != null;

    [RelayCommand(CanExecute = nameof(HasPlatform))]
    private void ScrapeMissing()
    {
        if (SelectedPlatform == null) return;
        _scrape.StartForLibrary(SelectedPlatform.Platform.Id);
        _navigator.GoTo<ScrapeViewModel>();
    }

    [RelayCommand]
    private async Task RenameAsync(RomRow? row)
    {
        row ??= SelectedRoms().FirstOrDefault();
        if (row == null) return;

        var name = await _dialogs.InputAsync(Loc.Get("RenameTitle"), Loc.Get("RenameMessage"), row.Rom.BaseName);
        if (string.IsNullOrWhiteSpace(name) || name == row.Rom.BaseName) return;

        try
        {
            _context.Library.Rename(row.Rom, name);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            await _dialogs.ShowAsync(Loc.Get("Error"), ex.Message);
        }
        LoadRoms();
    }

    /// <summary>Renames ROMs to their database (No-Intro / MAME) names. Saves follow on the next sync.</summary>
    [RelayCommand(CanExecute = nameof(HasPlatform))]
    private async Task RenameToCanonicalAsync()
    {
        var platform = SelectedPlatform?.Platform;
        if (platform == null) return;

        var targets = SelectedRoms();
        if (targets.Count == 0) targets = Roms.ToList();
        if (platform.Arcade)
        {
            await _dialogs.ShowAsync(Loc.Get("RenameCanonicalTitle"), Loc.Get("RenameCanonicalArcade"));
            return;
        }
        if (!await _dialogs.ConfirmAsync(Loc.Get("RenameCanonicalTitle"), Loc.Format("RenameCanonicalConfirm", targets.Count), Loc.Get("Rename")))
            return;

        var library = _context.Library;
        var identifier = _context.Identifier;
        var roms = targets.Select(r => r.Rom).ToList();

        var job = _jobs.Run(Loc.Format("JobRenameCanonical", platform.Name), async (progress, ct) =>
        {
            var renamed = 0;
            for (var i = 0; i < roms.Count; i++)
            {
                var rom = roms[i];
                var identification = await identifier.IdentifyAsync(library.Store, rom.File, platform, ct);
                if (identification.IsIdentified)
                {
                    var name = RelPath.SanitizeFileName(identification.Name!);
                    if (name != rom.BaseName)
                    {
                        try
                        {
                            library.Rename(rom, name);
                            renamed++;
                        }
                        catch (IOException)
                        {
                            // Name already taken (duplicate): leave it for the duplicates tool.
                        }
                    }
                }
                progress.Report(new Core.Jobs.JobProgress((i + 1.0) / roms.Count, rom.File.Name));
            }
            return renamed;
        });

        try
        {
            var renamed = (int)(await job.Completion)!;
            await _dialogs.ShowAsync(Loc.Get("RenameCanonicalTitle"), Loc.Format("RenameCanonicalResult", renamed, roms.Count));
        }
        catch (Exception)
        {
            // Failure/cancel is visible in the tasks panel.
        }
        LoadPlatforms();
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        var targets = SelectedRoms();
        if (targets.Count == 0) return;

        if (!await _dialogs.ConfirmAsync(Loc.Get("DeleteTitle"), Loc.Format("DeleteRomsMessage", targets.Count), Loc.Get("Delete")))
            return;

        _context.Library.Delete(targets.Select(r => r.Rom), ShellService.RecycleFile);
        LoadPlatforms();
    }

    [RelayCommand(CanExecute = nameof(HasPlatform))]
    private async Task FindDuplicatesAsync()
    {
        var platform = SelectedPlatform?.Platform;
        if (platform == null) return;

        var library = _context.Library;
        var identifier = _context.Identifier;
        var job = _jobs.Run(Loc.Format("JobDuplicates", platform.Name), (progress, ct) =>
            library.FindDuplicatesAsync(platform.Id, identifier, new Progress<double>(p => progress.Report(new Core.Jobs.JobProgress(p))), ct));

        try
        {
            var groups = (IReadOnlyList<IReadOnlyList<LibraryRom>>)(await job.Completion)!;
            if (groups.Count == 0)
            {
                await _dialogs.ShowAsync(Loc.Get("DuplicatesTitle"), Loc.Get("DuplicatesNone"));
                return;
            }

            // Select every copy except the first of each group, so the user can review and delete.
            var extra = groups.SelectMany(g => g.Skip(1)).Select(r => r.File.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var row in Roms) row.IsSelected = extra.Contains(row.Rom.File.Path);

            var lines = groups.Take(15).Select(g => string.Join("  =  ", g.Select(r => r.File.Name)));
            await _dialogs.ShowAsync(Loc.Get("DuplicatesTitle"), Loc.Format("DuplicatesFound", groups.Count, extra.Count) + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, lines));
        }
        catch (Exception)
        {
            // Visible in the tasks panel.
        }
    }

    [RelayCommand(CanExecute = nameof(HasPlatform))]
    private async Task CleanOrphansAsync()
    {
        var platform = SelectedPlatform?.Platform;
        if (platform == null) return;

        var orphans = _context.Library.GetOrphanBoxarts(platform.Id);
        if (orphans.Count == 0)
        {
            await _dialogs.ShowAsync(Loc.Get("OrphansTitle"), Loc.Get("OrphansNone"));
            return;
        }

        if (!await _dialogs.ConfirmAsync(Loc.Get("OrphansTitle"), Loc.Format("OrphansConfirm", orphans.Count), Loc.Get("Delete")))
            return;

        foreach (var orphan in orphans) ShellService.RecycleFile(_context.Library.Store.GetFullPath(orphan.Path));
        LoadPlatforms();
    }

    [RelayCommand(CanExecute = nameof(HasPlatform))]
    private void OpenFolder()
    {
        if (SelectedPlatform != null)
            ShellService.OpenFolder(_context.Library.Store.GetFullPath(LibraryService.RomsDir(SelectedPlatform.Platform.Id)));
    }

    [RelayCommand]
    private void OpenLibraryFolder() => ShellService.OpenFolder(_context.Library.RootPath);
}
