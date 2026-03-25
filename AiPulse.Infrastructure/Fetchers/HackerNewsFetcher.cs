using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AiPulse.Application.Interfaces;
using AiPulse.Application.Services;
using AiPulse.Domain.Enums;
using AiPulse.Domain.Models;
using AiPulse.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AiPulse.Infrastructure.Fetchers;

public class HackerNewsFetcher : ITrendFetcher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HackerNewsSettings _settings;
    private readonly UrlClassifier _urlClassifier;

    public HackerNewsFetcher(
        IHttpClientFactory httpClientFactory,
        IOptions<HackerNewsSettings> settings,
        UrlClassifier urlClassifier)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _urlClassifier = urlClassifier;
    }

    public async Task<IEnumerable<ContentItem>> FetchAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = _httpClientFactory.CreateClient("HackerNews");

        var topIds = await FetchStoryIdsAsync(client, "topstories", cancellationToken);
        var bestIds = await FetchStoryIdsAsync(client, "beststories", cancellationToken);

        var allIds = topIds.Union(bestIds).Take(_settings.StoryLimit).ToList();

        var items = new List<ContentItem>();
        foreach (var id in allIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = await FetchItemAsync(client, id, cancellationToken);
            if (item is null)
                continue;

            if (IsAiRelated(item.Title))
                items.Add(item);
        }

        return items;
    }

    private async Task<IEnumerable<int>> FetchStoryIdsAsync(
        HttpClient client, string listName, CancellationToken cancellationToken)
    {
        var response = await client.GetAsync($"/v0/{listName}.json", cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<int[]>(json, JsonOptions) ?? [];
    }

    private async Task<ContentItem?> FetchItemAsync(
        HttpClient client, int id, CancellationToken cancellationToken)
    {
        var response = await client.GetAsync($"/v0/item/{id}.json", cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var post = JsonSerializer.Deserialize<HnItem>(json, JsonOptions);

        if (post is null || post.Type != "story" || string.IsNullOrWhiteSpace(post.Title))
            return null;

        var url = string.IsNullOrWhiteSpace(post.Url)
            ? $"https://news.ycombinator.com/item?id={post.Id}"
            : post.Url;

        return new ContentItem
        {
            Id = $"hn_{post.Id}",
            Title = post.Title,
            Url = url,
            Source = SourceType.HackerNews,
            ContentType = _urlClassifier.Classify(url),
            Upvotes = post.Score,
            CommentCount = post.Descendants,
            PostedAt = DateTimeOffset.FromUnixTimeSeconds(post.Time).UtcDateTime
        };
    }

    private bool IsAiRelated(string title) =>
        _settings.AiKeywords.Any(kw =>
            Regex.IsMatch(title, $@"\b{Regex.Escape(kw)}(?:s?\b)", RegexOptions.IgnoreCase));

    // ── Deserialization types ──────────────────────────────────────────────

    private sealed class HnItem
    {
        public int Id { get; init; }
        public string? Title { get; init; }
        public string? Url { get; init; }
        public int Score { get; init; }
        public int Descendants { get; init; }
        public long Time { get; init; }
        public string? Type { get; init; }
    }
}
