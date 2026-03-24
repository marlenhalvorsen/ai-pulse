using AiPulse.Application.Interfaces;
using AiPulse.Domain.Enums;
using AiPulse.Domain.Models;
using AiPulse.Infrastructure.Jobs;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AiPulse.Tests.Infrastructure;

public class TrendRefreshJobTests
{
    private readonly Mock<IContentRepository> _repositoryMock = new();

    [Fact]
    public async Task ExecuteAsync_CallsEveryFetcher()
    {
        var fetcher1 = new Mock<ITrendFetcher>();
        var fetcher2 = new Mock<ITrendFetcher>();
        fetcher1.Setup(f => f.FetchAsync(default)).ReturnsAsync([]);
        fetcher2.Setup(f => f.FetchAsync(default)).ReturnsAsync([]);
        _repositoryMock.Setup(r => r.UpsertAsync(It.IsAny<IEnumerable<ContentItem>>(), default))
            .Returns(Task.CompletedTask);

        var job = new TrendRefreshJob([fetcher1.Object, fetcher2.Object], _repositoryMock.Object, NullLogger<TrendRefreshJob>.Instance);

        await job.ExecuteAsync();

        fetcher1.Verify(f => f.FetchAsync(default), Times.Once);
        fetcher2.Verify(f => f.FetchAsync(default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_UpsertsItemsFromEachFetcher()
    {
        var items1 = new[] { MakeItem("r-1"), MakeItem("r-2") };
        var items2 = new[] { MakeItem("hn-1") };

        var fetcher1 = new Mock<ITrendFetcher>();
        var fetcher2 = new Mock<ITrendFetcher>();
        fetcher1.Setup(f => f.FetchAsync(default)).ReturnsAsync(items1);
        fetcher2.Setup(f => f.FetchAsync(default)).ReturnsAsync(items2);

        var upserted = new List<ContentItem>();
        _repositoryMock
            .Setup(r => r.UpsertAsync(It.IsAny<IEnumerable<ContentItem>>(), default))
            .Callback<IEnumerable<ContentItem>, CancellationToken>((items, _) => upserted.AddRange(items))
            .Returns(Task.CompletedTask);

        var job = new TrendRefreshJob([fetcher1.Object, fetcher2.Object], _repositoryMock.Object, NullLogger<TrendRefreshJob>.Instance);

        await job.ExecuteAsync();

        upserted.Should().HaveCount(3);
        upserted.Select(i => i.Id).Should().BeEquivalentTo(["r-1", "r-2", "hn-1"]);
    }

    [Fact]
    public async Task ExecuteAsync_WhenFetcherThrows_ContinuesToNextFetcher()
    {
        var items2 = new[] { MakeItem("hn-1") };

        var failingFetcher = new Mock<ITrendFetcher>();
        var workingFetcher = new Mock<ITrendFetcher>();
        failingFetcher.Setup(f => f.FetchAsync(default)).ThrowsAsync(new HttpRequestException("timeout"));
        workingFetcher.Setup(f => f.FetchAsync(default)).ReturnsAsync(items2);

        var upserted = new List<ContentItem>();
        _repositoryMock
            .Setup(r => r.UpsertAsync(It.IsAny<IEnumerable<ContentItem>>(), default))
            .Callback<IEnumerable<ContentItem>, CancellationToken>((items, _) => upserted.AddRange(items))
            .Returns(Task.CompletedTask);

        var job = new TrendRefreshJob([failingFetcher.Object, workingFetcher.Object], _repositoryMock.Object, NullLogger<TrendRefreshJob>.Instance);

        await job.ExecuteAsync();

        upserted.Should().HaveCount(1);
        upserted.Single().Id.Should().Be("hn-1");
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoFetchers_DoesNotCallRepository()
    {
        var job = new TrendRefreshJob([], _repositoryMock.Object, NullLogger<TrendRefreshJob>.Instance);

        await job.ExecuteAsync();

        _repositoryMock.Verify(r => r.UpsertAsync(It.IsAny<IEnumerable<ContentItem>>(), default), Times.Never);
    }

    private static ContentItem MakeItem(string id) =>
        new()
        {
            Id = id,
            Title = $"Title {id}",
            Url = $"https://example.com/{id}",
            Source = SourceType.HackerNews,
            ContentType = ContentType.Article,
            Upvotes = 10,
            CommentCount = 2,
            PostedAt = DateTime.UtcNow
        };
}
