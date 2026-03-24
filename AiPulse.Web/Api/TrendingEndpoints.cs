using AiPulse.Application.DTOs;
using AiPulse.Application.UseCases;
using AiPulse.Domain.Enums;

namespace AiPulse.Web.Api;

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

        return app;
    }
}
