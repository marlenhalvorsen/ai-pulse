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

        if (_settings.Feeds.Length == 0)
            return [];

        var client = _httpClientFactory.CreateClient("Podcasts");
        var seen = new HashSet<string>();
        var items = new List<ContentItem>();

        foreach (var feed in _settings.Feeds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await client.GetAsync(feed.Url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var xml = await response.Content.ReadAsStringAsync(cancellationToken);
            var episodes = ParseFeed(xml, _settings.EpisodesPerFeed);

            foreach (var episode in episodes)
            {
                if (seen.Add(episode.Id))
                    items.Add(episode);
            }
        }

        return items;
    }

    private static IEnumerable<ContentItem> ParseFeed(string xml, int limit)
    {
        var doc = XDocument.Parse(xml);

        return doc.Descendants("item")
            .Take(limit)
            .Select(item => ParseItem(item))
            .Where(item => item is not null)
            .Cast<ContentItem>();
    }

    private static ContentItem? ParseItem(XElement item)
    {
        var title = item.Element("title")?.Value.Trim();
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var link = item.Element("link")?.Value.Trim();
        var enclosureUrl = item.Element("enclosure")?.Attribute("url")?.Value.Trim();
        var url = !string.IsNullOrWhiteSpace(link) ? link : enclosureUrl;

        if (string.IsNullOrWhiteSpace(url))
            return null;

        var guid = item.Element("guid")?.Value.Trim()
                   ?? item.Element("link")?.Value.Trim()
                   ?? title;

        var pubDateRaw = item.Element("pubDate")?.Value.Trim();
        var postedAt = ParsePubDate(pubDateRaw);

        return new ContentItem
        {
            Id = BuildId(guid),
            Title = title,
            Url = url,
            Source = SourceType.Podcast,
            ContentType = ContentType.Podcast,
            Upvotes = 0,
            CommentCount = 0,
            PostedAt = postedAt
        };
    }

    private static DateTime ParsePubDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return DateTime.UtcNow;

        if (DateTimeOffset.TryParse(raw, out var dto))
            return dto.UtcDateTime;

        return DateTime.UtcNow;
    }

    private static string BuildId(string guid)
    {
        var slug = Regex.Replace(guid.ToLowerInvariant(), @"[^a-z0-9]+", "_",
            RegexOptions.None, TimeSpan.FromMilliseconds(100)).Trim('_');
        return $"podcast_{slug}";
    }
}
