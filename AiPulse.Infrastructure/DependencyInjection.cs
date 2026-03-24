using AiPulse.Application.Interfaces;
using AiPulse.Application.Services;
using AiPulse.Infrastructure.Configuration;
using AiPulse.Infrastructure.Fetchers;
using AiPulse.Infrastructure.Jobs;
using AiPulse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        services.AddHttpClient<RedditFetcher>();
        services.AddHttpClient<HackerNewsFetcher>();

        services.AddScoped<ITrendFetcher, RedditFetcher>();
        services.AddScoped<ITrendFetcher, HackerNewsFetcher>();

        services.AddScoped<TrendRefreshJob>();

        return services;
    }
}
