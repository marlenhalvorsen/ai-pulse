using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AiPulse.Tests.Api;

public class IngestEndpointTests : IClassFixture<IngestEndpointTests.ApiFactory>
{
    public class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = $"test-ingest-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ProductHunt:DeveloperToken"] = "test-token",
                    ["Ingest:Secret"] = "test-secret-abc123",
                    ["ConnectionStrings:DefaultConnection"] = $"DataSource={_dbName};Mode=Memory;Cache=Shared"
                }));
        }
    }

    private readonly ApiFactory _factory;
    public IngestEndpointTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task PostIngestReddit_NoSecret_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/ingest/reddit", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostIngestReddit_WrongSecret_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Ingest-Secret", "wrong-secret");

        var response = await client.PostAsync("/api/ingest/reddit", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostIngestReddit_CorrectSecret_Returns202()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Ingest-Secret", "test-secret-abc123");

        var response = await client.PostAsync("/api/ingest/reddit", null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }
}
