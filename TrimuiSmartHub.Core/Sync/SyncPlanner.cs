using TrimuiSmartHub.Core.Devices;
using TrimuiSmartHub.Core.Games;
using TrimuiSmartHub.Core.Library;
using TrimuiSmartHub.Core.Storage;

namespace TrimuiSmartHub.Core.Sync;

/// <summary>
/// Builds a <see cref="SyncPlan"/> by comparing the library with a device. Nothing is changed here, so
/// the user can review the plan before running it.
/// </summary>
public sealed class SyncPlanner
{
    /// <summary>FAT32 stores modification times with 2-second precision.</summary>
    private static readonly TimeSpan TimeTolerance = TimeSpan.FromSeconds(2);

    private const long Fat32MaxFileSize = uint.MaxValue;

    /// <summary>Library → device: ROMs, box arts and BIOS. Pending renames are applied first; saves are never copied.</summary>
    public async Task<SyncPlan> PlanToDeviceAsync(LibraryService library, DeviceInfo device, SyncOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new SyncOptions();
        var plan = new SyncPlan { Direction = SyncDirection.ToDevice, Source = library.Store, Target = device.Store };
        var layout = device.Layout;
        var devicePlatforms = layout.GetPlatforms(device.Store).ToDictionary(p => p.PlatformId, StringComparer.OrdinalIgnoreCase);

        var saveFiles = layout.GetSaveDirs(device.Store)
            .SelectMany(dir => device.Store.EnumerateFiles(dir, recursive: true))
            .Select(f => f.Path)
            .ToList();

        foreach (var platformId in library.GetPlatformIds())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (options.Platforms != null && !options.Platforms.Contains(platformId)) continue;

            if (!devicePlatforms.TryGetValue(platformId, out var devicePlatform))
            {
                if (library.GetRoms(platformId).Count > 0)
                    plan.Warnings.Add($"{platformId}: emulator not installed on the device, skipped.");
                continue;
            }

            var romFiles = Index(device.Store, devicePlatform.RomsDir, recursive: true, layout);
            var imageFiles = devicePlatform.ImagesDir == null
                ? new Dictionary<string, FileEntry>(StringComparer.OrdinalIgnoreCase)
                : Index(device.Store, devicePlatform.ImagesDir, recursive: false, layout);

            ApplyRenames(plan, library.Index.GetRenames(platformId), devicePlatform, romFiles, imageFiles, saveFiles);

            if (options.IncludeRoms)
            {
                await DiffAsync(plan, library.Store, LibraryService.RomsDir(platformId), recursive: true,
                    device.Store, devicePlatform.RomsDir, romFiles, SyncCategory.Rom, platformId, layout, options, cancellationToken);
            }

            if (options.IncludeBoxarts && devicePlatform.ImagesDir != null)
            {
                await DiffAsync(plan, library.Store, LibraryService.BoxartDir(platformId), recursive: false,
                    device.Store, devicePlatform.ImagesDir, imageFiles, SyncCategory.Boxart, platformId, layout, options, cancellationToken);
            }
        }

        if (options.IncludeBios)
        {
            var biosFiles = Index(device.Store, layout.BiosDir, recursive: true, layout);
            await DiffAsync(plan, library.Store, LibraryService.BiosRoot, recursive: true,
                device.Store, layout.BiosDir, biosFiles, SyncCategory.Bios, null, layout, options, cancellationToken, reportOnlyOnTarget: false);
        }

