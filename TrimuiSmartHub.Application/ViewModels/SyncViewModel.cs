using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrimuiSmartHub.Application.Infrastructure;
using TrimuiSmartHub.Application.Localization;
using TrimuiSmartHub.Core.Settings;
using TrimuiSmartHub.Core.Sync;

namespace TrimuiSmartHub.Application.ViewModels;

public sealed partial class SyncRow : ObservableObject
{
    private readonly Action _changed;

    public SyncRow(SyncItem item, Action changed)
    {
        Item = item;
        _changed = changed;
    }

    public SyncItem Item { get; }

    public bool Selected
    {
        get => Item.Selected;
        set
        {
            if (Item.Selected == value) return;
            Item.Selected = value;
            OnPropertyChanged();
            _changed();
        }
    }

    public string Path => Item.Kind == SyncActionKind.Rename ? $"{Item.TargetPath}  →  {Item.DisplayName}" : Item.TargetPath;

    /// <summary>Renames move no data, so no size is shown for them.</summary>
    public string SizeText => Item.Kind == SyncActionKind.Rename ? string.Empty : Infrastructure.Format.Bytes(Item.Size);
}

/// <summary>
/// Compare → review → apply. The plan is always shown before anything is written, and deletions on
/// the device start unselected.
/// </summary>
public sealed partial class SyncViewModel : PageViewModel
{
    private readonly LibraryContext _library;
    private readonly JobsViewModel _jobs;
    private readonly DialogService _dialogs;
    private readonly SettingsStore _settings;
    private readonly SyncPlanner _planner;
    private readonly SyncExecutor _executor;
    private SyncPlan? _plan;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsToDevice))]
    private SyncDirection _direction = SyncDirection.ToDevice;

    [ObservableProperty]
    private bool _includeRoms = true;

    [ObservableProperty]
    private bool _includeBoxarts = true;

    [ObservableProperty]
    private bool _includeBios = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BuildPlanCommand), nameof(ApplyCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasPlan;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private string? _lastResult;

    public SyncViewModel(DeviceMonitor monitor, LibraryContext library, JobsViewModel jobs, DialogService dialogs, SettingsStore settings, SyncPlanner planner, SyncExecutor executor)
    {
        Monitor = monitor;
        _library = library;
        _jobs = jobs;
        _dialogs = dialogs;
        _settings = settings;
        _planner = planner;
        _executor = executor;

        monitor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(DeviceMonitor.Current)) return;
            ClearPlan();
            BuildPlanCommand.NotifyCanExecuteChanged();
        };
    }

    public DeviceMonitor Monitor { get; }

    public bool IsToDevice
    {
        get => Direction == SyncDirection.ToDevice;
        set => Direction = value ? SyncDirection.ToDevice : SyncDirection.FromDevice;
    }

    public ObservableCollection<SyncRow> Changes { get; } = new();

    public ObservableCollection<SyncRow> OnlyOnTarget { get; } = new();

    public ObservableCollection<string> Warnings { get; } = new();

    partial void OnDirectionChanged(SyncDirection value) => ClearPlan();

    private bool CanBuild() => !IsBusy && Monitor.Current != null;

    [RelayCommand(CanExecute = nameof(CanBuild))]
    private async Task BuildPlanAsync()
    {
        var device = Monitor.Current;
        if (device == null) return;

        IsBusy = true;
        try
        {
            var options = new SyncOptions
            {
                IncludeRoms = IncludeRoms,
                IncludeBoxarts = IncludeBoxarts,
                IncludeBios = IncludeBios,
                VerifyContent = _settings.Current.SyncVerifyContent,
            };
            var library = _library.Library;
            var direction = Direction;

            var plan = await Task.Run(() => direction == SyncDirection.ToDevice
                ? _planner.PlanToDeviceAsync(library, device, options)
                : _planner.PlanFromDeviceAsync(library, device, options));

            ShowPlan(plan);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await _dialogs.ShowAsync(Loc.Get("Error"), ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ShowPlan(SyncPlan plan)
    {
        _plan = plan;
        Changes.Clear();
        OnlyOnTarget.Clear();
        Warnings.Clear();

        foreach (var item in plan.Items) Changes.Add(new SyncRow(item, UpdateSummary));
        foreach (var item in plan.OnlyOnTarget) OnlyOnTarget.Add(new SyncRow(item, UpdateSummary));
        foreach (var warning in plan.Warnings) Warnings.Add(warning);

        HasPlan = true;
        UpdateSummary();
    }

    private void ClearPlan()
    {
        _plan = null;
        HasPlan = false;
        Changes.Clear();
        OnlyOnTarget.Clear();
        Warnings.Clear();
        Summary = string.Empty;
        ApplyCommand.NotifyCanExecuteChanged();
    }

    private void UpdateSummary()
    {
        if (_plan == null) return;
        var selected = _plan.SelectedItems.ToList();

        Summary = selected.Count == 0
            ? Loc.Get(_plan.IsEmpty ? "SyncUpToDate" : "SyncNothingSelected")
            : Loc.Format("SyncSummary",
                selected.Count(i => i.Kind is SyncActionKind.Copy or SyncActionKind.Update),
                selected.Count(i => i.Kind == SyncActionKind.Rename),
                selected.Count(i => i.Kind == SyncActionKind.Delete),
                Infrastructure.Format.Bytes(_plan.SelectedBytes));

        ApplyCommand.NotifyCanExecuteChanged();
    }

    private bool CanApply() => !IsBusy && _plan != null && _plan.SelectedItems.Any();

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        var plan = _plan;
        var device = Monitor.Current;
        if (plan == null || device == null) return;

        var deletes = plan.SelectedItems.Count(i => i.Kind == SyncActionKind.Delete);
        if (deletes > 0 && !await _dialogs.ConfirmAsync(Loc.Get("SyncDeleteTitle"), Loc.Format("SyncDeleteMessage", deletes), Loc.Get("Delete")))
            return;

        IsBusy = true;
        LastResult = null;
        try
        {
            var title = plan.Direction == SyncDirection.ToDevice ? Loc.Format("JobSyncTo", device.Name) : Loc.Format("JobImportFrom", device.Name);
            var job = _jobs.Run(title, (progress, ct) => _executor.ExecuteAsync(plan, device, progress, ct));

            SyncResult? result = null;
            try
            {
                result = (SyncResult?)await job.Completion;
            }
            catch (OperationCanceledException)
            {
                LastResult = Loc.Get("SyncCanceled");
            }
            catch (Exception ex)
            {
                LastResult = ex.Message;
            }

            if (result != null)
            {
                LastResult = result.Interrupted
                    ? Loc.Get("SyncInterrupted")
                    : Loc.Format("SyncResult", result.Copied, result.Renamed, result.Deleted, Infrastructure.Format.Bytes(result.BytesCopied), result.Failures.Count);

                if (result.Failures.Count > 0)
                {
                    var details = string.Join(Environment.NewLine, result.Failures.Take(10).Select(f => $"{f.Item.DisplayName}: {f.Error}"));
                    await _dialogs.ShowAsync(Loc.Get("SyncFailuresTitle"), details);
                }
            }
        }
        finally
        {
            IsBusy = false;
        }

        if (Monitor.Current != null) await BuildPlanAsync();
    }

    [RelayCommand]
    private void SelectAllChanges(bool? value)
    {
        foreach (var row in Changes) row.Selected = value ?? true;
    }

    [RelayCommand]
    private void SelectAllOrphans(bool? value)
    {
        foreach (var row in OnlyOnTarget) row.Selected = value ?? true;
    }
}
