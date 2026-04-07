namespace AiPulse.Infrastructure.Configuration;

public class PodcastSettings
{
    public string UserAgent { get; init; } = "ai-pulse/1.0";
    public int EpisodesPerShow { get; init; } = 3;
    public CuratedPodcastFeed[] CuratedFeeds { get; init; } = [];
}

public class CuratedPodcastFeed
{
    public string ShowName { get; init; } = string.Empty;
    public string RssUrl { get; init; } = string.Empty;
}
