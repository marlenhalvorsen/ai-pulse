---
name: implement
description: Make failing tests pass with clean production code. Use after write-tests. Part of the TDD pipeline. Enforces Clean Architecture, SOLID, DRY, and zero warnings.
---

# Implement

Make failing tests pass with minimum clean production code.
Tests must already exist and be failing before this skill runs.

## Architecture rules (non-negotiable)
Domain ← Application ← Infrastructure ← Web

- Domain — pure models and enums, zero dependencies
- Application — interfaces, use cases, DTOs — references Domain only
- Infrastructure — fetchers, repository, jobs — references Application only
- Web — Blazor pages, API endpoints, middleware — references Application only
- Never reference Infrastructure from Web directly — use DI interfaces

## SOLID
- One class, one job
- New data source = new ITrendFetcher — no existing code modified
- All implementations interchangeable via interfaces
- Small focused interfaces: ITrendFetcher, IContentRepository, ITrendingQuery
- All dependencies via constructor — no `new ConcreteClass()` outside Program.cs

## DRY
- Scoring logic only in TrendScoreCalculator
- URL classification only in UrlClassifier
- All config in appsettings.json — never hardcoded

## Security
- API keys only from IConfiguration — never hardcoded, never logged
- EF Core parameterised queries only — no raw SQL
- No cookies, no analytics, no PII

## Rules
1. Write minimum code to pass tests — no gold plating
2. `dotnet build` — zero warnings, fix all before continuing
3. `dotnet test` — all tests green including existing tests
4. If existing tests break, fix before continuing
5. Register new services in DependencyInjection.cs

## Commit
```
feat: implement {feature}

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
```

## Stop condition
Stop after zero warnings and all tests green. Pipeline continues with /review.
