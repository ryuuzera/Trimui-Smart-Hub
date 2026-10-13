using System.Text.Json;
using TrimuiSmartHub.Core.Storage;

namespace TrimuiSmartHub.Core.Devices;

public sealed class DeviceInfo
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Drive root or folder, e.g. "E:\".</summary>
    public required string RootPath { get; init; }

    public required IFileStore Store { get; init; }

    public required IDeviceLayout Layout { get; init; }

    public string? FirmwareVersion { get; init; }

    public string FirmwareDisplay => FirmwareVersion is null ? Layout.DisplayName : $"{Layout.DisplayName} {FirmwareVersion}";

    public override string ToString() => $"{Name} ({RootPath})";
}

/// <summary>
/// A small file on the SD card that gives each card a stable id and a user-chosen name. Save backups
/// are grouped by this id, so two devices (or two SD cards) never mix their saves.
/// </summary>
public sealed class DeviceIdentity
{
    public const string FileName = ".trimui-smart-hub.json";

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "Trimui";

    public static DeviceIdentity LoadOrCreate(IFileStore store, Func<DeviceIdentity>? factory = null)
    {
        try
        {
            if (store.FileExists(FileName))
            {
                using var stream = store.OpenRead(FileName);
                var identity = JsonSerializer.Deserialize<DeviceIdentity>(stream);
                if (identity != null && !string.IsNullOrWhiteSpace(identity.Id)) return identity;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Fall through and recreate it.
        }

        var created = factory?.Invoke() ?? new DeviceIdentity();
        TrySave(store, created);
        return created;
    }

    public static bool TrySave(IFileStore store, DeviceIdentity identity)
    {
        try
        {
            using var content = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(identity, new JsonSerializerOptions { WriteIndented = true }));
            store.WriteAtomicAsync(FileName, content).GetAwaiter().GetResult();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Read-only card: the device still works, it just gets a new id per session.
            return false;
        }
    }
}
