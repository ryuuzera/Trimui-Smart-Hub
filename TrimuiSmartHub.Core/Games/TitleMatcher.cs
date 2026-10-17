using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TrimuiSmartHub.Core.Games;

public sealed record TitleMatch(string Candidate, double Score);

/// <summary>
/// Fuzzy title matching used only when a ROM could not be identified by the database.
/// Replaces the old Levenshtein matcher, which scored any candidate *containing* the query as a perfect
/// match and then picked the shortest one ("Mario" matched the wrong game).
/// </summary>
public static partial class TitleMatcher
{
    /// <summary>Scores at or above this value are applied automatically; lower ones go to manual review.</summary>
    public const double AutoAcceptScore = 0.85;

    private static readonly string[] RegionPreference = { "world", "usa", "europe", "japan" };

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal) { "the", "a", "an", "of", "and", "rom", "roms", "iso" };

    /// <param name="query">Title without file extension.</param>
    /// <param name="candidates">Titles without file extension.</param>
    public static IReadOnlyList<TitleMatch> FindBest(string query, IEnumerable<string> candidates, int take = 5) =>
        new TitleIndex(candidates).FindBest(query, take);

    internal static double ScoreCandidate(string normalizedQuery, HashSet<string> queryTokens, string? queryRegion, TitleIndex.Entry candidate)
    {
        var score = Score(normalizedQuery, queryTokens, candidate.Normalized, candidate.Tokens);

        // Same base title in several regions: prefer the query's region, then World/USA/Europe/Japan.
        if (queryRegion != null && candidate.Region == queryRegion) score += 0.02;
        else if (candidate.Region != null) score += 0.01 * (RegionPreference.Length - Array.IndexOf(RegionPreference, candidate.Region)) / RegionPreference.Length;

        return score;
    }

    /// <summary>Lowercase, accents removed, "(USA)"/"[!]" tags and punctuation stripped, stop words dropped.</summary>
    public static string Normalize(string title)
    {
        var withoutTags = TagRegex().Replace(title, " ");

        var decomposed = withoutTags.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }

        var tokens = builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => !StopWords.Contains(t));
        return string.Join(' ', tokens);
    }

    private static double Score(string query, HashSet<string> queryTokens, string candidate, HashSet<string> candidateTokens)
    {
        if (candidate.Length == 0) return 0;
        if (query == candidate) return 1.0;

        var common = queryTokens.Intersect(candidateTokens).Count();
        var dice = 2.0 * common / (queryTokens.Count + candidateTokens.Count);

        var distance = Levenshtein(query, candidate);
        var similarity = 1.0 - (double)distance / Math.Max(query.Length, candidate.Length);

        // Token overlap handles reordered words ("Zelda, The Legend of"); edit similarity handles typos.
        return 0.6 * dice + 0.4 * similarity;
    }

    internal static HashSet<string> Tokens(string normalized) =>
        new(normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);

    internal static string? Region(string title)
    {
        var lower = title.ToLowerInvariant();
        return RegionPreference.FirstOrDefault(r => lower.Contains($"({r}") || lower.Contains($", {r}") || lower.Contains($"{r})"));
    }

    internal static int Levenshtein(string source, string target)
    {
        if (source.Length == 0) return target.Length;
        if (target.Length == 0) return source.Length;

        var previous = new int[target.Length + 1];
        var current = new int[target.Length + 1];
        for (var j = 0; j <= target.Length; j++) previous[j] = j;

        for (var i = 1; i <= source.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= target.Length; j++)
            {
                var cost = source[i - 1] == target[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }

        return previous[target.Length];
    }

    [GeneratedRegex(@"\([^)]*\)|\[[^\]]*\]")]
    private static partial Regex TagRegex();
}

/// <summary>
/// Pre-normalized candidate titles with an inverted token index. Only candidates sharing at least one
/// word with the query are scored, which keeps matching against ~10k thumbnails per system fast.
/// </summary>
public sealed class TitleIndex
{
    internal sealed record Entry(string Original, string Normalized, HashSet<string> Tokens, string? Region);

    /// <summary>
    /// Low on purpose: these are only suggestions for manual review (e.g. "zelda lttp" should still offer
    /// "Legend of Zelda, The - A Link to the Past"). Automatic use requires <see cref="TitleMatcher.AutoAcceptScore"/>.
    /// </summary>
    private const double MinSuggestionScore = 0.15;

    private readonly List<Entry> _entries = new();
    private readonly Dictionary<string, List<int>> _byToken = new(StringComparer.Ordinal);

    public TitleIndex(IEnumerable<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            var normalized = TitleMatcher.Normalize(candidate);
            if (normalized.Length == 0) continue;

            var tokens = TitleMatcher.Tokens(normalized);
            var index = _entries.Count;
            _entries.Add(new Entry(candidate, normalized, tokens, TitleMatcher.Region(candidate)));

            foreach (var token in tokens)
            {
                if (!_byToken.TryGetValue(token, out var list)) _byToken[token] = list = new List<int>();
                list.Add(index);
            }
        }
    }

    public int Count => _entries.Count;

    public IReadOnlyList<TitleMatch> FindBest(string query, int take = 5)
    {
        var normalizedQuery = TitleMatcher.Normalize(query);
        if (normalizedQuery.Length == 0) return Array.Empty<TitleMatch>();

        var queryTokens = TitleMatcher.Tokens(normalizedQuery);
        var queryRegion = TitleMatcher.Region(query);

        var candidateIds = new HashSet<int>();
        foreach (var token in queryTokens)
        {
            if (_byToken.TryGetValue(token, out var ids)) candidateIds.UnionWith(ids);
        }

        // Order by the raw score (region bonus included) and clamp only for display; clamping first made
        // "(Europe)" and "(USA)" tie at 1.0 for an exact title.
        return candidateIds
            .Select(id => _entries[id])
            .Select(entry => (Entry: entry, Raw: TitleMatcher.ScoreCandidate(normalizedQuery, queryTokens, queryRegion, entry)))
            .Where(m => m.Raw > MinSuggestionScore)
            .OrderByDescending(m => m.Raw)
            .ThenBy(m => m.Entry.Original.Length)
            .ThenBy(m => m.Entry.Original, StringComparer.Ordinal)
            .Take(take)
            .Select(m => new TitleMatch(m.Entry.Original, Math.Min(m.Raw, 1.0)))
            .ToList();
    }
}
