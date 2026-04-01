using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AiPulse.Application.Interfaces;
using AiPulse.Domain.Enums;
using AiPulse.Domain.Models;
using AiPulse.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AiPulse.Infrastructure.Fetchers;

public class DevToFetcher : ITrendFetcher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DevToSettings _settings;

    public DevToFetcher(IHttpClientFactory httpClientFactory, IOptions<DevToSettings> settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
    }

    public async Task<IEnumerable<ContentItem>> FetchAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = _httpClientFactory.CreateClient("DevTo");
        var seen = new HashSet<string>();
        var items = new List<ContentItem>();

        foreach (var tag in _settings.Tags)
        {
            var url = $"/api/articles?tag={tag}&per_page={_settings.ArticlesPerTag}";
            var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var articles = await response.Content.ReadFromJsonAsync<DevToArticle[]>(cancellationToken)
                           ?? [];

            foreach (var article in articles)
            {
                var id = $"devto_{article.Id}";
                if (!seen.Add(id))
                    continue;

                items.Add(new ContentItem
                {
                    Id = id,
                    Title = article.Title,
                    Url = article.Url,
                    Source = SourceType.DevTo,
                    ContentType = ContentType.Article,
                    Upvotes = article.PositiveReactionsCount,
                    CommentCount = article.CommentsCount,
                    PostedAt = article.PublishedAt
                });
            }
        }

        return items;
    }

    private sealed class DevToArticle
    {
        public int Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Url { get; init; } = string.Empty;
        [JsonPropertyName("positive_reactions_count")] public int PositiveReactionsCount { get; init; }
        [JsonPropertyName("comments_count")] public int CommentsCount { get; init; }
        [JsonPropertyName("published_at")] public DateTime PublishedAt { get; init; }
    }
}
