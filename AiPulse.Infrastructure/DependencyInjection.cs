using AiPulse.Application.Interfaces;
using AiPulse.Application.Services;
using AiPulse.Infrastructure.Configuration;
using AiPulse.Infrastructure.Fetchers;
using AiPulse.Infrastructure.Jobs;
using AiPulse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiPulse.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("DefaultConnection")
                ?? "DataSource=aipulse.db"));

        services.AddScoped<IContentRepository, ContentRepository>();
        services.AddScoped<ITrendingQuery, ContentRepository>();

        services.AddSingleton<TrendScoreCalculator>();
        services.AddSingleton<UrlClassifier>();

        services.Configure<RedditSettings>(configuration.GetSection("Reddit"));
        services.Configure<HackerNewsSettings>(configuration.GetSection("HackerNews"));
        services.Configure<DevToSettings>(configuration.GetSection("DevTo"));
        services.AddOptions<ProductHuntSettings>()
            .Bind(configuration.GetSection("ProductHunt"))
            .Validate(
                s => !string.IsNullOrWhiteSpace(s.DeveloperToken),
                "ProductHunt:DeveloperToken is not configured. " +
                "Add it to appsettings.Development.json or set the " +
                "ProductHunt__DeveloperToken environment variable. " +
                "Never commit this token to source control.")
            .ValidateOnStart();

        services.AddHttpClient("Reddit", (sp, client) =>
        {
            var settings = sp.GetRequiredService<IOptions<RedditSettings>>().Value;
            client.BaseAddress = new Uri(settings.BaseUrl);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(settings.UserAgent);
        });

        services.AddHttpClient("HackerNews", (sp, client) =>
        {
            var settings = sp.GetRequiredService<IOptions<HackerNewsSettings>>().Value;
            client.BaseAddress = new Uri(settings.BaseUrl);
        });

        services.AddHttpClient("ProductHunt", (sp, client) =>
        {
            var settings = sp.GetRequiredService<IOptions<ProductHuntSettings>>().Value;
            client.BaseAddress = new Uri(settings.BaseUrl);
        });

        services.AddHttpClient("DevTo", (sp, client) =>
        {
            var settings = sp.GetRequiredService<IOptions<DevToSettings>>().Value;
            client.BaseAddress = new Uri(settings.BaseUrl);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(settings.UserAgent);
        });

        services.AddScoped<ITrendFetcher, RedditFetcher>();
        services.AddScoped<ITrendFetcher, HackerNewsFetcher>();
        services.AddScoped<ITrendFetcher, ProductHuntFetcher>();
        services.AddScoped<ITrendFetcher, DevToFetcher>();

        services.AddScoped<TrendRefreshJob>();

        return services;
    }
}
