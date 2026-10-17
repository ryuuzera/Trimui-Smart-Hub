using TrimuiSmartHub.Core.Storage;

namespace TrimuiSmartHub.Core.Devices;

/// <summary>Where one emulator keeps its files on the device. All paths are relative to the device root.</summary>
public sealed record DevicePlatform(
    string PlatformId,
    string Label,
    string RomsDir,
    string? ImagesDir,
    IReadOnlyList<string> Extensions);

/// <summary>
/// Knowledge about a firmware's folder structure. Keeping it here (instead of hard-coded paths in
/// services) is what lets the app support stock firmware, CrossMix and future CFWs.
/// </summary>
public interface IDeviceLayout
{
    string Id { get; }

    string DisplayName { get; }

    /// <summary>Higher values are tested first, so specific layouts (CFWs) win over generic ones.</summary>
    int Priority { get; }

    bool Matches(IFileStore store);

    /// <summary>Optional firmware version text shown in the UI.</summary>
    string? ReadVersion(IFileStore store);

    IReadOnlyList<DevicePlatform> GetPlatforms(IFileStore store);

    string BiosDir { get; }

    /// <summary>Save and save-state folders that exist on this device.</summary>
    IReadOnlyList<string> GetSaveDirs(IFileStore store);

    /// <summary>
    /// Files that sync must never copy, overwrite or delete (game list caches, OS metadata, temp files).
    /// </summary>
    bool IsExcludedFromSync(string fileName);

    /// <summary>Removes the firmware's game list caches so the menu picks up new ROMs and images.</summary>
    int InvalidateGameListCache(IFileStore store, IEnumerable<DevicePlatform> platforms);
}
