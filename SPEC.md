# AI Pulse — Technical Specification

> This file is the detailed reference. Claude Code reads CLAUDE.md for instructions. Humans read this for the full picture.

---

## 1. Vision

The AI world moves fast. New papers, tools, videos, podcasts, and debates emerge daily — scattered across HackerNews, YouTube, Spotify, and dozens of newsletters. There is no single place that shows you what the AI community is *actually* talking about **right now**, across all formats, ranked by real social momentum rather than all-time popularity.

**AI Pulse** solves this. The trending signal is: *what are people sharing and discussing in the last 7 days*, not what has the most lifetime views.

---

## 2. Technology Stack

| Concern | Choice |
|---|---|
| API | C# .NET 8 — ASP.NET Core Web API, minimal API style (`AiPulse.Api`) |
| Frontend | Blazor WASM (`AiPulse.Client`) |
| Database | SQLite via Entity Framework Core |
| Background Jobs | Hangfire with SQLite storage (persists across restarts) |
| HTTP Client | `IHttpClientFactory` per named client |
| Testing | xUnit + Moq + FluentAssertions + NetArchTest |
| API hosting | Oracle Cloud free-tier VM · Cloudflare Tunnel · systemd |
| Frontend hosting | Cloudflare Pages |
| CI/CD | GitHub Actions |

---

## 3. Architecture & Engineering Principles

### Layer Map

Strict dependency rule: **Domain ← Application ← Infrastructure ← Api**

```
AiPulse.Domain          # Pure models, enums — no dependencies
AiPulse.Application     # Use cases, interfaces, DTOs — references Domain only
AiPulse.Infrastructure  # Fetchers, EF Core, Hangfire — references Application
AiPulse.Api             # ASP.NET host, DI wiring, endpoints — references Application
AiPulse.Client          # Blazor WASM frontend — references Application + Domain
AiPulse.Tests           # All tests — xUnit + Moq + NetArchTest
```

Dependency rule enforced by .csproj project references. `AiPulse.Api` never references Infrastructure by type.

### File Structure

```
AiPulse/
  AiPulse.Domain/
    Models/ContentItem.cs
    Models/TrendScore.cs
    Enums/ContentType.cs      # Video, Podcast, Article, Newsletter, ResearchPaper, Discussion
    Enums/SourceType.cs       # HackerNews, DevTo, GitHub, ProductHunt, Podcast, Reddit
  AiPulse.Application/
    Interfaces/
      ITrendFetcher.cs
      IContentRepository.cs
      ITrendingQuery.cs
    UseCases/
      GetTrendingItemsQuery.cs
      GetSourceItemsQuery.cs
    DTOs/TrendingItemDto.cs
    Services/
      TrendScoreCalculator.cs
      UrlClassifier.cs
  AiPulse.Infrastructure/
    Fetchers/
      HackerNewsFetcher.cs
      DevToFetcher.cs
      GitHubTrendingFetcher.cs
      ProductHuntFetcher.cs
      PodcastFetcher.cs
      RedditFetcher.cs          # present but not reliable in production
    Persistence/
      AppDbContext.cs
      ContentRepository.cs
    Jobs/
      TrendRefreshJob.cs
      PodcastDescriptionCleanup.cs
    Configuration/              # typed settings classes per source
    DependencyInjection.cs
  AiPulse.Api/
    Program.cs
    Api/
      TrendingEndpoints.cs      # GET /api/trending, GET /api/source/{name}
      IngestEndpoints.cs        # POST /api/ingest/reddit
    Middleware/SecurityHeadersMiddleware.cs
    appsettings.json
  AiPulse.Client/
    Pages/
      Index.razor               # Row-based trending layout
      Source.razor              # Per-source view
    Components/
      TrendingRow.razor
      TrendingCard.razor
      SiteLogo.razor
    wwwroot/
      appsettings.json          # ApiBaseUrl
  AiPulse.Tests/
    Domain/
      TrendScoreCalculatorTests.cs
      UrlClassifierTests.cs
    Infrastructure/
      HackerNewsFetcherTests.cs
      DevToFetcherTests.cs
      GitHubTrendingFetcherTests.cs
      ProductHuntFetcherTests.cs
      PodcastFetcherTests.cs
      RedditFetcherTests.cs
      ContentRepositoryTests.cs
      TrendRefreshJobTests.cs
    Api/
      TrendingEndpointTests.cs
      SourceEndpointTests.cs
      IngestEndpointTests.cs
    Architecture/LayerBoundaryTests.cs
```

