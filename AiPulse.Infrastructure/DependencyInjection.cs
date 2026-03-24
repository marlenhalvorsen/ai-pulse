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

        services.AddScoped<ITrendFetcher, RedditFetcher>();
        services.AddScoped<ITrendFetcher, HackerNewsFetcher>();

        services.AddScoped<TrendRefreshJob>();

        return services;
    }
}
