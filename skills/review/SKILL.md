---
name: review
description: Self-review diff before PR. Step 3 of pipeline.
---

# Review

## Checklist
- [ ] `dotnet build` — zero warnings
- [ ] `dotnet test` — zero failures
- [ ] No layer violations (see CLAUDE.md)
- [ ] No hardcoded values, secrets, or API keys
- [ ] No .db or appsettings.Development.json committed
- [ ] No Console.WriteLine or commented-out code
- [ ] All new production code has tests
- [ ] Semantic commits + Co-Authored-By on all commits

## If issues found
Fix → re-run build + tests → re-run checklist from top.

```
fix: {description}

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
```

## Stop
All checks pass → proceed to /open-pr.
