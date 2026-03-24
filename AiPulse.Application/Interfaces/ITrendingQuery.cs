using AiPulse.Domain.Enums;
using AiPulse.Domain.Models;

namespace AiPulse.Application.Interfaces;

public interface ITrendingQuery
{
    Task<IEnumerable<ContentItem>> GetTrendingAsync(
        ContentType? type,
        int limit,
        TimeSpan window,
        CancellationToken cancellationToken = default);
}
