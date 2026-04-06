using System.Net;
using AiPulse.Domain.Enums;
using AiPulse.Infrastructure.Configuration;
using AiPulse.Infrastructure.Fetchers;
using FluentAssertions;
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
        PodcastFeed[]? feeds = null,
        int episodesPerFeed = 10,
        string userAgent = "ai-pulse/1.0")
    {
        var client = new HttpClient(new FakeHttpMessageHandler(handler));
        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        var settings = Options.Create(new PodcastSettings
        {
            EpisodesPerFeed = episodesPerFeed,
            UserAgent = userAgent,
            Feeds = feeds ?? [new PodcastFeed { Name = "AI Daily Brief", Url = "https://feeds.example.com/aidailybrief" }]
        });
        return new PodcastFetcher(new StubHttpClientFactory(client), settings);
    }

    private const string SingleEpisodeRss = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0">
          <channel>
            <title>AI Daily Brief</title>
            <item>
              <title>GPT-5 Is Here: What You Need to Know</title>
              <link>https://aidailybrief.com/episodes/gpt5</link>
              <pubDate>Mon, 01 Apr 2024 10:00:00 +0000</pubDate>
              <guid>https://aidailybrief.com/episodes/gpt5</guid>
              <description>Today we discuss GPT-5 and its capabilities.</description>
            </item>
          </channel>
        </rss>
        """;

    private const string TwoEpisodeRss = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0">
          <channel>
            <title>AI Daily Brief</title>
            <item>
              <title>Episode 1</title>
              <link>https://aidailybrief.com/episodes/1</link>
              <pubDate>Mon, 01 Apr 2024 10:00:00 +0000</pubDate>
              <guid>aidailybrief-ep-1</guid>
              <description>First episode.</description>
            </item>
            <item>
              <title>Episode 2</title>
              <link>https://aidailybrief.com/episodes/2</link>
              <pubDate>Tue, 02 Apr 2024 10:00:00 +0000</pubDate>
              <guid>aidailybrief-ep-2</guid>
              <description>Second episode.</description>
            </item>
          </channel>
        </rss>
        """;

    private const string EmptyFeedRss = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0">
          <channel>
            <title>Empty Feed</title>
          </channel>
        </rss>
        """;

    [Fact]
    public async Task FetchAsync_ParsesEpisodeTitleAndUrl()
    {
        var sut = CreateFetcher(_ => OkXml(SingleEpisodeRss));

        var item = (await sut.FetchAsync()).Single();

        item.Title.Should().Be("GPT-5 Is Here: What You Need to Know");
        item.Url.Should().Be("https://aidailybrief.com/episodes/gpt5");
    }

    [Fact]
    public async Task FetchAsync_ParsesPublishedDate()
    {
        var sut = CreateFetcher(_ => OkXml(SingleEpisodeRss));

        var item = (await sut.FetchAsync()).Single();

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
    public async Task FetchAsync_EpisodeLinkUsedAsUrl()
    {
        var sut = CreateFetcher(_ => OkXml(SingleEpisodeRss));

        var item = (await sut.FetchAsync()).Single();

        item.Url.Should().Be("https://aidailybrief.com/episodes/gpt5",
            "episode URL must be the web page link, not the audio enclosure");
    }

    [Fact]
    public async Task FetchAsync_WhenLinkMissing_UsesEnclosureUrl()
    {
        const string rssWithEnclosureOnly = """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0">
              <channel>
                <title>AI Podcast</title>
                <item>
                  <title>Episode With No Link</title>
                  <enclosure url="https://cdn.example.com/episode.mp3" type="audio/mpeg" length="12345"/>
                  <pubDate>Mon, 01 Apr 2024 10:00:00 +0000</pubDate>
                  <guid>ep-no-link-1</guid>
                </item>
              </channel>
            </rss>
            """;
        var sut = CreateFetcher(_ => OkXml(rssWithEnclosureOnly));

        var item = (await sut.FetchAsync()).Single();

        item.Url.Should().Be("https://cdn.example.com/episode.mp3");
    }

    [Fact]
    public async Task FetchAsync_IdIsStableAndUnique()
    {
        var sut = CreateFetcher(_ => OkXml(TwoEpisodeRss));

        var items = (await sut.FetchAsync()).ToList();

        items.Should().HaveCount(2);
        items.Select(i => i.Id).Should().OnlyHaveUniqueItems();
        items.Should().AllSatisfy(i => i.Id.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public async Task FetchAsync_IdStartsWithPodcastPrefix()
    {
        var sut = CreateFetcher(_ => OkXml(SingleEpisodeRss));

        var item = (await sut.FetchAsync()).Single();

        item.Id.Should().StartWith("podcast_");
    }

    [Fact]
    public async Task FetchAsync_LimitsEpisodesPerFeed()
    {
        var manyEpisodesRss = BuildRssWithEpisodes(5);
        var sut = CreateFetcher(_ => OkXml(manyEpisodesRss), episodesPerFeed: 3);

        var items = (await sut.FetchAsync()).ToList();

        items.Should().HaveCount(3);
    }

    [Fact]
    public async Task FetchAsync_FetchesAllConfiguredFeeds()
    {
        var requestedUrls = new List<string>();
        var feeds = new[]
        {
            new PodcastFeed { Name = "Feed A", Url = "https://feeds.example.com/a" },
            new PodcastFeed { Name = "Feed B", Url = "https://feeds.example.com/b" }
        };
        var sut = CreateFetcher(req =>
        {
            requestedUrls.Add(req.RequestUri!.ToString());
            return OkXml(EmptyFeedRss);
        }, feeds: feeds);

        await sut.FetchAsync();

        requestedUrls.Should().HaveCount(2);
        requestedUrls.Should().Contain("https://feeds.example.com/a");
        requestedUrls.Should().Contain("https://feeds.example.com/b");
    }

    [Fact]
    public async Task FetchAsync_DeduplicatesEpisodesAcrossFeeds()
    {
        var feeds = new[]
        {
            new PodcastFeed { Name = "Feed A", Url = "https://feeds.example.com/a" },
            new PodcastFeed { Name = "Feed B", Url = "https://feeds.example.com/b" }
        };
        var sut = CreateFetcher(_ => OkXml(SingleEpisodeRss), feeds: feeds);

        var items = (await sut.FetchAsync()).ToList();

        items.Should().HaveCount(1, "same episode guid should not appear twice");
    }

    [Fact]
    public async Task FetchAsync_SendsUserAgentHeader()
    {
        HttpRequestMessage? captured = null;
        var sut = CreateFetcher(req => { captured = req; return OkXml(EmptyFeedRss); },
            userAgent: "ai-pulse/1.0");

        await sut.FetchAsync();

        captured!.Headers.UserAgent.ToString().Should().Be("ai-pulse/1.0");
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
        var sut = CreateFetcher(_ => OkXml(EmptyFeedRss));

        var act = async () => await sut.FetchAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task FetchAsync_EmptyFeed_ReturnsEmpty()
    {
        var sut = CreateFetcher(_ => OkXml(EmptyFeedRss));

        var items = await sut.FetchAsync();

        items.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchAsync_NoFeeds_ReturnsEmpty()
    {
        var sut = CreateFetcher(_ => OkXml(EmptyFeedRss), feeds: []);

        var items = await sut.FetchAsync();

        items.Should().BeEmpty();
    }

    private static string BuildRssWithEpisodes(int count)
    {
        var items = string.Concat(Enumerable.Range(1, count).Select(i => $"""
            <item>
              <title>Episode {i}</title>
              <link>https://aidailybrief.com/episodes/{i}</link>
              <pubDate>Mon, 0{i} Apr 2024 10:00:00 +0000</pubDate>
              <guid>ep-{i}</guid>
            </item>
            """));
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0"><channel><title>Test Feed</title>{items}</channel></rss>
            """;
    }
}
