# Competitive Analysis — Koras.Results

## Landscape summary

The Result-pattern space in .NET is mature and crowded. Any new entrant must be honest about that and differentiate on *system completeness* (domain → HTTP → telemetry), *dependency hygiene*, and *engineering rigor*, not on inventing a new monad.

## Existing NuGet alternatives

### FluentResults (`FluentResults`)
- **Model:** class-based `Result` / `Result<T>` with an extensible list of `IReason` (successes and errors), metadata dictionaries.
- **Strengths:** very popular; flexible reasons model; good docs.
- **Weaknesses vs Koras.Results:** every result allocates (class + lists); no error taxonomy → no principled HTTP mapping; ASP.NET Core integration is third-party/DIY; API surface encourages stringly-typed errors.

### ErrorOr (`ErrorOr`)
- **Model:** `ErrorOr<T>` struct, discriminated between a value and a *list* of `Error` structs with an `ErrorType` enum.
- **Strengths:** struct-based, typed error categories, popular in Clean Architecture tutorials.
- **Weaknesses vs Koras.Results:** single package — no first-party ASP.NET Core/FluentValidation/MediatR/OTel integrations; always carries a list even for one error; no non-generic result for void operations (`ErrorOr<Success>` idiom); no ProblemDetails story in the box.

### CSharpFunctionalExtensions (`CSharpFunctionalExtensions`)
- **Model:** `Result`, `Result<T>`, `Result<T, E>`, `Maybe<T>`, extensive combinator library.
- **Strengths:** enormous adoption; battle-tested combinators; string-or-generic error.
- **Weaknesses vs Koras.Results:** string-first error model (no taxonomy, no codes, no metadata); no HTTP integration; large API surface with many overloads; class-based results.

### Ardalis.Result (`Ardalis.Result`)
- **Model:** `Result<T>` with `ResultStatus` enum mirroring HTTP-ish statuses; companion `Ardalis.Result.AspNetCore`, `Ardalis.Result.FluentValidation`.
- **Strengths:** closest competitor in shape — ships ASP.NET Core mapping.
- **Weaknesses vs Koras.Results:** the status enum is effectively HTTP leaking into the domain (`Result.Unauthorized()` etc. mirror status codes rather than business semantics); error payload is `string[]` — no codes, no metadata, weak client contracts; class-based.

### LanguageExt (`LanguageExt.Core`)
- **Model:** full functional ecosystem (`Either`, `Option`, `Fin`, effects).
- **Not a direct competitor:** serves the FP-maximalist niche. We explicitly point that audience there.

### OneOf (`OneOf`)
- **Model:** general discriminated unions.
- **Not a direct competitor:** a union primitive, not an error-handling system; no error model, composition, or HTTP mapping.

## Differentiation matrix

| Capability | FluentResults | ErrorOr | CSFE | Ardalis.Result | **Koras.Results** |
|---|---|---|---|---|---|
| Allocation-free struct results | ✗ | ✓ | ✗ | ✗ | ✓ |
| Typed error taxonomy (domain semantics) | ✗ | ✓ | ✗ | ~ (HTTP-shaped) | ✓ |
| Error codes + metadata | ~ | ✓ | ✗ | ✗ | ✓ |
| First-party ProblemDetails (RFC 9457) | ✗ | ✗ | ✗ | ~ | ✓ |
| Minimal API + MVC integration | ✗ | ✗ | ✗ | ✓ | ✓ |
| FluentValidation integration | ✗ | ✗ | ✗ | ✓ | ✓ |
| MediatR integration | ✗ | ✗ | ✗ | ✗ | ✓ |
| OpenTelemetry error tagging | ✗ | ✗ | ✗ | ✗ | ✓ |
| Zero-dependency core | ✓ | ✓ | ✓ | ✓ | ✓ |
| Locked public API (analyzers) | ✗ | ✗ | ✗ | ✗ | ✓ |
| JSON serialization support (documented) | ✗ | ✗ | ✗ | ✗ | ✓ |

## Positioning statement

> For .NET teams building layered applications and HTTP APIs, **Koras.Results** is the result-and-error-handling package that covers the entire failure path — typed domain errors, functional composition, and standards-compliant ProblemDetails responses — as a family of small, zero-bloat, independently versioned packages.

## Naming, namespace, and NuGet ID assessment

- **Package name `Koras.Results`:** clear, category-descriptive, follows `Company.Product` convention. No conflict found with existing NuGet IDs (`Koras.*` prefix unclaimed). The `Koras` prefix should be reserved on NuGet.org (prefix reservation) before 1.0.
- **Root namespace `Koras.Results`:** matches package ID (required by our conventions); types like `Koras.Results.Result` read naturally. Satellite namespaces: `Koras.Results.AspNetCore`, `Koras.Results.FluentValidation`, `Koras.Results.MediatR`, `Koras.Results.OpenTelemetry`.
- **Risk:** `Results` collides conceptually with `Microsoft.AspNetCore.Http.Results` static class. Mitigated: users rarely import both namespaces in the same file (domain vs endpoint), and our types (`Result`, `Result<T>`) don't collide with the `Results` static class by simple name.

## Adoption strategy

1. **Five-minute README** with copy-paste quick start (install → return `Result<T>` → `ToHttpResult()`).
2. **Migration guides** from FluentResults, ErrorOr, and Ardalis.Result (mechanical mappings).
3. **Content:** blog-post-shaped docs (recipes) that rank for "result pattern ASP.NET Core ProblemDetails".
4. **Templates:** sample repos per app model; `dotnet new` template consideration post-1.0.
5. **Trust signals:** coverage badge, API-stability policy, SECURITY.md, changelog discipline.

## Monetization

None planned. MIT, free forever. Value to Koras Technologies is reputation and ecosystem credibility. (A paid support tier is conceivable but out of scope.)

## Open-source & community strategy

- MIT license, public GitHub, public roadmap (ROADMAP.md), ADRs public.
- `CONTRIBUTING.md` with build/test instructions and PR checklist; `good first issue` labeling.
- All features begin as issues with design discussion; breaking changes require an ADR.
- Releases automated from tags; changelog under Keep-a-Changelog format.
