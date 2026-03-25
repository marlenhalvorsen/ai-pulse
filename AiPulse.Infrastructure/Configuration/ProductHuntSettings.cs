namespace AiPulse.Infrastructure.Configuration;

public class ProductHuntSettings
{
    public string BaseUrl { get; init; } = "https://api.producthunt.com";
    public string DeveloperToken { get; init; } = string.Empty;
    public int PostLimit { get; init; } = 20;
}
