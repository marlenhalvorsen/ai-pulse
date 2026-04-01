using AiPulse.Application.DTOs;
using AiPulse.Application.Interfaces;
using AiPulse.Application.Services;
using AiPulse.Domain.Enums;
using AiPulse.Domain.Models;

namespace AiPulse.Application.UseCases;

public class GetSourceItemsQuery
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 50;

    private readonly ITrendingQuery _query;
    private readonly TrendScoreCalculator _calculator;

    public GetSourceItemsQuery(ITrendingQuery query, TrendScoreCalculator calculator)
    {
        _query = query;
        _calculator = calculator;
    }

    public async Task<SourceResponse> ExecuteAsync(
        SourceType source,
        int limit = DefaultLimit,
        string window = "week",
        CancellationToken cancellationToken = default)
    {
        var clampedLimit = Math.Clamp(limit, 1, MaxLimit);
        var timeWindow = window == "day" ? TimeSpan.FromHours(24) : TimeSpan.FromDays(7);

        var items = await _query.GetTrendingAsync(null, source, clampedLimit, timeWindow, cancellationToken);

        var dtos = items
            .Select(MapToDto)
            .OrderByDescending(d => d.TrendScore)
            .ToList();

        return new SourceResponse(DateTime.UtcNow, source.ToString(), dtos);
    }

    private TrendingItemDto MapToDto(ContentItem item) =>
        new(
            item.Id,
            item.Title,
            item.Url,
            FormatSourceName(item),
            _calculator.Calculate(item.Upvotes, item.CommentCount, item.PostedAt),
            item.Upvotes,
            item.CommentCount,
            item.PostedAt,
            item.ContentType.ToString());

    private static string FormatSourceName(ContentItem item)
    {
        if (item.Source == SourceType.Reddit && item.ExternalUrl is not null)
        {
            if (Uri.TryCreate(item.ExternalUrl, UriKind.Absolute, out var uri))
                return uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
        }

        return item.Source switch
        {
            SourceType.Reddit => "Reddit",
            SourceType.HackerNews => "HackerNews",
            SourceType.ProductHunt => "ProductHunt",
            _ => item.Source.ToString()
        };
    }
}
