using System.Management;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using TrimuiSmartHub.Core.Devices;

namespace TrimuiSmartHub.Application.Infrastructure;

/// <summary>
/// Watches drives being mounted/unmounted (Win32_VolumeChangeEvent) and recognizes devices by content.
/// Replaces the old 1-second WMI polling of USB devices named "Trimui": no CPU spent while idle, and SD
/// cards in a card reader or devices running a CFW are recognized too.
/// </summary>
public sealed partial class DeviceMonitor : ObservableObject, IDisposable
{
    private readonly DeviceDetector _detector;
    private readonly ILogger<DeviceMonitor> _logger;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _debounce;
    private readonly DispatcherTimer _fallback;
    private ManagementEventWatcher? _watcher;
    private int _scanning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnected))]
    private DeviceInfo? _current;

    [ObservableProperty]
    private IReadOnlyList<DeviceInfo> _devices = Array.Empty<DeviceInfo>();

    public DeviceMonitor(DeviceDetector detector, ILogger<DeviceMonitor> logger)
    {
        _detector = detector;
        _logger = logger;
        _dispatcher = Dispatcher.CurrentDispatcher;

        _debounce = new DispatcherTimer(DispatcherPriority.Background, _dispatcher) { Interval = TimeSpan.FromSeconds(1.5) };
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            Rescan();
        };

        // Safety net in case volume events are unavailable (WMI disabled, policies): cheap drive check.
        _fallback = new DispatcherTimer(DispatcherPriority.Background, _dispatcher) { Interval = TimeSpan.FromSeconds(10) };
        _fallback.Tick += (_, _) => Rescan();
    }

    public bool IsConnected => Current != null;

    /// <summary>Raised on the UI thread when a device (identified by its id) appears.</summary>
    public event Action<DeviceInfo>? Connected;

    /// <summary>Raised on the UI thread when the current device goes away.</summary>
    public event Action<DeviceInfo>? Disconnected;

    public void Start()
    {
        try
        {
            _watcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent"));
            _watcher.EventArrived += (_, _) => _dispatcher.BeginInvoke(() =>
            {
                _debounce.Stop();
                _debounce.Start();
            });
            _watcher.Start();

            // Events work: the periodic check is only a safety net, and scanning less often avoids
            // waking sleeping external hard drives.
            _fallback.Interval = TimeSpan.FromMinutes(1);
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            _logger.LogWarning(ex, "Volume events unavailable; using periodic checks only");
        }

        _fallback.Start();
        Rescan();
    }

    /// <summary>Scans drives in the background and updates <see cref="Current"/>.</summary>
    public void Rescan()
    {
        if (Interlocked.Exchange(ref _scanning, 1) == 1) return;

        Task.Run(() =>
        {
            try
            {
                var devices = _detector.ScanDrives();
                _dispatcher.BeginInvoke(() => Apply(devices));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Device scan failed");
            }
            finally
            {
                Interlocked.Exchange(ref _scanning, 0);
            }
        });
    }

    /// <summary>Lets the user pick which device to use when several are connected.</summary>
    public void Select(DeviceInfo device)
    {
        if (Current?.Id == device.Id) return;
        var previous = Current;
        Current = device;
        if (previous != null) Disconnected?.Invoke(previous);
        Connected?.Invoke(device);
    }

    private void Apply(IReadOnlyList<DeviceInfo> devices)
    {
        Devices = devices;
        var previous = Current;

        // Keep the selected device if it's still there; otherwise take the first one found.
        var next = devices.FirstOrDefault(d => d.Id == previous?.Id && d.RootPath == previous?.RootPath) ?? devices.FirstOrDefault();
        if (next?.Id == previous?.Id && next?.RootPath == previous?.RootPath) return;

        Current = next;
        if (previous != null)
        {
            _logger.LogInformation("Device disconnected: {Device}", previous);
            Disconnected?.Invoke(previous);
        }
        if (next != null)
        {
            _logger.LogInformation("Device connected: {Device} ({Layout})", next, next.Layout.Id);
            Connected?.Invoke(next);
        }
    }

    public void Dispose()
    {
        _fallback.Stop();
        _debounce.Stop();
        try
        {
            _watcher?.Stop();
        }
        catch (ManagementException)
        {
        }
        _watcher?.Dispose();
    }
}
