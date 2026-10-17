using System.Globalization;
using System.Text.Json;
using TrimuiSmartHub.Core.Devices;
using TrimuiSmartHub.Core.Jobs;
using TrimuiSmartHub.Core.Library;
using TrimuiSmartHub.Core.Storage;

namespace TrimuiSmartHub.Core.Saves;

public sealed record SaveFileInfo(string Path, long Size, DateTime LastWriteUtc);

public sealed class SaveBackup
{
    public const string ManifestName = "manifest.json";

    public required string DeviceId { get; init; }

    public string DeviceName { get; init; } = string.Empty;

    /// <summary>Folder name, e.g. "2026-10-13_183012".</summary>
    public required string Stamp { get; init; }

    public DateTime CreatedUtc { get; init; }

    /// <summary>Device-relative paths, exactly as they are on the device.</summary>
    public List<SaveFileInfo> Files { get; init; } = new();

    public long TotalSize => Files.Sum(f => f.Size);
}

public sealed class SaveRetention
{
    /// <summary>Most recent backups always kept.</summary>
    public int KeepLatest { get; init; } = 10;

    /// <summary>Besides those, keep the oldest backup of each of the last N months.</summary>
    public int KeepMonthly { get; init; } = 12;
}

/// <summary>
/// One-way, versioned backup of saves and save states (device → PC). Never a sync: restoring is always a
/// manual, explicit action, and the current device saves are backed up before a restore overwrites them.
/// </summary>
public sealed class SaveBackupService
{
    private const string StampFormat = "yyyy-MM-dd_HHmmss";

    private readonly LibraryService _library;

    public SaveBackupService(LibraryService library)
    {
        _library = library;
    }

    private IFileStore Store => _library.Store;

    private static string DeviceDir(string deviceId) => RelPath.Combine(LibraryService.BackupsRoot, deviceId);

    private static string BackupDir(string deviceId, string stamp) => RelPath.Combine(DeviceDir(deviceId), stamp);

    public IReadOnlyList<SaveFileInfo> ScanDevice(DeviceInfo device) =>
        device.Layout.GetSaveDirs(device.Store)
            .SelectMany(dir => device.Store.EnumerateFiles(dir, recursive: true))
            .Where(f => !LibraryService.IsIgnoredFile(f.Name))
            .Select(f => new SaveFileInfo(f.Path, f.Size, f.LastWriteUtc))
            .ToList();

    public IReadOnlyList<SaveBackup> ListBackups(string? deviceId = null)
    {
        var deviceDirs = deviceId == null
            ? Store.EnumerateDirectories(LibraryService.BackupsRoot)
            : new[] { DeviceDir(deviceId) };

        var result = new List<SaveBackup>();
        foreach (var deviceDir in deviceDirs)
        {
            foreach (var backupDir in Store.EnumerateDirectories(deviceDir))
            {
                var backup = ReadManifest(RelPath.Combine(backupDir, SaveBackup.ManifestName));
                if (backup != null) result.Add(backup);
            }
        }

        return result.OrderByDescending(b => b.CreatedUtc).ToList();
    }

    /// <summary>Creates a backup unless nothing changed since the last one. Returns null when skipped.</summary>
    public async Task<SaveBackup?> BackupAsync(DeviceInfo device, bool force = false, IProgress<JobProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var files = ScanDevice(device);
        if (files.Count == 0) return null;

        var latest = ListBackups(device.Id).FirstOrDefault();
        if (!force && latest != null && SameFiles(latest.Files, files)) return null;

        var now = DateTime.UtcNow;
        var stamp = now.ToLocalTime().ToString(StampFormat, CultureInfo.InvariantCulture);
        var backupDir = BackupDir(device.Id, stamp);

        for (var i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = files[i];
            progress?.Report(new JobProgress((double)i / files.Count, RelPath.GetFileName(file.Path)));

            await using var input = device.Store.OpenRead(file.Path);
            await Store.WriteAtomicAsync(RelPath.Combine(backupDir, "files", file.Path), input, file.LastWriteUtc, cancellationToken: cancellationToken);
        }

        var backup = new SaveBackup
        {
            DeviceId = device.Id,
            DeviceName = device.Name,
            Stamp = stamp,
            CreatedUtc = now,
            Files = files.ToList(),
        };

        // The manifest is written last: a backup interrupted by unplugging the cable has no manifest
        // and is therefore ignored (and cleaned up by the retention pass).
        await using (var manifest = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(backup, new JsonSerializerOptions { WriteIndented = true })))
        {
            await Store.WriteAtomicAsync(RelPath.Combine(backupDir, SaveBackup.ManifestName), manifest, cancellationToken: cancellationToken);
        }

