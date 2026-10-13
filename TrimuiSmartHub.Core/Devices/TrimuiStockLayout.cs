using System.Text.Json;
using TrimuiSmartHub.Core.Storage;

namespace TrimuiSmartHub.Core.Devices;

/// <summary>
/// Trimui stock firmware (Smart Pro / Brick): every emulator lives in "Emus/&lt;ID&gt;" with a config.json
/// whose "rompath"/"imgpath" are relative to the emulator folder (usually "../../Roms/&lt;ID&gt;").
/// </summary>
public class TrimuiStockLayout : IDeviceLayout
{
    public const string EmulatorsDir = "Emus";

    /// <summary>
    /// Candidate save folders used by RetroArch and the standalone emulators. Only the ones that
    /// exist on the device are used, which keeps this list safe across firmware versions.
    /// </summary>
    protected static readonly string[] SaveDirCandidates =
    {
        "RetroArch/.retroarch/saves",
        "RetroArch/.retroarch/states",
        "Saves",
        "Emus/NDS/drastic/backup",
        "Emus/NDS/drastic/savestates",
        "Emus/PPSSPP/.config/ppsspp/PSP/SAVEDATA",
        "Emus/PPSSPP/.config/ppsspp/PSP/PPSSPP_STATE",
    };

    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public virtual string Id => "trimui-stock";

    public virtual string DisplayName => "Trimui (stock)";

    public virtual int Priority => 0;

    public string BiosDir => "BIOS";

    public virtual bool Matches(IFileStore store)
    {
        if (!store.DirectoryExists(EmulatorsDir)) return false;

        return store.EnumerateDirectories(EmulatorsDir)
            .Any(dir => store.FileExists(RelPath.Combine(dir, "config.json")));
    }

    public virtual string? ReadVersion(IFileStore store) => null;

    public IReadOnlyList<DevicePlatform> GetPlatforms(IFileStore store)
    {
        var result = new List<DevicePlatform>();

        foreach (var emulatorDir in store.EnumerateDirectories(EmulatorsDir).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            var platform = ReadEmulatorConfig(store, emulatorDir);
            if (platform != null) result.Add(platform);
        }

        return result;
    }

    internal static DevicePlatform? ReadEmulatorConfig(IFileStore store, string emulatorDir)
    {
        var configPath = RelPath.Combine(emulatorDir, "config.json");
        if (!store.FileExists(configPath)) return null;

        try
        {
            using var stream = store.OpenRead(configPath);
            using var document = JsonDocument.Parse(stream, JsonOptions);
            var root = document.RootElement;

            var romPath = GetString(root, "rompath");
            if (string.IsNullOrWhiteSpace(romPath)) return null;

            var romsDir = RelPath.Resolve(RelPath.Combine(emulatorDir, romPath));
            if (romsDir == null) return null;

            var imgPath = GetString(root, "imgpath");
            var imagesDir = string.IsNullOrWhiteSpace(imgPath) ? null : RelPath.Resolve(RelPath.Combine(emulatorDir, imgPath));

            var extensions = (GetString(root, "extlist") ?? string.Empty)
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(e => e.TrimStart('.').ToLowerInvariant())
                .ToList();

            var id = RelPath.GetFileName(emulatorDir);
            var label = GetString(root, "label") ?? id;

            return new DevicePlatform(id, label, romsDir, imagesDir, extensions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement root, string property)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;

        foreach (var item in root.EnumerateObject())
        {
            if (item.Name.Equals(property, StringComparison.OrdinalIgnoreCase) && item.Value.ValueKind == JsonValueKind.String)
                return item.Value.GetString();
        }

        return null;
    }

    public IReadOnlyList<string> GetSaveDirs(IFileStore store) =>
        SaveDirCandidates.Where(store.DirectoryExists).ToList();

    public bool IsExcludedFromSync(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return true;
        if (fileName.StartsWith('.')) return true;
        if (fileName.EndsWith(LocalFileStore.TempSuffix, StringComparison.OrdinalIgnoreCase)) return true;

        var lower = fileName.ToLowerInvariant();
        return lower is "thumbs.db" or "desktop.ini"
            || lower.EndsWith(".db")
            || lower.EndsWith(".db-journal")
            || lower.EndsWith(".db-wal")
            || lower.EndsWith(".db-shm");
    }

    public int InvalidateGameListCache(IFileStore store, IEnumerable<DevicePlatform> platforms)
    {
        var removed = 0;

        foreach (var platform in platforms)
        {
            foreach (var file in store.EnumerateFiles(platform.RomsDir, recursive: false))
            {
                if (!IsGameListCache(file.Name)) continue;

                try
                {
                    store.Delete(file.Path);
                    removed++;
                }
                catch (IOException)
                {
                    // The firmware rebuilds the cache anyway; a locked file is not worth failing the sync.
                }
            }
        }

        return removed;
    }

    /// <summary>The stock menu caches each ROM folder in files such as "GBA_cache6.db".</summary>
    internal static bool IsGameListCache(string fileName) =>
        fileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase)
        && fileName.Contains("_cache", StringComparison.OrdinalIgnoreCase);
}
