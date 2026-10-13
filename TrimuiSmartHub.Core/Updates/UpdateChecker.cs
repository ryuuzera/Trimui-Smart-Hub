using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace TrimuiSmartHub.Core.Updates;

public sealed record AvailableUpdate(Version Version, string Tag, Uri Page);

/// <summary>Checks the project's GitHub releases for a newer version.</summary>
public sealed class UpdateChecker
{
    public const string Repository = "ryuuzera/TrimuiSmartHub";

    private readonly HttpClient _http;

    public UpdateChecker(HttpClient http)
    {
        _http = http;
    }

    public async Task<AvailableUpdate?> CheckAsync(Version current, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repository}/releases/latest");
        request.Headers.UserAgent.ParseAdd("TrimuiSmartHub");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;

        var release = await response.Content.ReadFromJsonAsync<Release>(cancellationToken: cancellationToken);
        if (release?.TagName == null || release.Draft || release.Prerelease) return null;

        var version = ParseVersion(release.TagName);
        if (version == null || version <= Normalize(current)) return null;

        return new AvailableUpdate(version, release.TagName, new Uri(release.HtmlUrl ?? $"https://github.com/{Repository}/releases"));
    }

    internal static Version? ParseVersion(string tag)
    {
        var text = tag.TrimStart('v', 'V').Split('-', '+')[0];
        return Version.TryParse(text, out var version) ? Normalize(version) : null;
    }

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build));

    private sealed class Release
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }
    }
}
