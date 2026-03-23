using AiPulse.Domain.Enums;

namespace AiPulse.Application.Services;

public class UrlClassifier
{
    public ContentType Classify(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return ContentType.Discussion;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return ContentType.Article;

        var host = uri.Host.ToLowerInvariant();
        var path = uri.AbsolutePath.ToLowerInvariant();

        // Video
        if (host is "www.youtube.com" or "youtube.com" or "youtu.be" ||
            host.EndsWith(".youtube.com"))
            return ContentType.Video;

        // Podcast
        if ((host is "open.spotify.com" or "spotify.com" || host.EndsWith(".spotify.com"))
            && path.StartsWith("/episode"))
            return ContentType.Podcast;

        if (host is "podcasts.apple.com")
            return ContentType.Podcast;

        // Research Paper
        if (host is "arxiv.org" || host.StartsWith("papers."))
            return ContentType.ResearchPaper;

        // Newsletter
        if (host.EndsWith(".substack.com") || host.EndsWith(".beehiiv.com"))
            return ContentType.Newsletter;

        // Discussion — HackerNews items
        if (host is "news.ycombinator.com" && path.StartsWith("/item"))
            return ContentType.Discussion;

        // Discussion — Reddit comment threads
        if ((host is "www.reddit.com" or "reddit.com" || host.EndsWith(".reddit.com"))
            && System.Text.RegularExpressions.Regex.IsMatch(path, @"^/r/[^/]+/comments/"))
            return ContentType.Discussion;

        return ContentType.Article;
    }
}
