using System.Net;
using AiPulse.Domain.Enums;
using AiPulse.Infrastructure.Configuration;
using AiPulse.Infrastructure.Fetchers;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace AiPulse.Tests.Infrastructure;

public class GitHubTrendingFetcherTests
{
    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(handler(request));
        }
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private static HttpResponseMessage OkHtml(string html) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html")
        };

    private static GitHubTrendingFetcher CreateFetcher(
        Func<HttpRequestMessage, HttpResponseMessage> handler,
        string[]? keywords = null,
        int repoLimit = 25,
        string userAgent = "ai-pulse/1.0")
    {
        var client = new HttpClient(new FakeHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("https://github.com")
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        var settings = Options.Create(new GitHubTrendingSettings
        {
            BaseUrl = "https://github.com",
            RepoLimit = repoLimit,
            UserAgent = userAgent,
            AiKeywords = keywords ?? ["llm", "ai", "machine learning", "openai"]
        });
        return new GitHubTrendingFetcher(new StubHttpClientFactory(client), settings);
    }

    private const string AiRepoHtml = """
        <article class="Box-row">
          <h2 class="h3 lh-condensed">
            <a href="/microsoft/semantic-kernel"><span class="text-normal">microsoft / </span>semantic-kernel</a>
          </h2>
          <p class="col-9 color-fg-muted my-1 pr-4">Integrate LLM technology quickly</p>
          <div class="f6 color-fg-muted mt-2">
            <span class="d-inline-block ml-0 mr-3">1,234 stars today</span>
          </div>
        </article>
        """;

    private const string NonAiRepoHtml = """
        <article class="Box-row">
          <h2 class="h3 lh-condensed">
            <a href="/rails/rails"><span class="text-normal">rails / </span>rails</a>
          </h2>
          <p class="col-9 color-fg-muted my-1 pr-4">Ruby on Rails web framework</p>
          <div class="f6 color-fg-muted mt-2">
            <span class="d-inline-block ml-0 mr-3">50 stars today</span>
          </div>
        </article>
        """;

    private static string WrapHtml(string articles) =>
        $"<!DOCTYPE html><html><body>{articles}</body></html>";

    [Fact]
    public async Task FetchAsync_FiltersOutNonAiRepos()
    {
        var sut = CreateFetcher(_ => OkHtml(WrapHtml(AiRepoHtml + NonAiRepoHtml)));

        var items = (await sut.FetchAsync()).ToList();

        items.Should().NotContain(i => i.Id == "github_rails_rails");
    }

    [Fact]
    public async Task FetchAsync_IncludesReposMatchingAiKeywords()
    {
        var sut = CreateFetcher(_ => OkHtml(WrapHtml(AiRepoHtml + NonAiRepoHtml)));

        var items = (await sut.FetchAsync()).ToList();

        items.Should().Contain(i => i.Id == "github_microsoft_semantic-kernel");
    }

    [Fact]
    public async Task FetchAsync_MapsRepoFieldsToContentItem()
    {
        var sut = CreateFetcher(_ => OkHtml(WrapHtml(AiRepoHtml)));

        var item = (await sut.FetchAsync()).Single();

        item.Id.Should().Be("github_microsoft_semantic-kernel");
        item.Title.Should().Be("microsoft/semantic-kernel");
        item.Url.Should().Be("https://github.com/microsoft/semantic-kernel");
        item.Upvotes.Should().Be(1234);
    }

    [Fact]
    public async Task FetchAsync_SetsSourceTypeToGitHub()
    {
        var sut = CreateFetcher(_ => OkHtml(WrapHtml(AiRepoHtml)));

        var items = await sut.FetchAsync();

        items.Should().AllSatisfy(i => i.Source.Should().Be(SourceType.GitHub));
    }

    [Fact]
    public async Task FetchAsync_SetsContentTypeToRepository()
    {
        var sut = CreateFetcher(_ => OkHtml(WrapHtml(AiRepoHtml)));

        var items = await sut.FetchAsync();

        items.Should().AllSatisfy(i => i.ContentType.Should().Be(ContentType.Repository));
    }

    [Fact]
    public async Task FetchAsync_PostedAt_IsRecent()
    {
        var sut = CreateFetcher(_ => OkHtml(WrapHtml(AiRepoHtml)));
        var before = DateTime.UtcNow;

        var items = (await sut.FetchAsync()).ToList();

        items.Should().AllSatisfy(i =>
            i.PostedAt.Should().BeOnOrAfter(before.AddMinutes(-1)).And.BeBefore(DateTime.UtcNow.AddMinutes(1)));
    }

    [Fact]
    public async Task FetchAsync_LimitsResultsToRepoLimit()
    {
        var repos = string.Concat(Enumerable.Range(1, 5).Select(i => $"""
            <article class="Box-row">
              <h2 class="h3 lh-condensed">
                <a href="/owner/llm-repo-{i}"><span class="text-normal">owner / </span>llm-repo-{i}</a>
              </h2>
              <p class="col-9 color-fg-muted my-1 pr-4">An LLM toolkit</p>
              <div class="f6 color-fg-muted mt-2">
                <span class="d-inline-block ml-0 mr-3">{i * 10} stars today</span>
              </div>
            </article>
            """));
        var sut = CreateFetcher(_ => OkHtml(WrapHtml(repos)), repoLimit: 3);

        var items = (await sut.FetchAsync()).ToList();

        items.Should().HaveCount(3);
    }

    [Fact]
    public async Task FetchAsync_SendsUserAgentHeader()
    {
        HttpRequestMessage? captured = null;
        var sut = CreateFetcher(req =>
        {
            captured = req;
            return OkHtml(WrapHtml(string.Empty));
        });

        await sut.FetchAsync();

        captured!.Headers.UserAgent.ToString().Should().Be("ai-pulse/1.0");
    }

    [Fact]
    public async Task FetchAsync_WhenHttpFails_ThrowsHttpRequestException()
    {
        var sut = CreateFetcher(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var act = async () => await sut.FetchAsync();

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task FetchAsync_RespectsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var sut = CreateFetcher(_ => OkHtml(WrapHtml(string.Empty)));

        var act = async () => await sut.FetchAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task FetchAsync_EmptyPage_ReturnsEmpty()
    {
        var sut = CreateFetcher(_ => OkHtml(WrapHtml(string.Empty)));

        var items = await sut.FetchAsync();

        items.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchAsync_KeywordMatchesDescription_IncludesRepo()
    {
        const string repoWithAiDescription = """
            <article class="Box-row">
              <h2 class="h3 lh-condensed">
                <a href="/some/cool-tool"><span class="text-normal">some / </span>cool-tool</a>
              </h2>
              <p class="col-9 color-fg-muted my-1 pr-4">A machine learning framework for production</p>
              <div class="f6 color-fg-muted mt-2">
                <span class="d-inline-block ml-0 mr-3">500 stars today</span>
              </div>
            </article>
            """;
        var sut = CreateFetcher(_ => OkHtml(WrapHtml(repoWithAiDescription)), keywords: ["machine learning"]);

        var items = (await sut.FetchAsync()).ToList();

        items.Should().Contain(i => i.Id == "github_some_cool-tool");
    }

    [Fact]
    public async Task FetchAsync_StarsWithCommas_ParsedCorrectly()
    {
        const string repoWithManyStars = """
            <article class="Box-row">
              <h2 class="h3 lh-condensed">
                <a href="/openai/llm-toolkit"><span class="text-normal">openai / </span>llm-toolkit</a>
              </h2>
              <p class="col-9 color-fg-muted my-1 pr-4">OpenAI LLM tools</p>
              <div class="f6 color-fg-muted mt-2">
                <span class="d-inline-block ml-0 mr-3">12,345 stars today</span>
              </div>
            </article>
            """;
        var sut = CreateFetcher(_ => OkHtml(WrapHtml(repoWithManyStars)));

        var item = (await sut.FetchAsync()).Single();

        item.Upvotes.Should().Be(12345);
    }
}
