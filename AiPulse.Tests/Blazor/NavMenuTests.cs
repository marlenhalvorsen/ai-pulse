using AiPulse.Web.Shared;
using Bunit;
using FluentAssertions;

namespace AiPulse.Tests.Blazor;

public class NavMenuTests : TestContext
{
    [Fact]
    public void NavMenu_DoesNotRenderBrandText()
    {
        var cut = RenderComponent<NavMenu>();

        cut.FindAll(".sidebar__brand").Should().BeEmpty();
    }

    [Fact]
    public void NavMenu_RendersToggleButton()
    {
        var cut = RenderComponent<NavMenu>();

        cut.FindAll(".sidebar__toggle").Should().HaveCount(1);
    }

    [Fact]
    public void NavMenu_RendersHomeLink()
    {
        var cut = RenderComponent<NavMenu>();

        cut.FindAll(".sidebar__link").Should().NotBeEmpty();
    }

    [Fact]
    public void NavMenu_RendersGitHubTrendingLink()
    {
        var cut = RenderComponent<NavMenu>();

        cut.FindAll(".sidebar__link")
            .Should().Contain(el => el.GetAttribute("href") == "source/github");
    }

    [Fact]
    public void NavMenu_GitHubLink_HasCorrectLabel()
    {
        var cut = RenderComponent<NavMenu>();

        var githubLink = cut.FindAll(".sidebar__link")
            .FirstOrDefault(el => el.GetAttribute("href") == "source/github");

        githubLink.Should().NotBeNull();
        githubLink!.TextContent.Should().Contain("GitHub");
    }
}
