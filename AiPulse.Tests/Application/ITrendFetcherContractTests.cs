using AiPulse.Application.Interfaces;
using AiPulse.Domain.Models;
using AiPulse.Infrastructure.Fetchers;
using FluentAssertions;

namespace AiPulse.Tests.Application;

public class ITrendFetcherContractTests
{
    private readonly ITrendFetcher _sut = new MockTrendFetcher();

    [Fact]
    public async Task FetchAsync_ReturnsNonNullCollection()
    {
        var result = await _sut.FetchAsync();

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task FetchAsync_ReturnsAtLeastOneItem()
    {
        var result = await _sut.FetchAsync();

        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task FetchAsync_AllItemsHaveNonEmptyTitle()
    {
        var result = await _sut.FetchAsync();

        result.Should().AllSatisfy(item => item.Title.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public async Task FetchAsync_AllItemsHaveNonEmptyUrl()
    {
        var result = await _sut.FetchAsync();

        result.Should().AllSatisfy(item => item.Url.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public async Task FetchAsync_AllItemsHaveNonEmptyId()
    {
        var result = await _sut.FetchAsync();

        result.Should().AllSatisfy(item => item.Id.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public async Task FetchAsync_AllItemsHavePostedAtInThePast()
    {
        var result = await _sut.FetchAsync();

        result.Should().AllSatisfy(item => item.PostedAt.Should().BeBefore(DateTime.UtcNow.AddMinutes(1)));
    }

    [Fact]
    public async Task FetchAsync_AllItemsHaveNonNegativeUpvotes()
    {
        var result = await _sut.FetchAsync();

        result.Should().AllSatisfy(item => item.Upvotes.Should().BeGreaterThanOrEqualTo(0));
    }

    [Fact]
    public async Task FetchAsync_AllItemsHaveNonNegativeCommentCount()
    {
        var result = await _sut.FetchAsync();

        result.Should().AllSatisfy(item => item.CommentCount.Should().BeGreaterThanOrEqualTo(0));
    }

    [Fact]
    public async Task FetchAsync_ReturnsItemsFromSingleSource()
    {
        var result = await _sut.FetchAsync();

        var sourceTypes = result.Select(i => i.Source).Distinct();
        sourceTypes.Should().HaveCount(1, "each fetcher represents one source");
    }

    [Fact]
    public async Task FetchAsync_RespectsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await _sut.FetchAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
