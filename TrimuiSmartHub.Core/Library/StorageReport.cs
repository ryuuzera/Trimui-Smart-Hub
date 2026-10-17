using TrimuiSmartHub.Core.Devices;
using TrimuiSmartHub.Core.Platforms;

namespace TrimuiSmartHub.Core.Library;

public sealed record PlatformUsage(string PlatformId, string Name, int LibraryRoms, long LibraryBytes, int DeviceRoms, long DeviceBytes);

public sealed record StorageReport(IReadOnlyList<PlatformUsage> Platforms, long? DeviceFree, long? DeviceTotal, long? LibraryFree)
{
    public long LibraryBytes => Platforms.Sum(p => p.LibraryBytes);

    public long DeviceBytes => Platforms.Sum(p => p.DeviceBytes);

    /// <summary>ROM space per platform, library and device side by side.</summary>
    public static StorageReport Build(LibraryService library, DeviceInfo? device, PlatformRegistry platforms)
    {
        var usage = new Dictionary<string, (int LibRoms, long LibBytes, int DevRoms, long DevBytes)>(StringComparer.OrdinalIgnoreCase);

        foreach (var id in library.GetPlatformIds())
        {
            var roms = library.GetRoms(id);
            usage[id] = (roms.Count, roms.Sum(r => r.File.Size), 0, 0);
        }

        long? total = null;
        if (device != null)
        {
            foreach (var platform in device.Layout.GetPlatforms(device.Store))
            {
                var files = device.Store.EnumerateFiles(platform.RomsDir, recursive: true)
                    .Where(f => !device.Layout.IsExcludedFromSync(f.Name))
                    .ToList();
                if (files.Count == 0 && !usage.ContainsKey(platform.PlatformId)) continue;

                var current = usage.GetValueOrDefault(platform.PlatformId);
                usage[platform.PlatformId] = (current.LibRoms, current.LibBytes, files.Count, files.Sum(f => f.Size));
            }

            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(device.RootPath)!);
                if (drive.IsReady) total = drive.TotalSize;
            }
            catch (Exception ex) when (ex is IOException or ArgumentException)
            {
                // Size unknown; the report still lists usage.
            }
        }

        var rows = usage
            .Select(kv => new PlatformUsage(kv.Key, platforms.GetOrUnknown(kv.Key).Name, kv.Value.LibRoms, kv.Value.LibBytes, kv.Value.DevRoms, kv.Value.DevBytes))
            .OrderByDescending(p => Math.Max(p.LibraryBytes, p.DeviceBytes))
            .ToList();

        return new StorageReport(rows, device?.Store.AvailableFreeSpace, total, library.Store.AvailableFreeSpace);
    }
}
