using AiPulse.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace AiPulse.Infrastructure.Jobs;

public class TrendRefreshJob
{
    private readonly IEnumerable<ITrendFetcher> _fetchers;
    private readonly IContentRepository _repository;
    private readonly ILogger<TrendRefreshJob> _logger;

    public TrendRefreshJob(
        IEnumerable<ITrendFetcher> fetchers,
        IContentRepository repository,
        ILogger<TrendRefreshJob> logger)
    {
        _fetchers = fetchers;
        _repository = repository;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        foreach (var fetcher in _fetchers)
        {
            try
            {
                var items = await fetcher.FetchAsync(cancellationToken);
                await _repository.UpsertAsync(items, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fetcher {Fetcher} failed during trend refresh", fetcher.GetType().Name);
            }
        }
    }
}