### SOLID

| Principle | How it applies |
|---|---|
| **S** Single Responsibility | Each fetcher fetches one source. `UrlClassifier` only classifies. `TrendScoreCalculator` only scores. |
| **O** Open/Closed | Adding a new source = implement `ITrendFetcher`. No existing class modified. |
| **L** Liskov Substitution | `MockTrendFetcher` and all real fetchers are interchangeable — all pass contract tests. |
| **I** Interface Segregation | `ITrendFetcher` (fetch), `IContentRepository` (read/write), `ITrendingQuery` (read-only). No fat interfaces. |
| **D** Dependency Inversion | All dependencies injected via constructor. `new ConcreteClass()` only in `Program.cs`. |

---

## 4. MoSCoW Prioritisation

### ✅ Must Have (complete)

- GitHub Actions CI/CD — `dotnet build` + `dotnet test` on every PR, blocks merge on failure
- TrendScoreCalculator with 7-day decay window
- HackerNewsFetcher (topstories + beststories, AI keyword filter)
- DevToFetcher (AI tags)
- GitHubTrendingFetcher (AI keyword filter)
- ProductHuntFetcher
- PodcastFetcher (curated RSS feeds)
- UrlClassifier mapping links to ContentType
- ContentRepository (EF Core + SQLite)
- TrendRefreshJob via Hangfire (every 30 minutes, SQLite-backed)
- Public read-only API: `GET /api/trending`, `GET /api/source/{name}`
- Secret-gated ingest trigger: `POST /api/ingest/reddit`
- Blazor WASM frontend — row-based layout per content type
- Rate limiting (60 req/min per IP, 429 on breach)
- Security headers middleware (CSP, HSTS, X-Frame-Options, etc.)
- CORS locked to known frontend origins
- Full TDD test suite for all domain logic, fetchers, and API endpoints
- Architecture boundary tests (NetArchTest)
- Deploy pipeline: GitHub Actions → Oracle Cloud VM (API) + Cloudflare Pages (frontend)
- Cloudflare Tunnel exposing API at `api.marlenhalvorsen.dev` (no open inbound ports)
- Daily GitHub Actions scheduled trigger for data refresh

### 🔵 Should Have

- RedditFetcher reliable in production (currently present but bypassed due to datacenter IP blocking)
- Keyword search across trending item titles
- Filter by time window: Last 24h / Last 7 days
- Health check endpoint: `GET /health`
- Structured logging

### 🟡 Could Have

- RSS feed output of current trending items
- Dark / light mode toggle
- Trending sparkline chart (score over 7 days)
- OpenAPI / Swagger docs

### 🔴 Won't Have

- User accounts, login, or any authentication
- Personalisation or recommendation algorithms
- Paid tiers or paywalled features
- Scraping third-party sites (public APIs only)
- Native mobile apps
- Push notifications or email digests

---

## 5. Data Sources & Trending Signal

### Active Sources

| Source | Fetcher | Notes |
|---|---|---|
| HackerNews | `HackerNewsFetcher` | topstories + beststories, AI keyword filter |
| Dev.to | `DevToFetcher` | AI/ML tags |
| GitHub Trending | `GitHubTrendingFetcher` | AI keyword filter on repo descriptions |
| Product Hunt | `ProductHuntFetcher` | AI category |
| Podcasts | `PodcastFetcher` | Curated RSS feeds (Latent Space, Hard Fork, etc.) |

### Known Limitations

| Source | Status | Reason |
|---|---|---|
| Reddit | Present, not reliable in production | Reddit blocks requests from datacenter IPs; Cloudflare Worker proxy in place but not fully validated |

### TrendScore Formula

