---
name: pipeline
description: Full TDD development pipeline for one feature. Use when implementing a new feature from scratch. Runs write-tests → implement → review → open-pr in sequence.
---

# Pipeline

Orchestrate the full development pipeline for one feature at a time.
Run each step in sequence. Do not skip steps. Do not move to the next step until the current one is complete.

## Usage
```
/pipeline feature: {description of what to build}
```

Example:
```
/pipeline feature: add Dev.to fetcher for AI articles using tag=ai, implements ITrendFetcher, ContentType = Article
```

---

## Step 1 — Branch
```
git checkout main
git pull origin main
git checkout -b feature/{kebab-case-feature-name}
```
Never start work on main. Never branch from another feature branch.

## Step 2 — Write tests
Follow `/write-tests` skill.
- Write failing tests only
- Run `dotnet test` — confirm tests fail
- Commit: `test: add failing tests for {feature}`
- Do NOT write production code

## Step 3 — Implement
Follow `/implement` skill.
- Write minimum code to make tests pass
- `dotnet build` — zero warnings allowed
- `dotnet test` — all tests green including existing
- Commit: `feat: implement {feature}`

## Step 4 — Review
Follow `/review` skill.
- Run full checklist
- Fix any issues found
- Re-run build and tests after fixes
- Only proceed when ALL checks pass

## Step 5 — Open PR
Follow `/open-pr` skill.
- Push branch and open PR with `gh pr create`
- **STOP after opening PR**
- Wait for human to say "continue"

---

## Hard rules
- One feature per pipeline run — never bundle
- Never merge own PRs
- Never start next pipeline without explicit "continue" from human
- Zero warnings and zero test failures before PR — no exceptions
- Never commit appsettings.Development.json or .db files
