namespace TrimuiSmartHub.Core.Storage;

/// <summary>Helpers for store-relative paths, which always use '/' regardless of the OS.</summary>
public static class RelPath
{
    public static string Normalize(string path)
    {
        var normalized = path.Replace('\\', '/');
        while (normalized.Contains("//")) normalized = normalized.Replace("//", "/");
        return normalized.Trim('/');
    }

    public static string Combine(params string[] parts) =>
        Normalize(string.Join('/', parts.Where(p => !string.IsNullOrEmpty(p))));

    public static string GetFileName(string path)
    {
        var normalized = Normalize(path);
        var index = normalized.LastIndexOf('/');
        return index < 0 ? normalized : normalized[(index + 1)..];
    }

    public static string GetDirectory(string path)
    {
        var normalized = Normalize(path);
        var index = normalized.LastIndexOf('/');
        return index < 0 ? string.Empty : normalized[..index];
    }

    /// <summary>
    /// File name without its last extension. Unlike splitting on the first '.', this keeps
    /// names such as "Super Mario Bros. 3.nes" intact.
    /// </summary>
    public static string GetBaseName(string path) => Path.GetFileNameWithoutExtension(GetFileName(path));

    public static string GetExtension(string path) => Path.GetExtension(GetFileName(path)).TrimStart('.').ToLowerInvariant();

    /// <summary>Resolves "." and ".." segments. Returns null when the path escapes the root.</summary>
    public static string? Resolve(string path)
    {
        var stack = new List<string>();
        foreach (var segment in Normalize(path).Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".") continue;
            if (segment == "..")
            {
                if (stack.Count == 0) return null;
                stack.RemoveAt(stack.Count - 1);
                continue;
            }
            stack.Add(segment);
        }
        return string.Join('/', stack);
    }

    /// <summary>Replaces characters that are invalid in FAT32/exFAT/NTFS file names.</summary>
    public static string SanitizeFileName(string name)
    {
        var invalid = new HashSet<char>(Path.GetInvalidFileNameChars()) { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };
        var chars = name.Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray();
        return new string(chars).Trim().TrimEnd('.');
    }
}
