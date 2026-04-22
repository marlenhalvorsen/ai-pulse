---
name: docs
description: Update README.md and SPEC.md to reflect a newly merged feature. Use when a PR has been merged, a feature is complete, or the user says "update docs", "sync documentation", "docs are out of date", or "update README". Keeps documentation in sync with the codebase automatically.
---

# Docs

Keep README.md and SPEC.md in sync with the codebase after a feature is merged.

## When to run

Run this skill after every merged PR, or when the user says "update docs", "sync documentation", or "docs are out of date".

---

## Step 1 — Understand what changed

Read the merged PR or the latest commits to understand what was built:

```bash
git log main --oneline -10
git diff HEAD~1 HEAD --stat
```

If the user names a specific PR, use `gh pr view {number}` to read the PR title and description.

---

## Step 2 — Update SPEC.md

Open `SPEC.md` and apply exactly the changes the new feature warrants. Do not rewrite sections unrelated to the feature.

### Section 4 — MoSCoW Prioritisation

Move completed items from "Must Have" to "✅ Must Have (complete)":

```markdown
### ✅ Must Have (complete)
- {feature that was just merged}
```

Remove it from the pending "Must Have" list if it was listed there.

### Section 5 — Data Sources & Trending Signal

If the feature adds or changes a fetcher, update the Active Sources table. If a known limitation is resolved, remove it from the Known Limitations table.

### Section 2 — Technology Stack

If the feature adds a dependency (new NuGet package, new service), add a row.

### Section 6 — API Contract

If the feature adds or changes an endpoint, update the relevant contract block.

### No other sections

Do not touch sections not affected by the feature. Precision over completeness.

---

## Step 3 — Update README.md

Open `README.md` and apply targeted updates. Do not rewrite the whole file.

### "How It Works" section

If the feature changes the data flow or pipeline (new source, new job, new endpoint), update the numbered steps to reflect reality.

### "Tech Stack" table

If a new technology was introduced, add a row.

### "Production Deployment" workflows table

If a new GitHub Actions workflow was added, add a row to the workflows table.

### MoSCoW / Engineering Methodology section

Do not update these. They describe process, not features.

---

## Step 4 — Commit

Stage only the documentation files:

```bash
git add README.md SPEC.md
git commit -m "docs: update README and SPEC for {feature-name}

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>"
```

Push directly to the current branch if on a feature branch, or to `main` if it is a standalone docs update.

---

## Hard rules

- Never rewrite sections unrelated to the merged feature
- Never add speculative or future-tense content — only document what is already shipped
- Never mark a Must Have item as complete unless the PR was actually merged
- Always verify the current state of the code before updating docs — read the relevant files first
- Keep the same tone and formatting style as the existing document
