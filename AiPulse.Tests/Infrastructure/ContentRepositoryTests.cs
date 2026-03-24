using AiPulse.Application.Interfaces;
using AiPulse.Application.Services;
using AiPulse.Domain.Enums;
using AiPulse.Domain.Models;
using AiPulse.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AiPulse.Tests.Infrastructure;

public class ContentRepositoryTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ContentRepository _repository;

    public ContentRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _db = new AppDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _repository = new ContentRepository(_db, new TrendScoreCalculator());
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    [Fact]
    public async Task UpsertAsync_NewItems_StoresThemInDatabase()
    {
        var items = new[]
        {
            MakeItem("item-1", ContentType.Article),
            MakeItem("item-2", ContentType.Video)
        };

        await _repository.UpsertAsync(items);

        var stored = await _db.ContentItems.ToListAsync();
        stored.Should().HaveCount(2);
        stored.Select(i => i.Id).Should().BeEquivalentTo(["item-1", "item-2"]);
    }

    [Fact]
    public async Task UpsertAsync_ExistingItem_UpdatesUpvotesAndCommentCount()
    {
        var original = MakeItem("item-1", ContentType.Article, upvotes: 10, comments: 2);
        await _repository.UpsertAsync([original]);

        var updated = MakeItem("item-1", ContentType.Article, upvotes: 99, comments: 20);
        await _repository.UpsertAsync([updated]);

        var stored = await _db.ContentItems.SingleAsync();
        stored.Upvotes.Should().Be(99);
        stored.CommentCount.Should().Be(20);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllStoredItems()
    {
        await _repository.UpsertAsync(
        [
            MakeItem("a", ContentType.Article),
            MakeItem("b", ContentType.Video),
            MakeItem("c", ContentType.Discussion)
        ]);

        var all = await _repository.GetAllAsync();

        all.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetTrendingAsync_NoTypeFilter_ReturnsItemsSortedByScoreDescending()
    {
        var now = DateTime.UtcNow;
        await _repository.UpsertAsync(
        [
            MakeItem("low",  ContentType.Article,  upvotes: 10,   comments: 1,  postedAt: now),
            MakeItem("high", ContentType.Article,  upvotes: 1000, comments: 50, postedAt: now)
        ]);

        var results = (await _repository.GetTrendingAsync(null, 10, TimeSpan.FromDays(7))).ToList();

        results[0].Id.Should().Be("high");
        results[1].Id.Should().Be("low");
    }

    [Fact]
    public async Task GetTrendingAsync_WithTypeFilter_ReturnsOnlyMatchingType()
    {
        var now = DateTime.UtcNow;
        await _repository.UpsertAsync(
        [
            MakeItem("article-1", ContentType.Article,    postedAt: now),
            MakeItem("video-1",   ContentType.Video,      postedAt: now),
            MakeItem("article-2", ContentType.Article,    postedAt: now)
        ]);

        var results = await _repository.GetTrendingAsync(ContentType.Article, 10, TimeSpan.FromDays(7));

        results.Should().HaveCount(2);
        results.Should().OnlyContain(i => i.ContentType == ContentType.Article);
    }

    [Fact]
    public async Task GetTrendingAsync_RespectsLimit()
    {
        var now = DateTime.UtcNow;
        await _repository.UpsertAsync(
        [
            MakeItem("a", ContentType.Article, postedAt: now),
            MakeItem("b", ContentType.Article, postedAt: now),
            MakeItem("c", ContentType.Article, postedAt: now)
        ]);

        var results = await _repository.GetTrendingAsync(null, 2, TimeSpan.FromDays(7));

        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTrendingAsync_ExcludesItemsOlderThanWindow()
    {
        var now = DateTime.UtcNow;
        await _repository.UpsertAsync(
        [
            MakeItem("recent", ContentType.Article, postedAt: now.AddDays(-3)),
            MakeItem("old",    ContentType.Article, postedAt: now.AddDays(-8))
        ]);

        var results = await _repository.GetTrendingAsync(null, 10, TimeSpan.FromDays(7));

        results.Should().HaveCount(1);
        results.Single().Id.Should().Be("recent");
    }

    [Fact]
    public async Task GetTrendingAsync_ItemsWithZeroScore_AreExcluded()
    {
        var now = DateTime.UtcNow;
        await _repository.UpsertAsync(
        [
            MakeItem("good",  ContentType.Article, upvotes: 100, postedAt: now),
            MakeItem("stale", ContentType.Article, upvotes: 500, postedAt: now.AddDays(-7))
        ]);

        var results = await _repository.GetTrendingAsync(null, 10, TimeSpan.FromDays(7));

        results.Should().HaveCount(1);
        results.Single().Id.Should().Be("good");
    }

    private static ContentItem MakeItem(
        string id,
        ContentType contentType,
        int upvotes = 100,
        int comments = 10,
        DateTime? postedAt = null) =>
        new()
        {
            Id = id,
            Title = $"Title for {id}",
            Url = $"https://example.com/{id}",
            Source = SourceType.Reddit,
            ContentType = contentType,
            Upvotes = upvotes,
            CommentCount = comments,
            PostedAt = postedAt ?? DateTime.UtcNow
        };
}
