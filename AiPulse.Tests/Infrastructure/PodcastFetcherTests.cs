using System.Net;
using AiPulse.Domain.Enums;
using AiPulse.Infrastructure.Configuration;
using AiPulse.Infrastructure.Fetchers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AiPulse.Tests.Infrastructure;

public class PodcastFetcherTests
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

    private static HttpResponseMessage OkXml(string xml) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(xml, System.Text.Encoding.UTF8, "application/rss+xml")
        };

    private static PodcastFetcher CreateFetcher(
        Func<HttpRequestMessage, HttpResponseMessage> handler,
        string[]? keywords = null,
        CuratedPodcastFeed[]? curatedFeeds = null,
        string topChartsUrl = "https://itunes.apple.com/us/rss/toppodcasts",
        string lookupBaseUrl = "https://itunes.apple.com/lookup")
    {
        var client = new HttpClient(new FakeHttpMessageHandler(handler));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ai-pulse/1.0");
        var settings = Options.Create(new PodcastSettings
        {
            TopChartsUrl = topChartsUrl,
            LookupBaseUrl = lookupBaseUrl,
            UserAgent = "ai-pulse/1.0",
            AiKeywords = keywords ?? ["ai", "machine learning", "llm"],
            CuratedFeeds = curatedFeeds ?? [],
            EpisodesPerShow = 1
        });
        return new PodcastFetcher(new StubHttpClientFactory(client), settings, NullLogger<PodcastFetcher>.Instance);
    }

    // ── Test data ─────────────────────────────────────────────────────────────

    private const string TopChartsWithAiAndNonAi = """
        {
          "feed": {
            "entry": [
              {
                "im:name": { "label": "AI Explained Podcast" },
                "id": { "attributes": { "im:id": "111" } }
              },
              {
                "im:name": { "label": "True Crime Weekly" },
                "id": { "attributes": { "im:id": "222" } }
              }
            ]
          }
        }
        """;

    private const string TopChartsEmpty = """{ "feed": { "entry": [] } }""";

    private static string LookupResponse(int collectionId, string name, string feedUrl) => $$"""
        {
          "resultCount": 1,
          "results": [
            { "collectionId": {{collectionId}}, "collectionName": "{{name}}", "feedUrl": "{{feedUrl}}" }
          ]
        }
        """;

    private static string RssWithEpisode(string title, string link, string description, string pubDate = "Mon, 01 Apr 2024 10:00:00 +0000") => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0">
          <channel>
            <title>Test Feed</title>
            <item>
              <title>{title}</title>
              <link>{link}</link>
              <pubDate>{pubDate}</pubDate>
              <guid>{link}</guid>
              <description>{description}</description>
            </item>
          </channel>
        </rss>
        """;

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FetchAsync_FetchesTopChartsUrl()
    {
        var requestedUrls = new List<string>();
        var sut = CreateFetcher(req =>
        {
            requestedUrls.Add(req.RequestUri!.ToString());
            if (req.RequestUri.ToString().Contains("toppodcasts")) return OkJson(TopChartsEmpty);
            return OkJson("""{ "resultCount": 0, "results": [] }""");
        });

        await sut.FetchAsync();

        requestedUrls.Should().Contain(u => u.Contains("toppodcasts"));
    }

    [Fact]
    public async Task FetchAsync_FiltersChartsByAiKeywords_ExcludesNonAi()
    {
        // "True Crime Weekly" (id 222) should be excluded; only "AI Explained" (id 111) should be looked up
        var lookupIds = new List<string>();
        var sut = CreateFetcher(req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("toppodcasts")) return OkJson(TopChartsWithAiAndNonAi);
            if (url.Contains("lookup"))
            {
                lookupIds.Add(url);
                return OkJson(LookupResponse(111, "AI Explained Podcast", "https://feeds.example.com/ai"));
            }
            return OkXml(RssWithEpisode("Ep 1", "https://aiexplained.com/1", "desc"));
        });

        await sut.FetchAsync();

        lookupIds.Should().AllSatisfy(u => u.Should().Contain("111"));
        lookupIds.Should().AllSatisfy(u => u.Should().NotContain("222"));
    }

    [Fact]
    public async Task FetchAsync_AlwaysIncludesCuratedFeeds()
    {
        var requestedUrls = new List<string>();
        var curated = new[] { new CuratedPodcastFeed { ShowName = "Lex Fridman", RssUrl = "https://curated.example.com/lex" } };
        var sut = CreateFetcher(req =>
        {
            requestedUrls.Add(req.RequestUri!.ToString());
            if (req.RequestUri.ToString().Contains("toppodcasts")) return OkJson(TopChartsEmpty);
            return OkXml(RssWithEpisode("Ep 1", "https://lex.com/1", "AI talk"));
        }, curatedFeeds: curated);

        await sut.FetchAsync();

        requestedUrls.Should().Contain("https://curated.example.com/lex");
    }

    [Fact]
    public async Task FetchAsync_LooksUpRssFeedForMatchedChartEntry()
    {
        var requestedUrls = new List<string>();
        var sut = CreateFetcher(req =>
        {
            var url = req.RequestUri!.ToString();
            requestedUrls.Add(url);
            if (url.Contains("toppodcasts")) return OkJson(TopChartsWithAiAndNonAi);
            if (url.Contains("lookup")) return OkJson(LookupResponse(111, "AI Explained", "https://feeds.example.com/ai"));
            return OkXml(RssWithEpisode("Ep", "https://aiexplained.com/1", "desc"));
        });

        await sut.FetchAsync();

        requestedUrls.Should().Contain(u => u.Contains("lookup") && u.Contains("111"));
    }

    [Fact]
    public async Task FetchAsync_FetchesRssFeedFromLookupResult()
    {
        var requestedUrls = new List<string>();
        var sut = CreateFetcher(req =>
        {
            var url = req.RequestUri!.ToString();
            requestedUrls.Add(url);
            if (url.Contains("toppodcasts")) return OkJson(TopChartsWithAiAndNonAi);
            if (url.Contains("lookup")) return OkJson(LookupResponse(111, "AI Explained", "https://feeds.example.com/ai-show"));
            return OkXml(RssWithEpisode("Ep 1", "https://aiexplained.com/1", "desc"));
        });

        await sut.FetchAsync();

        requestedUrls.Should().Contain("https://feeds.example.com/ai-show");
    }

    [Fact]
    public async Task FetchAsync_MapsEpisodeFieldsToContentItem()
    {
        var curated = new[] { new CuratedPodcastFeed { ShowName = "AI Daily Brief", RssUrl = "https://feeds.example.com/adb" } };
        var sut = CreateFetcher(req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("toppodcasts")) return OkJson(TopChartsEmpty);
            return OkXml(RssWithEpisode(
                "GPT-5 Drops: Everything You Need To Know",
                "https://aidailybrief.com/episodes/gpt5",
                "Today we cover the release of GPT-5 and what it means for the industry.",
                "Mon, 01 Apr 2024 10:00:00 +0000"));
        }, curatedFeeds: curated);

        var item = (await sut.FetchAsync()).Single();

        item.Title.Should().Be("GPT-5 Drops: Everything You Need To Know");
        item.Url.Should().Be("https://aidailybrief.com/episodes/gpt5");
        item.Description.Should().Be("Today we cover the release of GPT-5 and what it means for the industry.");
        item.ShowName.Should().Be("AI Daily Brief");
        item.PostedAt.Should().Be(new DateTime(2024, 4, 1, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task FetchAsync_SetsSourceTypeToPodcast()
    {
        var curated = new[] { new CuratedPodcastFeed { ShowName = "AI Show", RssUrl = "https://feeds.example.com/ai" } };
        var sut = CreateFetcher(req =>
        {
            if (req.RequestUri!.ToString().Contains("toppodcasts")) return OkJson(TopChartsEmpty);
            return OkXml(RssWithEpisode("Ep", "https://ai.com/1", "desc"));
        }, curatedFeeds: curated);

        var items = await sut.FetchAsync();

        items.Should().AllSatisfy(i => i.Source.Should().Be(SourceType.Podcast));
    }

    [Fact]
    public async Task FetchAsync_SetsContentTypeToPodcast()
    {
        var curated = new[] { new CuratedPodcastFeed { ShowName = "AI Show", RssUrl = "https://feeds.example.com/ai" } };
        var sut = CreateFetcher(req =>
        {
            if (req.RequestUri!.ToString().Contains("toppodcasts")) return OkJson(TopChartsEmpty);
            return OkXml(RssWithEpisode("Ep", "https://ai.com/1", "desc"));
        }, curatedFeeds: curated);

        var items = await sut.FetchAsync();

        items.Should().AllSatisfy(i => i.ContentType.Should().Be(ContentType.Podcast));
    }

    [Fact]
    public async Task FetchAsync_ShowNameFromCuratedFeed_IsUsed()
    {
        var curated = new[] { new CuratedPodcastFeed { ShowName = "TWIML AI Podcast", RssUrl = "https://feeds.example.com/twiml" } };
        var sut = CreateFetcher(req =>
        {
            if (req.RequestUri!.ToString().Contains("toppodcasts")) return OkJson(TopChartsEmpty);
            return OkXml(RssWithEpisode("Ep 42", "https://twimlai.com/42", "ML research deep dive"));
        }, curatedFeeds: curated);

        var item = (await sut.FetchAsync()).Single();

        item.ShowName.Should().Be("TWIML AI Podcast");
    }

    [Fact]
    public async Task FetchAsync_ShowNameFromCharts_IsUsedFromLookupResult()
    {
        var sut = CreateFetcher(req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("toppodcasts")) return OkJson(TopChartsWithAiAndNonAi);
            if (url.Contains("lookup")) return OkJson(LookupResponse(111, "AI Explained Podcast", "https://feeds.example.com/ai"));
            return OkXml(RssWithEpisode("Ep 1", "https://aiexplained.com/1", "AI recap"));
        });

        var item = (await sut.FetchAsync()).Single();

        item.ShowName.Should().Be("AI Explained Podcast");
    }

    [Fact]
    public async Task FetchAsync_DeduplicatesWhenSameShowInChartsAndCurated()
    {
        // "AI Explained Podcast" shows up in top charts AND curated — should appear once
        var curated = new[] { new CuratedPodcastFeed { ShowName = "AI Explained Podcast", RssUrl = "https://feeds.example.com/ai-curated" } };
        var sut = CreateFetcher(req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("toppodcasts")) return OkJson(TopChartsWithAiAndNonAi);
            if (url.Contains("lookup")) return OkJson(LookupResponse(111, "AI Explained Podcast", "https://feeds.example.com/ai-charts"));
            return OkXml(RssWithEpisode("Latest Ep", "https://aiexplained.com/latest", "AI news"));
        }, curatedFeeds: curated);

        var items = (await sut.FetchAsync()).ToList();

        items.Where(i => i.ShowName == "AI Explained Podcast").Should().HaveCount(1);
    }

    [Fact]
    public async Task FetchAsync_WhenHttpFails_ThrowsHttpRequestException()
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
        var sut = CreateFetcher(_ => OkJson(TopChartsEmpty));

        var act = async () => await sut.FetchAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task FetchAsync_WhenLookupReturnsNoFeedUrl_SkipsThatPodcast()
    {
        var sut = CreateFetcher(req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("toppodcasts")) return OkJson(TopChartsWithAiAndNonAi);
            // Lookup returns result with no feedUrl
            if (url.Contains("lookup")) return OkJson("""{ "resultCount": 0, "results": [] }""");
            return OkXml(RssWithEpisode("Ep", "https://example.com/1", "desc"));
        });

        var items = await sut.FetchAsync();

        items.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchAsync_IdStartsWithPodcastPrefix()
    {
        var curated = new[] { new CuratedPodcastFeed { ShowName = "AI Show", RssUrl = "https://feeds.example.com/ai" } };
        var sut = CreateFetcher(req =>
        {
            if (req.RequestUri!.ToString().Contains("toppodcasts")) return OkJson(TopChartsEmpty);
            return OkXml(RssWithEpisode("Ep", "https://ai.com/1", "desc"));
        }, curatedFeeds: curated);

        var item = (await sut.FetchAsync()).Single();

        item.Id.Should().StartWith("podcast_");
    }

    [Fact]
    public async Task FetchAsync_WhenCuratedFeedReturns404_ContinuesWithRemainingFeeds()
    {
        var curated = new[]
        {
            new CuratedPodcastFeed { ShowName = "Bad Feed", RssUrl = "https://feeds.example.com/broken" },
            new CuratedPodcastFeed { ShowName = "Good Feed", RssUrl = "https://feeds.example.com/good" }
        };
        var sut = CreateFetcher(req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("toppodcasts")) return OkJson(TopChartsEmpty);
            if (url.Contains("broken")) return new HttpResponseMessage(HttpStatusCode.NotFound);
            return OkXml(RssWithEpisode("Good Episode", "https://good.com/1", "Great AI content"));
        }, curatedFeeds: curated);

        var items = (await sut.FetchAsync()).ToList();

        items.Should().HaveCount(1);
        items.Single().ShowName.Should().Be("Good Feed");
    }

    [Fact]
    public async Task FetchAsync_WhenChartRssFeedReturns404_ContinuesWithCuratedFeeds()
    {
        var curated = new[] { new CuratedPodcastFeed { ShowName = "Curated AI Show", RssUrl = "https://feeds.example.com/curated" } };
        var sut = CreateFetcher(req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("toppodcasts")) return OkJson(TopChartsWithAiAndNonAi);
            if (url.Contains("lookup")) return OkJson(LookupResponse(111, "AI Explained Podcast", "https://feeds.example.com/broken-rss"));
            if (url.Contains("broken-rss")) return new HttpResponseMessage(HttpStatusCode.NotFound);
            return OkXml(RssWithEpisode("Curated Ep", "https://curated.com/1", "AI content"));
        }, curatedFeeds: curated);

        var items = (await sut.FetchAsync()).ToList();

        items.Should().Contain(i => i.ShowName == "Curated AI Show");
    }

    [Fact]
    public async Task FetchAsync_SendsUserAgentHeader()
    {
        HttpRequestMessage? captured = null;
        var sut = CreateFetcher(req =>
        {
            captured ??= req;
            return OkJson(TopChartsEmpty);
        });

        await sut.FetchAsync();

        captured!.Headers.UserAgent.ToString().Should().Be("ai-pulse/1.0");
    }
}
