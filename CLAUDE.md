# CLAUDE.md — Koras.Results

Guidance for Claude Code (and any AI agent) working in this repository.

## 1. Project mission

Koras.Results gives .NET teams one canonical, allocation-free way to express success and failure — typed domain errors, functional composition, and RFC 9457 ProblemDetails responses — as a zero-dependency core plus opt-in integration packages. See `docs/product/product-vision.md`.

## 2. Package scope

Five packages, fixed boundaries (see `docs/architecture/package-boundaries.md`):
`Koras.Results` (core, ZERO dependencies) · `Koras.Results.AspNetCore` · `Koras.Results.FluentValidation` · `Koras.Results.MediatR` · `Koras.Results.OpenTelemetry`.
Out of scope, do not add: Option/Either types, validation engines, mediator implementations, HTTP client concerns, retry policies.

## 3. Architecture rules

- Dependency direction: satellites → core; core → nothing. Only satellite-to-satellite edge allowed: MediatR → FluentValidation.
- HTTP semantics exist ONLY in `Koras.Results.AspNetCore`. The core never knows about status codes.
- The core never: performs I/O, logs, reads configuration, holds mutable static state, throws for control flow.
- `ErrorType` is a closed taxonomy (ADR-0004); extensibility lives in `Error.Code` and metadata.
- Architecture tests in `tests/Koras.Results.ArchitectureTests` enforce these — keep them green.

## 4. Coding conventions

- Modern C#: file-scoped namespaces, nullable reference types (no unjustified `!`), pattern matching, expression bodies for one-liners.
- `ConfigureAwait(false)` on every await in `src/`. Never `.Result`, `.Wait()`, `Thread.Sleep`, sync-over-async.
- `readonly struct` for result primitives; immutable classes for errors; everything public is thread-safe by immutability.
- Guards: `ArgumentNullException`/`ArgumentException` eagerly at public entry points; `InvalidOperationException` for invalid state access, with actionable messages.
- Delegate-taking combinators never catch user exceptions (only `Result.Try*` catches, and it always rethrows `OperationCanceledException`).

## 5. Naming conventions

Normative vocabulary in `docs/api/naming-guidelines.md`. Highlights: `Map`/`Bind`/`Match`/`Switch`/`Ensure`/`Tap`/`TapError`/`MapError`/`Try`/`Combine` — never synonyms. `Async` suffix on Task-returning methods. Error codes `Subject.Condition` PascalCase-dotted. Metadata keys camelCase. Namespaces mirror package IDs exactly.

## 6. Dependency restrictions

- `Koras.Results` core: ZERO package references, forever. Framework references only where a satellite needs them.
- Every new dependency requires an ADR per `docs/architecture/dependency-rules.md` (license, maintenance, size, API leak analysis).
- MediatR must stay `[12.4, 13.0)` — licensing (ADR-0006). Close (do not merge) Dependabot majors for MediatR 13+.
- All versions in `Directory.Packages.props` (CPM). Never put `Version=` in a csproj.

## 7. Public API rules

- `docs/api/public-api-design.md` is the contract. Change the doc and the code together, or don't change either.
- Any public-surface change must update `PublicAPI.Unshipped.txt` in the same commit (PublicApiAnalyzers enforces).
- Additive-only within a major (`docs/api/backward-compatibility.md`). Run `docs/api/public-api-review-checklist.md` on every API PR.
- XML docs on every public member; `<exception>` tags for every throw.

## 8. Test requirements

- New code ships with tests in the same commit: happy path, invalid input, boundary, failure path; async APIs also get cancellation + fault-propagation tests.
- Combinators must test short-circuit semantics with delegate-invocation counters.
- ASP.NET Core features get end-to-end `WebApplicationFactory` tests (both Minimal API and MVC where applicable).
- Coverage bar: core ≥ 90 % branch, satellites ≥ 80 %. Don't chase percentage with meaningless asserts.

