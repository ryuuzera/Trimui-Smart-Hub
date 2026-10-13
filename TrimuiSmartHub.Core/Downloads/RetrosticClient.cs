using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using TrimuiSmartHub.Core.Jobs;
using TrimuiSmartHub.Core.Platforms;
using TrimuiSmartHub.Core.Storage;

namespace TrimuiSmartHub.Core.Downloads;

public sealed record RemoteGame(string PlatformId, string Title, string Region, Uri? BoxArt, Uri DownloadPage);

/// <summary>
/// Optional download source (retrostic.com). It scrapes HTML, so it can break whenever the site changes;
/// it is isolated here so it can be removed or turned into a plugin without touching the rest of the app.
/// </summary>
public sealed partial class RetrosticClient
{
    private const int MaxPages = 40;

    private static readonly Uri BaseUri = new("https://www.retrostic.com/");

    private readonly HttpClient _http;
    private readonly HtmlParser _parser = new();

    public RetrosticClient(HttpClient http)
    {
        _http = http;
    }

    public static bool Supports(Platform platform) => !string.IsNullOrEmpty(platform.Retrostic);

    /// <summary>First page of the platform listing (most popular games).</summary>
    public async Task<IReadOnlyList<RemoteGame>> ListPopularAsync(Platform platform, CancellationToken cancellationToken = default)
    {
        var html = await _http.GetStringAsync(PlatformUri(platform), cancellationToken);
        return ParseListing(html, platform.Id, out _);
    }

    /// <summary>
    /// Searches by title. The site lists games by initial letter, so this walks that letter's pages
    /// (all of them, not only the first one) and filters by the query.
    /// </summary>
    public async Task<IReadOnlyList<RemoteGame>> SearchAsync(Platform platform, string query, CancellationToken cancellationToken = default)
    {
        var normalizedQuery = Simplify(query);
        if (normalizedQuery.Length == 0) return Array.Empty<RemoteGame>();

        var letter = char.ToLowerInvariant(query.Trim()[0]);
        var sorting = char.IsLetter(letter) ? letter.ToString() : "0";
        Uri? next = new($"{PlatformUri(platform)}?sorting={Uri.EscapeDataString(sorting)}");

        var result = new List<RemoteGame>();
        for (var page = 0; next != null && page < MaxPages; page++)
        {
            var html = await _http.GetStringAsync(next, cancellationToken);
            var games = ParseListing(html, platform.Id, out next);
            result.AddRange(games.Where(g => Simplify(g.Title).Contains(normalizedQuery, StringComparison.Ordinal)));
        }

        return result;
    }

    internal IReadOnlyList<RemoteGame> ParseListing(string html, string platformId, out Uri? nextPage)
    {
        var document = _parser.ParseDocument(html);
        var games = new List<RemoteGame>();

        // Parsing row by row keeps title, image and region together. The previous code collected them
        // in separate lists and matched them by index, which misaligned regions whenever a row had no flag.
        foreach (var row in document.QuerySelectorAll("tbody tr[itemtype*='VideoGame']"))
        {
            var link = row.QuerySelectorAll("a[href*='/roms/']").FirstOrDefault(a => a.HasAttribute("title"));
            if (link == null) continue;

            var href = link.GetAttribute("href");
            if (string.IsNullOrEmpty(href)) continue;

            var image = link.QuerySelector("img");
            var imageSrc = image?.GetAttribute("data-src") ?? image?.GetAttribute("src");

            var regionCell = row.QuerySelectorAll("td").FirstOrDefault(td => td.QuerySelector("img[src*='/flags']") != null);
            var region = regionCell?.TextContent.Trim() ?? string.Empty;

            games.Add(new RemoteGame(
                platformId,
                CleanTitle(link.GetAttribute("title")!),
                region,
                string.IsNullOrEmpty(imageSrc) ? null : new Uri(BaseUri, imageSrc),
                new Uri(BaseUri, href)));
        }

        var nextLink = document.QuerySelectorAll("a.page-link").FirstOrDefault(a => a.TextContent.Trim() == ">");
        var nextHref = nextLink?.GetAttribute("href");
        nextPage = string.IsNullOrEmpty(nextHref) ? null : new Uri(BaseUri, nextHref);

        return games;
    }

    /// <summary>Downloads the game to a temporary file and returns its path. The caller imports it into the library.</summary>
    public async Task<string> DownloadAsync(RemoteGame game, string tempDirectory, IProgress<JobProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report(new JobProgress(null, "Requesting download link..."));
        var downloadLink = await ResolveDownloadLinkAsync(game, cancellationToken);

        using var response = await _http.GetAsync(downloadLink, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
            ?? Uri.UnescapeDataString(downloadLink.Segments.LastOrDefault() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(Path.GetExtension(fileName))) fileName = game.Title + ".zip";

        Directory.CreateDirectory(tempDirectory);
        var target = Path.Combine(tempDirectory, RelPath.SanitizeFileName(fileName));
        var total = response.Content.Headers.ContentLength;

        try
        {
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, useAsync: true);

            var buffer = new byte[1 << 20];
            long received = 0;
            var started = DateTime.UtcNow;
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                received += read;

                var seconds = Math.Max(0.001, (DateTime.UtcNow - started).TotalSeconds);
                var speed = received / seconds / (1024 * 1024);
                var fraction = total is > 0 ? (double)received / total.Value : (double?)null;
                progress?.Report(new JobProgress(fraction, $"{received / (1024.0 * 1024):0.0} MB · {speed:0.0} MB/s"));
            }
        }
        catch
        {
            File.Delete(target);
            throw;
        }

        return target;
    }

    private async Task<Uri> ResolveDownloadLinkAsync(RemoteGame game, CancellationToken cancellationToken)
    {
        var page = _parser.ParseDocument(await _http.GetStringAsync(game.DownloadPage, cancellationToken));
        var session = page.QuerySelector("input[name*='session']")?.GetAttribute("value");

        // Download pages look like /roms/<console>/<rom>.
        var segments = game.DownloadPage.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (string.IsNullOrEmpty(session) || segments.Length < 3)
            throw new InvalidOperationException("The download page format changed; the source may be unavailable.");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["session"] = session,
            ["rom_url"] = segments[2],
            ["console_url"] = segments[1],
        });

        var postUri = new Uri(game.DownloadPage.ToString().TrimEnd('/') + "/download");
        using var response = await _http.PostAsync(postUri, form, cancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        var match = DownloadLinkRegex().Match(html);
        if (!match.Success) throw new InvalidOperationException("Download link not found; the source may be unavailable.");

        return new Uri(match.Groups[1].Value);
    }

    private static Uri PlatformUri(Platform platform) =>
        new(platform.Retrostic ?? throw new NotSupportedException($"{platform.Id} has no download source."));

    internal static string CleanTitle(string title) =>
        TagRegex().Replace(title.Replace("Roms", string.Empty).Replace("ISO", string.Empty), string.Empty).Trim();

    private static string Simplify(string value) =>
        new(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    [GeneratedRegex(@"window\.location\.href\s*=\s*""(https://[^\s""]+)""")]
    private static partial Regex DownloadLinkRegex();

    [GeneratedRegex(@"\(\s*[^)]+\s*\)|\[\s*[^\]]+\s*\]")]
    private static partial Regex TagRegex();
}
