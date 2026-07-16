# MVP Scope — Koras.Results 0.1.0

## Goal

A developer can install `Koras.Results` (+ `Koras.Results.AspNetCore`), return `Result<T>` from services, and get correct RFC 9457 ProblemDetails responses from Minimal APIs or MVC within five minutes — with the whole path unit- and integration-tested.

## In scope (0.1.0)

**Packages shipped:** `Koras.Results`, `Koras.Results.AspNetCore`, `Koras.Results.FluentValidation`, `Koras.Results.MediatR`, `Koras.Results.OpenTelemetry`.

**Features:** KR-001 … KR-016 (see [feature-catalog.md](feature-catalog.md)).

**Quality gates:**
- ≥ 90 % branch coverage on `Koras.Results` core; ≥ 80 % on satellites.
- Architecture tests enforcing dependency direction and immutability conventions.
- Public API tracked by PublicApiAnalyzers in every shipped project.
- Integration tests exercising a real ASP.NET Core host for both Minimal API and MVC.
- Deterministic, Source-Linked, symbol-published packages passing `dotnet pack` validation and a consumption smoke test.
- Complete docs tree and four runnable samples.

## Out of MVP (deliberately)

| Item | Why deferred |
|---|---|
| EF Core / gRPC / GraphQL integrations | Each adds a heavy dependency axis; validate the core contract first |
| Roslyn analyzers | High value but separate engineering track; needs stable API to lint against |
| Source generators | 2.0 theme; requires frozen error model |
| LINQ query syntax | Sugar; needs community signal that it's wanted |
| `netstandard2.0` TFM | Target users are on modern .NET; would degrade annotations and API (ADR-0002) |

## MVP acceptance checklist

- [ ] All KR-001…KR-016 acceptance criteria green
- [ ] `dotnet build -c Release` warning-free (warnings as errors)
- [ ] `dotnet test -c Release` fully green on net8.0/net9.0/net10.0
- [ ] `dotnet pack -c Release` produces validated packages with README, icon, symbols
- [ ] Package-consumption smoke test passes against built `.nupkg`s
- [ ] `dotnet format --verify-no-changes` clean
- [ ] No vulnerable dependencies (`dotnet list package --vulnerable --include-transitive`)
- [ ] README five-minute quick start verified end-to-end
