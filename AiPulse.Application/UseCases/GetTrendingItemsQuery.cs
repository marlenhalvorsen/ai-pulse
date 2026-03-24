using AiPulse.Application.DTOs;
using AiPulse.Application.Interfaces;
using AiPulse.Application.Services;
using AiPulse.Domain.Enums;
using AiPulse.Domain.Models;

namespace AiPulse.Application.UseCases;

public class GetTrendingItemsQuery
{
    private const int DefaultLimit = 10;
    private const int MaxLimit = 50;

    private readonly ITrendingQuery _query;
    private readonly TrendScoreCalculator _calculator;

    public GetTrendingItemsQuery(ITrendingQuery query, TrendScoreCalculator calculator)
    {
        _query = query;
        _calculator = calculator;
    }

    public async Task<TrendingResponse> ExecuteAsync(
        ContentType? type = null,
        int limit = DefaultLimit,
        string window = "week",
        CancellationToken cancellationToken = default)
    {
        var clampedLimit = Math.Clamp(limit, 1, MaxLimit);
        var timeWindow = window == "day" ? TimeSpan.FromHours(24) : TimeSpan.FromDays(7);

        var items = await _query.GetTrendingAsync(type, clampedLimit, timeWindow, cancellationToken);

        var rows = items
            .Select(i => MapToDto(i))
            .GroupBy(d => d.ContentType)
            .Select(g => new TrendingRowDto(g.Key, g.ToList()));

        return new TrendingResponse(DateTime.UtcNow, rows.ToList());
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

    private static string FormatSourceName(ContentItem item) =>
        item.Source switch
        {
            SourceType.Reddit => "Reddit",
            SourceType.HackerNews => "HackerNews",
            _ => item.Source.ToString()
        };
}
