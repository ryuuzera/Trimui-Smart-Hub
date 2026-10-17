using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrimuiSmartHub.Application.Infrastructure;
using TrimuiSmartHub.Application.Localization;
using TrimuiSmartHub.Core.Bios;
using TrimuiSmartHub.Core.Library;
using TrimuiSmartHub.Core.Platforms;
using TrimuiSmartHub.Core.Storage;
using TrimuiSmartHub.Core.Sync;

namespace TrimuiSmartHub.Application.ViewModels;

public sealed record BiosRow(string Platform, string File, bool Required, string? Note, BiosState Library, BiosState? Device, bool PlatformReadyOnDevice);

/// <summary>Which BIOS files are present and valid (MD5), in the library and on the device.</summary>
public sealed partial class BiosViewModel : PageViewModel
{
    private readonly LibraryContext _context;
    private readonly PlatformRegistry _platforms;
    private readonly BiosChecker _checker;
    private readonly DialogService _dialogs;
    private readonly Navigator _navigator;
    private readonly SyncViewModel _sync;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _summary;

    public BiosViewModel(DeviceMonitor monitor, LibraryContext context, PlatformRegistry platforms, BiosChecker checker, DialogService dialogs, Navigator navigator, SyncViewModel sync)
    {
        Monitor = monitor;
        _context = context;
        _platforms = platforms;
        _checker = checker;
        _dialogs = dialogs;
        _navigator = navigator;
        _sync = sync;
    }

    public DeviceMonitor Monitor { get; }

    public ObservableCollection<BiosRow> Rows { get; } = new();

    public override void OnNavigatedTo() => _ = CheckAsync();

    [RelayCommand]
    private async Task CheckAsync()
    {
        IsLoading = true;
        try
        {
            var library = _context.Library;
            var device = Monitor.Current;

            var (libraryStatus, deviceStatus) = await Task.Run(async () =>
            {
                var installed = device?.Layout.GetPlatforms(device.Store).Select(p => p.PlatformId).ToList();
                var lib = await _checker.CheckAsync(library.Store, LibraryService.BiosRoot, installed);
                var dev = device == null ? null : await _checker.CheckAsync(device.Store, device.Layout.BiosDir, installed);
                return (lib, dev);
            });

            Rows.Clear();
            foreach (var platform in libraryStatus)
            {
                var onDevice = deviceStatus?.FirstOrDefault(d => d.Platform.Id == platform.Platform.Id);
                for (var i = 0; i < platform.Files.Count; i++)
                {
                    var file = platform.Files[i];
                    Rows.Add(new BiosRow(platform.Platform.Name, file.File.File, file.File.Required, file.File.Note,
                        file.State, onDevice?.Files[i].State, onDevice?.IsReady ?? platform.IsReady));
                }
            }

            var notReady = (deviceStatus ?? libraryStatus).Where(p => p.HasRequired && !p.IsReady).Select(p => p.Platform.Name).ToList();
            Summary = notReady.Count == 0 ? Loc.Get("BiosAllReady") : Loc.Format("BiosMissingFor", string.Join(", ", notReady));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Summary = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Copies picked files into the library BIOS folder, at the path each emulator expects (e.g. "dc/dc_boot.bin").</summary>
    [RelayCommand]
    private async Task AddFilesAsync()
    {
        var files = _dialogs.PickFiles(Loc.Get("BiosPickFiles"));
        if (files.Count == 0) return;

        var known = _platforms.All.SelectMany(p => p.Bios)
            .GroupBy(b => RelPath.GetFileName(b.File), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().File, StringComparer.OrdinalIgnoreCase);

        var store = _context.Library.Store;
        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            var target = RelPath.Combine(LibraryService.BiosRoot, known.GetValueOrDefault(name, name));
            await using var input = File.OpenRead(file);
            await store.WriteAtomicAsync(target, input, File.GetLastWriteTimeUtc(file));
        }

        await CheckAsync();
    }

    [RelayCommand]
    private void SyncBios()
    {
        _sync.Direction = SyncDirection.ToDevice;
        _sync.IncludeRoms = false;
        _sync.IncludeBoxarts = false;
        _sync.IncludeBios = true;
        _navigator.GoTo<SyncViewModel>();
        _sync.BuildPlanCommand.Execute(null);
    }

    [RelayCommand]
    private void OpenFolder() => ShellService.OpenFolder(_context.Library.Store.GetFullPath(LibraryService.BiosRoot));
}
