using TrimuiSmartHub.Core.Storage;

namespace TrimuiSmartHub.Core.Sync;

public enum SyncDirection
{
    /// <summary>Library → device. The library always wins.</summary>
    ToDevice,

    /// <summary>Device → library ("import from device"). Never deletes anything.</summary>
    FromDevice,
}

public enum SyncActionKind
{
    Copy,
    Update,
    Rename,
    Delete,
}

public enum SyncCategory
{
    Rom,
    Boxart,
    Bios,
    Save,
}

public sealed class SyncItem
{
    public required SyncActionKind Kind { get; init; }

    public required SyncCategory Category { get; init; }

    public string? PlatformId { get; init; }

    /// <summary>Source file for Copy/Update (in the source store).</summary>
    public string? SourcePath { get; init; }

    /// <summary>Destination for Copy/Update, file to delete for Delete, current name for Rename.</summary>
    public required string TargetPath { get; init; }

    /// <summary>New name for Rename.</summary>
    public string? NewTargetPath { get; init; }

    public long Size { get; init; }

    public DateTime? SourceLastWriteUtc { get; init; }

    /// <summary>Deletes start unselected: nothing is removed from the device without an explicit choice.</summary>
    public bool Selected { get; set; } = true;

    public string DisplayName => RelPath.GetFileName(NewTargetPath ?? TargetPath);

    public override string ToString() => Kind == SyncActionKind.Rename ? $"{Kind} {TargetPath} -> {NewTargetPath}" : $"{Kind} {TargetPath}";
}

public sealed class SyncPlan
{
    public required SyncDirection Direction { get; init; }

    public required IFileStore Source { get; init; }

    public required IFileStore Target { get; init; }

    public List<SyncItem> Items { get; } = new();

    /// <summary>Files that exist only on the target. Offered as optional deletes (unselected).</summary>
    public List<SyncItem> OnlyOnTarget { get; } = new();

    public List<string> Warnings { get; } = new();

    /// <summary>Platform ids touched by the plan, used to invalidate the firmware game list cache.</summary>
    public HashSet<string> Platforms { get; } = new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<SyncItem> SelectedItems => Items.Concat(OnlyOnTarget).Where(i => i.Selected);

    public long SelectedBytes => SelectedItems.Where(i => i.Kind is SyncActionKind.Copy or SyncActionKind.Update).Sum(i => i.Size);

    public bool IsEmpty => Items.Count == 0 && OnlyOnTarget.Count == 0;
}

public sealed class SyncOptions
{
    /// <summary>Only these platforms (null = all).</summary>
    public IReadOnlySet<string>? Platforms { get; init; }

    public bool IncludeRoms { get; init; } = true;

    public bool IncludeBoxarts { get; init; } = true;

    public bool IncludeBios { get; init; } = true;

    /// <summary>
    /// When sizes match but dates differ, compare content (CRC). Off by default: on a slow SD card the
    /// first sync of files copied by other tools would hash the whole card.
    /// </summary>
    public bool VerifyContent { get; init; }
}
