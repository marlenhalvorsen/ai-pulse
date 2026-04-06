namespace AiPulse.Infrastructure.Configuration;

public class GitHubTrendingSettings
{
    public string BaseUrl { get; init; } = "https://github.com";
    public int RepoLimit { get; init; } = 25;
    public string UserAgent { get; init; } = "ai-pulse/1.0";
    public string[] AiKeywords { get; init; } = [];
}
