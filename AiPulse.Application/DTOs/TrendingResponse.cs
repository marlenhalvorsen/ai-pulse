namespace AiPulse.Application.DTOs;

public record TrendingRowDto(string ContentType, IEnumerable<TrendingItemDto> Items);

public record TrendingResponse(DateTime GeneratedAt, IEnumerable<TrendingRowDto> Rows);
