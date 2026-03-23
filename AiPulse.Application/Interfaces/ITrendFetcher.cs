using AiPulse.Domain.Models;

namespace AiPulse.Application.Interfaces;

public interface ITrendFetcher
{
    Task<IEnumerable<ContentItem>> FetchAsync(CancellationToken cancellationToken = default);
}
