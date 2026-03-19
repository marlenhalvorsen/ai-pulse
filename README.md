# AI Pulse

> ⚠️ **Under active development.** This project is being built in public using Claude Code. Expect breaking changes.

> **What the AI world is talking about right now.**

---

## About this project

AI Pulse is two things at once.

The first is a product — a free platform that surfaces what the AI community is actually talking about right now, across podcasts, videos, articles, newsletters and discussions, ranked by real social momentum from Reddit and HackerNews.

The second is an experiment. My other projects are built by me. This one is an exploration of agentic coding — where Claude Code writes the code, and I stay at the wheel. I define the architecture, review every pull request, and decide what gets merged. Claude Code implements. The goal is to find out what that collaboration actually feels like in practice, and whether the result holds up.

Built in public. Human in the loop.

> **What the AI world is talking about right now.**

AI Pulse is a free, open, read-only web platform that surfaces trending AI content across every major format — podcasts, videos, articles, newsletters, and research papers — driven by real social signals from Reddit and HackerNews.

No login. No tracking. No algorithms deciding what you should think. Just the conversation, as it is happening.

---

## ⚠️ AI-Generated Codebase

This project is built with [Claude Code](https://claude.ai/code) following a human-defined specification. All code is AI-generated under human review — every Pull Request is reviewed and approved by a human before merging. The architecture, principles, and priorities are defined by the project owner; Claude Code implements them.

---

## Why AI Pulse?

The AI world moves fast. New papers, tools, videos, and debates emerge daily — scattered across Reddit, HackerNews, YouTube, Spotify, and dozens of newsletters. There is no single place that shows you what the AI community is *actually* talking about right now, ranked by real momentum rather than all-time popularity.

**Trending means trending this week** — not what has the most lifetime views.

---

## How It Works

1. Every 30 minutes, AI Pulse fetches posts from Reddit (`r/MachineLearning`, `r/artificial`, `r/ChatGPT`, `r/LocalLLaMA`, `r/singularity`) and HackerNews
2. Each post that links to external content is classified by type — video, podcast, article, newsletter, research paper, or discussion
3. A trend score is calculated based on upvotes, comments, and recency — items older than 7 days decay to zero
4. The frontend displays results in rows by content type, highest trending first

---

## Tech Stack

| Concern | Choice |
|---|---|
| Backend | C# .NET 8 — ASP.NET Core Web API |
| Frontend | Blazor Server / Razor Pages |
| Database | SQLite via Entity Framework Core |
| Background Jobs | Hangfire |
| Testing | xUnit + Moq + FluentAssertions |

---

## Running Locally

```bash
# Clone the repo
git clone git@github.com:marlenhalvorsen/ai-pulse.git
cd ai-pulse

# Run the app
dotnet run --project AiPulse.Web

# Run tests
dotnet test
```

Requirements: [.NET 8 SDK](https://dotnet.microsoft.com/download)

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
