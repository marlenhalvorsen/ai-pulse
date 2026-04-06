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
}
