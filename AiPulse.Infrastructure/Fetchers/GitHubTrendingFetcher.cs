using AiPulse.Application.Interfaces;
using AiPulse.Domain.Models;
using AiPulse.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AiPulse.Infrastructure.Fetchers;

public class GitHubTrendingFetcher : ITrendFetcher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GitHubTrendingSettings _settings;

    public GitHubTrendingFetcher(
        IHttpClientFactory httpClientFactory,
        IOptions<GitHubTrendingSettings> settings)
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
