# Problem Statement — Koras.Results

## The problem

.NET applications need to represent operations that can fail for *expected* business reasons — a user not found, a duplicate email, invalid input, insufficient permissions. The platform gives teams two blunt instruments:

1. **Exceptions** — expensive, non-local, invisible in signatures, and semantically wrong for expected outcomes.
2. **Nulls / booleans / out parameters** — lossy: they say *that* something failed, never *why*.

So teams build their own Result types. The consequences, observed across real enterprise codebases:

| Symptom | Cost |
|---|---|
| Every service has a slightly different `Result`/`Response` type | Cross-service code sharing breaks; onboarding friction |
| Error codes are ad-hoc strings or per-project enums | No consistent client contract; error handling untestable |
| Validation failures throw exceptions | Performance cost; stack traces as control flow; noisy logs |
| ProblemDetails mapping is copy-pasted per controller/endpoint | Drift between endpoints; RFC 9457 violations; leaked internals |
| Home-grown types are untested and undocumented | Subtle bugs (e.g., accessing `Value` on failure) reach production |
| Exception mapping to HTTP lives in scattered middleware | Inconsistent 4xx/5xx semantics across an API surface |

## Who has this problem

- **ASP.NET Core developers** hand-writing error → status-code mapping in every endpoint.
- **Clean Architecture / DDD teams** that need domain-layer failure semantics with zero framework references in the domain.
- **Microservice teams** that need the *same* error contract across dozens of services.
- **Library authors** who need to return rich failures without forcing exception handling on consumers.

## Evidence the problem is real

- FluentResults (~50M downloads), ErrorOr, CSharpFunctionalExtensions (~100M downloads), and Ardalis.Result all exist and are heavily used — the demand is proven.
- ASP.NET Core added first-class ProblemDetails services (`AddProblemDetails`, `IProblemDetailsService`) because inconsistent error responses were endemic.
- "Result pattern in C#" is a perennially top-ranked search topic for .NET architecture.

## Why existing packages don't fully solve it

Existing libraries solve the *domain* half (a Result type) or the *HTTP* half (status mapping), but not the whole path with a clean seam between them. See [competitive-analysis.md](competitive-analysis.md).

## The solution in one paragraph

Koras.Results provides `Result` / `Result<T>` structs with a typed, taxonomy-based `Error` model and functional composition, plus satellite packages that project failures into ASP.NET Core ProblemDetails (Minimal API and MVC), convert FluentValidation output, short-circuit MediatR pipelines, and tag OpenTelemetry activities — each opt-in, each depending only on what it must.
