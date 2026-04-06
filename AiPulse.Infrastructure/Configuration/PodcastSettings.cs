namespace AiPulse.Infrastructure.Configuration;

public class PodcastSettings
{
    public int EpisodesPerFeed { get; init; } = 10;
    public string UserAgent { get; init; } = "ai-pulse/1.0";
    public PodcastFeed[] Feeds { get; init; } = [];
}

public class PodcastFeed
{
    public string Name { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
}
