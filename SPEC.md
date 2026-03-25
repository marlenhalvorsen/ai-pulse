# AI Pulse — Technical Specification

> This file is the detailed reference. Claude Code reads CLAUDE.md for instructions. Humans read this for the full picture.

---

## 1. Vision

The AI world moves fast. New papers, tools, videos, podcasts, and debates emerge daily — scattered across Reddit, HackerNews, YouTube, Spotify, and dozens of newsletters. There is no single place that shows you what the AI community is *actually* talking about **right now**, across all formats, ranked by real social momentum rather than all-time popularity.

**AI Pulse** solves this. The trending signal is: *what are people sharing and discussing in the last 7 days*, not what has the most lifetime views.

---

## 2. Technology Stack

| Concern | Choice |
|---|---|
| Backend | C# .NET 8 — ASP.NET Core Web API (minimal API style) |
| Frontend | Blazor Server (single deployable unit) |
| Database | SQLite via Entity Framework Core |
| Background Jobs | Hangfire (in-process, periodic Reddit + HN fetches) |
| HTTP Client | `IHttpClientFactory` + Polly retry/circuit-breaker |
| Testing | xUnit + Moq + FluentAssertions + NetArchTest |
| Hosting | Self-contained .NET publish — Linux/Windows VPS |

---

## 3. Architecture & Engineering Principles

### Layer Map (SoC)

Strict dependency rule: **Domain ← Application ← Infrastructure ← Web**

```
AiPulse.Domain          # Pure models, no dependencies
AiPulse.Application     # Use cases, interfaces, DTOs — references Domain only
AiPulse.Infrastructure  # Reddit client, HN client, EF Core, Hangfire — references Application
AiPulse.Web             # ASP.NET host, DI wiring, Blazor pages — references Application only
AiPulse.Tests           # All tests — xUnit + Moq + NetArchTest
```

Dependency rule enforced by .csproj project references. Web never references Infrastructure by type.

### File Structure

```
AiPulse/
  AiPulse.Domain/
    Models/ContentItem.cs
    Models/TrendScore.cs
    Enums/ContentType.cs          # Video, Podcast, Article, Newsletter, ResearchPaper, Discussion
    Enums/SourceType.cs           # Reddit, HackerNews
  AiPulse.Application/
    Interfaces/ITrendFetcher.cs
    Interfaces/IContentRepository.cs
    Interfaces/ITrendingQuery.cs
    UseCases/GetTrendingItemsQuery.cs
    DTOs/TrendingItemDto.cs
    Services/TrendScoreCalculator.cs
    Services/UrlClassifier.cs
  AiPulse.Infrastructure/
    Fetchers/RedditFetcher.cs
    Fetchers/HackerNewsFetcher.cs
    Persistence/AppDbContext.cs
    Persistence/ContentRepository.cs
    Jobs/TrendRefreshJob.cs
    DependencyInjection.cs
  AiPulse.Web/
    Program.cs
    Pages/Index.razor              # Row-based trending layout
    Api/TrendingEndpoints.cs       # GET /api/trending
    Middleware/SecurityHeadersMiddleware.cs
    appsettings.json
  AiPulse.Tests/
    Domain/TrendScoreCalculatorTests.cs
    Domain/UrlClassifierTests.cs
    Infrastructure/RedditFetcherTests.cs
    Infrastructure/HackerNewsFetcherTests.cs
    Api/TrendingEndpointTests.cs
    Architecture/LayerBoundaryTests.cs
```

### SOLID

| Principle | How it applies |
|---|---|
| **S** Single Responsibility | `RedditFetcher` only fetches. `UrlClassifier` only classifies. `TrendScoreCalculator` only scores. |
| **O** Open/Closed | Adding a new source = implement `ITrendFetcher`. No existing class modified. |
| **L** Liskov Substitution | `MockTrendFetcher` and `RedditFetcher` are interchangeable — both pass contract tests. |
| **I** Interface Segregation | `ITrendFetcher` (fetch), `IContentRepository` (read/write), `ITrendingQuery` (read-only). No fat interfaces. |
| **D** Dependency Inversion | All dependencies injected via constructor. `new ConcreteClass()` only in `Program.cs`. |

### DRY

- `TrendScoreCalculator` — one class, one place for scoring logic
- `UrlClassifier` — one class, one place for URL→ContentType mapping
- Subreddit lists, API base URLs, decay constants — in `appsettings.json`, never hardcoded
- Shared test fixtures extracted into `TestBase`

---

## 4. MoSCoW Prioritisation

### ✅ Must Have

