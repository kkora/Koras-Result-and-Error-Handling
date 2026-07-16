<p align="center">
  <img src="assets/icon.png" alt="Koras.Results" width="96" />
</p>

<h1 align="center">Koras.Results</h1>

<p align="center">
  <b>One canonical, allocation-free way to express success and failure in .NET — from typed domain errors to RFC 9457 ProblemDetails responses.</b>
</p>

<p align="center">
  <a href="https://github.com/korastechnologies/koras-results/actions/workflows/build.yml"><img alt="Build" src="https://github.com/korastechnologies/koras-results/actions/workflows/build.yml/badge.svg" /></a>
  <a href="https://github.com/korastechnologies/koras-results/actions/workflows/test.yml"><img alt="Tests" src="https://github.com/korastechnologies/koras-results/actions/workflows/test.yml/badge.svg" /></a>
  <a href="https://www.nuget.org/packages/Koras.Results"><img alt="NuGet" src="https://img.shields.io/nuget/v/Koras.Results.svg" /></a>
  <a href="https://www.nuget.org/packages/Koras.Results"><img alt="Downloads" src="https://img.shields.io/nuget/dt/Koras.Results.svg" /></a>
  <a href="LICENSE"><img alt="License: MIT" src="https://img.shields.io/badge/license-MIT-blue.svg" /></a>
  <img alt=".NET" src="https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-512BD4" />
</p>

---

Every non-trivial .NET codebase eventually invents a `Result<T>`, a pile of error-code strings, and per-endpoint ProblemDetails mapping — all slightly different, all untested. **Koras.Results** replaces them with a small, rigorously tested package family:

| Package | What it gives you | Dependencies |
|---|---|---|
| **Koras.Results** | `Result` / `Result<T>` structs, semantic error taxonomy, functional composition, JSON serialization | **none** |
| **Koras.Results.AspNetCore** | RFC 9457 ProblemDetails for Minimal APIs & MVC, one extension method per endpoint | framework only |
| **Koras.Results.FluentValidation** | Validate directly into `Result<T>` | FluentValidation |
| **Koras.Results.MediatR** | Validation pipeline behavior that short-circuits with failed results (no exceptions) | MediatR 12.x |
| **Koras.Results.OpenTelemetry** | `error.type` / `error.code` trace tags with one call | **none** |

## Key features

- ⚡ **Allocation-free success path** — `readonly struct` results; a success allocates nothing. The failure path is ~1000× cheaper than throwing an exception ([benchmarks](docs/performance/benchmarks.md)).
- 🧭 **Domain-first error taxonomy** — `Validation`, `NotFound`, `Conflict`, `Unauthorized`, `Forbidden`, `Unavailable`, `Failure`, `Unexpected` — business semantics in the domain, HTTP status codes only at the edge.
- 🧩 **Whole-path coverage** — typed errors carry code + metadata all the way into a standards-compliant `application/problem+json` response, including the validation `errors` dictionary.
- 🔒 **Secure by default** — unexpected-error details are never sent to clients unless you opt in; metadata exposure is off by default; the default exception mapper never leaks exception messages.
- 🛡️ **Fail-safe by construction** — `default(Result)` is a failure, success can never carry `null`, and cancellation is never converted into a failure.
- 📦 **Zero-dependency core** — safe to reference from your innermost domain project.
- 🔎 **Locked public API** — every surface change is tracked by API analyzers; no accidental breaking changes.

## Installation

```bash
dotnet add package Koras.Results
dotnet add package Koras.Results.AspNetCore        # web APIs
dotnet add package Koras.Results.FluentValidation  # optional
dotnet add package Koras.Results.MediatR           # optional
dotnet add package Koras.Results.OpenTelemetry     # optional
```

Supported target frameworks: **net8.0, net9.0, net10.0**.

## Five-minute quick start

**1. Define your errors once** — stable codes, business semantics:

```csharp
using Koras.Results;

public static class TodoErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("Todo.NotFound", $"No todo with id '{id}'.");

    public static Error AlreadyCompleted(Guid id) =>
        Error.Failure("Todo.AlreadyCompleted", $"Todo '{id}' is already completed.");
}
```

**2. Return results from your domain** — no exceptions, no nulls:

```csharp
public Result<Todo> Complete(Guid id)
{
    if (!_todos.TryGetValue(id, out var todo))
    {
        return TodoErrors.NotFound(id);          // implicit conversion to failure
    }

    if (todo.Completed)
    {
        return TodoErrors.AlreadyCompleted(id);
    }

    return todo with { Completed = true };       // implicit conversion to success
}
```

**3. Convert at the edge** — one method per endpoint:

```csharp
builder.Services.AddKorasResults();

app.MapPost("/todos/{id:guid}/complete", (Guid id, TodoStore store) =>
    store.Complete(id).ToHttpResult());
```

