using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrimuiSmartHub.Application.Infrastructure;
using TrimuiSmartHub.Application.Localization;
using TrimuiSmartHub.Core.Downloads;
using TrimuiSmartHub.Core.Library;
using TrimuiSmartHub.Core.Platforms;

namespace TrimuiSmartHub.Application.ViewModels;

/// <summary>
/// Optional download source. Downloads land in the library (not straight on the device), are extracted
/// when the platform needs it, and reach the device through the normal sync.
/// </summary>
public sealed partial class DownloadsViewModel : PageViewModel
{
    private readonly RetrosticClient _client;
    private readonly LibraryContext _context;
    private readonly JobsViewModel _jobs;
    private readonly DialogService _dialogs;
    private readonly string _tempDirectory;
    private CancellationTokenSource? _searchCancellation;

    [ObservableProperty]
    private Platform? _platform;

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _status;

    public DownloadsViewModel(RetrosticClient client, PlatformRegistry platforms, LibraryContext context, JobsViewModel jobs, DialogService dialogs, AppPaths paths)
    {
        _client = client;
        _context = context;
        _jobs = jobs;
        _dialogs = dialogs;
        _tempDirectory = paths.DownloadsTemp;
        Platforms = platforms.All.Where(RetrosticClient.Supports).OrderBy(p => p.Name).ToList();
    }

    public IReadOnlyList<Platform> Platforms { get; }

    public ObservableCollection<RemoteGame> Results { get; } = new();

    partial void OnPlatformChanged(Platform? value)
    {
        Query = string.Empty;
        _ = LoadAsync(null);
    }

    [RelayCommand]
    private Task SearchAsync() => LoadAsync(string.IsNullOrWhiteSpace(Query) ? null : Query.Trim());

    private async Task LoadAsync(string? query)
    {
        var platform = Platform;
        if (platform == null) return;

        _searchCancellation?.Cancel();
        var cancellation = _searchCancellation = new CancellationTokenSource();

        Results.Clear();
        Status = null;
        IsLoading = true;
        try
        {
            var games = query == null
                ? await _client.ListPopularAsync(platform, cancellation.Token)
                : await _client.SearchAsync(platform, query, cancellation.Token);

            if (cancellation.IsCancellationRequested) return;
            foreach (var game in games) Results.Add(game);
            Status = games.Count == 0 ? Loc.Get("DownloadsNoResults") : null;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            Status = Loc.Format("DownloadsSourceError", ex.Message);
        }
        finally
        {
            if (_searchCancellation == cancellation) IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DownloadAsync(RemoteGame? game)
    {
        var platform = Platform;
        if (game == null || platform == null) return;

        var library = _context.Library;
        var job = _jobs.Run(Loc.Format("JobDownload", game.Title), async (progress, ct) =>
        {
            var file = await _client.DownloadAsync(game, _tempDirectory, progress, ct);
            try
            {
                progress.Report(new Core.Jobs.JobProgress(null, Loc.Get("DownloadsImporting")));
                return await library.ImportFileAsync(file, platform, ct);
            }
            finally
            {
                File.Delete(file);
            }
        });

        try
        {
            var outcome = (ImportOutcome)(await job.Completion)!;
            if (outcome.Status == ImportStatus.Failed) await _dialogs.ShowAsync(Loc.Get("Error"), outcome.Error ?? string.Empty);
        }
        catch (Exception)
        {
            // Failure/cancel is visible in the tasks panel.
        }
    }
}
