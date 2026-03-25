using System.Net;
using AiPulse.Application.Services;
using AiPulse.Domain.Enums;
using AiPulse.Infrastructure.Configuration;
using AiPulse.Infrastructure.Fetchers;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace AiPulse.Tests.Infrastructure;

public class RedditFetcherTests
{
    // Link post: external URL, is_self=false
    private const string SinglePostJson = """
        {
          "data": {
            "children": [
              {
                "kind": "t3",
                "data": {
                  "id": "abc123",
                  "title": "GPT-5 drops today",
                  "url": "https://www.youtube.com/watch?v=testid",
                  "permalink": "/r/MachineLearning/comments/abc123/gpt_5_drops_today/",
                  "is_self": false,
                  "score": 1200,
                  "num_comments": 88,
                  "created_utc": 1742000000.0,
                  "subreddit": "MachineLearning"
                }
              }
            ]
          }
        }
        """;

    // Self post: no external URL, is_self=true
    private const string SelfPostJson = """
        {
          "data": {
            "children": [
              {
                "kind": "t3",
                "data": {
                  "id": "xyz789",
                  "title": "Weekly discussion thread",
                  "url": "https://www.reddit.com/r/MachineLearning/comments/xyz789/weekly_thread",
                  "permalink": "/r/MachineLearning/comments/xyz789/weekly_thread/",
                  "is_self": true,
                  "score": 300,
                  "num_comments": 45,
                  "created_utc": 1742000000.0,
                  "subreddit": "MachineLearning"
                }
              }
            ]
          }
        }
        """;

    private static RedditFetcher CreateFetcher(
        Func<HttpRequestMessage, HttpResponseMessage> handler,
        string[]? subreddits = null)
    {
        var settings = new RedditSettings
        {
            Subreddits = subreddits ?? ["MachineLearning"],
            BaseUrl = "https://www.reddit.com",
            PostsPerSubreddit = 25,
            UserAgent = "test:ai-pulse:v0"
        };

        var httpClient = new HttpClient(new FakeHttpMessageHandler(handler))
        {
            BaseAddress = new Uri(settings.BaseUrl)
        };

        var factory = new StubHttpClientFactory(httpClient);
        return new RedditFetcher(factory, Options.Create(settings), new UrlClassifier());
    }

    [Fact]
    public async Task FetchAsync_ParsesPostFields()
    {
        var sut = CreateFetcher(_ => OkJson(SinglePostJson));

        var items = (await sut.FetchAsync()).ToList();

        items.Should().HaveCount(1);
        var item = items[0];
        item.Id.Should().Be("reddit_abc123");
        item.Title.Should().Be("GPT-5 drops today");
        item.Url.Should().Be("https://www.youtube.com/watch?v=testid",
            "link posts must use the external URL, not the Reddit permalink");
        item.Upvotes.Should().Be(1200);
        item.CommentCount.Should().Be(88);
        item.PostedAt.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1742000000).UtcDateTime);
    }

    [Fact]
    public async Task FetchAsync_SetsSourceTypeToReddit()
    {
        var sut = CreateFetcher(_ => OkJson(SinglePostJson));

        var items = await sut.FetchAsync();

        items.Should().AllSatisfy(i => i.Source.Should().Be(SourceType.Reddit));
    }

    [Fact]
    public async Task FetchAsync_SelfPost_UsesPermalinkAndClassifiesAsDiscussion()
    {
        var sut = CreateFetcher(_ => OkJson(SelfPostJson));

        var item = (await sut.FetchAsync()).Single();

        item.Url.Should().Be("https://www.reddit.com/r/MachineLearning/comments/xyz789/weekly_thread/",
            "self-posts must link to the Reddit thread");
        item.ContentType.Should().Be(ContentType.Discussion);
    }

    [Fact]
    public async Task FetchAsync_LinkPost_UsesExternalUrlAndClassifiesByUrl()
    {
        // is_self=false with an arxiv URL — should use the external URL and classify via UrlClassifier
        var json = """
            {"data":{"children":[{"kind":"t3","data":{
              "id":"p1","title":"Some paper","score":500,"num_comments":20,
              "created_utc":1742000000.0,
              "url":"https://arxiv.org/abs/2401.00001",
              "permalink":"/r/MachineLearning/comments/p1/some_paper/",
              "is_self":false
            }}]}}
            """;
        var sut = CreateFetcher(_ => OkJson(json));

        var item = (await sut.FetchAsync()).Single();

        item.Url.Should().Be("https://arxiv.org/abs/2401.00001",
            "link posts must use the external URL so classification is accurate");
        item.ContentType.Should().Be(ContentType.ResearchPaper);
    }

    [Fact]
    public async Task FetchAsync_SelfPostUrl_ClassifiedAsDiscussion()
    {
        var sut = CreateFetcher(_ => OkJson(SelfPostJson));

        var items = await sut.FetchAsync();

        items.Single().ContentType.Should().Be(ContentType.Discussion);
    }

    [Fact]
    public async Task FetchAsync_FetchesAllConfiguredSubreddits()
    {
        var requestedUrls = new List<string>();
        var sut = CreateFetcher(req =>
        {
            requestedUrls.Add(req.RequestUri!.PathAndQuery);
            return OkJson(SinglePostJson);
        }, subreddits: ["MachineLearning", "artificial", "ChatGPT"]);

        var items = (await sut.FetchAsync()).ToList();

        requestedUrls.Should().HaveCount(3);
        requestedUrls.Should().Contain(s => s.Contains("MachineLearning"));
        requestedUrls.Should().Contain(s => s.Contains("artificial"));
        requestedUrls.Should().Contain(s => s.Contains("ChatGPT"));
        items.Should().HaveCount(3);
    }

    [Fact]
    public async Task FetchAsync_WhenHttpReturnsError_ThrowsHttpRequestException()
    {
        var sut = CreateFetcher(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));

        var act = async () => await sut.FetchAsync();

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task FetchAsync_RespectsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var sut = CreateFetcher(_ => OkJson(SinglePostJson));

        var act = async () => await sut.FetchAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void UserAgent_DefaultValue_IsFullyPreservedAfterParseAdd()
    {
        // ParseAdd silently truncates at the first invalid character.
        // "web:ai-pulse:v1.0 ..." — the colon after "web" is not a valid token char
        // so only "web" would be kept, meaning Reddit never sees the full identifier.
        var settings = new RedditSettings();
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(settings.UserAgent);

        client.DefaultRequestHeaders.UserAgent.ToString()
            .Should().Contain("ai-pulse",
                "the full User-Agent must reach Reddit so our app is identifiable; " +
                "colons in the product token cause silent truncation");
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