```
TrendScore = (upvotes × 0.6) + (comments × 0.3) + (recency_boost × 0.1)
```

- Items older than 7 days decay to zero
- Refreshed every 30 minutes via Hangfire
- Triggered on startup (all environments) and via `POST /api/ingest/reddit`
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
  "generatedAt": "2026-04-18T10:00:00Z",
  "rows": [
    {
      "contentType": "Podcast",
      "items": [
        {
          "id": "podcast_latent_space_ep42",
          "title": "The GPT-5 episode",
          "url": "https://www.latent.space/p/gpt5",
          "sourceName": "Latent Space",
          "trendScore": 312.4,
          "upvotes": 0,
          "commentCount": 0,
          "postedAt": "2026-04-17T09:00:00Z",
          "contentType": "Podcast"
        }
      ]
    }
  ]
}
```

### `GET /api/source/{sourceName}`

Returns trending items filtered to a single source.

- `sourceName`: `HackerNews`, `DevTo`, `GitHub`, `ProductHunt`, `Podcast`, `Reddit`
- Query parameters: `limit` (default 20, max 50), `window` (`day` or `week`)

### `POST /api/ingest/reddit`

Triggers `TrendRefreshJob` as a background task. Called by the daily GitHub Actions workflow.

- Auth: `X-Ingest-Secret` request header, value must match `RedditIngest__Secret` env var on server
- Returns `202 Accepted` immediately; job runs in background
- Returns `401 Unauthorized` if header is missing or wrong

---

## 7. Hosting & Deployment

### Production Architecture

```
Browser → Cloudflare Pages (marlenhalvorsen.dev)
               ↓ HTTPS API calls to api.marlenhalvorsen.dev
         Cloudflare Tunnel → Oracle Cloud VM → AiPulse.Api (systemd, port 5000)
                                                     ↓
                                               SQLite + Hangfire (SQLite-backed)
```

### GitHub Actions Workflows

| Workflow | Trigger | Action |
|---|---|---|
| `ci.yml` | Push / PR to `main` | `dotnet build` + `dotnet test` |
| `tests.yml` | Push / PR to `main` | Full test suite |
| `deploy-api.yml` | Push to `main` | `dotnet publish` → SCP to VM → systemd restart |
| `deploy.yml` | Push to `main` | `dotnet publish AiPulse.Client` → Cloudflare Pages |
| `ingest-reddit.yml` | Daily 06:00 UTC + manual | `POST /api/ingest/reddit` |

### Environment Variables (VM)

| Variable | Purpose |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_URLS` | `http://localhost:5000` |
| `RedditIngest__Secret` | Shared secret for `/api/ingest/reddit` |
| `ConnectionStrings__DefaultConnection` | Path to SQLite app DB |
| `ConnectionStrings__HangfireConnection` | Path to SQLite Hangfire DB |

---

## 8. Privacy & Security

### What the application NEVER does

- Never sets any identifying cookie
- Never loads third-party analytics scripts
- Never logs IP addresses
- Never stores information about who visited or clicked

### Security Headers

Implemented in `SecurityHeadersMiddleware`, applied to every response:

```
Content-Security-Policy:   default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: i.ytimg.com; frame-ancestors 'none'
X-Frame-Options:           DENY
X-Content-Type-Options:    nosniff
Referrer-Policy:           no-referrer
Permissions-Policy:        camera=(), microphone=(), geolocation=()
Strict-Transport-Security: max-age=63072000; includeSubDomains
Server header:             Removed entirely
```

### Other Security Controls

- **CORS**: `AllowedOrigins` in config — locked to `localhost:5243`, `ai-pulse-70n.pages.dev`, `marlenhalvorsen.dev`, `www.marlenhalvorsen.dev`
- **Rate limiting**: Fixed window, 60 req/min per IP on all API endpoints; `429 Too Many Requests` on breach
- **Ingest endpoint**: Shared-secret authentication (`X-Ingest-Secret` header)
- **EF Core parameterised queries**: No raw SQL

---

*AI Pulse — Free. Open. No login. No tracking. Just the AI conversation happening right now.*
