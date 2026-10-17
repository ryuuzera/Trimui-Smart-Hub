using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TrimuiSmartHub.Core.Storage;

namespace TrimuiSmartHub.Core.Devices;

/// <summary>
/// Recognizes a device by the content of a drive (an "Emus" folder with emulator configs) instead of the
/// USB device name. This makes SD cards in a card reader and custom firmwares work the same way.
/// </summary>
public sealed class DeviceDetector
{
    private readonly IReadOnlyList<IDeviceLayout> _layouts;
    private readonly ILogger<DeviceDetector> _logger;

    public DeviceDetector(IEnumerable<IDeviceLayout> layouts, ILogger<DeviceDetector>? logger = null)
    {
        _layouts = layouts.OrderByDescending(l => l.Priority).ToList();
        _logger = logger ?? NullLogger<DeviceDetector>.Instance;
    }

    public static DeviceDetector CreateDefault() => new(new IDeviceLayout[] { new CrossMixLayout(), new TrimuiStockLayout() });

    public IDeviceLayout? DetectLayout(IFileStore store) => _layouts.FirstOrDefault(l => SafeMatches(l, store));

    public DeviceInfo? TryOpen(string rootPath)
    {
        try
        {
            if (!Directory.Exists(rootPath)) return null;

            var store = new LocalFileStore(rootPath);
            var layout = DetectLayout(store);
            if (layout == null) return null;

            var identity = DeviceIdentity.LoadOrCreate(store);

            return new DeviceInfo
            {
                Id = identity.Id,
                Name = identity.Name,
                RootPath = store.RootPath,
                Store = store,
                Layout = layout,
                FirmwareVersion = layout.ReadVersion(store),
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "Could not inspect {Root}", rootPath);
            return null;
        }
    }

    /// <summary>Scans the removable and fixed drives of this computer, except the system drive.</summary>
    public IReadOnlyList<DeviceInfo> ScanDrives()
    {
        var systemRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        var devices = new List<DeviceInfo>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady) continue;
                if (drive.DriveType is not (DriveType.Removable or DriveType.Fixed)) continue;
                if (!string.IsNullOrEmpty(systemRoot) && drive.RootDirectory.FullName.Equals(systemRoot, StringComparison.OrdinalIgnoreCase)) continue;

                var device = TryOpen(drive.RootDirectory.FullName);
                if (device != null) devices.Add(device);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogDebug(ex, "Skipping drive {Drive}", drive.Name);
            }
        }

        return devices;
    }

    private bool SafeMatches(IDeviceLayout layout, IFileStore store)
    {
        try
        {
            return layout.Matches(store);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "Layout {Layout} check failed", layout.Id);
            return false;
        }
    }
}
