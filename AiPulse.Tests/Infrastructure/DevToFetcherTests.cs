using System.Net;
using AiPulse.Domain.Enums;
using AiPulse.Infrastructure.Configuration;
using AiPulse.Infrastructure.Fetchers;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace AiPulse.Tests.Infrastructure;

public class DevToFetcherTests
{
    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(handler(request));
        }
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private static HttpResponseMessage OkJson(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };

    private static DevToFetcher CreateFetcher(
        Func<HttpRequestMessage, HttpResponseMessage> handler,
        string[]? tags = null,
        int articlesPerTag = 10)
    {
        var client = new HttpClient(new FakeHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("https://dev.to")
        };
        var settings = Options.Create(new DevToSettings
        {
            BaseUrl = "https://dev.to",
            Tags = tags ?? ["ai"],
            ArticlesPerTag = articlesPerTag
        });
        return new DevToFetcher(new StubHttpClientFactory(client), settings);
    }

    [Fact]
    public async Task FetchAsync_ParsesArticleFields()
    {
        var fetcher = CreateFetcher(_ => OkJson("""
            [{
                "id": 12345,
                "title": "AI is great",
                "url": "https://dev.to/user/ai-is-great-abc",
                "positive_reactions_count": 100,
                "comments_count": 20,
                "published_at": "2024-01-15T10:00:00Z"
            }]
        """));

        var items = (await fetcher.FetchAsync()).ToList();

        items.Should().HaveCount(1);
        var item = items[0];
        item.Id.Should().Be("devto_12345");
        item.Title.Should().Be("AI is great");
        item.Url.Should().Be("https://dev.to/user/ai-is-great-abc");
        item.Upvotes.Should().Be(100);
        item.CommentCount.Should().Be(20);
        item.PostedAt.Should().Be(new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task FetchAsync_SetsSourceTypeToDevTo()
    {
        var fetcher = CreateFetcher(_ => OkJson("""
            [{"id": 1, "title": "T", "url": "https://dev.to/u/t",
              "positive_reactions_count": 0, "comments_count": 0, "published_at": "2024-01-01T00:00:00Z"}]
        """));

        var items = await fetcher.FetchAsync();

        items.Should().AllSatisfy(i => i.Source.Should().Be(SourceType.DevTo));
    }

    [Fact]
    public async Task FetchAsync_SetsContentTypeToArticle()
    {
        var fetcher = CreateFetcher(_ => OkJson("""
            [{"id": 1, "title": "T", "url": "https://dev.to/u/t",
              "positive_reactions_count": 0, "comments_count": 0, "published_at": "2024-01-01T00:00:00Z"}]
        """));

        var items = await fetcher.FetchAsync();

        items.Should().AllSatisfy(i => i.ContentType.Should().Be(ContentType.Article));
    }

    [Fact]
    public async Task FetchAsync_FetchesAllConfiguredTags()
    {
        var requestedUrls = new List<string>();
        var fetcher = CreateFetcher(req =>
        {
            requestedUrls.Add(req.RequestUri!.ToString());
            return OkJson("[]");
        }, tags: ["ai", "machinelearning", "llm"]);

        await fetcher.FetchAsync();

        requestedUrls.Should().HaveCount(3);
        requestedUrls.Should().Contain(u => u.Contains("tag=ai"));
        requestedUrls.Should().Contain(u => u.Contains("tag=machinelearning"));
        requestedUrls.Should().Contain(u => u.Contains("tag=llm"));
    }

    [Fact]
    public async Task FetchAsync_DeduplicatesArticlesAcrossTags()
    {
        const string articleJson = """
            [{"id": 42, "title": "Dup", "url": "https://dev.to/u/dup",
              "positive_reactions_count": 5, "comments_count": 1, "published_at": "2024-01-01T00:00:00Z"}]
        """;
        var fetcher = CreateFetcher(_ => OkJson(articleJson), tags: ["ai", "machinelearning"]);

        var items = (await fetcher.FetchAsync()).ToList();

        items.Should().HaveCount(1);
    }

    [Fact]
    public async Task FetchAsync_EmptyResponse_ReturnsEmpty()
    {
        var fetcher = CreateFetcher(_ => OkJson("[]"));

        var items = await fetcher.FetchAsync();

        items.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchAsync_WhenHttpReturnsError_ThrowsHttpRequestException()
    {
        var fetcher = CreateFetcher(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await fetcher.Invoking(f => f.FetchAsync())
            .Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task FetchAsync_RespectsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var fetcher = CreateFetcher(_ => OkJson("[]"));

        await fetcher.Invoking(f => f.FetchAsync(cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }
}
