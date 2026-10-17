using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TrimuiSmartHub.Core.Devices;
using TrimuiSmartHub.Core.Jobs;
using TrimuiSmartHub.Core.Storage;

namespace TrimuiSmartHub.Core.Sync;

public sealed record SyncFailure(SyncItem Item, string Error);

public sealed class SyncResult
{
    public int Copied { get; set; }

    public int Renamed { get; set; }

    public int Deleted { get; set; }

    public long BytesCopied { get; set; }

    public int CachesInvalidated { get; set; }

    public List<SyncFailure> Failures { get; } = new();

    /// <summary>The target disappeared (cable unplugged). Running the sync again resumes where it stopped.</summary>
    public bool Interrupted { get; set; }
}

public sealed class SyncExecutor
{
    private readonly ILogger<SyncExecutor> _logger;

    public SyncExecutor(ILogger<SyncExecutor>? logger = null)
    {
        _logger = logger ?? NullLogger<SyncExecutor>.Instance;
    }

    /// <param name="device">When syncing to a device, its game list cache is invalidated at the end.</param>
    public async Task<SyncResult> ExecuteAsync(SyncPlan plan, DeviceInfo? device = null, IProgress<JobProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var result = new SyncResult();
        var selected = plan.SelectedItems.ToList();

        // Renames first (they free names the copies may need), then deletes (they free space), then copies.
        var ordered = selected.Where(i => i.Kind == SyncActionKind.Rename)
            .Concat(selected.Where(i => i.Kind == SyncActionKind.Delete))
            .Concat(selected.Where(i => i.Kind is SyncActionKind.Copy or SyncActionKind.Update))
            .ToList();

        var totalBytes = Math.Max(1, plan.SelectedBytes);
        long doneBytes = 0;
        var doneItems = 0;

        foreach (var item in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new JobProgress(ordered.Count == 0 ? 1 : (double)doneBytes / totalBytes, $"{doneItems + 1}/{ordered.Count} · {item.DisplayName}"));

            try
            {
                switch (item.Kind)
                {
                    case SyncActionKind.Rename:
                        plan.Target.Move(item.TargetPath, item.NewTargetPath!);
                        result.Renamed++;
                        break;

                    case SyncActionKind.Delete:
                        plan.Target.Delete(item.TargetPath);
                        result.Deleted++;
                        break;

                    default:
                        var baseBytes = doneBytes;
                        var fileProgress = new InlineProgress<long>(written =>
                            progress?.Report(new JobProgress((double)(baseBytes + written) / totalBytes, $"{doneItems + 1}/{ordered.Count} · {item.DisplayName}")));

                        await using (var input = plan.Source.OpenRead(item.SourcePath!))
                        {
                            await plan.Target.WriteAtomicAsync(item.TargetPath, input, item.SourceLastWriteUtc, fileProgress, cancellationToken);
                        }

                        doneBytes += item.Size;
                        result.BytesCopied += item.Size;
                        result.Copied++;
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Sync item failed: {Item}", item);

                if (!TargetAvailable(plan.Target))
                {
                    result.Interrupted = true;
                    result.Failures.Add(new SyncFailure(item, "Device disconnected."));
                    break;
                }

                result.Failures.Add(new SyncFailure(item, ex.Message));
            }

            doneItems++;
        }

        if (device != null && plan.Direction == SyncDirection.ToDevice && !result.Interrupted && (result.Copied + result.Renamed + result.Deleted) > 0)
        {
            var touched = device.Layout.GetPlatforms(device.Store).Where(p => plan.Platforms.Contains(p.PlatformId));
            result.CachesInvalidated = device.Layout.InvalidateGameListCache(device.Store, touched);
        }

        progress?.Report(new JobProgress(1, "Done"));
        return result;
    }

    private static bool TargetAvailable(IFileStore store)
    {
        try
        {
            return store.DirectoryExists(string.Empty);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
