using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiPulse.Application.Interfaces;
using AiPulse.Domain.Enums;
using AiPulse.Domain.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AiPulse.Tests.Api;

public class TrendingEndpointTests : IClassFixture<TrendingEndpointTests.ApiFactory>
{
    // Provides a test token so ValidateOnStart does not throw in the test host.
    public class ApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ProductHunt:DeveloperToken"] = "test-token"
                }));
        }
    }

    private readonly ApiFactory _factory;

    public TrendingEndpointTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient(IEnumerable<ContentItem>? items = null)
    {
        var trendingItems = items ?? [MakeItem("item-1", ContentType.Discussion, SourceType.Reddit)];
        var mock = new Mock<ITrendingQuery>();
        mock.Setup(q => q.GetTrendingAsync(
                It.IsAny<ContentType?>(),
                It.IsAny<SourceType?>(),
                It.IsAny<int>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(trendingItems);

        return _factory.WithWebHostBuilder(b =>
            b.ConfigureServices(services =>
            {
                services.AddSingleton(mock.Object);
            }))
            .CreateClient();
    }

    [Fact]
    public async Task GetTrending_Returns200()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/trending");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTrending_ResponseContainsGeneratedAtAndRows()
    {
        var client = CreateClient();
        var json = await client.GetFromJsonAsync<JsonElement>("/api/trending");

        json.TryGetProperty("generatedAt", out _).Should().BeTrue();
        json.TryGetProperty("rows", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetTrending_DiscussionItemsGroupedBySource()
    {
        var mock = new Mock<ITrendingQuery>();
        mock.Setup(q => q.GetTrendingAsync(ContentType.Discussion, SourceType.Reddit, It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeItem("r1", ContentType.Discussion, SourceType.Reddit), MakeItem("r2", ContentType.Discussion, SourceType.Reddit)]);
        mock.Setup(q => q.GetTrendingAsync(ContentType.Discussion, SourceType.HackerNews, It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeItem("h1", ContentType.Discussion, SourceType.HackerNews)]);
        mock.Setup(q => q.GetTrendingAsync(It.Is<ContentType>(ct => ct != ContentType.Discussion), It.IsAny<SourceType?>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var client = _factory.WithWebHostBuilder(b =>
            b.ConfigureServices(s => s.AddSingleton(mock.Object)))
            .CreateClient();

        var json = await client.GetFromJsonAsync<JsonElement>("/api/trending");
        var rows = json.GetProperty("rows").EnumerateArray().ToList();

        rows.Should().HaveCount(2);
        var redditRow = rows.Single(r => r.GetProperty("contentType").GetString() == "Reddit");
        redditRow.GetProperty("items").GetArrayLength().Should().Be(2);
        var hnRow = rows.Single(r => r.GetProperty("contentType").GetString() == "HackerNews");
        hnRow.GetProperty("items").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task GetTrending_LimitExceeding50_IsCappedAt50()
    {
        var mock = new Mock<ITrendingQuery>();
        mock.Setup(q => q.GetTrendingAsync(
                It.IsAny<ContentType?>(), It.IsAny<SourceType?>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var client = _factory.WithWebHostBuilder(b =>
            b.ConfigureServices(s => s.AddSingleton(mock.Object)))
            .CreateClient();

        await client.GetAsync("/api/trending?limit=999");

        // 5 non-Discussion ContentTypes + 2 Discussion source calls (Reddit + HN) = ContentType.Length + 1
        mock.Verify(q => q.GetTrendingAsync(
            It.IsAny<ContentType>(), It.IsAny<SourceType?>(), 50, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Exactly(Enum.GetValues<ContentType>().Length + 1));
    }

    [Fact]
    public async Task GetTrending_WindowDay_Passes24HourTimeSpan()
    {
        var mock = new Mock<ITrendingQuery>();
        mock.Setup(q => q.GetTrendingAsync(
                It.IsAny<ContentType?>(), It.IsAny<SourceType?>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var client = _factory.WithWebHostBuilder(b =>
            b.ConfigureServices(s => s.AddSingleton(mock.Object)))
            .CreateClient();

        await client.GetAsync("/api/trending?window=day");

        mock.Verify(q => q.GetTrendingAsync(
            It.IsAny<ContentType>(), It.IsAny<SourceType?>(), It.IsAny<int>(), TimeSpan.FromHours(24), It.IsAny<CancellationToken>()),
            Times.Exactly(Enum.GetValues<ContentType>().Length + 1));
    }

    [Fact]
    public async Task SecurityHeaders_AreSetOnEveryResponse()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/trending");

        response.Headers.Should().ContainKey("X-Frame-Options");
        response.Headers.Should().ContainKey("X-Content-Type-Options");
        response.Headers.Should().ContainKey("Referrer-Policy");
        response.Headers.Should().ContainKey("Permissions-Policy");
    }

    [Fact]
    public async Task SecurityHeaders_XFrameOptions_IsDeny()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/trending");
        response.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
    }

    [Fact]
    public async Task SecurityHeaders_ServerHeader_IsAbsent()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/trending");
        response.Headers.Contains("Server").Should().BeFalse();
    }

    [Fact]
    public async Task GetTrending_RedditLinkPost_ShowsDomainAsSourceName()
    {
        var linkPostItem = new ContentItem
        {
            Id = "reddit_link1",
            Title = "Some external article",
            Url = "https://www.reddit.com/r/MachineLearning/comments/link1/some_article/",
            ExternalUrl = "https://theguardian.com/tech/ai-article",
            Source = SourceType.Reddit,
            ContentType = ContentType.Discussion,
            Upvotes = 500,
            CommentCount = 50,
            PostedAt = DateTime.UtcNow
        };

        var client = CreateClient([linkPostItem]);
        var json = await client.GetFromJsonAsync<JsonElement>("/api/trending");
        var rows = json.GetProperty("rows").EnumerateArray().ToList();
        var item = rows
            .SelectMany(r => r.GetProperty("items").EnumerateArray())
            .First(i => i.GetProperty("id").GetString() == "reddit_link1");

        item.GetProperty("sourceName").GetString().Should().Be("theguardian.com",
            "Reddit link posts should show the external article domain, not 'Reddit'");
    }

    private static ContentItem MakeItem(string id, ContentType contentType, SourceType source = SourceType.Reddit) =>
        new()
        {
            Id = id,
            Title = $"Title {id}",
            Url = $"https://example.com/{id}",
            Source = source,
            ContentType = contentType,
            Upvotes = 100,
            CommentCount = 10,
            PostedAt = DateTime.UtcNow
        };
}
