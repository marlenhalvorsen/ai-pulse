using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;
using IndexPage = AiPulse.Client.Pages.Index;

namespace AiPulse.Tests.Blazor;

public class IndexPageTests : TestContext
{
    public IndexPageTests()
    {
        Services.AddSingleton(new HttpClient(new EmptyTrendingHandler())
        {
            BaseAddress = new Uri("http://localhost/")
        });
    }

    [Fact]
    public void Index_RendersAppDescription()
    {
        var cut = RenderComponent<IndexPage>();

        cut.Markup.Should().Contain("AI Pulse tracks what the AI community is actually talking about right now");
    }

    private sealed class EmptyTrendingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var json = """{"generatedAt":"2026-01-01T00:00:00Z","rows":[]}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
