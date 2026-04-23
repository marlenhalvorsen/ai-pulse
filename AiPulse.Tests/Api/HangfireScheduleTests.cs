using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiPulse.Tests.Api;

public class HangfireScheduleTests : IClassFixture<HangfireScheduleTests.ApiFactory>
{
    public class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = $"test-schedule-{Guid.NewGuid():N}";
        private readonly string _hangfireName = $"hangfire-schedule-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ProductHunt:DeveloperToken"] = "test-token",
                    ["ConnectionStrings:DefaultConnection"] = $"DataSource={_dbName};Mode=Memory;Cache=Shared",
                    ["ConnectionStrings:HangfireConnection"] = $"DataSource={_hangfireName};Mode=Memory;Cache=Shared"
                }));
        }
    }

    private readonly ApiFactory _factory;

    public HangfireScheduleTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public void TrendRefreshCron_IsConfiguredAsEvery6Hours()
    {
        var config = _factory.Services.GetRequiredService<IConfiguration>();

        config["Hangfire:TrendRefreshCron"].Should().Be("0 */6 * * *");
    }
}
