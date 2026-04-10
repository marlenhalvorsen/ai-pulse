using AiPulse.Application.DTOs;
using AiPulse.Web.Components;
using Bunit;
using FluentAssertions;

namespace AiPulse.Tests.Blazor;

public class TrendingCardTests : TestContext
{
    [Fact]
    public void TrendingCard_RendersTitle()
    {
        var item = MakeItem("Why GPT-5 changes everything", "https://example.com/gpt5");

        var cut = RenderComponent<TrendingCard>(p => p.Add(c => c.Item, item));

        cut.Find(".trending-card__title").TextContent.Should().Be("Why GPT-5 changes everything");
    }

    [Fact]
    public void TrendingCard_TitleIsLinkedToUrl()
    {
        var item = MakeItem("Some title", "https://arxiv.org/abs/1234");

        var cut = RenderComponent<TrendingCard>(p => p.Add(c => c.Item, item));

        cut.Find("a").GetAttribute("href").Should().Be("https://arxiv.org/abs/1234");
    }

    [Fact]
    public void TrendingCard_TitleLinkOpensInNewTab()
    {
        var item = MakeItem("Some title", "https://arxiv.org/abs/1234");

        var cut = RenderComponent<TrendingCard>(p => p.Add(c => c.Item, item));

        cut.Find("a").GetAttribute("target").Should().Be("_blank");
    }

    [Fact]
    public void TrendingCard_RendersSourceName()
    {
        var item = MakeItem("Some title", "https://example.com", sourceName: "HackerNews");

        var cut = RenderComponent<TrendingCard>(p => p.Add(c => c.Item, item));

        cut.Markup.Should().Contain("HackerNews");
    }

    [Fact]
    public void TrendingCard_RendersTrendScore()
    {
        var item = MakeItem("Some title", "https://example.com", trendScore: 847.3);

        var cut = RenderComponent<TrendingCard>(p => p.Add(c => c.Item, item));

        cut.Markup.Should().Contain("847");
    }

    [Fact]
    public void TrendingCard_RendersUpvotesAndComments()
    {
        var item = MakeItem("Some title", "https://example.com", upvotes: 1243, commentCount: 89);

        var cut = RenderComponent<TrendingCard>(p => p.Add(c => c.Item, item));

        cut.Markup.Should().Contain("1243");
        cut.Markup.Should().Contain("89");
    }

    [Theory]
    [InlineData("Reddit")]
    [InlineData("reddit.com")]
    [InlineData("i.redd.it")]
    [InlineData("redd.it")]
    public void TrendingCard_RedditSourceVariants_ShowRedditBadge(string sourceName)
    {
        var item = MakeItem("Some title", "https://example.com", sourceName: sourceName);

        var cut = RenderComponent<TrendingCard>(p => p.Add(c => c.Item, item));

        cut.Find(".source-badge").ClassList.Should().Contain("source-badge--reddit",
            $"source name '{sourceName}' should be recognised as Reddit");
    }

    [Fact]
    public void TrendingCard_Podcast_ShowsDescription()
    {
        var item = MakePodcastItem(description: "Deep dive into transformer architecture and attention mechanisms.");

        var cut = RenderComponent<TrendingCard>(p => p.Add(c => c.Item, item));

        cut.Markup.Should().Contain("Deep dive into transformer architecture");
    }

    [Fact]
    public void TrendingCard_Podcast_HidesUpvotesAndComments()
    {
        var item = MakePodcastItem(upvotes: 0, commentCount: 0);

        var cut = RenderComponent<TrendingCard>(p => p.Add(c => c.Item, item));

        cut.FindAll(".trending-card__stats").Should().BeEmpty();
    }

    [Fact]
    public void TrendingCard_NonPodcast_ShowsUpvotesAndComments()
    {
        var item = MakeItem("GPT-5 is here", upvotes: 999, commentCount: 42);

        var cut = RenderComponent<TrendingCard>(p => p.Add(c => c.Item, item));

        cut.Markup.Should().Contain("999");
        cut.Markup.Should().Contain("42");
    }

    [Fact]
    public void TrendingCard_Podcast_ShowsShowNameAsBadge()
    {
        var item = MakePodcastItem(sourceName: "Lex Fridman Podcast");

        var cut = RenderComponent<TrendingCard>(p => p.Add(c => c.Item, item));

        cut.Markup.Should().Contain("Lex Fridman Podcast");
    }

    [Fact]
    public void TrendingCard_Podcast_HidesScoreBadge()
    {
        var item = MakePodcastItem();

        var cut = RenderComponent<TrendingCard>(p => p.Add(c => c.Item, item));

        cut.FindAll(".trending-card__score").Should().BeEmpty();
    }

    [Fact]
    public void TrendingCard_DevToSource_ShowsDevToBadge()
    {
        var item = MakeItem("Some title", "https://dev.to/article", sourceName: "Dev.to");

        var cut = RenderComponent<TrendingCard>(p => p.Add(c => c.Item, item));

        cut.Find(".source-badge").ClassList.Should().Contain("source-badge--devto",
            "Dev.to source should show a Dev.to-coloured badge");
    }

    [Fact]
    public void TrendingCard_WhenDescriptionNull_DoesNotRenderDescriptionElement()
    {
        var item = MakeItem("Title");  // non-podcast, no description

        var cut = RenderComponent<TrendingCard>(p => p.Add(c => c.Item, item));

        cut.FindAll(".trending-card__description").Should().BeEmpty();
    }

    private static TrendingItemDto MakeItem(
        string title = "Title",
        string url = "https://example.com",
        string sourceName = "Reddit",
        double trendScore = 100.0,
        int upvotes = 100,
        int commentCount = 10) =>
        new(
            Id: "item-1",
            Title: title,
            Url: url,
            SourceName: sourceName,
            TrendScore: trendScore,
            Upvotes: upvotes,
            CommentCount: commentCount,
            PostedAt: DateTime.UtcNow,
            ContentType: "Article");

    private static TrendingItemDto MakePodcastItem(
        string sourceName = "AI Daily Brief",
        string description = "Episode description here.",
        int upvotes = 0,
        int commentCount = 0) =>
        new(
            Id: "pod-1",
            Title: "Episode Title",
            Url: "https://podcast.com/ep1",
            SourceName: sourceName,
            TrendScore: 50.0,
            Upvotes: upvotes,
            CommentCount: commentCount,
            PostedAt: DateTime.UtcNow,
            ContentType: "Podcast",
            Description: description);
}
