namespace AiPulse.Infrastructure.Configuration;

public class HackerNewsSettings
{
    public string BaseUrl { get; init; } = "https://hacker-news.firebaseio.com";
    public int StoryLimit { get; init; } = 50;
    public string[] AiKeywords { get; init; } = [];
}
