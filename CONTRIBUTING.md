# Contributing to Koras.Results

Thank you for considering a contribution! This project values small, well-tested, well-documented changes.

## Before you start

- **Bugs:** open an issue with a minimal repro (a failing test is ideal).
- **Features:** open an issue first. Check `docs/features/future-roadmap.md` and the out-of-scope list in `docs/product/product-vision.md` — PRs for out-of-scope features will be declined regardless of quality.
- **Breaking changes:** not accepted within a major version. See `docs/api/backward-compatibility.md`.

## Development setup

```bash
git clone https://github.com/korastechnologies/koras-results.git
cd koras-results
dotnet build -c Release        # requires .NET SDK 10 (see global.json); builds net8.0/net9.0/net10.0
dotnet test  -c Release
```

## Working on a change

1. Fork and branch from `main` (`feat/short-name`, `fix/issue-123`).
2. Follow the repository conventions — they are enforced by analyzers and CI:
   - warnings are errors; `dotnet format --verify-no-changes` must pass;
   - every public member has XML docs;
   - tests accompany code in the same PR (happy path, invalid input, boundary, failure path).
3. **Public API changes:** the build fails until you update `PublicAPI.Unshipped.txt` next to the project. Use the IDE code-fix ("Add to public API") or edit the file manually. Also update `docs/api/public-api-design.md` and complete `docs/api/public-api-review-checklist.md` in the PR description.
4. **New dependencies** require prior discussion and an ADR (`docs/architecture/dependency-rules.md`). The core package accepts none.
5. Update `CHANGELOG.md` under `[Unreleased]` (Keep a Changelog format).

## Commit and PR conventions

- Imperative messages with a type prefix: `feat:`, `fix:`, `docs:`, `test:`, `build:`, `chore:`, `perf:`.
- One logical change per PR. Fill in the PR template; CI must be fully green.

## Validation before pushing

```bash
dotnet build -c Release
dotnet test -c Release
dotnet format --verify-no-changes
```

## Code of conduct & security

By participating you agree to the [Code of Conduct](CODE_OF_CONDUCT.md). **Never** report security vulnerabilities in public issues — see [SECURITY.md](SECURITY.md).

## License

Contributions are accepted under the MIT license of this repository.
