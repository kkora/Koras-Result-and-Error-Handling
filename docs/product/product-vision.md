# Product Vision — Koras.Results

## Vision statement

**Koras.Results gives every .NET team one canonical, allocation-free way to express success and failure — from the domain layer all the way to an RFC 9457 ProblemDetails HTTP response — without exceptions as control flow and without framework lock-in.**

## Why this package should exist

Almost every non-trivial .NET codebase eventually invents one of these:

- a hand-rolled `Result<T>` / `OperationOutcome` / `ServiceResponse<T>` class,
- an ad-hoc `ErrorCode` enum or magic-string error codes,
- copy-pasted controller code translating domain failures into HTTP status codes,
- a `try/catch` pyramid that turns validation failures into exceptions and back again.

Each invention is slightly different, undocumented, untested, and incompatible with the one in the next service. Koras.Results replaces all of them with a small, rigorously tested, documented, and versioned package.

## What "better" means here

1. **Whole-path coverage.** Most Result libraries stop at the domain layer. Koras.Results covers domain → application → HTTP: typed errors carry enough semantic information (`ErrorType`, code, metadata) to produce a correct ProblemDetails response with zero per-endpoint mapping code.
2. **Domain-first error taxonomy.** Errors are classified by *business meaning* (NotFound, Conflict, Validation, …), not by HTTP status codes. HTTP mapping is a configurable projection that lives in the AspNetCore package — the domain never references HTTP.
3. **Allocation-free happy path.** `Result` and `Result<T>` are `readonly struct`s. Success results allocate nothing.
4. **Zero-dependency core.** `Koras.Results` references no NuGet packages. Integrations (ASP.NET Core, FluentValidation, MediatR, OpenTelemetry) are opt-in satellite packages.
5. **Boringly trustworthy.** Locked public API surface, semantic versioning, >90 % branch coverage on the core, CI on every commit, security policy, signed provenance via deterministic builds and Source Link.

## What should NOT be included

- A full functional-programming framework (`Option`, `Either`, monad transformers, higher-kinded emulation). Teams wanting that should use LanguageExt.
- A validation framework. We integrate with FluentValidation; we do not compete with it.
- A mediator implementation. We integrate with MediatR; we do not replace it.
- HTTP client/resilience concerns. Out of scope entirely.
- Exception-replacement dogma. Exceptions remain correct for truly exceptional conditions; the docs say so explicitly.

## What would create unnecessary complexity

- Deep generic combinator hierarchies (`Result<T1, T2, TError1, TError2>`).
- An extensible "reasons" object graph (FluentResults-style) — powerful but allocation-heavy and rarely used well.
- Auto-magical exception-to-error mapping via reflection or attributes.
- Configuration knobs for behavior that should simply be correct by default.

## What makes developers trust a package like this

- A public API that never breaks within a major version (enforced by API analyzers in CI).
- Tests they can read; coverage they can verify.
- Docs that answer the question they actually have within five minutes.
- A dependency graph they can audit in ten seconds (core: none).
- MIT license, public roadmap, responsive issue triage.

## What could prevent adoption

- Crowded space (FluentResults, ErrorOr, CSharpFunctionalExtensions, Ardalis.Result) — mitigated by the whole-path story and modularity.
- Fear of lock-in — mitigated by the tiny surface: migrating away is mechanical.
- Team skepticism of "railway-oriented" style — mitigated by supporting plain `if (result.IsFailure)` usage as a first-class pattern; combinators are optional sugar.

## Success criteria (18 months)

- 100k+ NuGet downloads across the package family.
- Zero unintentional breaking changes shipped.
- Median time-to-first-successful-use under five minutes (README quick start).
- External contributors landing merged PRs.
