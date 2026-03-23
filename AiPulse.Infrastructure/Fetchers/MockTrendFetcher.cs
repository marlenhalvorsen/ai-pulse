using AiPulse.Application.Interfaces;
using AiPulse.Domain.Enums;
using AiPulse.Domain.Models;

namespace AiPulse.Infrastructure.Fetchers;

public class MockTrendFetcher : ITrendFetcher
{
    private static readonly IReadOnlyList<ContentItem> Items =
    [
        new ContentItem
        {
            Id = "mock-001",
            Title = "GPT-5 released with major reasoning improvements",
            Url = "https://www.reddit.com/r/MachineLearning/comments/mock001/gpt5_released",
            Source = SourceType.Reddit,
            ContentType = ContentType.Discussion,
            Upvotes = 4200,
            CommentCount = 318,
            PostedAt = DateTime.UtcNow.AddHours(-6)
        },
        new ContentItem
        {
            Id = "mock-002",
            Title = "Attention Is All You Need — revisited three years later",
            Url = "https://arxiv.org/abs/mock002",
            Source = SourceType.Reddit,
            ContentType = ContentType.ResearchPaper,
            Upvotes = 1850,
            CommentCount = 92,
            PostedAt = DateTime.UtcNow.AddHours(-14)
        },
        new ContentItem
        {
            Id = "mock-003",
            Title = "Running LLaMA 3 locally on a MacBook — full walkthrough",
            Url = "https://www.youtube.com/watch?v=mockv003",
            Source = SourceType.Reddit,
            ContentType = ContentType.Video,
            Upvotes = 3100,
            CommentCount = 204,
            PostedAt = DateTime.UtcNow.AddHours(-30)
        },
        new ContentItem
        {
            Id = "mock-004",
            Title = "The Gradient Podcast: Yann LeCun on the limits of LLMs",
            Url = "https://open.spotify.com/episode/mockpod004",
            Source = SourceType.Reddit,
            ContentType = ContentType.Podcast,
            Upvotes = 720,
            CommentCount = 45,
            PostedAt = DateTime.UtcNow.AddDays(-2)
        },
        new ContentItem
        {
            Id = "mock-005",
            Title = "The Batch — Issue 247: State of AI in 2026",
            Url = "https://deeplearning.beehiiv.com/p/batch-247",
            Source = SourceType.Reddit,
            ContentType = ContentType.Newsletter,
            Upvotes = 540,
            CommentCount = 28,
            PostedAt = DateTime.UtcNow.AddDays(-3)
        }
    ];

    public Task<IEnumerable<ContentItem>> FetchAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IEnumerable<ContentItem>>(Items);
    }
}
