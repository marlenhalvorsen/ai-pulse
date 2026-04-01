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

public class SourceEndpointTests : IClassFixture<SourceEndpointTests.ApiFactory>
{
    public class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = $"test-source-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ProductHunt:DeveloperToken"] = "test-token",
                    ["ConnectionStrings:DefaultConnection"] = $"DataSource={_dbName};Mode=Memory;Cache=Shared"
                }));
        }
    }

    private readonly ApiFactory _factory;

    public SourceEndpointTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient(IEnumerable<ContentItem>? items = null)
    {
        var trendingItems = items ?? [MakeItem("item-1", SourceType.Reddit)];
        var mock = new Mock<ITrendingQuery>();
        mock.Setup(q => q.GetTrendingAsync(
                It.IsAny<ContentType?>(),
                It.IsAny<SourceType?>(),
                It.IsAny<int>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(trendingItems);

        return _factory.WithWebHostBuilder(b =>
            b.ConfigureServices(services => services.AddSingleton(mock.Object)))
            .CreateClient();
    }

    [Fact]
    public async Task GetSource_Returns200_ForReddit()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/source/reddit");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSource_Returns200_ForHackerNews()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/source/hackernews");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSource_Returns200_ForProductHunt()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/source/producthunt");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSource_Returns404_ForUnknownSource()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/source/unknown");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetSource_IsCaseInsensitive()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/source/Reddit");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSource_ResponseContainsSourceAndItems()
    {
        var client = CreateClient([MakeItem("r1", SourceType.Reddit)]);
        var json = await client.GetFromJsonAsync<JsonElement>("/api/source/reddit");

        json.TryGetProperty("source", out var source).Should().BeTrue();
        source.GetString().Should().Be("Reddit");
        json.TryGetProperty("items", out var items).Should().BeTrue();
        items.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task GetSource_ResponseContainsGeneratedAt()
    {
        var client = CreateClient();
        var json = await client.GetFromJsonAsync<JsonElement>("/api/source/reddit");
        json.TryGetProperty("generatedAt", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetSource_CallsQueryWithNullTypeAndCorrectSource()
    {
        var mock = new Mock<ITrendingQuery>();
        mock.Setup(q => q.GetTrendingAsync(
                (ContentType?)null, SourceType.Reddit,
                It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeItem("r1", SourceType.Reddit)]);
        mock.Setup(q => q.GetTrendingAsync(
                It.Is<ContentType?>(ct => ct.HasValue),
                It.IsAny<SourceType?>(),
                It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var client = _factory.WithWebHostBuilder(b =>
            b.ConfigureServices(s => s.AddSingleton(mock.Object)))
            .CreateClient();

        await client.GetAsync("/api/source/reddit");

        mock.Verify(q => q.GetTrendingAsync(
            (ContentType?)null, SourceType.Reddit,
            It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static ContentItem MakeItem(string id, SourceType source) => new()
    {
        Id = id,
        Title = $"Title {id}",
        Url = $"https://example.com/{id}",
        Source = source,
        ContentType = ContentType.Discussion,
        Upvotes = 100,
        CommentCount = 10,
        PostedAt = DateTime.UtcNow
    };
}
