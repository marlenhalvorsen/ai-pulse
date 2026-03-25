using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiPulse.Application.Interfaces;
using AiPulse.Domain.Enums;
using AiPulse.Domain.Models;
using AiPulse.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AiPulse.Infrastructure.Fetchers;

public class ProductHuntFetcher : ITrendFetcher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ProductHuntSettings _settings;

    public ProductHuntFetcher(IHttpClientFactory httpClientFactory, IOptions<ProductHuntSettings> settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
    }

    public async Task<IEnumerable<ContentItem>> FetchAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = _httpClientFactory.CreateClient("ProductHunt");
        var today = DateTime.UtcNow.Date.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var graphql = $$"""
            { "query": "{ posts(order: VOTES, first: {{_settings.PostLimit}}, postedAfter: \"{{today}}\") { edges { node { id name slug tagline website votesCount commentsCount createdAt } } } }" }
            """;

        var request = new HttpRequestMessage(HttpMethod.Post, "/v2/api/graphql")
        {
            Content = new StringContent(graphql, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.DeveloperToken);

        var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<PhResponse>(json, JsonOptions);

        return result?.Data?.Posts?.Edges?
            .Where(e => e.Node is not null)
            .Select(e => MapToContentItem(e.Node!))
            ?? [];
    }

    private static ContentItem MapToContentItem(PhPost post) => new()
    {
        Id = $"ph_{post.Id}",
        Title = post.Name,
        Url = $"https://www.producthunt.com/posts/{post.Slug}",
        Source = SourceType.ProductHunt,
        ContentType = ContentType.Tool,
        Upvotes = post.VotesCount,
        CommentCount = post.CommentsCount,
        PostedAt = post.CreatedAt
    };

    // ── Deserialization types ──────────────────────────────────────────────

    private sealed class PhResponse
    {
        public PhData? Data { get; init; }
    }

    private sealed class PhData
    {
        public PhPostConnection? Posts { get; init; }
    }

    private sealed class PhPostConnection
    {
        public PhEdge[]? Edges { get; init; }
    }

    private sealed class PhEdge
    {
        public PhPost? Node { get; init; }
    }

    private sealed class PhPost
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Slug { get; init; } = string.Empty;
        public string Tagline { get; init; } = string.Empty;
        public string Website { get; init; } = string.Empty;

        [JsonPropertyName("votesCount")]
        public int VotesCount { get; init; }

        [JsonPropertyName("commentsCount")]
        public int CommentsCount { get; init; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; init; }
    }
}
