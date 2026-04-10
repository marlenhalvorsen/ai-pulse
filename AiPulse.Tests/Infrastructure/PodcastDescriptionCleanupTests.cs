using AiPulse.Application.Interfaces;
using AiPulse.Application.Services;
using AiPulse.Domain.Enums;
using AiPulse.Domain.Models;
using AiPulse.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AiPulse.Tests.Infrastructure;

public class PodcastDescriptionCleanupTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly IContentRepository _repository;

    public PodcastDescriptionCleanupTests()
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

    private static ContentItem PodcastItem(string id, string description = "Clean description.") => new()
    {
        Id = id,
        Title = "Episode",
        Url = $"https://podcast.example.com/{id}",
        Source = SourceType.Podcast,
        ContentType = ContentType.Podcast,
        Description = description,
        PostedAt = DateTime.UtcNow
    };

    private static ContentItem ArticleItem(string id) => new()
    {
        Id = id,
        Title = "Article",
        Url = $"https://article.example.com/{id}",
        Source = SourceType.HackerNews,
        ContentType = ContentType.Article,
        Description = "Article description.",
        PostedAt = DateTime.UtcNow
    };

    private static Mock<ITrendFetcher> EmptyFetcher()
    {
        var mock = new Mock<ITrendFetcher>();
        mock.Setup(f => f.FetchAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ContentItem>());
        return mock;
    }

    // ── Deletion ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_DeletesAllExistingPodcastItems()
    {
        _db.ContentItems.Add(PodcastItem("pod1"));
        _db.ContentItems.Add(PodcastItem("pod2"));
        await _db.SaveChangesAsync();

        await new PodcastDescriptionCleanup(_db, EmptyFetcher().Object, _repository).RunAsync();

        _db.ChangeTracker.Clear();
        var remaining = await _db.ContentItems
            .Where(i => i.ContentType == ContentType.Podcast)
            .ToListAsync();
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_LeavesNonPodcastItemsUntouched()
    {
        _db.ContentItems.Add(PodcastItem("pod1"));
        _db.ContentItems.Add(ArticleItem("art1"));
        await _db.SaveChangesAsync();

        await new PodcastDescriptionCleanup(_db, EmptyFetcher().Object, _repository).RunAsync();

        _db.ChangeTracker.Clear();
        var article = await _db.ContentItems.FindAsync("art1");
        article.Should().NotBeNull();
    }

    [Fact]
    public async Task RunAsync_WorksWhenNoPodcastItemsExist()
    {
        var act = async () =>
            await new PodcastDescriptionCleanup(_db, EmptyFetcher().Object, _repository).RunAsync();

        await act.Should().NotThrowAsync();
    }

    // ── Re-fetch ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CallsPodcastFetcherExactlyOnce()
    {
        var fetcher = EmptyFetcher();

        await new PodcastDescriptionCleanup(_db, fetcher.Object, _repository).RunAsync();

        fetcher.Verify(f => f.FetchAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_SavesFreshItemsFromFetcher()
    {
        var freshItem = PodcastItem("pod-fresh", "Already clean description.");
        var fetcher = new Mock<ITrendFetcher>();
        fetcher.Setup(f => f.FetchAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { freshItem });

        await new PodcastDescriptionCleanup(_db, fetcher.Object, _repository).RunAsync();

        _db.ChangeTracker.Clear();
        var saved = await _db.ContentItems.FindAsync("pod-fresh");
        saved.Should().NotBeNull();
        saved!.Description.Should().Be("Already clean description.");
    }

    [Fact]
    public async Task RunAsync_ReplacesOldItemsWithFreshOnes()
    {
        _db.ContentItems.Add(PodcastItem("pod-old", "<p>Stale HTML description</p>"));
        await _db.SaveChangesAsync();

        var freshItem = PodcastItem("pod-new", "Fresh clean description.");
        var fetcher = new Mock<ITrendFetcher>();
        fetcher.Setup(f => f.FetchAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { freshItem });

        await new PodcastDescriptionCleanup(_db, fetcher.Object, _repository).RunAsync();

        _db.ChangeTracker.Clear();
        var old = await _db.ContentItems.FindAsync("pod-old");
        var fresh = await _db.ContentItems.FindAsync("pod-new");
        old.Should().BeNull();
        fresh.Should().NotBeNull();
        fresh!.Description.Should().Be("Fresh clean description.");
    }

    [Fact]
    public async Task RunAsync_PassesCancellationTokenToFetcher()
    {
        using var cts = new CancellationTokenSource();
        var capturedToken = CancellationToken.None;
        var fetcher = new Mock<ITrendFetcher>();
        fetcher.Setup(f => f.FetchAsync(It.IsAny<CancellationToken>()))
            .Callback<CancellationToken>(ct => capturedToken = ct)
            .ReturnsAsync(Array.Empty<ContentItem>());

        await new PodcastDescriptionCleanup(_db, fetcher.Object, _repository).RunAsync(cts.Token);

        capturedToken.Should().Be(cts.Token);
    }
}
