using System.Text.Json;
using System.Text.Json.Serialization;
using AiPulse.Application.Interfaces;
using AiPulse.Domain.Enums;
using AiPulse.Domain.Models;
using AiPulse.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AiPulse.Infrastructure.Fetchers;

public class RedditFetcher : ITrendFetcher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RedditSettings _settings;

    public RedditFetcher(
        IHttpClientFactory httpClientFactory,
        IOptions<RedditSettings> settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
    }

    public async Task<IEnumerable<ContentItem>> FetchAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = _httpClientFactory.CreateClient("Reddit");
        var items = new List<ContentItem>();

        foreach (var subreddit in _settings.Subreddits)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var posts = await FetchSubredditAsync(client, subreddit, cancellationToken);
            items.AddRange(posts);
        }

        return items;
    }

    private async Task<IEnumerable<ContentItem>> FetchSubredditAsync(
        HttpClient client, string subreddit, CancellationToken cancellationToken)
    {
        var url = $"/r/{subreddit}/hot.json?limit={_settings.PostsPerSubreddit}";
        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var listing = JsonSerializer.Deserialize<RedditListing>(json, JsonOptions);

        if (listing?.Data?.Children is null)
            return [];

        return listing.Data.Children
            .Where(c => c.Data is not null)
            .Select(c => MapToContentItem(c.Data!));
    }

    private ContentItem MapToContentItem(RedditPost post)
    {
        var threadUrl = $"https://www.reddit.com{post.Permalink}";
        var externalUrl = post.IsSelf ? null : post.Url;

        return new()
        {
            Id = $"reddit_{post.Id}",
            Title = post.Title,
            Url = threadUrl,
            ExternalUrl = externalUrl,
            Source = SourceType.Reddit,
            ContentType = ContentType.Discussion,
            Upvotes = post.Score,
            CommentCount = post.NumComments,
            PostedAt = DateTimeOffset.FromUnixTimeSeconds((long)post.CreatedUtc).UtcDateTime
        };
    }

    // ── Deserialization types ──────────────────────────────────────────────

    private sealed class RedditListing
    {
        public RedditListingData? Data { get; init; }
    }

    private sealed class RedditListingData
    {
        public RedditChild[]? Children { get; init; }
    }

    private sealed class RedditChild
    {
        public RedditPost? Data { get; init; }
    }

    private sealed class RedditPost
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Url { get; init; } = string.Empty;
        public string Permalink { get; init; } = string.Empty;
        public int Score { get; init; }

        [JsonPropertyName("is_self")]
        public bool IsSelf { get; init; }

        [JsonPropertyName("num_comments")]
        public int NumComments { get; init; }

        [JsonPropertyName("created_utc")]
        public double CreatedUtc { get; init; }
    }
}
