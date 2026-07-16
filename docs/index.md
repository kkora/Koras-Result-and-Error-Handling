# Koras.Results Documentation

**Koras.Results gives every .NET team one canonical, allocation-free way to express success and failure — from the domain layer all the way to an RFC 9457 ProblemDetails HTTP response — without exceptions as control flow and without framework lock-in.** The core package (`Koras.Results`) is a zero-dependency library of `readonly struct` result types and an immutable, semantically classified error model; four opt-in satellite packages project those results into ASP.NET Core responses, FluentValidation pipelines, MediatR behaviors, and OpenTelemetry activity tags. Errors are classified by business meaning (`NotFound`, `Conflict`, `Validation`, …), carry stable machine-readable codes, and are translated into correct HTTP responses with zero per-endpoint mapping code.

## Packages

| Package | Purpose | Dependencies |
|---|---|---|
| [`Koras.Results`](https://www.nuget.org/packages/Koras.Results) | Core: `Result`, `Result<T>`, the `Error` model, composition (`Map`/`Bind`/`Match`/`Ensure`/`Try`/`Combine`), System.Text.Json serialization | **None** |
| [`Koras.Results.AspNetCore`](https://www.nuget.org/packages/Koras.Results.AspNetCore) | RFC 9457 ProblemDetails projection, Minimal API `IResult` and MVC `IActionResult` adapters, configurable status mapping, localization hooks | ASP.NET Core shared framework |
| [`Koras.Results.FluentValidation`](https://www.nuget.org/packages/Koras.Results.FluentValidation) | `ValidationResult` → `ValidationError` conversion; `ValidateToResult` / `ValidateToResultAsync` | FluentValidation |
| [`Koras.Results.MediatR`](https://www.nuget.org/packages/Koras.Results.MediatR) | Pipeline behavior that short-circuits invalid requests with a failed `Result` instead of throwing | MediatR 12.x (Apache-2.0), Koras.Results.FluentValidation |
| [`Koras.Results.OpenTelemetry`](https://www.nuget.org/packages/Koras.Results.OpenTelemetry) | Tags the current `Activity` with `error.type` and `koras.error.code` on failures | System.Diagnostics.DiagnosticSource |

All packages are MIT-licensed and target `net8.0`, `net9.0`, and `net10.0`. Source: [github.com/korastechnologies/koras-results](https://github.com/korastechnologies/koras-results).

## Documentation map

### Getting started
- [Installation](getting-started/installation.md) — packages, TFMs, SDK requirements
- [Quick start](getting-started/quick-start.md) — from `dotnet add package` to a ProblemDetails response in five minutes
- [Your first application](getting-started/first-application.md) — build the Minimal API todo sample step by step
- [Dependency injection](getting-started/dependency-injection.md) — `AddKorasResults`, lifetimes, localizer replacement, testing
- [Configuration](getting-started/configuration.md) — `KorasResultsOptions` tour

### Concepts
- [Overview](concepts/overview.md) — why expected failures should be values, not exceptions
- [Architecture](concepts/architecture.md) — package topology and dependency direction
- [Core abstractions](concepts/core-abstractions.md) — `Result`, `Result<T>`, `Error`, `ErrorType`, `ValidationError`, `AggregateError`
- [Result lifecycle](concepts/lifecycle.md) — creation → composition → terminal consumption
- [Error handling](concepts/error-handling.md) — designing error catalogs, choosing error types, metadata rules
- [Cancellation](concepts/cancellation.md) — why cancellation is never a failure
- [Thread safety](concepts/thread-safety.md) — immutability, struct copies, singleton safety

### Using the library
- [Features](features/) — feature catalog, feature matrix, MVP scope, roadmap
- [Guides](guides/) — task-oriented walkthroughs (Minimal API, MVC, MediatR, workers)
- [Recipes](recipes/) — copy-paste solutions for common situations
- [Configuration reference](configuration/) — every option, every default
- [Troubleshooting](troubleshooting/) — symptoms, causes, fixes
- [Migration](migration/) — moving from FluentResults, ErrorOr, exceptions, or hand-rolled results

### Reference
- [API reference](api-reference/) — generated per-member reference
- [API design](api/) — public API contract, naming guidelines, backward-compatibility policy
- [Architecture (deep)](architecture/) — overview, error model, package boundaries, dependency rules, observability, [decision records](architecture/decision-records/)
- [Security](security/) — leak-safe defaults, metadata exposure policy, reporting
- [Performance](performance/) — allocation behavior, benchmarks
- [Testing](testing/) — testing code that returns results; testing strategy of the library itself
- [Release](release/) — versioning policy, changelogs, support windows

### Project
- [Product](product/) — vision, personas, use cases, competitive analysis, roadmap
- [Planning](planning/) — implementation plan, milestones, backlog, definition of done

## Runnable samples

Each sample in [`samples/`](../samples/) is a self-contained project with its own README:

| Sample | Shows |
|---|---|
| [`Console.Sample`](../samples/Console.Sample/) | Zero-dependency core: composition, `Result.Try`, `Combine`, async pipelines |
| [`MinimalApi.Sample`](../samples/MinimalApi.Sample/) | Minimal API todos: `ToHttpResult`, `ToCreatedHttpResult`, FluentValidation, status remapping |
| [`WebApi.Sample`](../samples/WebApi.Sample/) | MVC + MediatR: controllers, `ValidationBehavior`, `ToActionResult` |
| [`WorkerService.Sample`](../samples/WorkerService.Sample/) | Non-HTTP usage: `Result.TryAsync`, taxonomy-driven retries, OpenTelemetry tagging, clean cancellation |

## Where to start

- New to the Result pattern? Read the [concepts overview](concepts/overview.md), then the [quick start](getting-started/quick-start.md).
- Evaluating the library? Skim [core abstractions](concepts/core-abstractions.md) and the [product vision](product/product-vision.md).
- Ready to build? Follow [your first application](getting-started/first-application.md).
