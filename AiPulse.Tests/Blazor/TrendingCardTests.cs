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

        cut.Find("a").TextContent.Should().Be("Why GPT-5 changes everything");
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
}
