using System.Text.Json;

namespace TrimuiSmartHub.Core.Platforms;

public sealed class PlatformRegistry
{
    /// <summary>Extensions shared by several platforms: they never identify a platform on their own.</summary>
    public static readonly IReadOnlySet<string> AmbiguousExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "zip", "7z", "iso", "cue", "bin", "chd", "pbp", "img", "m3u", "ccd", "mds", "ecm" };

    private readonly Dictionary<string, Platform> _byId;
    private readonly Dictionary<string, Platform> _byExtension;

    public IReadOnlyList<Platform> All { get; }

    public PlatformRegistry(IEnumerable<Platform> platforms)
    {
        All = platforms.ToList();
        _byId = All.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);

        _byExtension = new Dictionary<string, Platform>(StringComparer.OrdinalIgnoreCase);
        foreach (var platform in All)
        {
            foreach (var ext in platform.Extensions)
            {
                if (AmbiguousExtensions.Contains(ext)) continue;
                _byExtension.TryAdd(ext, platform);
            }
        }
    }

    public static PlatformRegistry LoadDefault()
    {
        var assembly = typeof(PlatformRegistry).Assembly;
        using var stream = assembly.GetManifestResourceStream("TrimuiSmartHub.Core.platforms.json")
            ?? throw new InvalidOperationException("platforms.json resource not found.");
        return Load(stream);
    }

    public static PlatformRegistry Load(Stream json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip };
        var file = JsonSerializer.Deserialize<PlatformsFile>(json, options)
            ?? throw new InvalidDataException("Invalid platforms.json.");
        return new PlatformRegistry(file.Platforms);
    }

    public Platform? Find(string id) => _byId.GetValueOrDefault(id);

    /// <summary>Returns the known platform or a generic one, so unknown device folders can still be synced.</summary>
    public Platform GetOrUnknown(string id) =>
        Find(id) ?? new Platform { Id = id, Name = id, IsUnknown = true };

    public Platform? FindByExtension(string extension) =>
        _byExtension.GetValueOrDefault(extension.TrimStart('.'));

    private sealed class PlatformsFile
    {
        public List<Platform> Platforms { get; set; } = new();
    }
}
