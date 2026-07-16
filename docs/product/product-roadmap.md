# Product Roadmap — Koras.Results

## Release train

| Version | Theme | Target contents |
|---|---|---|
| **0.1.0-preview.1** | Core preview | `Koras.Results` core + `Koras.Results.AspNetCore`; docs skeleton; CI |
| **0.1.0** | Full preview | All five packages; full test suite; samples; complete docs |
| **0.5.0** | API freeze candidate | Feedback-driven API refinements; benchmark-driven tuning; migration guides; `PublicAPI.Shipped.txt` candidate baseline |
| **1.0.0** | Stable | Frozen public API; compatibility policy in force; NuGet prefix reservation |
| **1.1.0** | Ecosystem | EF Core exception mapping; localization providers; LINQ query syntax for `Result<T>` |
| **1.2.0** | Protocol edges | gRPC status conversion; GraphQL (HotChocolate) error conversion |
| **2.0.0** | Source generation | Source-generated error catalogs; source-generated mapping; TFM roll-forward (drop EOL frameworks) |

## Feature classification

### MVP (0.1.0)
Result/Result\<T>, error taxonomy, ValidationError, combinators (sync + async), Try/exception conversion, Combine, JSON serialization, ProblemDetails + Minimal API + MVC integration, FluentValidation conversion, MediatR validation behavior, OpenTelemetry activity tagging, DI/options for AspNetCore package.

### Version 1.1
- `Koras.Results.EntityFrameworkCore` — `DbUpdateException` → `Conflict`/`Unavailable` mapping helpers
- Localization provider abstractions beyond the hook (IStringLocalizer adapter)
- LINQ query-expression support (`Select`/`SelectMany`/`Where` for `Result<T>`)

### Version 1.2
- `Koras.Results.Grpc` — `ErrorType` → `StatusCode` mapping, `RpcException` conversion
- `Koras.Results.GraphQL` — `IError` conversion for HotChocolate

### Version 2.0
- Source-generated error catalogs (`[ErrorCatalog]` partial classes; compile-time uniqueness of codes)
- Source-generated ProblemDetails mappers (zero-reflection AOT path)

### Experimental
- Analyzer package (`Koras.Results.Analyzers`): warn on unobserved `Result` values, `Value` access without `IsSuccess` check. High value; prototyped post-1.0.

### Out of scope
- Option/Maybe types, Either, monad transformers
- Validation rule engine (use FluentValidation)
- Mediator implementation (use MediatR or similar)
- HTTP client resilience/error handling
- Exception-replacement policy frameworks

## Support policy

- Latest major receives features; previous major receives fixes for 12 months after the next major ships.
- TFMs are dropped only in major versions, tracking Microsoft's support lifecycle.