        progress?.Report(new JobProgress(1, null));
        return backup;
    }

    /// <summary>Restores the given files (device-relative paths) from a backup. Backs up current saves first.</summary>
    public async Task<int> RestoreAsync(DeviceInfo device, SaveBackup backup, IEnumerable<string>? onlyPaths = null, IProgress<JobProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!backup.DeviceId.Equals(device.Id, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This backup belongs to another device.");

        await BackupAsync(device, force: false, cancellationToken: cancellationToken);

        var selection = onlyPaths == null ? null : new HashSet<string>(onlyPaths, StringComparer.OrdinalIgnoreCase);
        var files = backup.Files.Where(f => selection == null || selection.Contains(f.Path)).ToList();
        var backupDir = BackupDir(backup.DeviceId, backup.Stamp);

        for (var i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = files[i];
            progress?.Report(new JobProgress((double)i / files.Count, RelPath.GetFileName(file.Path)));

            await using var input = Store.OpenRead(RelPath.Combine(backupDir, "files", file.Path));
            await device.Store.WriteAtomicAsync(file.Path, input, file.LastWriteUtc, cancellationToken: cancellationToken);
        }

        return files.Count;
    }

    /// <summary>Deletes old backups according to the retention policy and incomplete backups. Returns the number removed.</summary>
    public int ApplyRetention(string deviceId, SaveRetention retention, Action<string> deleteDirectory)
    {
        var deviceDir = DeviceDir(deviceId);
        var complete = ListBackups(deviceId);

        var keep = new HashSet<string>(complete.Take(retention.KeepLatest).Select(b => b.Stamp));
        foreach (var month in complete.GroupBy(b => (b.CreatedUtc.Year, b.CreatedUtc.Month)).OrderByDescending(g => g.Key).Take(retention.KeepMonthly))
        {
            keep.Add(month.OrderBy(b => b.CreatedUtc).First().Stamp);
        }

        var removed = 0;
        foreach (var dir in Store.EnumerateDirectories(deviceDir))
        {
            var stamp = RelPath.GetFileName(dir);
            var isComplete = complete.Any(b => b.Stamp == stamp);
            if (isComplete && keep.Contains(stamp)) continue;

            // Incomplete backups younger than an hour may still be running.
            if (!isComplete && DateTime.TryParseExact(stamp, StampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var created)
                && DateTime.Now - created < TimeSpan.FromHours(1)) continue;

            deleteDirectory(_library.Store.GetFullPath(dir));
            removed++;
        }

        return removed;
    }

    private SaveBackup? ReadManifest(string path)
    {
        try
        {
            if (!Store.FileExists(path)) return null;
            using var stream = Store.OpenRead(path);
            return JsonSerializer.Deserialize<SaveBackup>(stream);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static bool SameFiles(IReadOnlyList<SaveFileInfo> backup, IReadOnlyList<SaveFileInfo> current)
    {
        if (backup.Count != current.Count) return false;

        var byPath = backup.ToDictionary(f => f.Path, StringComparer.OrdinalIgnoreCase);
        return current.All(f => byPath.TryGetValue(f.Path, out var b)
            && b.Size == f.Size
            && (b.LastWriteUtc - f.LastWriteUtc).Duration() <= TimeSpan.FromSeconds(2));
    }
}
