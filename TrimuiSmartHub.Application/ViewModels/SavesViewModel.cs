using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrimuiSmartHub.Application.Infrastructure;
using TrimuiSmartHub.Application.Localization;
using TrimuiSmartHub.Core.Library;
using TrimuiSmartHub.Core.Saves;
using TrimuiSmartHub.Core.Storage;

namespace TrimuiSmartHub.Application.ViewModels;

public sealed partial class SaveFileRow : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected = true;

    public SaveFileRow(SaveFileInfo file)
    {
        File = file;
    }

    public SaveFileInfo File { get; }

    public string Name => RelPath.GetFileName(File.Path);

    public string Folder => RelPath.GetDirectory(File.Path);

    public DateTime LastWrite => File.LastWriteUtc.ToLocalTime();
}

/// <summary>Versioned save backups (device → PC) and manual restore. Saves are never part of the sync.</summary>
public sealed partial class SavesViewModel : PageViewModel
{
    private readonly LibraryContext _context;
    private readonly JobsViewModel _jobs;
    private readonly DialogService _dialogs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRestoreSelected))]
    private SaveBackup? _selectedBackup;

    [ObservableProperty]
    private bool _onlyThisDevice = true;

    [ObservableProperty]
    private string _saveDirs = string.Empty;

    public SavesViewModel(DeviceMonitor monitor, LibraryContext context, JobsViewModel jobs, DialogService dialogs)
    {
        Monitor = monitor;
        _context = context;
        _jobs = jobs;
        _dialogs = dialogs;

        monitor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(DeviceMonitor.Current)) return;
            Load();
            OnPropertyChanged(nameof(CanRestoreSelected));
        };
        jobs.PropertyChanged += (_, e) =>
        {
            // Refresh when an automatic backup finishes.
            if (e.PropertyName == nameof(JobsViewModel.ActiveCount) && jobs.ActiveCount == 0) Load();
        };
    }

    public DeviceMonitor Monitor { get; }

    public ObservableCollection<SaveBackup> Backups { get; } = new();

    public ObservableCollection<SaveFileRow> Files { get; } = new();

    /// <summary>Restoring is only allowed to the device the backup came from.</summary>
    public bool CanRestoreSelected => SelectedBackup != null && Monitor.Current?.Id == SelectedBackup.DeviceId;

    public override void OnNavigatedTo() => Load();

    partial void OnOnlyThisDeviceChanged(bool value) => Load();

    partial void OnSelectedBackupChanged(SaveBackup? value)
    {
        Files.Clear();
        if (value == null) return;
        foreach (var file in value.Files.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase)) Files.Add(new SaveFileRow(file));
    }

    [RelayCommand]
    private void Load()
    {
        var device = Monitor.Current;
        SaveDirs = device == null ? string.Empty : string.Join("   ·   ", device.Layout.GetSaveDirs(device.Store));

        var selectedStamp = SelectedBackup?.Stamp;
        Backups.Clear();
        var deviceId = OnlyThisDevice ? device?.Id : null;
        foreach (var backup in _context.Saves.ListBackups(deviceId)) Backups.Add(backup);
        SelectedBackup = Backups.FirstOrDefault(b => b.Stamp == selectedStamp) ?? Backups.FirstOrDefault();
    }

    [RelayCommand]
    private void BackupNow()
    {
        var device = Monitor.Current;
        if (device == null) return;
        var saves = _context.Saves;
        _jobs.Run(Loc.Format("JobBackupSaves", device.Name), (progress, ct) => saves.BackupAsync(device, force: true, progress, ct));
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        var device = Monitor.Current;
        var backup = SelectedBackup;
        if (device == null || backup == null || !CanRestoreSelected) return;

        var paths = Files.Where(f => f.IsSelected).Select(f => f.File.Path).ToList();
        if (paths.Count == 0) return;

        var when = backup.CreatedUtc.ToLocalTime().ToString("g");
        if (!await _dialogs.ConfirmAsync(Loc.Get("RestoreTitle"), Loc.Format("RestoreConfirm", paths.Count, when), Loc.Get("Restore")))
            return;

        var saves = _context.Saves;
        var job = _jobs.Run(Loc.Format("JobRestoreSaves", when), (progress, ct) => saves.RestoreAsync(device, backup, paths, progress, ct));
        try
        {
            var restored = (int)(await job.Completion)!;
            await _dialogs.ShowAsync(Loc.Get("RestoreTitle"), Loc.Format("RestoreDone", restored));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _dialogs.ShowAsync(Loc.Get("Error"), ex.Message);
        }
        catch (OperationCanceledException)
        {
        }
        Load();
    }

    [RelayCommand]
    private void SelectAllFiles()
    {
        var value = !Files.All(f => f.IsSelected);
        foreach (var file in Files) file.IsSelected = value;
    }

    [RelayCommand]
    private void OpenFolder() => ShellService.OpenFolder(_context.Library.Store.GetFullPath(LibraryService.BackupsRoot));
}
