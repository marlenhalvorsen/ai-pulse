using AiPulse.Application.Services;
using AiPulse.Domain.Enums;
using FluentAssertions;

namespace AiPulse.Tests.Domain;

public class UrlClassifierTests
{
    private readonly UrlClassifier _sut = new();

    // --- Video ---

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtube.com/watch?v=abc123")]
    [InlineData("https://youtu.be/abc123")]
    [InlineData("https://m.youtube.com/watch?v=xyz")]
    public void Classify_YouTubeUrl_ReturnsVideo(string url)
    {
        _sut.Classify(url).Should().Be(ContentType.Video);
    }

    // --- Podcast ---

    [Theory]
    [InlineData("https://open.spotify.com/episode/3skn4bHIBJRjvCNYhZQ4uW")]
    [InlineData("https://spotify.com/episode/abc")]
    [InlineData("https://podcasts.apple.com/us/podcast/lex-fridman-podcast/id1434243584")]
    public void Classify_PodcastUrl_ReturnsPodcast(string url)
    {
        _sut.Classify(url).Should().Be(ContentType.Podcast);
    }

    // --- Research Paper ---

    [Theory]
    [InlineData("https://arxiv.org/abs/2303.08774")]
    [InlineData("https://arxiv.org/pdf/2303.08774")]
    [InlineData("https://papers.nips.cc/paper/2022/hash/abc")]
    [InlineData("https://papers.ssrn.com/sol3/papers.cfm?abstract_id=123")]
    public void Classify_ResearchPaperUrl_ReturnsResearchPaper(string url)
    {
        _sut.Classify(url).Should().Be(ContentType.ResearchPaper);
    }

    // --- Newsletter ---

    [Theory]
    [InlineData("https://simonwillison.substack.com/p/ai-weekly")]
    [InlineData("https://newsletter.substack.com/p/something")]
    [InlineData("https://aiweekly.beehiiv.com/p/issue-42")]
    [InlineData("https://example.beehiiv.com/subscribe")]
    public void Classify_NewsletterUrl_ReturnsNewsletter(string url)
    {
        _sut.Classify(url).Should().Be(ContentType.Newsletter);
    }

    // --- Discussion (self-post / null / empty) ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Classify_NullOrEmptyUrl_ReturnsDiscussion(string? url)
    {
        _sut.Classify(url).Should().Be(ContentType.Discussion);
    }

    // --- Article (catch-all) ---

    [Theory]
    [InlineData("https://techcrunch.com/2024/01/01/gpt-5-released")]
    [InlineData("https://github.com/openai/openai-python")]
    [InlineData("https://huggingface.co/blog/llama-3")]
    [InlineData("https://example.com/some-random-article")]
    public void Classify_GenericUrl_ReturnsArticle(string url)
    {
        _sut.Classify(url).Should().Be(ContentType.Article);
    }

    // --- Spotify without /episode should NOT be Podcast ---

    [Fact]
    public void Classify_SpotifyAlbumUrl_ReturnsArticle()
    {
        _sut.Classify("https://open.spotify.com/album/6rqhFgbbKwnb9MLmUQDhG6")
            .Should().Be(ContentType.Article);
    }

    // --- Case-insensitive host matching ---

    [Fact]
    public void Classify_UpperCaseYouTubeHost_ReturnsVideo()
    {
        _sut.Classify("HTTPS://WWW.YOUTUBE.COM/watch?v=test")
            .Should().Be(ContentType.Video);
    }
}