- GitHub Actions CI/CD — runs `dotnet build` + `dotnet test` on every PR, blocks merge if tests fail
- TrendScoreCalculator with 7-day decay window
- RedditFetcher (r/MachineLearning, r/artificial, r/ChatGPT, r/LocalLLaMA, r/singularity)
- HackerNewsFetcher (topstories + beststories, AI keyword filter)
- UrlClassifier mapping links to ContentType
- ContentRepository (EF Core + SQLite)
- TrendRefreshJob via Hangfire (every 30 minutes)
- Public read-only API: `GET /api/trending?type=&limit=`
- Blazor frontend — row-based layout: Podcasts · Videos · Articles · Newsletters · Research · Discussions
- Responsive — works on mobile and desktop
- Privacy by Design: no cookies, no tracking, no PII collected anywhere
- Security headers middleware
- Full TDD test suite for all domain logic and API endpoints
- Architecture boundary tests (NetArchTest)

### 🔵 Should Have

- Keyword search across trending item titles
- Filter by time window: Last 24h / Last 7 days
- Source badge on each card (Reddit / HackerNews)
- Click-through count tracked server-side (no JS analytics, no fingerprinting)
- Rate limiting on API endpoints
- Structured logging with Serilog (file sink only)
- Health check endpoint: `GET /health`
- OpenAPI / Swagger docs auto-generated
- Additional content sources: Dev.to, Lobste.rs, GitHub Trending, Product Hunt, Arxiv (essential for comprehensive AI aggregation)

### 🟡 Could Have

- RSS feed output of current trending items
- Dark / light mode toggle (CSS custom properties, localStorage only)
- Share button generating a direct link to a specific item
- Trending sparkline chart (score over 7 days)
- Admin flag to hide inappropriate items (env-var protected, no login)

### 🔴 Won't Have

- User accounts, login, or any authentication
- Personalisation or recommendation algorithms
- Paid tiers or paywalled features
- Scraping third-party sites (public APIs only)
- Embedding podcast audio or YouTube video players (links out only)
- Push notifications or email digests (no PII = impossible by design)
- Native mobile apps
- Social login (Google, GitHub)

---

## 5. Data Sources & Trending Signal

### Sources (free, public APIs only)

| Source | API | Endpoints |
|---|---|---|
| Reddit | Free JSON API (no key needed) | r/MachineLearning, r/artificial, r/ChatGPT, r/LocalLLaMA, r/singularity |
| HackerNews | Open Firebase API | /topstories, /beststories — filtered by AI keywords |

### TrendScore Formula

```
TrendScore = (upvotes × 0.6) + (comments × 0.3) + (recency_boost × 0.1)
```

- Items older than 7 days decay to zero
- Refreshed every 30 minutes via Hangfire
- All scoring logic lives exclusively in `TrendScoreCalculator`

---

## 6. API Contract

### `GET /api/trending`

Query parameters:
- `type` (optional) — `video`, `podcast`, `article`, `newsletter`, `research`, `discussion`
- `limit` (optional, default 10, max 50)
- `window` (optional, default `week`) — `day` or `week`

Response shape:
```json
{
  "generatedAt": "2026-03-17T10:00:00Z",
  "rows": [
    {
      "contentType": "Video",
      "items": [
        {
          "id": "abc123",
          "title": "Why GPT-5 changes everything",
          "url": "https://youtube.com/watch?v=...",
          "sourceName": "Reddit · r/MachineLearning",
          "trendScore": 847.3,
          "upvotes": 1243,
          "commentCount": 89,
          "postedAt": "2026-03-16T14:22:00Z",
          "contentType": "Video"
        }
      ]
    }
  ]
}
```

---

## 7. Privacy & Security

### What the application NEVER does

- Never sets any identifying cookie
- Never loads third-party analytics scripts
- Never logs IP addresses
- Never stores information about who clicked what
- Never makes client-side calls to external APIs

### Security Headers

Implemented in `SecurityHeadersMiddleware` from day one:

```
Content-Security-Policy:   default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: i.ytimg.com; frame-ancestors 'none'
X-Frame-Options:           DENY
X-Content-Type-Options:    nosniff
Referrer-Policy:           no-referrer
Permissions-Policy:        camera=(), microphone=(), geolocation=()
Strict-Transport-Security: max-age=63072000; includeSubDomains
Server header:             Remove entirely
```

### Input Validation

- All query parameters validated and sanitised
- EF Core parameterised queries only — no raw SQL
- URLs from Reddit/HN validated against allowlist before storing
- All output HTML-encoded by Blazor by default
- Outbound HTTP: strict 5s timeout + Polly circuit breaker

### Dependency Security

- NuGet packages locked via `packages.lock.json`
- No npm / JavaScript build pipeline

---

*AI Pulse — Free. Open. No login. No tracking. Just the AI conversation happening right now.*