**4. Get correct HTTP for free:**

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "No todo with id '00000000-0000-0000-0000-000000000001'.",
  "errorCode": "Todo.NotFound",
  "traceId": "00-9e497e6d4eda6f2c806fe28d91f9a17c-a205171db4368327-00"
}
```

`NotFound` → 404, `ValidationError` → 400 with an `errors` dictionary, `Conflict` → 409, `Unavailable` → 503 — all overridable per type or per error code.

## Composition

```csharp
var outcome = await ParseOrder(input)                        // Result<Order>
    .Bind(order => catalog.Find(order.Sku)
        .Map(product => (order, product)))
    .Ensure(p => p.product.Stock >= p.order.Quantity,
            p => Error.Conflict("Order.InsufficientStock", $"Only {p.product.Stock} left."))
    .MapAsync(p => pricing.PriceAsync(p.order, p.product))   // async continues the chain
    .TapErrorAsync(error => logger.LogWarning("Order rejected: {Code}", error.Code))
    .MatchAsync(total => $"Total: {total:C}", error => $"Rejected: {error.Code}");
```

Failures short-circuit: later steps never run and the original error propagates untouched. Exceptions stay exceptions — bridge them explicitly at infrastructure boundaries with `Result.Try` / `Result.TryAsync` (cancellation always rethrows).

## Dependency injection & configuration

```csharp
builder.Services.AddKorasResults(options =>
{
    options.MapErrorType(ErrorType.Failure, StatusCodes.Status400BadRequest); // house style
    options.MapErrorCode("Order.InsufficientStock", StatusCodes.Status409Conflict);
    options.ProblemTypeUriFactory = error => $"https://errors.example.com/{error.Code}";
    // Secure defaults you can (but probably shouldn't) change:
    // options.IncludeUnexpectedErrorDetails = true;
    // options.MetadataExposure = MetadataExposurePolicy.All;
});
```

## Validation without exceptions

```csharp
// FluentValidation → Result
Result<CreateTodo> validated = await validator.ValidateToResultAsync(command, ct);

// MediatR: invalid commands never reach your handler
services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
services.AddValidatorsFromAssemblyContaining<Program>();
services.AddKorasResultsValidationBehavior();
```

## Observability

```csharp
var result = await orders.PlaceAsync(cmd, ct)
    .TapActivityErrorAsync();   // trace tags: error.type=conflict, koras.error.code=Order.InsufficientStock
```

ProblemDetails responses carry a `traceId` extension so client error reports join server traces.

## Documentation

- 📚 [Documentation index](docs/index.md) · [Quick start](docs/getting-started/quick-start.md) · [Concepts](docs/concepts/overview.md)
- 🧭 Guides: [Minimal APIs](docs/guides/minimal-api.md) · [MVC](docs/guides/aspnet-core.md) · [Workers](docs/guides/worker-service.md) · [Testing](docs/guides/testing.md)
- ⚙️ [Configuration reference](docs/configuration/all-options.md) · [Troubleshooting](docs/troubleshooting/common-errors.md) · [FAQ](docs/troubleshooting/faq.md)
- 🏛️ [Architecture](docs/architecture/overview.md) & [decision records](docs/architecture/decision-records/README.md) · [Public API design](docs/api/public-api-design.md)

## Samples

| Sample | Shows |
|---|---|
| [Console.Sample](samples/Console.Sample) | Core composition, validation errors, `Try`, `Combine` |
| [MinimalApi.Sample](samples/MinimalApi.Sample) | Todo API with ProblemDetails + FluentValidation |
| [WebApi.Sample](samples/WebApi.Sample) | MVC + MediatR validation behavior |
| [WorkerService.Sample](samples/WorkerService.Sample) | Taxonomy-driven retries + OpenTelemetry tagging |

## Architecture

```
Koras.Results  (core — zero dependencies)
├── Koras.Results.AspNetCore       (framework reference only)
├── Koras.Results.FluentValidation (FluentValidation ≥ 11)
├── Koras.Results.MediatR          (MediatR 12.x — Apache-2.0 line, deliberately capped)
└── Koras.Results.OpenTelemetry    (zero dependencies — in-box Activity)
```

Satellites depend on the core; the core depends on nothing. Enforced by architecture tests on every build.

## Security

Secure by default: unexpected-error details and error metadata never reach clients unless explicitly enabled, and the default exception mapper excludes exception messages. The JSON converters construct only sealed, known types — no polymorphic deserialization. See the [threat model](docs/security/threat-model.md) and [SECURITY.md](SECURITY.md) for private vulnerability reporting.

## Performance

Success results are allocation-free `readonly struct`s; a full `Map → Bind → Ensure → Match` chain runs in ~8 ns with zero allocations, and returning a failure is ~1000× cheaper than throwing. Methodology and full numbers: [benchmarks](docs/performance/benchmarks.md).

## Versioning

Semantic versioning with a hard no-breaking-changes rule within a major — binary, source, behavioral, and wire-format — enforced by API analyzers and package validation. Details: [versioning policy](docs/release/versioning.md).

## Contributing & support

- [CONTRIBUTING.md](CONTRIBUTING.md) — build/test instructions, PR checklist, API-change workflow
- [SUPPORT.md](SUPPORT.md) — where to ask questions
- [ROADMAP.md](ROADMAP.md) — what's planned
- [CHANGELOG.md](CHANGELOG.md) — what's shipped

## License

[MIT](LICENSE) © Koras Technologies
