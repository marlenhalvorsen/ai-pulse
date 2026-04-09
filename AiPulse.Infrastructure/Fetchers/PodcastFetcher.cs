using System.Text.RegularExpressions;
using System.Xml.Linq;
using HtmlAgilityPack;
using AiPulse.Application.Interfaces;
using AiPulse.Domain.Enums;
using AiPulse.Domain.Models;
using AiPulse.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiPulse.Infrastructure.Fetchers;

public class PodcastFetcher : ITrendFetcher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PodcastSettings _settings;
    private readonly ILogger<PodcastFetcher> _logger;

    public PodcastFetcher(
        IHttpClientFactory httpClientFactory,
        IOptions<PodcastSettings> settings,
        ILogger<PodcastFetcher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<IEnumerable<ContentItem>> FetchAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = _httpClientFactory.CreateClient("Podcasts");
        var items = new List<ContentItem>();

        foreach (var feed in _settings.CuratedFeeds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var episodes = await FetchEpisodesAsync(client, feed.ShowName, feed.RssUrl, cancellationToken);
                items.AddRange(episodes);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("Podcast feed unavailable for '{Show}' ({Url}): {Status}",
                    feed.ShowName, feed.RssUrl, ex.StatusCode);
            }
        }

        return items;
    }

    // ── RSS fetching ──────────────────────────────────────────────────────────

    private async Task<IEnumerable<ContentItem>> FetchEpisodesAsync(
        HttpClient client, string showName, string rssUrl, CancellationToken cancellationToken)
    {
        var response = await client.GetAsync(rssUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        var xml = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseEpisodes(xml, showName, _settings.EpisodesPerShow);
    }

    private static readonly XNamespace Itunes = "http://www.itunes.com/dtds/podcast-1.0.dtd";

    private static IEnumerable<ContentItem> ParseEpisodes(string xml, string showName, int limit)
    {
        var doc = XDocument.Parse(xml);
        var items = new List<ContentItem>();

        foreach (var item in doc.Descendants("item").Take(limit))
        {
            var title = item.Element("title")?.Value.Trim();
            if (string.IsNullOrWhiteSpace(title)) continue;

            var link = item.Element("link")?.Value.Trim();
            var enclosureUrl = item.Element("enclosure")?.Attribute("url")?.Value.Trim();
            var url = !string.IsNullOrWhiteSpace(link) ? link : enclosureUrl;
            if (string.IsNullOrWhiteSpace(url)) continue;

            var guid = item.Element("guid")?.Value.Trim() ?? url;
            var pubDateRaw = item.Element("pubDate")?.Value.Trim();

            items.Add(new ContentItem
            {
                Id = BuildId(guid),
                Title = title,
                Url = url,
                Source = SourceType.Podcast,
                ContentType = ContentType.Podcast,
                ShowName = showName,
                Description = ResolveDescription(item),
                Upvotes = 0,
                CommentCount = 0,
                PostedAt = ParsePubDate(pubDateRaw)
            });
        }

        return items;
    }

    private static readonly string[] TranscriptMarkers = ["This is a transcript", "The timestamps in"];
    private static readonly string[] SponsorPhrases = ["check out our sponsors", "brought to you by", "support this podcast"];

    private static string? ResolveDescription(XElement item)
    {
        var summary = item.Element(Itunes + "summary")?.Value.Trim();
        if (!string.IsNullOrWhiteSpace(summary))
            return StripHtml(summary);

        var subtitle = item.Element(Itunes + "subtitle")?.Value.Trim();
        if (!string.IsNullOrWhiteSpace(subtitle))
            return StripHtml(subtitle);

        var description = item.Element("description")?.Value.Trim();
        if (IsTranscript(description)) return null;
        return StripHtml(description);
    }

    private static bool IsTranscript(string? text) =>
        !string.IsNullOrWhiteSpace(text) &&
        TranscriptMarkers.Any(m => text.StartsWith(m, StringComparison.OrdinalIgnoreCase));

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string? StripHtml(string? raw, int maxLength = 200)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var doc = new HtmlDocument();
        doc.LoadHtml(raw);
        var text = HtmlEntity.DeEntitize(doc.DocumentNode.InnerText);
        text = RemoveSponsorText(text);
        text = Regex.Replace(text, @"\s+", " ", RegexOptions.None, TimeSpan.FromMilliseconds(100)).Trim();
        if (string.IsNullOrWhiteSpace(text)) return null;
        return text.Length <= maxLength ? text : text[..maxLength].TrimEnd() + "…";
    }

    private static string RemoveSponsorText(string text)
    {
        var cutIndex = text.Length;

        foreach (var phrase in SponsorPhrases)
        {
            var idx = text.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0 && idx < cutIndex) cutIndex = idx;
        }

        // URL on its own line (newline followed by https://, or text starts with https://)
        var urlMatch = Regex.Match(text, @"(?:^|\n)\s*https://", RegexOptions.None, TimeSpan.FromMilliseconds(100));
        if (urlMatch.Success && urlMatch.Index < cutIndex) cutIndex = urlMatch.Index;

        return cutIndex < text.Length ? text[..cutIndex].TrimEnd() : text;
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
}
