# Pull Request

## What does this change?

<!-- One or two sentences. Link the issue: Fixes #123 -->

## Type of change

- [ ] Bug fix
- [ ] New feature
- [ ] Documentation
- [ ] Build / CI
- [ ] Refactoring (no behavior change)

## Checklist

- [ ] `dotnet build -c Release` — zero warnings
- [ ] `dotnet test -c Release` — green on all target frameworks
- [ ] `dotnet format --verify-no-changes` — clean
- [ ] Tests added/updated for the change (happy path, invalid input, failure path)
- [ ] XML docs on any new public member
- [ ] `CHANGELOG.md` updated under `[Unreleased]`
- [ ] Documentation updated (feature guide / concepts / samples as applicable)

## Public API impact

- [ ] No public API change
- [ ] `PublicAPI.Unshipped.txt` updated **and** `docs/api/public-api-design.md` updated **and** [API review checklist](../docs/api/public-api-review-checklist.md) completed below

## Dependencies

- [ ] No new dependencies
- [ ] New dependency justified with an ADR per [dependency rules](../docs/architecture/dependency-rules.md)
