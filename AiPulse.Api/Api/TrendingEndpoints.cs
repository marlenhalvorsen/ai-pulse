using AiPulse.Application.DTOs;
using AiPulse.Application.UseCases;
using AiPulse.Domain.Enums;

namespace AiPulse.Api.Api;

public static class TrendingEndpoints
{
    public static IEndpointRouteBuilder MapTrendingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/trending", async (
            string? type,
            int limit = 10,
            string window = "week",
            GetTrendingItemsQuery query = null!) =>
        {
            ContentType? contentType = type?.ToLowerInvariant() switch
            {
                "video" => ContentType.Video,
                "podcast" => ContentType.Podcast,
                "article" => ContentType.Article,
                "newsletter" => ContentType.Newsletter,
                "research" => ContentType.ResearchPaper,
                "discussion" => ContentType.Discussion,
                _ => null
            };

            var result = await query.ExecuteAsync(contentType, limit, window);
            return Results.Ok(result);
        });

        app.MapGet("/api/source/{sourceName}", async (
            string sourceName,
            int limit = 20,
            string window = "week",
            GetSourceItemsQuery query = null!) =>
        {
            if (!Enum.TryParse<SourceType>(sourceName, ignoreCase: true, out var source))
                return Results.NotFound();

            var result = await query.ExecuteAsync(source, limit, window);
            return Results.Ok(result);
        });

        return app;
    }
}
