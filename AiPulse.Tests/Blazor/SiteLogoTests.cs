using AiPulse.Client.Components;
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
    public void SiteLogo_RendersHeartbeatSvg()
    {
        var cut = RenderComponent<SiteLogo>();

        cut.FindAll("svg").Should().HaveCount(1);
    }

    [Fact]
    public void SiteLogo_HasNoTagline()
    {
        var cut = RenderComponent<SiteLogo>();

        cut.FindAll(".page-header__sub").Should().BeEmpty();
    }
}
