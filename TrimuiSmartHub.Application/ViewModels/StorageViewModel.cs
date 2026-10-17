using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrimuiSmartHub.Application.Infrastructure;
using TrimuiSmartHub.Core.Library;
using TrimuiSmartHub.Core.Platforms;

namespace TrimuiSmartHub.Application.ViewModels;

public sealed record UsageRow(string PlatformId, string Name, int LibraryRoms, long LibraryBytes, int DeviceRoms, long DeviceBytes, double LibraryShare, double DeviceShare);

/// <summary>Space used per platform, in the library and on the device.</summary>
public sealed partial class StorageViewModel : PageViewModel
{
    private readonly LibraryContext _context;
    private readonly PlatformRegistry _platforms;

    [ObservableProperty]
    private StorageReport? _report;

    [ObservableProperty]
    private IReadOnlyList<UsageRow> _rows = Array.Empty<UsageRow>();

    [ObservableProperty]
    private double _deviceUsedFraction;

    [ObservableProperty]
    private bool _isLoading;

    public StorageViewModel(DeviceMonitor monitor, LibraryContext context, PlatformRegistry platforms)
    {
        Monitor = monitor;
        _context = context;
        _platforms = platforms;
    }

    public DeviceMonitor Monitor { get; }

    public override void OnNavigatedTo() => _ = RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            var library = _context.Library;
            var device = Monitor.Current;
            var report = await Task.Run(() => StorageReport.Build(library, device, _platforms));

            // Bars are relative to the largest platform so small ones remain visible.
            var max = Math.Max(1, report.Platforms.Select(p => Math.Max(p.LibraryBytes, p.DeviceBytes)).DefaultIfEmpty(1).Max());
            Rows = report.Platforms
                .Select(p => new UsageRow(p.PlatformId, p.Name, p.LibraryRoms, p.LibraryBytes, p.DeviceRoms, p.DeviceBytes, (double)p.LibraryBytes / max, (double)p.DeviceBytes / max))
                .ToList();

            DeviceUsedFraction = report.DeviceTotal is > 0 && report.DeviceFree.HasValue
                ? 1 - (double)report.DeviceFree.Value / report.DeviceTotal.Value
                : 0;
            Report = report;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Rows = Array.Empty<UsageRow>();
        }
        finally
        {
            IsLoading = false;
        }
    }
}
