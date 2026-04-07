using AiPulse.Application.DTOs;
using AiPulse.Web.Components;
using Bunit;
using FluentAssertions;

namespace AiPulse.Tests.Blazor;

public class TrendingRowTests : TestContext
{
    [Fact]
    public void TrendingRow_RendersContentTypeHeading()
    {
        var row = new TrendingRowDto("Video", [MakeItem("v1"), MakeItem("v2")]);

        var cut = RenderComponent<TrendingRow>(p => p.Add(r => r.Row, row));

        cut.Find("h2").TextContent.Should().Be("Videos");
    }

    [Fact]
    public void TrendingRow_RendersOneCardPerItem()
    {
        var row = new TrendingRowDto("Article", [MakeItem("a1"), MakeItem("a2"), MakeItem("a3")]);

        var cut = RenderComponent<TrendingRow>(p => p.Add(r => r.Row, row));

        cut.FindAll(".trending-card").Should().HaveCount(3);
    }

    [Fact]
    public void TrendingRow_EmptyItems_RendersNothing()
    {
        var row = new TrendingRowDto("Podcast", []);

        var cut = RenderComponent<TrendingRow>(p => p.Add(r => r.Row, row));

        cut.Markup.Should().BeEmpty();
    }

    [Fact]
    public void TrendingRow_DevTo_HasCorrectSourceHref()
    {
        var row = new TrendingRowDto("DevTo", [MakeItem("d1")]);

        var cut = RenderComponent<TrendingRow>(p => p.Add(r => r.Row, row));

        cut.Find(".trending-row__see-all").GetAttribute("href").Should().Be("source/devto");
    }

    [Fact]
    public void TrendingRow_Podcast_HasCorrectSourceHref()
    {
        var row = new TrendingRowDto("Podcast", [MakeItem("p1")]);

        var cut = RenderComponent<TrendingRow>(p => p.Add(r => r.Row, row));

        cut.Find(".trending-row__see-all").GetAttribute("href").Should().Be("source/podcast");
    }

    [Fact]
    public void TrendingRow_GitHub_HasCorrectSourceHref()
    {
        var row = new TrendingRowDto("GitHub", [MakeItem("g1")]);

        var cut = RenderComponent<TrendingRow>(p => p.Add(r => r.Row, row));

        cut.Find(".trending-row__see-all").GetAttribute("href").Should().Be("source/github");
    }

    [Theory]
    [InlineData("Video", "Videos")]
    [InlineData("Podcast", "Podcasts")]
    [InlineData("Article", "Articles")]
    [InlineData("Newsletter", "Newsletters")]
    [InlineData("ResearchPaper", "Research")]
    [InlineData("Discussion", "Discussions")]
    [InlineData("DevTo", "🟢 Dev.to")]
    [InlineData("GitHub", "⚫ GitHub Trending")]
    public void TrendingRow_HeadingLabel_MatchesContentType(string contentType, string expectedHeading)
    {
        var row = new TrendingRowDto(contentType, [MakeItem("x")]);

        var cut = RenderComponent<TrendingRow>(p => p.Add(r => r.Row, row));

        cut.Find("h2").TextContent.Should().Be(expectedHeading);
    }

    private static TrendingItemDto MakeItem(string id) =>
        new(id, $"Title {id}", "https://example.com", "Reddit", 100, 50, 5, DateTime.UtcNow, "Article");
}
