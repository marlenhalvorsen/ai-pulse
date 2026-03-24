using FluentAssertions;
using NetArchTest.Rules;

namespace AiPulse.Tests.Architecture;

public class LayerBoundaryTests
{
    private const string DomainNs = "AiPulse.Domain";
    private const string ApplicationNs = "AiPulse.Application";
    private const string InfrastructureNs = "AiPulse.Infrastructure";
    private const string WebNs = "AiPulse.Web";

    [Fact]
    public void Domain_ShouldNotDependOnAnyOtherLayer()
    {
        var result = Types.InAssembly(typeof(AiPulse.Domain.Models.ContentItem).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNs, InfrastructureNs, WebNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Domain must be dependency-free; failing types: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Application_ShouldNotDependOnInfrastructureOrWeb()
    {
        var result = Types.InAssembly(typeof(AiPulse.Application.Interfaces.ITrendFetcher).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNs, WebNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Application must not reference Infrastructure or Web; failing types: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Infrastructure_ShouldNotDependOnWeb()
    {
        var result = Types.InAssembly(typeof(AiPulse.Infrastructure.Fetchers.RedditFetcher).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(WebNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Infrastructure must not reference Web; failing types: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Domain_ShouldOnlyContainModelsAndEnums()
    {
        var result = Types.InAssembly(typeof(AiPulse.Domain.Models.ContentItem).Assembly)
            .Should()
            .ResideInNamespace(DomainNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "All Domain types must reside in the AiPulse.Domain namespace; failing types: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Application_ShouldOnlyDependOnDomain()
    {
        var result = Types.InAssembly(typeof(AiPulse.Application.Interfaces.ITrendFetcher).Assembly)
            .That()
            .HaveDependencyOn(InfrastructureNs)
            .GetTypes();

        result.Should().BeEmpty(
            because: "Application types must not reference Infrastructure");
    }

    [Fact]
    public void Infrastructure_Fetchers_ShouldImplementITrendFetcher()
    {
        var result = Types.InAssembly(typeof(AiPulse.Infrastructure.Fetchers.RedditFetcher).Assembly)
            .That()
            .ResideInNamespace($"{InfrastructureNs}.Fetchers")
            .And()
            .AreClasses()
            .And()
            .ArePublic()
            .And()
            .AreNotNested()
            .Should()
            .ImplementInterface(typeof(AiPulse.Application.Interfaces.ITrendFetcher))
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "All public top-level classes in Infrastructure.Fetchers must implement ITrendFetcher; failing types: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Web_ShouldNotDirectlyInstantiateInfrastructureTypes()
    {
        var result = Types.InAssembly(typeof(AiPulse.Web.Middleware.SecurityHeadersMiddleware).Assembly)
            .That()
            .DoNotResideInNamespace($"{WebNs}.Program")
            .ShouldNot()
            .HaveDependencyOn($"{InfrastructureNs}.Fetchers")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Web layer must not directly reference Infrastructure.Fetchers; failing types: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }
}