## 9. Documentation requirements

- Feature work updates: the feature guide in `docs/features/`, relevant concepts/guides pages, sample code if user-visible, and `CHANGELOG.md` under `[Unreleased]`.
- Docs show runnable code, not fragments, wherever feasible.

## 10. Security requirements

- Never commit secrets, tokens, connection strings, or PII — including in tests, samples, and docs (use `example.com`, obvious placeholders).
- Client-facing error output is secure by default: `Unexpected` details suppressed; metadata exposure opt-in. Do not weaken these defaults.
- No polymorphic deserialization; JSON converters construct only sealed known types.
- See `docs/security/` for the threat model and checklist.

## 11. Performance requirements

- Success-path `Result`/`Result<T>` operations must not allocate. Failure paths may allocate (errors carry data).
- No LINQ/closures on hot paths in the core; no boxing of result structs.
- Benchmarks live in `benchmarks/`; run them when touching core primitives and record regressions in the PR.

## 12. Git workflow

- Branch from `main`; small, focused commits with imperative messages (`feat:`, `fix:`, `docs:`, `test:`, `build:`, `chore:` prefixes).
- Never force-push shared branches. Never commit generated artifacts (`bin/`, `obj/`, `artifacts/`, `BenchmarkDotNet.Artifacts/`).

## 13. Pull-request checklist

Build green (warnings-as-errors) · all tests green on all TFMs · `dotnet format --verify-no-changes` clean · PublicAPI files updated · docs + CHANGELOG updated · no new dependencies without ADR · security checklist consulted for input-handling changes.

## 14. Release workflow

Documented in `docs/release/release-process.md`. Summary: update CHANGELOG, set version in `Directory.Build.props`, tag `vX.Y.Z`, GitHub Actions `release.yml` builds/tests/packs/publishes to NuGet.org via Trusted Publishing (OIDC, `nuget-release` environment; no stored API key).

## 15. Definition of done

`docs/planning/definition-of-done.md` is normative. Short version: implemented per API doc, tested, documented, formatted, analyzed, changelogged, no placeholders, no secrets.

## 16. Commands Claude should use

```bash
export PATH=/usr/local/dotnet:$PATH        # this container installs the SDK here
dotnet restore
dotnet build -c Release --no-restore       # warnings are errors
dotnet test -c Release --no-build          # all TFMs
dotnet test -c Release --no-build -f net10.0   # fast inner loop
dotnet format --verify-no-changes          # before committing
dotnet pack -c Release -o artifacts
dotnet list package --vulnerable --include-transitive
dotnet list package --outdated
```

## 17. Files Claude must not modify without justification

`PublicAPI.Shipped.txt` (release process only) · `LICENSE` · `Directory.Packages.props` MediatR bound (ADR-0006) · `docs/architecture/decision-records/*` (append new ADRs; never rewrite accepted ones) · serialization wire-shape tests (changing them = breaking change) · `.github/workflows/release.yml` publish steps.

## 18. Rules for adding dependencies

Follow `docs/architecture/dependency-rules.md` checklist: necessity, license (MIT/Apache/BSD only), maintenance, trim/AOT compatibility, ADR, CPM entry. The answer for the core package is always no.

## 19. Rules for breaking changes

None within a major, ever — binary, source, behavioral, or wire-format (`docs/api/backward-compatibility.md`). Deprecate with `[Obsolete]` for at least one minor before any major removal. If a task seems to require a break, stop and surface it instead of implementing.

## 20. Required validation before completing a task

Run, in order, and report actual results (never claim success without running):
1. `dotnet build -c Release` — zero warnings
2. `dotnet test -c Release` — all green, all TFMs
3. `dotnet format --verify-no-changes` — clean
4. If packaging touched: `dotnet pack -c Release -o artifacts` + package content check
5. If dependencies touched: `dotnet list package --vulnerable --include-transitive`
