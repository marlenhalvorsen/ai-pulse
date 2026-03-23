namespace AiPulse.Infrastructure.Configuration;

public class RedditSettings
{
    public string[] Subreddits { get; init; } = [];
    public string BaseUrl { get; init; } = "https://www.reddit.com";
    public int PostsPerSubreddit { get; init; } = 25;
    public string UserAgent { get; init; } = "web:ai-pulse:v1.0 (by /u/ai-pulse-app)";
}
