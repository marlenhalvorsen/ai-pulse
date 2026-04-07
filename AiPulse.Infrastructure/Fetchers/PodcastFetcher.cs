using AiPulse.Application.Interfaces;
using AiPulse.Domain.Models;
using AiPulse.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AiPulse.Infrastructure.Fetchers;

public class PodcastFetcher : ITrendFetcher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PodcastSettings _settings;

    public PodcastFetcher(
        IHttpClientFactory httpClientFactory,
        IOptions<PodcastSettings> settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
    }

    public Task<IEnumerable<ContentItem>> FetchAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotImplementedException();
    }
}
