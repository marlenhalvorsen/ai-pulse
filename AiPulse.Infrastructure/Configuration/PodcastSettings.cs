namespace AiPulse.Infrastructure.Configuration;

public class PodcastSettings
{
    public string TopChartsUrl { get; init; } = "https://itunes.apple.com/us/rss/toppodcasts/limit=25/genre=1318/json";
    public string LookupBaseUrl { get; init; } = "https://itunes.apple.com/lookup";
    public string UserAgent { get; init; } = "ai-pulse/1.0";
    public string[] AiKeywords { get; init; } = [];
    public CuratedPodcastFeed[] CuratedFeeds { get; init; } = [];
    public int EpisodesPerShow { get; init; } = 1;
}

public class CuratedPodcastFeed
{
    public string ShowName { get; init; } = string.Empty;
    public string RssUrl { get; init; } = string.Empty;
}
