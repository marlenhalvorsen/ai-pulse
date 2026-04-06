using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using AiPulse.Application.Interfaces;
using AiPulse.Domain.Enums;
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

    public async Task<IEnumerable<ContentItem>> FetchAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = _httpClientFactory.CreateClient("GitHubTrending");
        var response = await client.GetAsync("/trending", cancellationToken);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return await ParseReposAsync(html);
    }

    private async Task<IEnumerable<ContentItem>> ParseReposAsync(string html)
    {
        var context = BrowsingContext.New(AngleSharp.Configuration.Default);
        var document = await context.OpenAsync(req => req.Content(html));

        var items = new List<ContentItem>();

        foreach (var article in document.QuerySelectorAll("article.Box-row"))
        {
            if (items.Count >= _settings.RepoLimit)
                break;

            var item = ParseRepo(article);
            if (item is null)
                continue;

            var searchText = $"{item.Title} {GetDescription(article)}";
            if (IsAiRelated(searchText))
                items.Add(item);
        }

        return items;
    }

    private static ContentItem? ParseRepo(IElement article)
    {
        var link = article.QuerySelector("h2 a");
        if (link is null)
            return null;

        var href = link.GetAttribute("href")?.Trim('/');
        if (string.IsNullOrWhiteSpace(href))
            return null;

        var parts = href.Split('/');
        if (parts.Length < 2)
            return null;

        var owner = parts[0];
        var repo = parts[1];
        var title = $"{owner}/{repo}";
        var url = $"https://github.com/{owner}/{repo}";

        return new ContentItem
        {
            Id = $"github_{owner}_{repo}",
            Title = title,
            Url = url,
            Source = SourceType.GitHub,
            ContentType = ContentType.Repository,
            Upvotes = ParseStarsToday(article),
            CommentCount = 0,
            PostedAt = DateTime.UtcNow
        };
    }

    private static string GetDescription(IElement article)
    {
        var descEl = article.QuerySelector("p.col-9");
        return descEl?.TextContent.Trim() ?? string.Empty;
    }

    private static int ParseStarsToday(IElement article)
    {
        foreach (var span in article.QuerySelectorAll("span"))
        {
            var text = span.TextContent.Trim();
            if (!text.Contains("stars today", StringComparison.OrdinalIgnoreCase))
                continue;

            var digits = Regex.Replace(text, @"[^\d]", string.Empty,
                RegexOptions.None, TimeSpan.FromMilliseconds(100));
            if (int.TryParse(digits, out var stars))
                return stars;
        }

        return 0;
    }

    private bool IsAiRelated(string text) =>
        _settings.AiKeywords.Any(kw =>
            Regex.IsMatch(text, $@"\b{Regex.Escape(kw)}(?:s?\b)", RegexOptions.IgnoreCase));
}
