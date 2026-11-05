using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace AiPulse.Tests.Api;

public class DatabaseConfigurationTests
{
    [Theory]
    [InlineData("Production", "DefaultConnection", "/var/lib/ai-pulse/aipulse.db")]
    [InlineData("Production", "HangfireConnection", "/var/lib/ai-pulse/hangfire.db")]
    [InlineData("Development", "DefaultConnection", "aipulse.db")]
    [InlineData("Development", "HangfireConnection", "hangfire.db")]
    public void ConnectionStrings_UseEnvironmentSpecificStorage(
        string environment, string connectionName, string expectedPath)
    {
        // ASP.NET copies appsettings files to the API assembly's output directory.
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.GetDirectoryName(typeof(Program).Assembly.Location)!)
            .AddJsonFile("appsettings.json")
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .Build();

        var connection = new SqliteConnectionStringBuilder(
            configuration.GetConnectionString(connectionName));

        connection.DataSource.Should().Be(expectedPath);
    }
}
