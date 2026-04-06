using AiPulse.Web.Components;
using Bunit;
using FluentAssertions;

namespace AiPulse.Tests.Blazor;

public class SiteLogoTests : TestContext
{
    [Fact]
    public void SiteLogo_RendersBrandName()
    {
        var cut = RenderComponent<SiteLogo>();

        cut.Markup.Should().Contain("AI Pulse");
    }

    [Fact]
    public void SiteLogo_RendersTwoHeartbeatSvgs()
    {
        var cut = RenderComponent<SiteLogo>();

        cut.FindAll("svg").Should().HaveCount(2);
    }

    [Fact]
    public void SiteLogo_BrandNameIsBetweenSvgs()
    {
        var cut = RenderComponent<SiteLogo>();

        var children = cut.Find(".site-logo").Children;
        // order: svg | span.site-logo__name | svg
        children[0].TagName.Should().BeEquivalentTo("svg");
        children[1].GetAttribute("class").Should().Contain("site-logo__name");
        children[2].TagName.Should().BeEquivalentTo("svg");
    }

    [Fact]
    public void SiteLogo_HasNoTagline()
    {
        var cut = RenderComponent<SiteLogo>();

        cut.FindAll(".page-header__sub").Should().BeEmpty();
    }
}
