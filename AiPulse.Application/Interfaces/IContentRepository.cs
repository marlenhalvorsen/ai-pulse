using AiPulse.Domain.Models;

namespace AiPulse.Application.Interfaces;

public interface IContentRepository
{
    Task UpsertAsync(IEnumerable<ContentItem> items, CancellationToken cancellationToken = default);
    Task<IEnumerable<ContentItem>> GetAllAsync(CancellationToken cancellationToken = default);
}
