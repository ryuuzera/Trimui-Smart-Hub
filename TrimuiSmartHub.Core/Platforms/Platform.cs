namespace TrimuiSmartHub.Core.Platforms;

public sealed class Platform
{
    /// <summary>Folder name used by the device (e.g. "GBA", "FC"). Also used as the library folder name.</summary>
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    /// <summary>True for arcade systems: the .zip itself is the ROM and must never be extracted or hashed.</summary>
    public bool Arcade { get; init; }

    /// <summary>Emulators for this platform don't load zipped ROMs, so downloads/imports are extracted.</summary>
    public bool ExtractZip { get; init; }

    /// <summary>Extensions that unambiguously identify this platform (without the dot).</summary>
    public IReadOnlyList<string> Extensions { get; init; } = Array.Empty<string>();

    /// <summary>Folder names on thumbnails.libretro.com, in order of preference.</summary>
    public IReadOnlyList<string> LibretroSystems { get; init; } = Array.Empty<string>();

    /// <summary>Values of the "system" column in the bundled game database.</summary>
    public IReadOnlyList<string> DbSystems { get; init; } = Array.Empty<string>();

    public string? Retrostic { get; init; }

    public IReadOnlyList<BiosFile> Bios { get; init; } = Array.Empty<BiosFile>();

    /// <summary>True when the platform is not in platforms.json and was created from an unknown device folder.</summary>
    public bool IsUnknown { get; init; }

    public override string ToString() => $"{Id} ({Name})";
}

public sealed class BiosFile
{
    /// <summary>Path relative to the BIOS folder, using '/' separators.</summary>
    public string File { get; init; } = string.Empty;

    /// <summary>Accepted MD5 hashes (lowercase). Empty means any content is accepted.</summary>
    public IReadOnlyList<string> Md5 { get; init; } = Array.Empty<string>();

    public bool Required { get; init; }

    /// <summary>Files sharing a group are alternatives: any one valid file satisfies the group.</summary>
    public string? Group { get; init; }

    public string? Note { get; init; }
}
