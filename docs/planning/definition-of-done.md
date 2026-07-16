# Definition of Done — Koras.Results

A task/feature is **done** only when every line below holds. "It compiles" is not done.

## Code
- [ ] Implements the public API exactly as specified in `docs/api/public-api-design.md` (or that doc was updated in the same change, passing the API review checklist)
- [ ] `dotnet build -c Release` green — TreatWarningsAsErrors, all TFMs
- [ ] No suppressed warnings without an inline justification comment
- [ ] Nullable reference types fully honored (no `!` without justification)
- [ ] XML documentation on every public member
- [ ] `PublicAPI.Unshipped.txt` updated for any surface change
- [ ] No placeholder code: no `NotImplementedException`, no `TODO`, no fake success values

## Tests
- [ ] Unit tests: happy path, invalid input, boundary, failure path
- [ ] Async APIs: cancellation and fault-propagation tests
- [ ] Integration tests where the feature touches ASP.NET Core / MediatR / FluentValidation
- [ ] All tests pass on all TFMs: `dotnet test -c Release`
- [ ] Coverage did not regress below target (core ≥ 90 % branch, satellites ≥ 80 %)

## Quality gates
- [ ] `dotnet format --verify-no-changes` clean
- [ ] Analyzers (built-in + PublicApiAnalyzers) raise nothing
- [ ] Architecture tests still green (dependency direction, conventions)

## Documentation
- [ ] Feature guide / concepts page updated
- [ ] Sample updated if user-visible behavior changed
- [ ] `CHANGELOG.md` entry under `[Unreleased]`

## Security & hygiene
- [ ] No secrets, credentials, tokens, or personal data in code, tests, samples, or docs
- [ ] New dependencies (if any) documented per `docs/architecture/dependency-rules.md` with an ADR

## Release-level DoD (additionally, per release)
- [ ] `dotnet pack -c Release` succeeds; package contents validated (README, icon, symbols, XML docs, SourceLink)
- [ ] Package-consumption smoke test passes against the produced `.nupkg`
- [ ] `dotnet list package --vulnerable --include-transitive` reports nothing
- [ ] Release checklist in `docs/release/release-checklist.md` completed
