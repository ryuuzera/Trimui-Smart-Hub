using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrimuiSmartHub.Application.Infrastructure;
using TrimuiSmartHub.Application.Localization;
using TrimuiSmartHub.Core.Devices;
using TrimuiSmartHub.Core.Sync;

namespace TrimuiSmartHub.Application.ViewModels;

/// <summary>Home page: the connected device, or how to connect one. Everything else works without it.</summary>
public sealed partial class DeviceViewModel : PageViewModel
{
    private readonly LibraryContext _library;
    private readonly JobsViewModel _jobs;
    private readonly DialogService _dialogs;
    private readonly Navigator _navigator;
    private readonly SyncViewModel _sync;

    [ObservableProperty]
    private int _platformCount;

    [ObservableProperty]
    private long? _freeSpace;

    [ObservableProperty]
    private string? _format;

    [ObservableProperty]
    private int _saveDirCount;

    public DeviceViewModel(DeviceMonitor monitor, LibraryContext library, JobsViewModel jobs, DialogService dialogs, Navigator navigator, SyncViewModel sync)
    {
        Monitor = monitor;
        _library = library;
        _jobs = jobs;
        _dialogs = dialogs;
        _navigator = navigator;
        _sync = sync;

        monitor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DeviceMonitor.Current)) Refresh();
        };
    }

    public DeviceMonitor Monitor { get; }

    public override void OnNavigatedTo() => Refresh();

    private void Refresh()
    {
        var device = Monitor.Current;
        if (device == null)
        {
            PlatformCount = 0;
            FreeSpace = null;
            Format = null;
            SaveDirCount = 0;
            return;
        }

        Task.Run(() =>
        {
            var platforms = device.Layout.GetPlatforms(device.Store).Count;
            var saves = device.Layout.GetSaveDirs(device.Store).Count;
            var free = device.Store.AvailableFreeSpace;
            var format = device.Store.Format;
            System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
            {
                PlatformCount = platforms;
                SaveDirCount = saves;
                FreeSpace = free;
                Format = format;
            });
        });
    }

    [RelayCommand]
    private void Rescan() => Monitor.Rescan();

    [RelayCommand]
    private void SelectDevice(DeviceInfo? device)
    {
        if (device != null) Monitor.Select(device);
    }

    [RelayCommand]
    private void SyncToDevice()
    {
        _sync.Direction = SyncDirection.ToDevice;
        _navigator.GoTo<SyncViewModel>();
        _sync.BuildPlanCommand.Execute(null);
    }

    [RelayCommand]
    private void ImportFromDevice()
    {
        _sync.Direction = SyncDirection.FromDevice;
        _navigator.GoTo<SyncViewModel>();
        _sync.BuildPlanCommand.Execute(null);
    }

    [RelayCommand]
    private void BackupNow()
    {
        var device = Monitor.Current;
        if (device == null) return;

        var saves = _library.Saves;
        _jobs.Run(Loc.Format("JobBackupSaves", device.Name), (progress, ct) => saves.BackupAsync(device, force: true, progress, ct));
        _navigator.GoTo<SavesViewModel>();
    }

    [RelayCommand]
    private void OpenFolder()
    {
        if (Monitor.Current != null) ShellService.OpenFolder(Monitor.Current.RootPath);
    }

    [RelayCommand]
    private async Task RenameAsync()
    {
        var device = Monitor.Current;
        if (device == null) return;

        var name = await _dialogs.InputAsync(Loc.Get("DeviceRenameTitle"), Loc.Get("DeviceRenameMessage"), device.Name);
        if (string.IsNullOrWhiteSpace(name)) return;

        if (!DeviceIdentity.TrySave(device.Store, new DeviceIdentity { Id = device.Id, Name = name.Trim() }))
        {
            await _dialogs.ShowAsync(Loc.Get("Error"), Loc.Get("DeviceRenameFailed"));
            return;
        }
        Monitor.Rescan();
    }

    [RelayCommand]
    private async Task EjectAsync()
    {
        var device = Monitor.Current;
        if (device == null) return;

        if (_jobs.ActiveCount > 0 && !await _dialogs.ConfirmAsync(Loc.Get("EjectTitle"), Loc.Get("EjectWhileBusy"), Loc.Get("Eject")))
            return;

        if (!ShellService.Eject(device.RootPath))
            await _dialogs.ShowAsync(Loc.Get("EjectTitle"), Loc.Get("EjectFailed"));
        else
            Monitor.Rescan();
    }
}
