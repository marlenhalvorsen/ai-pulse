using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AiPulse.Application.Interfaces;
using AiPulse.Domain.Enums;
using AiPulse.Domain.Models;
using AiPulse.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AiPulse.Infrastructure.Fetchers;

public class PodcastFetcher : ITrendFetcher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PodcastSettings _settings;

    public PodcastFetcher(
        IHttpClientFactory httpClientFactory,
        IOptions<PodcastSettings> settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
    }

    public async Task<IEnumerable<ContentItem>> FetchAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = _httpClientFactory.CreateClient("Podcasts");

        // Tracks seen show names (case-insensitive) to deduplicate across charts + curated
        var seenShows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<ContentItem>();

        // 1. Apple Podcasts Top Charts → filter AI → lookup RSS → fetch latest episode
        var chartItems = await FetchChartEpisodesAsync(client, cancellationToken);
        foreach (var item in chartItems)
        {
            if (seenShows.Add(item.ShowName ?? item.Id))
                items.Add(item);
        }

        // 2. Curated feeds — always included, skip shows already fetched from charts
        foreach (var feed in _settings.CuratedFeeds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!seenShows.Add(feed.ShowName))
                continue;

            var episode = await FetchLatestEpisodeAsync(client, feed.ShowName, feed.RssUrl, cancellationToken);
            if (episode is not null)
                items.Add(episode);
        }

        return items;
    }

    // ── Top Charts ────────────────────────────────────────────────────────────

    private async Task<IEnumerable<ContentItem>> FetchChartEpisodesAsync(
        HttpClient client, CancellationToken cancellationToken)
    {
        var chartsResponse = await client.GetAsync(_settings.TopChartsUrl, cancellationToken);
        chartsResponse.EnsureSuccessStatusCode();
        var chartsJson = await chartsResponse.Content.ReadAsStringAsync(cancellationToken);

        var entries = ParseTopCharts(chartsJson);
        var aiEntries = entries.Where(e => IsAiRelated(e.Name)).ToList();
        if (aiEntries.Count == 0)
            return [];

        // Batch lookup to get RSS feed URLs
        var ids = string.Join(",", aiEntries.Select(e => e.Id));
        var lookupResponse = await client.GetAsync($"{_settings.LookupBaseUrl}?id={ids}", cancellationToken);
        lookupResponse.EnsureSuccessStatusCode();
        var lookupJson = await lookupResponse.Content.ReadAsStringAsync(cancellationToken);

        var rssByCollectionId = ParseLookup(lookupJson);

        var items = new List<ContentItem>();
        foreach (var entry in aiEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!rssByCollectionId.TryGetValue(entry.Id, out var feedInfo))
                continue;

            var episode = await FetchLatestEpisodeAsync(client, feedInfo.ShowName, feedInfo.FeedUrl, cancellationToken);
            if (episode is not null)
                items.Add(episode);
        }

        return items;
    }

    // ── RSS fetching ──────────────────────────────────────────────────────────

    private async Task<ContentItem?> FetchLatestEpisodeAsync(
        HttpClient client, string showName, string rssUrl, CancellationToken cancellationToken)
    {
        var response = await client.GetAsync(rssUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        var xml = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseLatestEpisode(xml, showName);
    }

    private static ContentItem? ParseLatestEpisode(string xml, string showName)
    {
        var doc = XDocument.Parse(xml);
        var item = doc.Descendants("item").FirstOrDefault();
        if (item is null) return null;

        var title = item.Element("title")?.Value.Trim();
        if (string.IsNullOrWhiteSpace(title)) return null;

        var link = item.Element("link")?.Value.Trim();
        var enclosureUrl = item.Element("enclosure")?.Attribute("url")?.Value.Trim();
        var url = !string.IsNullOrWhiteSpace(link) ? link : enclosureUrl;
        if (string.IsNullOrWhiteSpace(url)) return null;

        var guid = item.Element("guid")?.Value.Trim() ?? url;
        var description = item.Element("description")?.Value.Trim();
        var pubDateRaw = item.Element("pubDate")?.Value.Trim();

        return new ContentItem
        {
            Id = BuildId(guid),
            Title = title,
            Url = url,
            Source = SourceType.Podcast,
            ContentType = ContentType.Podcast,
            ShowName = showName,
            Description = description,
            Upvotes = 0,
            CommentCount = 0,
            PostedAt = ParsePubDate(pubDateRaw)
        };
    }

    // ── Parsing helpers ───────────────────────────────────────────────────────

    private static IReadOnlyList<(string Id, string Name)> ParseTopCharts(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("feed", out var feed) ||
            !feed.TryGetProperty("entry", out var entries))
            return [];

        var results = new List<(string Id, string Name)>();
        foreach (var entry in entries.EnumerateArray())
        {
            var name = entry.TryGetProperty("im:name", out var nameEl) &&
                       nameEl.TryGetProperty("label", out var nameLabel)
                ? nameLabel.GetString() ?? string.Empty
                : string.Empty;

            var id = entry.TryGetProperty("id", out var idEl) &&
                     idEl.TryGetProperty("attributes", out var attrs) &&
                     attrs.TryGetProperty("im:id", out var imId)
                ? imId.GetString() ?? string.Empty
                : string.Empty;

            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
                results.Add((id, name));
        }

        return results;
    }

    private record FeedInfo(string ShowName, string FeedUrl);

    private static IReadOnlyDictionary<string, FeedInfo> ParseLookup(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("results", out var results))
            return new Dictionary<string, FeedInfo>();

        var dict = new Dictionary<string, FeedInfo>();
        foreach (var result in results.EnumerateArray())
        {
            var feedUrl = result.TryGetProperty("feedUrl", out var feedProp)
                ? feedProp.GetString() : null;

            if (string.IsNullOrWhiteSpace(feedUrl)) continue;

            var collectionId = result.TryGetProperty("collectionId", out var idProp)
                ? idProp.GetInt64().ToString() : null;

            var showName = result.TryGetProperty("collectionName", out var nameProp)
                ? nameProp.GetString() ?? string.Empty : string.Empty;

            if (!string.IsNullOrWhiteSpace(collectionId))
                dict[collectionId] = new FeedInfo(showName, feedUrl);
        }

        return dict;
    }

    private static DateTime ParsePubDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return DateTime.UtcNow;
        return DateTimeOffset.TryParse(raw, out var dto) ? dto.UtcDateTime : DateTime.UtcNow;
    }

    private static string BuildId(string guid)
    {
        var slug = Regex.Replace(guid.ToLowerInvariant(), @"[^a-z0-9]+", "_",
            RegexOptions.None, TimeSpan.FromMilliseconds(100)).Trim('_');
        return $"podcast_{slug}";
    }

    private bool IsAiRelated(string name) =>
        _settings.AiKeywords.Any(kw =>
            name.Contains(kw, StringComparison.OrdinalIgnoreCase));
}
