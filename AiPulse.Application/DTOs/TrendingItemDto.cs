namespace AiPulse.Application.DTOs;

public record TrendingItemDto(
    string Id,
    string Title,
    string Url,
    string SourceName,
    double TrendScore,
    int Upvotes,
    int CommentCount,
    DateTime PostedAt,
    string ContentType,
    string? Description = null);
