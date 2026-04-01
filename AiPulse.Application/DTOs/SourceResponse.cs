namespace AiPulse.Application.DTOs;

public record SourceResponse(DateTime GeneratedAt, string Source, IEnumerable<TrendingItemDto> Items);
