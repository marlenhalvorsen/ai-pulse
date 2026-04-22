---
name: write-tests
description: Write failing tests before any production code. Use at the start of every feature implementation. Part of the TDD pipeline.
---

# Write Tests

Write failing tests for a feature. This is step 1 of the pipeline.
Do NOT write production code in this step.

## Architecture context
- Domain/ — unit tests for pure domain logic
- Infrastructure/ — unit tests with mocked HTTP clients (no real network calls)
- Api/ — integration tests via WebApplicationFactory
- Architecture/ — NetArchTest boundary tests

Test framework: xUnit + Moq + FluentAssertions + bUnit (Blazor)

## Rules
1. Write the test file first
2. Run `dotnet test` — confirm tests FAIL before stopping
3. Tests must verify real behaviour — not just "it exists"
4. Use `[Theory]` + `[InlineData]` for multiple input cases
5. Mock all external dependencies — no real HTTP or DB calls
6. Name tests: `MethodName_Scenario_ExpectedResult`
7. No production code — not a single line

## Commit
```
test: add failing tests for {feature}

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
```

## Stop condition
Stop after confirming tests fail. The pipeline continues with /implement.
