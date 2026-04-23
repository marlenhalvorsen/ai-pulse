# AI Pulse

[![CI](https://github.com/marlenhalvorsen/ai-pulse/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/marlenhalvorsen/ai-pulse/actions/workflows/ci.yml)
[![Tests](https://github.com/marlenhalvorsen/ai-pulse/actions/workflows/tests.yml/badge.svg?branch=main)](https://github.com/marlenhalvorsen/ai-pulse/actions/workflows/tests.yml)

> **Live at [marlenhalvorsen.dev](https://marlenhalvorsen.dev)**

> ⚠️ **Under active development.** This project is being built in public using Claude Code. Expect breaking changes.

> **What the AI world is talking about right now.**

---

![AI Pulse](docs/screenshot.png)

## My Role

I designed the layered architecture, wrote CLAUDE.md as an architectural contract
governing Claude Code's behaviour, defined the MoSCoW priorities, built the skills
pipeline (write-tests → implement → review → pr → docs), and reviewed every PR
before merge.

See [CLAUDE.md](CLAUDE.md) for the architectural contract I wrote governing
Claude Code's behaviour.

---

## About this project

The AI world moves fast. New papers, tools, videos, and debates emerge daily — scattered across HackerNews, YouTube, Spotify, and dozens of newsletters. There is no single place that shows you what the AI community is *actually* talking about right now, ranked by real momentum rather than all-time popularity. **Trending means trending this week** — not what has the most lifetime views.

AI Pulse is that place. A free, read-only platform that surfaces what the AI community is discussing across podcasts, videos, articles, newsletters, and discussions — ranked by real social momentum. No login. No tracking. No algorithms.

It is also an experiment in agentic coding — where Claude Code writes the code, and I stay at the wheel. I define the architecture, review every pull request, and decide what gets merged. Claude Code implements. Built in public. Human in the loop.

---

## Engineering Methodology

Every feature follows the same disciplined pipeline — no exceptions.

### Source-of-truth documents

| Document | Purpose |
|---|---|
| [`CLAUDE.md`](CLAUDE.md) | Architecture decisions, engineering rules, and non-negotiable constraints — the contract between human architect and AI engineer |
| [`SPEC.md`](SPEC.md) | Full technical specification: stack, layer map, API contract, MoSCoW priorities, hosting architecture |

`CLAUDE.md` is not a style guide. It is an architectural contract: strict TDD rules, SOLID/DRY enforcement, layer boundaries, git workflow, and security constraints. Claude Code reads it at the start of every session and cannot deviate from it.

### TDD pipeline via specialized Claude skills

Each feature moves through a fixed sequence of focused, single-purpose skills:

```
/pipeline feature: {description}
    └── /write-tests    → writes failing xUnit tests — no production code yet
    └── /implement      → implements only enough to make the tests pass
    └── /review         → self-reviews the diff for quality, coverage, and regressions
    └── /open-pr        → pushes the branch, opens the PR, and stops
    └── /docs           → updates README.md and SPEC.md to reflect the merged feature
```

No production code is written before a failing test exists. The pipeline enforces this — Claude Code cannot skip steps. A human reviews and approves every PR before it merges to `main`.

### How this prevents breaking changes

| Safeguard | What it catches |
|---|---|
| TDD — red before green | Regressions and incorrect implementations before they ship |
| Focused single-purpose skills | Scope creep and accidental bundling of unrelated changes |
| One feature = one branch = one PR | Unreviewed changes reaching `main` |
| Human PR review | Anything the automated checks miss |

Every merged PR has passing tests, passes CI, respects the architecture, and was reviewed by a human.

---

## How It Works

1. AI Pulse fetches posts from HackerNews and other sources on a regular schedule
2. Each post that links to external content is classified by type — video, podcast, article, newsletter, research paper, or discussion
3. A trend score is calculated based on upvotes, comments, and recency — items older than 7 days decay to zero
4. The frontend displays results in rows by content type, highest trending first

---

## Tech Stack

| Concern | Choice |
|---|---|
| API | C# .NET 8 — ASP.NET Core Web API (`AiPulse.Api`) |
| Frontend | Blazor WASM (`AiPulse.Client`) |
| Database | SQLite via Entity Framework Core |
| Background Jobs | Hangfire (SQLite storage) |
| Testing | xUnit + Moq + FluentAssertions |
| API hosting | Oracle Cloud free-tier VM + Cloudflare Tunnel |
| Frontend hosting | Cloudflare Pages |

---

## Running Locally

```bash
# Clone the repo
git clone git@github.com:marlenhalvorsen/ai-pulse.git
cd ai-pulse

# Run the API (http://localhost:5293)
dotnet run --project AiPulse.Api

# Run tests
dotnet test
```

Requirements: [.NET 8 SDK](https://dotnet.microsoft.com/download)

---

## Production Deployment

Both components are live:

| Component | Platform | URL | How |
|---|---|---|---|
| `AiPulse.Api` | Oracle Cloud VM (free tier) | `https://api.marlenhalvorsen.dev` | GitHub Actions → SSH → systemd |
| `AiPulse.Client` | Cloudflare Pages | `https://marlenhalvorsen.dev` | GitHub Actions → `cloudflare/pages-action` |

### Architecture

```
Browser → Cloudflare Pages (static WASM files)
               ↓ API calls to api.marlenhalvorsen.dev
         Cloudflare Tunnel → Oracle Cloud VM → AiPulse.Api (systemd, port 5000)
                                                     ↓
                                               SQLite + Hangfire
```

The Blazor WASM client is served as static files from Cloudflare Pages. All API calls are routed through a Cloudflare Tunnel to `AiPulse.Api` on an Oracle Cloud free-tier VM — no inbound ports are open on the VM.

### GitHub Actions workflows

| Workflow | Trigger | What it does |
|---|---|---|
| `ci.yml` | Push / PR to `main` | `dotnet build` + `dotnet test` |
| `tests.yml` | Push / PR to `main` | Full test suite |
| `deploy-api.yml` | Push to `main` | Publishes `AiPulse.Api`, copies to VM via SCP, restarts systemd service |
| `deploy.yml` | Push to `main` | Publishes `AiPulse.Client`, deploys static files to Cloudflare Pages |
| `ingest-reddit.yml` | Daily 06:00 UTC + manual | Calls `POST /api/ingest/reddit` to trigger a data refresh |

### Repository secrets

| Secret | Used by | Description |
|---|---|---|
| `ORACLE_HOST` | `deploy-api.yml` | VM public IP |
| `ORACLE_USER` | `deploy-api.yml` | SSH user |
| `ORACLE_SSH_KEY` | `deploy-api.yml` | Private key (PEM) |
| `CLOUDFLARE_API_TOKEN` | `deploy.yml` | Pages edit permissions |
| `CLOUDFLARE_ACCOUNT_ID` | `deploy.yml` | Cloudflare account ID |
| `REDDIT_INGEST_SECRET` | `ingest-reddit.yml` | Shared secret for `/api/ingest/reddit` |

### VM setup (one-time)

```bash
# Install .NET 8 runtime
sudo apt install -y dotnet-runtime-8.0

# Create app directory
sudo mkdir -p /opt/ai-pulse/app

# Create systemd service
sudo tee /etc/systemd/system/ai-pulse-api.service <<EOF
[Unit]
Description=AI Pulse API
After=network.target

[Service]
WorkingDirectory=/opt/ai-pulse/app
ExecStart=/usr/bin/dotnet /opt/ai-pulse/app/AiPulse.Api.dll
Restart=always
RestartSec=10
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5000
Environment=RedditIngest__Secret=<your-secret>

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl enable --now ai-pulse-api

# Cloudflare Tunnel (exposes the API at api.marlenhalvorsen.dev)
# cloudflared tunnel login
# cloudflared tunnel create ai-pulse
# cloudflared tunnel route dns ai-pulse api.marlenhalvorsen.dev
# cloudflared tunnel run ai-pulse
```

---

## Reflections

The hardest architectural decision was keeping Reddit as a fetcher despite
datacenter IP blocking — I chose to keep it in the codebase as documented
technical debt rather than delete it. It represents a real constraint worth
preserving honestly.

Building with Claude Code clarified something quickly: the quality of the output
is a direct function of the quality of the specification. Vague input produces
vague code. Writing CLAUDE.md as a strict architectural contract — not just a
prompt — was the most important thing I did.

---

## Contributing

Contributions are welcome. Please follow the project's Git workflow:

**Branch strategy:**
- Never commit directly to `main`
- Create a feature branch: `feature/your-feature-name`
- One feature per branch, one PR per feature

**Commit messages — semantic commits only:**
```
feat: add RSS feed output
fix: correct decay calculation for old items
test: add edge cases for UrlClassifier
chore: update Hangfire dependency
```

Open a PR against `main` and describe what you built and what tests cover it.

---

## Privacy

AI Pulse collects zero personal data. No cookies. No analytics scripts. No IP logging. No tracking of any kind. This is enforced by architecture — there is no login system, no session, nothing to collect.

---

## License

MIT — free to use, modify, and distribute.

---

*Built in public. Powered by curiosity.*
