using TrimuiSmartHub.Core.Storage;

namespace TrimuiSmartHub.Core.Devices;

/// <summary>
/// CrossMix-OS keeps the stock "Emus/Roms/Imgs" structure, so it reuses the stock layout and only
/// adds its own detection and version.
/// </summary>
public sealed class CrossMixLayout : TrimuiStockLayout
{
    internal const string VersionFile = "System/usr/trimui/crossmix-version.txt";

    public override string Id => "crossmix";

    public override string DisplayName => "CrossMix-OS";

    public override int Priority => 10;

    public override bool Matches(IFileStore store) => store.FileExists(VersionFile) && base.Matches(store);

    public override string? ReadVersion(IFileStore store)
    {
        try
        {
            using var reader = new StreamReader(store.OpenRead(VersionFile));
            return reader.ReadLine()?.Trim();
        }
        catch (IOException)
        {
            return null;
        }
    }
}