        AddCapacityWarnings(plan);
        return plan;
    }

    /// <summary>Device → library. Used once to build the library from what is already on the SD card.</summary>
    public async Task<SyncPlan> PlanFromDeviceAsync(LibraryService library, DeviceInfo device, SyncOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new SyncOptions();
        var plan = new SyncPlan { Direction = SyncDirection.FromDevice, Source = device.Store, Target = library.Store };
        var layout = device.Layout;

        foreach (var devicePlatform in layout.GetPlatforms(device.Store))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var platformId = devicePlatform.PlatformId;
            if (options.Platforms != null && !options.Platforms.Contains(platformId)) continue;

            if (options.IncludeRoms)
            {
                var targetDir = LibraryService.RomsDir(platformId);
                await DiffAsync(plan, device.Store, devicePlatform.RomsDir, recursive: true,
                    library.Store, targetDir, Index(library.Store, targetDir, true, layout), SyncCategory.Rom, platformId, layout, options, cancellationToken, reportOnlyOnTarget: false);
            }

            if (options.IncludeBoxarts && devicePlatform.ImagesDir != null)
            {
                var targetDir = LibraryService.BoxartDir(platformId);
                await DiffAsync(plan, device.Store, devicePlatform.ImagesDir, recursive: false,
                    library.Store, targetDir, Index(library.Store, targetDir, false, layout), SyncCategory.Boxart, platformId, layout, options, cancellationToken, reportOnlyOnTarget: false);
            }
        }

        if (options.IncludeBios)
        {
            await DiffAsync(plan, device.Store, layout.BiosDir, recursive: true,
                library.Store, LibraryService.BiosRoot, Index(library.Store, LibraryService.BiosRoot, true, layout), SyncCategory.Bios, null, layout, options, cancellationToken, reportOnlyOnTarget: false);
        }

        AddCapacityWarnings(plan);
        return plan;
    }

    private static Dictionary<string, FileEntry> Index(IFileStore store, string directory, bool recursive, IDeviceLayout layout) =>
        store.EnumerateFiles(directory, recursive)
            .Where(f => !layout.IsExcludedFromSync(f.Name))
            .ToDictionary(f => f.Path, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Replays library renames on the device: the ROM, its image and its saves are renamed (moved) instead
    /// of copying a new ROM and orphaning the old saves. The in-memory indexes are updated so the diff that
    /// follows sees the device as it will be after the renames.
    /// </summary>
    private static void ApplyRenames(SyncPlan plan, IReadOnlyList<RenameRecord> renames, DevicePlatform devicePlatform,
        Dictionary<string, FileEntry> romFiles, Dictionary<string, FileEntry> imageFiles, List<string> saveFiles)
    {
        foreach (var rename in renames)
        {
            var romsRenamed = RenameInIndex(plan, romFiles, rename, SyncCategory.Rom, devicePlatform.PlatformId);
            if (romsRenamed == 0) continue;

            RenameInIndex(plan, imageFiles, rename, SyncCategory.Boxart, devicePlatform.PlatformId);

            for (var i = 0; i < saveFiles.Count; i++)
            {
                var path = saveFiles[i];
                var newName = SaveFileMatcher.Rename(RelPath.GetFileName(path), rename.OldBaseName, rename.NewBaseName);
                if (newName == null) continue;

                var newPath = RelPath.Combine(RelPath.GetDirectory(path), newName);
                if (saveFiles.Contains(newPath, StringComparer.OrdinalIgnoreCase))
                {
                    plan.Warnings.Add($"Save \"{newPath}\" already exists; \"{path}\" was not renamed.");
                    continue;
                }

                plan.Items.Add(new SyncItem { Kind = SyncActionKind.Rename, Category = SyncCategory.Save, PlatformId = devicePlatform.PlatformId, TargetPath = path, NewTargetPath = newPath });
                saveFiles[i] = newPath;
            }
        }
    }

    private static int RenameInIndex(SyncPlan plan, Dictionary<string, FileEntry> files, RenameRecord rename, SyncCategory category, string platformId)
    {
        var renamed = 0;
        var matches = files.Values.Where(f => RelPath.GetBaseName(f.Name).Equals(rename.OldBaseName, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var file in matches)
        {
            var newPath = RelPath.Combine(RelPath.GetDirectory(file.Path), rename.NewBaseName + Path.GetExtension(file.Name));
            if (files.ContainsKey(newPath)) continue;

            plan.Items.Add(new SyncItem { Kind = SyncActionKind.Rename, Category = category, PlatformId = platformId, TargetPath = file.Path, NewTargetPath = newPath, Size = file.Size });
            plan.Platforms.Add(platformId);
            files.Remove(file.Path);
            files[newPath] = file with { Path = newPath };
            renamed++;
        }

        return renamed;
    }

    private static async Task DiffAsync(SyncPlan plan, IFileStore source, string sourceDir, bool recursive,
        IFileStore target, string targetDir, Dictionary<string, FileEntry> targetFiles, SyncCategory category, string? platformId,
        IDeviceLayout layout, SyncOptions options, CancellationToken cancellationToken, bool reportOnlyOnTarget = true)
    {
        var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in source.EnumerateFiles(sourceDir, recursive))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (layout.IsExcludedFromSync(file.Name) || LibraryService.IsIgnoredFile(file.Name)) continue;

            var relative = file.Path.Length > sourceDir.Length ? file.Path[(sourceDir.Length + 1)..] : file.Name;
            var targetPath = RelPath.Combine(targetDir, relative);
            matched.Add(targetPath);

            SyncActionKind? kind;
            if (!targetFiles.TryGetValue(targetPath, out var existing))
            {
                kind = SyncActionKind.Copy;
            }
            else if (existing.Size != file.Size)
            {
                kind = SyncActionKind.Update;
            }
            else if (options.VerifyContent && (existing.LastWriteUtc - file.LastWriteUtc).Duration() > TimeTolerance
                     && !await SameContentAsync(source, file, target, existing, cancellationToken))
            {
                kind = SyncActionKind.Update;
            }
            else
            {
                kind = null;
            }

            if (kind == null) continue;

            plan.Items.Add(new SyncItem
            {
                Kind = kind.Value,
                Category = category,
                PlatformId = platformId,
                SourcePath = file.Path,
                TargetPath = targetPath,
                Size = file.Size,
                SourceLastWriteUtc = file.LastWriteUtc,
            });
            if (platformId != null) plan.Platforms.Add(platformId);
        }

        if (!reportOnlyOnTarget) return;

        foreach (var orphan in targetFiles.Values.Where(f => !matched.Contains(f.Path)))
        {
            plan.OnlyOnTarget.Add(new SyncItem
            {
                Kind = SyncActionKind.Delete,
                Category = category,
                PlatformId = platformId,
                TargetPath = orphan.Path,
                Size = orphan.Size,
                Selected = false,
            });
        }
    }

    private static async Task<bool> SameContentAsync(IFileStore source, FileEntry sourceFile, IFileStore target, FileEntry targetFile, CancellationToken cancellationToken)
    {
        if (sourceFile.Size > FileHasher.MaxHashSize) return true;

        await using var a = source.OpenRead(sourceFile.Path);
        await using var b = target.OpenRead(targetFile.Path);
        return await FileHasher.Crc32Async(a, cancellationToken) == await FileHasher.Crc32Async(b, cancellationToken);
    }

    private static void AddCapacityWarnings(SyncPlan plan)
    {
        if (string.Equals(plan.Target.Format, "FAT32", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var item in plan.Items.Where(i => i.Size > Fat32MaxFileSize && i.Kind is SyncActionKind.Copy or SyncActionKind.Update))
            {
                item.Selected = false;
                plan.Warnings.Add($"\"{item.DisplayName}\" is larger than 4 GB and can't be stored on a FAT32 card.");
            }
        }

        var free = plan.Target.AvailableFreeSpace;
        if (free.HasValue && plan.SelectedBytes > free.Value)
        {
            plan.Warnings.Add($"Not enough free space: {Format(plan.SelectedBytes)} needed, {Format(free.Value)} available.");
        }
    }

    internal static string Format(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value:0.#} {units[unit]}";
    }
}
