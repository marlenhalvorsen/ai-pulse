using System.Net;
using AiPulse.Application.Services;
using AiPulse.Domain.Enums;
using AiPulse.Infrastructure.Configuration;
using AiPulse.Infrastructure.Fetchers;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace AiPulse.Tests.Infrastructure;

public class HackerNewsFetcherTests
{
    private static HackerNewsFetcher CreateFetcher(
        Func<HttpRequestMessage, HttpResponseMessage> handler,
        string[]? keywords = null,
        int storyLimit = 10)
    {
        var settings = new HackerNewsSettings
        {
            BaseUrl = "https://hacker-news.firebaseio.com",
            StoryLimit = storyLimit,
            AiKeywords = keywords ?? ["llm", "gpt", "machine learning", "openai"]
        };

        var httpClient = new HttpClient(new FakeHttpMessageHandler(handler))
        {
            BaseAddress = new Uri(settings.BaseUrl)
        };

        return new HackerNewsFetcher(
            new StubHttpClientFactory(httpClient),
            Options.Create(settings),
            new UrlClassifier());
    }

    private static HttpResponseMessage OkJson(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage RouteHandler(HttpRequestMessage req)
    {
        var path = req.RequestUri!.PathAndQuery;

        if (path.Contains("topstories"))
            return OkJson("[1, 2]");

        if (path.Contains("beststories"))
            return OkJson("[3]");

        if (path.Contains("/item/1"))
            return OkJson("""{"id":1,"title":"GPT-5 is here","url":"https://openai.com/gpt5","score":800,"descendants":120,"time":1742000000,"type":"story"}""");

        if (path.Contains("/item/2"))
            return OkJson("""{"id":2,"title":"Unrelated cooking post","url":"https://example.com/pasta","score":50,"descendants":10,"time":1742000000,"type":"story"}""");

        if (path.Contains("/item/3"))
            return OkJson("""{"id":3,"title":"New OpenAI model benchmark results","url":"https://arxiv.org/abs/mock999","score":600,"descendants":80,"time":1742000000,"type":"story"}""");

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FetchAsync_FiltersOutNonAiItems()
    {
        var sut = CreateFetcher(RouteHandler);

        var items = (await sut.FetchAsync()).ToList();

        items.Should().NotContain(i => i.Title == "Unrelated cooking post");
    }

    [Fact]
    public async Task FetchAsync_IncludesItemsMatchingAiKeywords()
    {
        var sut = CreateFetcher(RouteHandler);

        var items = (await sut.FetchAsync()).ToList();

        items.Should().Contain(i => i.Title == "GPT-5 is here");
        items.Should().Contain(i => i.Title == "New OpenAI model benchmark results");
    }

    [Fact]
    public async Task FetchAsync_FetchesBothTopStoriesAndBestStories()
    {
        var requestedPaths = new List<string>();
        var sut = CreateFetcher(req =>
        {
            requestedPaths.Add(req.RequestUri!.PathAndQuery);
            return RouteHandler(req);
        });

        await sut.FetchAsync();

        requestedPaths.Should().Contain(p => p.Contains("topstories"));
        requestedPaths.Should().Contain(p => p.Contains("beststories"));
    }

    [Fact]
    public async Task FetchAsync_DeduplicatesStoriesAcrossBothLists()
    {
        // Item 1 appears in both topstories and beststories
        var sut = CreateFetcher(req =>
        {
            var path = req.RequestUri!.PathAndQuery;
            if (path.Contains("topstories")) return OkJson("[1]");
            if (path.Contains("beststories")) return OkJson("[1]");
            if (path.Contains("/item/1")) return OkJson("""{"id":1,"title":"GPT-5 is here","url":"https://openai.com","score":500,"descendants":40,"time":1742000000,"type":"story"}""");
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var items = (await sut.FetchAsync()).ToList();

        items.Where(i => i.Id == "hn_1").Should().HaveCount(1);
    }

    [Fact]
    public async Task FetchAsync_MapsItemFieldsToContentItem()
    {
        var sut = CreateFetcher(req =>
        {
            var path = req.RequestUri!.PathAndQuery;
            if (path.Contains("topstories")) return OkJson("[42]");
            if (path.Contains("beststories")) return OkJson("[]");
            if (path.Contains("/item/42")) return OkJson("""{"id":42,"title":"LLM reasoning paper","url":"https://arxiv.org/abs/42","score":950,"descendants":77,"time":1742000000,"type":"story"}""");
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var item = (await sut.FetchAsync()).Single();

        item.Id.Should().Be("hn_42");
        item.Title.Should().Be("LLM reasoning paper");
        item.Url.Should().Be("https://arxiv.org/abs/42");
        item.Upvotes.Should().Be(950);
        item.CommentCount.Should().Be(77);
        item.PostedAt.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1742000000).UtcDateTime);
    }

    [Fact]
    public async Task FetchAsync_SetsSourceTypeToHackerNews()
    {
        var sut = CreateFetcher(RouteHandler);

        var items = await sut.FetchAsync();

        items.Should().AllSatisfy(i => i.Source.Should().Be(SourceType.HackerNews));
    }

    [Fact]
    public async Task FetchAsync_SelfPostWithNoUrl_UsesHnItemUrl()
    {
        var sut = CreateFetcher(req =>
        {
            var path = req.RequestUri!.PathAndQuery;
            if (path.Contains("topstories")) return OkJson("[7]");
            if (path.Contains("beststories")) return OkJson("[]");
            // No "url" field — self-post
            if (path.Contains("/item/7")) return OkJson("""{"id":7,"title":"Ask HN: Best LLM for local inference?","score":300,"descendants":95,"time":1742000000,"type":"story"}""");
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var item = (await sut.FetchAsync()).Single();

        item.Url.Should().Be("https://news.ycombinator.com/item?id=7");
        item.ContentType.Should().Be(ContentType.Discussion);
    }

    [Fact]
    public async Task FetchAsync_UsesUrlClassifierToSetContentType()
    {
        var sut = CreateFetcher(req =>
        {
            var path = req.RequestUri!.PathAndQuery;
            if (path.Contains("topstories")) return OkJson("[8]");
            if (path.Contains("beststories")) return OkJson("[]");
            if (path.Contains("/item/8")) return OkJson("""{"id":8,"title":"GPT training walkthrough video","url":"https://www.youtube.com/watch?v=abc","score":400,"descendants":30,"time":1742000000,"type":"story"}""");
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var item = (await sut.FetchAsync()).Single();

        item.ContentType.Should().Be(ContentType.Video);
    }

    [Fact]
    public async Task FetchAsync_WhenStoryListHttpFails_ThrowsHttpRequestException()
    {
        var sut = CreateFetcher(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var act = async () => await sut.FetchAsync();

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task FetchAsync_RespectsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var sut = CreateFetcher(RouteHandler);

        var act = async () => await sut.FetchAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData("How to train your dragon")]
    [InlineData("Best hiking trails in Spain")]
    [InlineData("Explained: rain patterns in Brazil")]
    public async Task FetchAsync_AiKeyword_DoesNotMatchEmbeddedInOtherWords(string nonAiTitle)
    {
        var sut = CreateFetcher(req =>
        {
            var path = req.RequestUri!.PathAndQuery;
            if (path.Contains("topstories")) return OkJson("[99]");
            if (path.Contains("beststories")) return OkJson("[]");
            if (path.Contains("/item/99")) return OkJson($$"""{"id":99,"title":"{{nonAiTitle}}","url":"https://example.com","score":100,"descendants":5,"time":1742000000,"type":"story"}""");
            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        }, keywords: ["ai"]);

        var items = await sut.FetchAsync();

        items.Should().BeEmpty($"'{nonAiTitle}' should not match standalone keyword 'ai'");
    }

    [Fact]
    public async Task FetchAsync_MultiWordKeyword_MatchesTitleContainingPhrase()
    {
        var sut = CreateFetcher(req =>
        {
            var path = req.RequestUri!.PathAndQuery;
            if (path.Contains("topstories")) return OkJson("[100]");
            if (path.Contains("beststories")) return OkJson("[]");
            if (path.Contains("/item/100")) return OkJson("""{"id":100,"title":"Claude Code now supports MCP","url":"https://example.com","score":400,"descendants":30,"time":1742000000,"type":"story"}""");
            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        }, keywords: ["claude code"]);

        var items = await sut.FetchAsync();

        items.Should().ContainSingle(i => i.Title == "Claude Code now supports MCP");
    }

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
