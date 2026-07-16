# Architecture Overview — Koras.Results

## Shape: core + integration satellites

Koras.Results is **one zero-dependency core package plus four integration packages**, not a monolith and not a metapackage. Rationale (ADR-0001):

- The core must be safe to reference from a domain layer — that mandates zero dependencies.
- Each integration (ASP.NET Core, FluentValidation, MediatR, OpenTelemetry) drags its own dependency axis; bundling any of them into the core would tax every consumer.
- A metapackage adds a versioning surface without adding value at this scale (5 packages).

```
Koras.Results                    (core: no dependencies)
├── Koras.Results.AspNetCore     (+ ASP.NET Core shared framework)
├── Koras.Results.FluentValidation (+ FluentValidation)
├── Koras.Results.MediatR        (+ MediatR 12.x, → also refs Koras.Results.FluentValidation)
└── Koras.Results.OpenTelemetry  (+ System.Diagnostics.DiagnosticSource)
```

## Layered view

| Layer | Types | Owner |
|---|---|---|
| **Primitives** | `Result`, `Result<T>` (readonly structs) | Core |
| **Error model** | `Error`, `ValidationError`, `FieldError`, `AggregateError`, `ErrorType` | Core |
| **Composition** | `ResultExtensions` (Map/Bind/Match/Ensure/Tap/…), async variants, `Result.Try*`, `Result.Combine` | Core |
| **Serialization** | STJ converters, attribute-wired | Core |
| **Projections** | ProblemDetails / IResult / IActionResult / Activity tags / FluentValidation adapters | Satellites |
| **Configuration** | `KorasResultsOptions`, DI registration | AspNetCore satellite |

## Key design decisions (with ADR references)

| Decision | Choice | ADR |
|---|---|---|
| Package topology | Core + satellites | ADR-0001 |
| Target frameworks | `net8.0;net9.0;net10.0`, no netstandard2.0 | ADR-0002 |
| Result representation | `readonly struct`, default = failure | ADR-0003 |
| Error taxonomy | 8 fixed `ErrorType` values, domain-first semantics | ADR-0004 |
| HTTP mapping location | Exclusively in AspNetCore package; core never references HTTP | ADR-0004 |
| Error object model | Immutable class with static factories; `ValidationError` subclass | ADR-0005 |
| MediatR version pin | 12.x (Apache-2.0), not 13+ (commercial) | ADR-0006 |
| Serialization | System.Text.Json only, converter-attributed | ADR-0007 |
| API stability | PublicApiAnalyzers with Shipped/Unshipped tracking | ADR-0008 |

## Cross-cutting models

- **Thread safety:** everything public is immutable; all types are safe to share across threads. Satellite services are registered as singletons.
- **Async model:** async-first extension surface; `ConfigureAwait(false)` in all library code; no sync-over-async; `ValueTask` is not used in public signatures (simplicity > micro-optimization; documented in ADR-0003 notes).
- **Cancellation model:** cancellation is *not* failure. `OperationCanceledException` always propagates; `Result.TryAsync` explicitly rethrows it. Async APIs that accept user delegates performing I/O accept a `CancellationToken`.
- **Error model:** see [error-model.md](error-model.md).
- **Logging model:** the core never logs (it's a data library). The AspNetCore package logs through `ILogger<T>` only at Debug level for mapping decisions and Warning for suppressed `Unexpected` details.
- **Telemetry model:** see [observability.md](observability.md).
- **Caching model:** none — the library holds no state and caches nothing beyond static readonly sentinels (`Error.None`).
- **Versioning:** SemVer 2.0; public API frozen within a major; TFMs change only in majors. See `docs/release/versioning.md`.

## What the core will never do

Reference ASP.NET Core, perform I/O, log, read configuration, touch statics that mutate, or throw for control flow.
