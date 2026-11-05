using System.Net;
using AiPulse.Application.Interfaces;
using AiPulse.Infrastructure.Configuration;
using AiPulse.Infrastructure.Fetchers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiPulse.Tests.Api;

public class HangfireStorageTests
{
    [Fact]
    public async Task Startup_AbsoluteHangfireConnectionString_CreatesDatabaseAtIntendedPath()
    {
        var directory = Directory.CreateTempSubdirectory("aipulse-hangfire-");
        try
        {
            var databasePath = Path.Combine(directory.FullName, "hangfire.db");
            using var factory = new ApiFactory(directory.FullName, $"DataSource={databasePath}");
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/api/trending");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            File.Exists(databasePath).Should().BeTrue();
            using var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('Job', 'JobQueue', 'DistributedLock')";
            Convert.ToInt64(command.ExecuteScalar()).Should().Be(3);
        }
        finally
        {
            // Only remove this test's newly created, isolated databases.
            directory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("DataSource=")]
    [InlineData("DataSource=   ")]
    public void Startup_EmptyHangfireFilename_FailsWithClearConfigurationError(string connectionString)
    {
        var directory = Directory.CreateTempSubdirectory("aipulse-hangfire-");
        try
        {
            using var factory = new ApiFactory(directory.FullName, connectionString);
            Action start = () => factory.CreateClient();

            start.Should().Throw<InvalidOperationException>()
                .WithMessage("HangfireConnection must specify a non-empty SQLite DataSource filename.");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private sealed class ApiFactory(string directory, string hangfireConnection) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ProductHunt:DeveloperToken"] = "test-token",
                    ["ConnectionStrings:DefaultConnection"] = new SqliteConnectionStringBuilder
                    {
                        DataSource = Path.Combine(directory, "aipulse.db"),
                        Pooling = false
                    }.ToString(),
                    ["ConnectionStrings:HangfireConnection"] = hangfireConnection
                }));
            builder.ConfigureServices(services =>
            {
                // Preserve the real Hangfire provider, but keep all ingestion offline.
                services.RemoveAll<ITrendFetcher>();
                services.AddScoped<ITrendFetcher>(provider => new PodcastFetcher(
                    provider.GetRequiredService<IHttpClientFactory>(),
                    Options.Create(new PodcastSettings()),
                    provider.GetRequiredService<ILogger<PodcastFetcher>>()));
            });
        }
    }
}
