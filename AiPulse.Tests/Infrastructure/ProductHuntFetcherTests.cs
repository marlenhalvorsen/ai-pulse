using System.Net;
using System.Net.Http.Headers;
using AiPulse.Domain.Enums;
using AiPulse.Infrastructure.Configuration;
using AiPulse.Infrastructure.Fetchers;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace AiPulse.Tests.Infrastructure;

public class ProductHuntFetcherTests
{
    private const string SinglePostResponse = """
        {
          "data": {
            "posts": {
              "edges": [
                {
                  "node": {
                    "id": "123456",
                    "name": "AI Tool X",
                    "tagline": "The best AI tool ever",
                    "website": "https://aitoolx.com",
                    "votesCount": 500,
                    "commentsCount": 42,
                    "createdAt": "2025-03-25T00:00:00.000Z"
                  }
                }
              ]
            }
          }
        }
        """;

    private const string EmptyResponse = """
        { "data": { "posts": { "edges": [] } } }
        """;

    private static ProductHuntFetcher CreateFetcher(
        Func<HttpRequestMessage, HttpResponseMessage> handler,
        string token = "test-token",
        int postLimit = 20)
    {
        var settings = new ProductHuntSettings
        {
            BaseUrl = "https://api.producthunt.com",
            DeveloperToken = token,
            PostLimit = postLimit
        };

        var httpClient = new HttpClient(new FakeHttpMessageHandler(handler))
        {
            BaseAddress = new Uri(settings.BaseUrl)
        };

        return new ProductHuntFetcher(new StubHttpClientFactory(httpClient), Options.Create(settings));
    }

    [Fact]
    public async Task FetchAsync_ParsesPostFields()
    {
        var sut = CreateFetcher(_ => OkJson(SinglePostResponse));

        var item = (await sut.FetchAsync()).Single();

        item.Id.Should().Be("ph_123456");
        item.Title.Should().Be("AI Tool X");
        item.Url.Should().Be("https://aitoolx.com");
        item.Upvotes.Should().Be(500);
        item.CommentCount.Should().Be(42);
        item.PostedAt.Should().Be(DateTime.Parse("2025-03-25T00:00:00.000Z").ToUniversalTime());
    }

    [Fact]
    public async Task FetchAsync_SetsSourceTypeToProductHunt()
    {
        var sut = CreateFetcher(_ => OkJson(SinglePostResponse));
        var items = await sut.FetchAsync();
        items.Should().AllSatisfy(i => i.Source.Should().Be(SourceType.ProductHunt));
    }

    [Fact]
    public async Task FetchAsync_SetsContentTypeToTool()
    {
        var sut = CreateFetcher(_ => OkJson(SinglePostResponse));
        var items = await sut.FetchAsync();
        items.Should().AllSatisfy(i => i.ContentType.Should().Be(ContentType.Tool));
    }

    [Fact]
    public async Task FetchAsync_SendsBearerTokenInAuthorizationHeader()
    {
        string? authHeader = null;
        var sut = CreateFetcher(req =>
        {
            authHeader = req.Headers.Authorization?.ToString();
            return OkJson(SinglePostResponse);
        }, token: "my-secret-token");

        await sut.FetchAsync();

        authHeader.Should().Be("Bearer my-secret-token",
            "the Developer Token must be sent as a Bearer token on every request");
    }

    [Fact]
    public async Task FetchAsync_EmptyEdges_ReturnsEmpty()
    {
        var sut = CreateFetcher(_ => OkJson(EmptyResponse));
        var items = await sut.FetchAsync();
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchAsync_WhenHttpReturnsError_ThrowsHttpRequestException()
    {
        var sut = CreateFetcher(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var act = async () => await sut.FetchAsync();
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task FetchAsync_RespectsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var sut = CreateFetcher(_ => OkJson(SinglePostResponse));
        var act = async () => await sut.FetchAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static HttpResponseMessage OkJson(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };

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
}
