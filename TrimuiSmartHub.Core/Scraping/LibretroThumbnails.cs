using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;

namespace TrimuiSmartHub.Core.Scraping;

public enum MediaKind
{
    Boxart,
    Snap,
    Title,
}

/// <summary>
/// Client for thumbnails.libretro.com. Thumbnails are named after the canonical game name, so an
/// identified ROM maps directly to a URL. Directory listings (used for fuzzy matching) are downloaded
/// once per system and cached on disk for a week, instead of once per ROM as before.
/// </summary>
public sealed partial class LibretroThumbnails
{
    public static readonly Uri BaseUri = new("https://thumbnails.libretro.com/");

    private static readonly TimeSpan IndexMaxAge = TimeSpan.FromDays(7);

    private readonly HttpClient _http;
    private readonly string _cacheDirectory;
    private readonly ConcurrentDictionary<string, Task<IReadOnlyList<string>>> _indexes = new();

    public LibretroThumbnails(HttpClient http, string cacheDirectory)
    {
        _http = http;
        _cacheDirectory = cacheDirectory;
    }

    public static string FolderFor(MediaKind kind) => kind switch
    {
        MediaKind.Snap => "Named_Snaps",
        MediaKind.Title => "Named_Titles",
        _ => "Named_Boxarts",
    };

    /// <summary>Libretro replaces &amp;*/:`&lt;&gt;?\|" with "_" in thumbnail file names.</summary>
    public static string SanitizeTitle(string title)
    {
        var chars = title.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if ("&*/:`<>?\\|\"".Contains(chars[i])) chars[i] = '_';
        }
        return new string(chars);
    }

    public static Uri BuildUrl(string system, MediaKind kind, string title) =>
        new(BaseUri, $"{Uri.EscapeDataString(system)}/{FolderFor(kind)}/{Uri.EscapeDataString(SanitizeTitle(title))}.png");

    /// <summary>Titles (sanitized, without ".png") available for a system.</summary>
    public Task<IReadOnlyList<string>> GetIndexAsync(string system, MediaKind kind, CancellationToken cancellationToken = default) =>
        _indexes.GetOrAdd($"{system}|{kind}", _ => LoadIndexAsync(system, kind, cancellationToken));

    private async Task<IReadOnlyList<string>> LoadIndexAsync(string system, MediaKind kind, CancellationToken cancellationToken)
    {
        var cacheFile = Path.Combine(_cacheDirectory, "libretro-index", $"{SanitizeFile(system)}_{FolderFor(kind)}.txt");

        try
        {
            if (File.Exists(cacheFile) && DateTime.UtcNow - File.GetLastWriteTimeUtc(cacheFile) < IndexMaxAge)
                return await File.ReadAllLinesAsync(cacheFile, cancellationToken);

            var url = new Uri(BaseUri, $"{Uri.EscapeDataString(system)}/{FolderFor(kind)}/");
            var html = await _http.GetStringAsync(url, cancellationToken);
            var titles = ParseIndex(html);

            Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
            await File.WriteAllLinesAsync(cacheFile, titles, cancellationToken);
            return titles;
        }
        catch (HttpRequestException) when (File.Exists(cacheFile))
        {
            // Offline: an old index is better than none.
            return await File.ReadAllLinesAsync(cacheFile, cancellationToken);
        }
        catch (Exception)
        {
            // Let a later call retry instead of caching the failure forever.
            _indexes.TryRemove($"{system}|{kind}", out _);
            throw;
        }
    }

    internal static IReadOnlyList<string> ParseIndex(string html) =>
        HrefRegex().Matches(html)
            // UnescapeDataString (not UrlDecode): '+' is a literal character in titles, not a space.
            .Select(m => WebUtility.HtmlDecode(Uri.UnescapeDataString(m.Groups[1].Value)))
            .Where(name => name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .Select(name => name[..^4])
            .Distinct()
            .ToList();

    /// <summary>Downloads a PNG. Returns null when it doesn't exist.</summary>
    public async Task<byte[]?> DownloadAsync(Uri url, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync(url, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return IsPng(bytes) ? bytes : null;
    }

    internal static bool IsPng(byte[] bytes) =>
        bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;

    private static string SanitizeFile(string name) => string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    [GeneratedRegex("href=\"([^\"?]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex HrefRegex();
}
