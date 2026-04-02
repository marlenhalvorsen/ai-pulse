namespace AiPulse.Infrastructure.Configuration;

public class DevToSettings
{
    public string BaseUrl { get; init; } = "https://dev.to";
    public string[] Tags { get; init; } = [];
    public int ArticlesPerTag { get; init; } = 10;
    public string UserAgent { get; init; } = "ai-pulse/1.0";
}
