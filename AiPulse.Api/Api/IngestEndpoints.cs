using AiPulse.Infrastructure.Jobs;
using Microsoft.Extensions.Primitives;

namespace AiPulse.Api.Api;

public static class IngestEndpoints
{
    public static IEndpointRouteBuilder MapIngestEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/ingest/reddit", (
            HttpContext context,
            IConfiguration config,
            IServiceScopeFactory scopeFactory) =>
        {
            var expected = config["RedditIngest:Secret"] ?? string.Empty;
            context.Request.Headers.TryGetValue("X-Ingest-Secret", out StringValues provided);

            if (string.IsNullOrEmpty(expected) || provided.ToString() != expected)
                return Results.Unauthorized();

            _ = Task.Run(async () =>
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<TrendRefreshJob>().ExecuteAsync();
            });

            return Results.Accepted();
        });

        return app;
    }
}
