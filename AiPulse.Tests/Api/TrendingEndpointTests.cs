using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiPulse.Application.Interfaces;
using AiPulse.Domain.Enums;
using AiPulse.Domain.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AiPulse.Tests.Api;

public class TrendingEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public TrendingEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient(IEnumerable<ContentItem>? items = null)
    {
        var trendingItems = items ?? [MakeItem("item-1", ContentType.Article)];
        var mock = new Mock<ITrendingQuery>();
        mock.Setup(q => q.GetTrendingAsync(
                It.IsAny<ContentType?>(),
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
    public async Task GetTrending_RowsGroupedByContentType()
    {
        var mock = new Mock<ITrendingQuery>();
        mock.Setup(q => q.GetTrendingAsync(ContentType.Article, It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeItem("a1", ContentType.Article), MakeItem("a2", ContentType.Article)]);
        mock.Setup(q => q.GetTrendingAsync(ContentType.Video, It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeItem("v1", ContentType.Video)]);
        mock.Setup(q => q.GetTrendingAsync(It.IsNotIn(ContentType.Article, ContentType.Video), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var client = _factory.WithWebHostBuilder(b =>
            b.ConfigureServices(s => s.AddSingleton(mock.Object)))
            .CreateClient();

        var json = await client.GetFromJsonAsync<JsonElement>("/api/trending");
        var rows = json.GetProperty("rows").EnumerateArray().ToList();

        rows.Should().HaveCount(2);
        var articleRow = rows.Single(r => r.GetProperty("contentType").GetString() == "Article");
        articleRow.GetProperty("items").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task GetTrending_LimitExceeding50_IsCappedAt50()
    {
        var mock = new Mock<ITrendingQuery>();
        mock.Setup(q => q.GetTrendingAsync(
                It.IsAny<ContentType?>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var client = _factory.WithWebHostBuilder(b =>
            b.ConfigureServices(s => s.AddSingleton(mock.Object)))
            .CreateClient();

        await client.GetAsync("/api/trending?limit=999");

        // One call per ContentType, each capped at 50
        mock.Verify(q => q.GetTrendingAsync(
            It.IsAny<ContentType>(), 50, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Exactly(Enum.GetValues<ContentType>().Length));
    }

    [Fact]
    public async Task GetTrending_WindowDay_Passes24HourTimeSpan()
    {
        var mock = new Mock<ITrendingQuery>();
        mock.Setup(q => q.GetTrendingAsync(
                It.IsAny<ContentType?>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var client = _factory.WithWebHostBuilder(b =>
            b.ConfigureServices(s => s.AddSingleton(mock.Object)))
            .CreateClient();

        await client.GetAsync("/api/trending?window=day");

        // One call per ContentType, all using the 24-hour window
        mock.Verify(q => q.GetTrendingAsync(
            It.IsAny<ContentType>(), It.IsAny<int>(), TimeSpan.FromHours(24), It.IsAny<CancellationToken>()),
            Times.Exactly(Enum.GetValues<ContentType>().Length));
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

    private static ContentItem MakeItem(string id, ContentType contentType) =>
        new()
        {
            Id = id,
            Title = $"Title {id}",
            Url = $"https://example.com/{id}",
            Source = SourceType.Reddit,
            ContentType = contentType,
            Upvotes = 100,
            CommentCount = 10,
            PostedAt = DateTime.UtcNow
        };
}
