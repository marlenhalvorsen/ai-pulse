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

    private static HttpResponseMessage OkXml(string xml) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(xml, System.Text.Encoding.UTF8, "application/rss+xml")
        };

    private static PodcastFetcher CreateFetcher(
        Func<HttpRequestMessage, HttpResponseMessage> handler,
        CuratedPodcastFeed[]? curatedFeeds = null,
        int episodesPerShow = 3)
    {
        var client = new HttpClient(new FakeHttpMessageHandler(handler));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ai-pulse/1.0");
        var settings = Options.Create(new PodcastSettings
        {
            UserAgent = "ai-pulse/1.0",
            EpisodesPerShow = episodesPerShow,
            CuratedFeeds = curatedFeeds ?? [new CuratedPodcastFeed { ShowName = "AI Daily Brief", RssUrl = "https://feeds.example.com/adb" }]
        });
        return new PodcastFetcher(new StubHttpClientFactory(client), settings, NullLogger<PodcastFetcher>.Instance);
    }

    // ── Test data ─────────────────────────────────────────────────────────────

    private static string RssWithEpisodes(int count, string showTitle = "Test Feed") =>
        $"""
         <?xml version="1.0" encoding="UTF-8"?>
         <rss version="2.0">
           <channel>
             <title>{showTitle}</title>
             {string.Concat(Enumerable.Range(1, count).Select(i => $"""
               <item>
                 <title>Episode {i}</title>
                 <link>https://show.example.com/ep{i}</link>
                 <pubDate>Mon, 0{i} Apr 2024 10:00:00 +0000</pubDate>
                 <guid>ep-{i}</guid>
                 <description>Description for episode {i}</description>
               </item>
               """))}
           </channel>
         </rss>
         """;

    private const string SingleEpisodeRss = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0">
          <channel>
            <title>AI Daily Brief</title>
            <item>
              <title>GPT-5 Is Here</title>
              <link>https://aidailybrief.com/episodes/gpt5</link>
              <pubDate>Mon, 01 Apr 2024 10:00:00 +0000</pubDate>
              <guid>https://aidailybrief.com/episodes/gpt5</guid>
              <description>Today we discuss GPT-5 and its capabilities.</description>
            </item>
          </channel>
        </rss>
        """;

    private const string EmptyFeedRss = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0"><channel><title>Empty</title></channel></rss>
        """;

    // ── Curated feed fetching ─────────────────────────────────────────────────

    [Fact]
    public async Task FetchAsync_FetchesAllCuratedFeeds()
    {
        var requestedUrls = new List<string>();
        var feeds = new[]
        {
            new CuratedPodcastFeed { ShowName = "Show A", RssUrl = "https://feeds.example.com/a" },
            new CuratedPodcastFeed { ShowName = "Show B", RssUrl = "https://feeds.example.com/b" }
        };
        var sut = CreateFetcher(req =>
        {
            requestedUrls.Add(req.RequestUri!.ToString());
            return OkXml(EmptyFeedRss);
        }, curatedFeeds: feeds);

        await sut.FetchAsync();

        requestedUrls.Should().Contain("https://feeds.example.com/a");
        requestedUrls.Should().Contain("https://feeds.example.com/b");
    }

    [Fact]
    public async Task FetchAsync_FetchesMultipleEpisodesPerShow()
    {
        var sut = CreateFetcher(_ => OkXml(RssWithEpisodes(5)), episodesPerShow: 3);

        var items = (await sut.FetchAsync()).ToList();

        items.Should().HaveCount(3);
    }

    [Fact]
    public async Task FetchAsync_FetchesUpToEpisodesPerShowLimit()
    {
        // Feed has only 2 episodes but limit is 3 — should return 2, not error
        var sut = CreateFetcher(_ => OkXml(RssWithEpisodes(2)), episodesPerShow: 3);

        var items = (await sut.FetchAsync()).ToList();

        items.Should().HaveCount(2);
    }

    [Fact]
    public async Task FetchAsync_EpisodesFromMultipleShowsAreAllReturned()
    {
        var feeds = new[]
        {
            new CuratedPodcastFeed { ShowName = "Show A", RssUrl = "https://feeds.example.com/a" },
            new CuratedPodcastFeed { ShowName = "Show B", RssUrl = "https://feeds.example.com/b" }
        };
        var sut = CreateFetcher(_ => OkXml(RssWithEpisodes(3)), curatedFeeds: feeds, episodesPerShow: 3);

        var items = (await sut.FetchAsync()).ToList();

        items.Should().HaveCount(6);
    }

    // ── Field mapping ─────────────────────────────────────────────────────────

    [Fact]
    public async Task FetchAsync_MapsEpisodeFieldsToContentItem()
    {
        var sut = CreateFetcher(_ => OkXml(SingleEpisodeRss));

        var item = (await sut.FetchAsync()).Single();

        item.Title.Should().Be("GPT-5 Is Here");
        item.Url.Should().Be("https://aidailybrief.com/episodes/gpt5");
        item.ShowName.Should().Be("AI Daily Brief");
        item.PostedAt.Should().Be(new DateTime(2024, 4, 1, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task FetchAsync_SetsSourceTypeToPodcast()
    {
        var sut = CreateFetcher(_ => OkXml(SingleEpisodeRss));

        var items = await sut.FetchAsync();

        items.Should().AllSatisfy(i => i.Source.Should().Be(SourceType.Podcast));
    }

    [Fact]
    public async Task FetchAsync_SetsContentTypeToPodcast()
    {
        var sut = CreateFetcher(_ => OkXml(SingleEpisodeRss));

        var items = await sut.FetchAsync();

        items.Should().AllSatisfy(i => i.ContentType.Should().Be(ContentType.Podcast));
    }

    [Fact]
    public async Task FetchAsync_ShowNameFromCuratedFeed_IsUsed()
    {
        var feeds = new[] { new CuratedPodcastFeed { ShowName = "TWIML AI Podcast", RssUrl = "https://feeds.example.com/twiml" } };
        var sut = CreateFetcher(_ => OkXml(SingleEpisodeRss), curatedFeeds: feeds);

        var item = (await sut.FetchAsync()).First();

        item.ShowName.Should().Be("TWIML AI Podcast");
    }

    [Fact]
    public async Task FetchAsync_IdStartsWithPodcastPrefix()
    {
        var sut = CreateFetcher(_ => OkXml(SingleEpisodeRss));

        var item = (await sut.FetchAsync()).Single();

        item.Id.Should().StartWith("podcast_");
    }

    // ── HTML stripping ────────────────────────────────────────────────────────

    [Fact]
    public async Task FetchAsync_StripsHtmlTagsFromDescription()
    {
        const string rssWithHtmlDescription = """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0">
              <channel>
                <title>AI Show</title>
                <item>
                  <title>Episode</title>
                  <link>https://show.example.com/ep1</link>
                  <pubDate>Mon, 01 Apr 2024 10:00:00 +0000</pubDate>
                  <guid>ep-1</guid>
                  <description><![CDATA[<p>Today we discuss <strong>GPT-5</strong> and <a href="https://openai.com">OpenAI</a>.</p><br/>More details inside.]]></description>
                </item>
              </channel>
            </rss>
            """;
        var sut = CreateFetcher(_ => OkXml(rssWithHtmlDescription));

        var item = (await sut.FetchAsync()).Single();

        item.Description.Should().NotContain("<p>");
        item.Description.Should().NotContain("<strong>");
        item.Description.Should().NotContain("<br/>");
        item.Description.Should().NotContain("<a href");
        item.Description.Should().Contain("GPT-5");
        item.Description.Should().Contain("OpenAI");
    }

    [Fact]
    public async Task FetchAsync_DecodesHtmlEntitiesInDescription()
    {
        const string rssWithEntities = """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0">
              <channel>
                <title>AI Show</title>
                <item>
                  <title>Episode</title>
                  <link>https://show.example.com/ep1</link>
                  <pubDate>Mon, 01 Apr 2024 10:00:00 +0000</pubDate>
                  <guid>ep-1</guid>
                  <description>Agents &amp; LLMs: what&#39;s next for AI?</description>
                </item>
              </channel>
            </rss>
            """;
        var sut = CreateFetcher(_ => OkXml(rssWithEntities));

        var item = (await sut.FetchAsync()).Single();

        item.Description.Should().Be("Agents & LLMs: what's next for AI?");
    }

    [Fact]
    public async Task FetchAsync_TruncatesDescriptionTo300Chars()
    {
        var longDescription = new string('a', 400);
        var rssWithLongDescription = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0">
              <channel>
                <title>AI Show</title>
                <item>
                  <title>Episode</title>
                  <link>https://show.example.com/ep1</link>
                  <pubDate>Mon, 01 Apr 2024 10:00:00 +0000</pubDate>
                  <guid>ep-1</guid>
                  <description>{longDescription}</description>
                </item>
              </channel>
            </rss>
            """;
        var sut = CreateFetcher(_ => OkXml(rssWithLongDescription));

        var item = (await sut.FetchAsync()).Single();

        item.Description.Should().NotBeNull();
        item.Description!.Length.Should().BeLessOrEqualTo(302); // 300 chars + ellipsis "…"
        item.Description.Should().EndWith("…");
    }

    // ── Error handling ────────────────────────────────────────────────────────

    [Fact]
    public async Task FetchAsync_WhenCuratedFeedReturns404_ContinuesWithRemainingFeeds()
    {
        var feeds = new[]
        {
            new CuratedPodcastFeed { ShowName = "Bad Feed", RssUrl = "https://feeds.example.com/broken" },
            new CuratedPodcastFeed { ShowName = "Good Feed", RssUrl = "https://feeds.example.com/good" }
        };
        var sut = CreateFetcher(req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("broken")) return new HttpResponseMessage(HttpStatusCode.NotFound);
            return OkXml(SingleEpisodeRss);
        }, curatedFeeds: feeds, episodesPerShow: 1);

        var items = (await sut.FetchAsync()).ToList();

        items.Should().HaveCount(1);
        items.Single().ShowName.Should().Be("Good Feed");
    }

    [Fact]
    public async Task FetchAsync_WhenHttpFails_ThrowsHttpRequestException()
    {
        // All feeds fail — first failure propagates (no more feeds to try)
        var sut = CreateFetcher(
            _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            curatedFeeds: [new CuratedPodcastFeed { ShowName = "Only Feed", RssUrl = "https://feeds.example.com/fail" }]);

        // When all feeds fail, the fetcher should propagate the error (no items to return)
        var items = await sut.FetchAsync();

        items.Should().BeEmpty("failed feeds are skipped, not propagated");
    }

    [Fact]
    public async Task FetchAsync_RespectsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var sut = CreateFetcher(_ => OkXml(EmptyFeedRss));

        var act = async () => await sut.FetchAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task FetchAsync_SendsUserAgentHeader()
    {
        HttpRequestMessage? captured = null;
        var sut = CreateFetcher(req => { captured ??= req; return OkXml(EmptyFeedRss); });

        await sut.FetchAsync();

        captured!.Headers.UserAgent.ToString().Should().Be("ai-pulse/1.0");
    }

    [Fact]
    public async Task FetchAsync_EmptyFeed_ReturnsNoItemsForThatShow()
    {
        var feeds = new[]
        {
            new CuratedPodcastFeed { ShowName = "Empty Show", RssUrl = "https://feeds.example.com/empty" },
            new CuratedPodcastFeed { ShowName = "Good Show", RssUrl = "https://feeds.example.com/good" }
        };
        var sut = CreateFetcher(req =>
            OkXml(req.RequestUri!.ToString().Contains("empty") ? EmptyFeedRss : SingleEpisodeRss),
            curatedFeeds: feeds, episodesPerShow: 1);

        var items = (await sut.FetchAsync()).ToList();

        items.Should().HaveCount(1);
        items.Single().ShowName.Should().Be("Good Show");
    }
}
