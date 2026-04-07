using AiPulse.Domain.Enums;

namespace AiPulse.Domain.Models;

public class ContentItem
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string? ExternalUrl { get; init; }
    public SourceType Source { get; init; }
    public ContentType ContentType { get; init; }
    public int Upvotes { get; init; }
    public int CommentCount { get; init; }
    public DateTime PostedAt { get; init; }
    public string? Description { get; init; }
    public string? ShowName { get; init; }
}
