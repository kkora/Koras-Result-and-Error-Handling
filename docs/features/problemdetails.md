# ProblemDetails (RFC 9457) conversion

## Overview

`Koras.Results.AspNetCore` converts failed `Result` / `Result<T>` values into [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) `application/problem+json` responses. A single internal builder is the source of truth for both the Minimal API and MVC adapters, so every failure — plain `Error`, `ValidationError`, or `AggregateError` — produces the same, predictable payload: a mapped HTTP status code, standard `title`/`type` fields, an `errorCode` extension, an optional `traceId` extension, and (for validation failures) an `errors` dictionary grouped by property name.

Configuration lives in `KorasResultsOptions`, registered through `AddKorasResults`. All defaults are safe for production.

## When to use it

- You return `Result`/`Result<T>` from application code and want failures projected into standards-compliant HTTP error responses without hand-writing status-code switches.
- You want one consistent error payload shape across Minimal APIs, MVC controllers, and exception middleware.
- You need stable, machine-readable error codes (`errorCode`) and trace correlation (`traceId`) in client-visible errors.

## When not to use it

- Non-HTTP applications (workers, console tools) — the error taxonomy is useful there, but ProblemDetails is an HTTP payload format; use the core package and `Koras.Results.OpenTelemetry` instead.
- APIs with a bespoke, non-RFC-9457 error envelope contract you cannot change.
- gRPC services, where errors travel as status codes and rich error details, not JSON problem documents.

## Installation

```bash
dotnet add package Koras.Results.AspNetCore
```

The core `Koras.Results` package is a dependency and comes transitively; the package uses the `Microsoft.AspNetCore.App` framework reference, so no additional ASP.NET Core packages are needed.

## Basic configuration

```csharp
using Koras.Results.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKorasResults(); // defaults only; the callback is optional
```

`AddKorasResults` registers `KorasResultsOptions` via the options system and the default pass-through `IErrorMessageLocalizer` (with `TryAddSingleton`, so it never overwrites yours). It is safe to call multiple times.

## Basic usage

Every `ErrorType` has a built-in status code:

| `ErrorType` | Status |
|---|---|
| `Validation` | 400 Bad Request |
| `Unauthorized` | 401 Unauthorized |
| `Forbidden` | 403 Forbidden |
| `NotFound` | 404 Not Found |
| `Conflict` | 409 Conflict |
| `Failure` | 422 Unprocessable Entity |
| `Unexpected` | 500 Internal Server Error |
| `Unavailable` | 503 Service Unavailable |

Endpoint code normally never calls the conversion directly — `ToHttpResult` / `ToActionResult` do it. For middleware, tests, or non-endpoint code, use the eager `ToProblemDetails` overloads:

```csharp
using Koras.Results;
using Koras.Results.AspNetCore;

var error = Error.NotFound("User.NotFound", "No user with id '42'.");
var problem = error.ToProblemDetails(); // built-in defaults, pass-through localizer
// problem.Status == 404, problem.Extensions["errorCode"] == "User.NotFound"
```

A `NotFound` failure rendered by an endpoint produces:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "No todo with id '00000000-0000-0000-0000-000000000001'.",
  "errorCode": "Todo.NotFound",
  "traceId": "00-8f0a..."
}
```

A `ValidationError` becomes a `ValidationProblemDetails` with an `errors` dictionary grouped by property name (matching ASP.NET Core's own shape):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "errors": { "Title": ["'Title' must not be empty."] },
  "errorCode": "Validation.Failed",
  "traceId": "00-8f0a..."
}
```

## Dependency-injection usage

Configure mapping once at startup; the deferred endpoint adapters resolve `IOptions<KorasResultsOptions>`, `IErrorMessageLocalizer`, and `ILoggerFactory` from `HttpContext.RequestServices` at execution time:

```csharp
builder.Services.AddKorasResults(options =>
{
    options.MapErrorType(ErrorType.Failure, StatusCodes.Status400BadRequest);
    options.ProblemTypeUriFactory = error => $"https://errors.example.com/{error.Code}";
});
```

## ASP.NET Core usage

See the [Minimal API guide](minimal-api.md) and the [MVC guide](mvc.md) for the endpoint adapters. The conversion itself is also useful in exception middleware:

```csharp
app.Use(async (context, next) =>
{
    var result = Result.Try(DoWork); // exception boundary -> Result
    if (result.IsFailure)
    {
        var problem = result.ToProblemDetails();
        context.Response.StatusCode = problem.Status ?? 500;
        await context.Response.WriteAsJsonAsync(problem);
        return;
    }
    await next(context);
});
```

## Console application usage

Not applicable: ProblemDetails is an HTTP response format; console applications should consume `Result` values directly via `Match`/`Switch` from the core package.

## Advanced configuration

Status-code resolution follows a strict precedence: **exact error code > error type > built-in default** (`KorasResultsOptions.GetStatusCode`):

```csharp
builder.Services.AddKorasResults(options =>
{
    options.MapErrorType(ErrorType.Failure, StatusCodes.Status400BadRequest); // all Failure -> 400
    options.MapErrorCode("Payment.Required", StatusCodes.Status402PaymentRequired); // wins over type mapping
    options.IncludeTraceId = true;                          // default: true
    options.IncludeUnexpectedErrorDetails = false;          // default: false (secure)
    options.MetadataExposure = MetadataExposurePolicy.None; // default: None (secure)
    options.ProblemTypeUriFactory = error => $"https://errors.example.com/{error.Code}";
});
```

- `ProblemTypeUriFactory` — when `null` (default) the `type` field uses the standard RFC 9110 section URI for the mapped status code; when the factory returns `null` for a given error, that error also falls back to the default.
- `MapErrorCode` uses ordinal string comparison and validates status codes to the 100–599 range.
- `MetadataExposure = MetadataExposurePolicy.All` adds `Error.Metadata` as the `metadata` extension. Only enable this when every error producer treats metadata as client-safe.

## Public API

```csharp
public sealed class KorasResultsOptions
{
    public bool IncludeUnexpectedErrorDetails { get; set; }          // default false
    public MetadataExposurePolicy MetadataExposure { get; set; }     // default None
    public Func<Error, string?>? ProblemTypeUriFactory { get; set; } // default null
    public bool IncludeTraceId { get; set; }                         // default true
    public KorasResultsOptions MapErrorType(ErrorType type, int statusCode);
    public KorasResultsOptions MapErrorCode(string errorCode, int statusCode);
    public int GetStatusCode(Error error);
}

public enum MetadataExposurePolicy { None = 0, All = 1 }

public static class ProblemDetailsExtensions
{
    public static ProblemDetails ToProblemDetails(this Error error, KorasResultsOptions? options = null, IErrorMessageLocalizer? localizer = null);
    public static ProblemDetails ToProblemDetails(this Result result, KorasResultsOptions? options = null, IErrorMessageLocalizer? localizer = null);
    public static ProblemDetails ToProblemDetails<T>(this Result<T> result, KorasResultsOptions? options = null, IErrorMessageLocalizer? localizer = null);
}

public static class KorasResultsServiceCollectionExtensions
{
    public static IServiceCollection AddKorasResults(this IServiceCollection services, Action<KorasResultsOptions>? configure = null);
}
```

## Execution lifecycle

1. `GetStatusCode(error)` resolves the status (code override → type override → default).
2. `ValidationError` builds a `ValidationProblemDetails` with the grouped `errors` dictionary; other errors build a plain `ProblemDetails`.
3. `title`/`type` are filled from the RFC 9110 defaults for the status; `ProblemTypeUriFactory` may then override `type`.
4. `detail` is the localized error message — unless the error is `Unexpected` and `IncludeUnexpectedErrorDetails` is `false`, in which case clients see `"An unexpected error occurred."` and the real message is logged at Warning.
5. Extensions are added: `errorCode` always; `traceId` (from `Activity.Current?.Id`, falling back to `HttpContext.TraceIdentifier`) when `IncludeTraceId` is true; `metadata` when the exposure policy is `All` and metadata is non-empty.

The endpoint adapters run these steps at response-execution time with services from the request; the `ToProblemDetails` overloads run them eagerly with the arguments you pass (defaults when omitted).

## Error handling

Conversion is total — every `ErrorType` maps to a status, and unknown values fall back to 500. Calling `ToProblemDetails` on a *successful* `Result`/`Result<T>` throws `InvalidOperationException` (accessing `Result.Error` on success is a programming error). `ToProblemDetails(Error)` throws `ArgumentNullException` for a null error.

## Cancellation

Not applicable: conversion is a synchronous, in-memory projection with no I/O to cancel.

## Logging

When invoked through the endpoint adapters, the builder logs under the category `Koras.Results.AspNetCore.ResultHttpMapper`:

- **Debug** (event id 1): `Mapped error {ErrorCode} ({ErrorType}) to HTTP {StatusCode}`
- **Warning** (event id 2): `Suppressed details of unexpected error {ErrorCode} from the HTTP response; original message: {ErrorMessage}` — emitted whenever an `Unexpected` error's detail is withheld from the client, so the real message is never lost.

The eager `ToProblemDetails` overloads take no logger and log nothing.

## Telemetry

`traceId` is sourced from `Activity.Current?.Id` (the W3C trace id when OpenTelemetry/ASP.NET Core tracing is active), falling back to `HttpContext.TraceIdentifier`, letting clients quote an id you can look up in your tracing backend. Pair with [Koras.Results.OpenTelemetry](opentelemetry.md) to also tag server-side spans.

## Security considerations

- `IncludeUnexpectedErrorDetails = false` (default) prevents leaking exception-derived internals; keep it off in production.
- `MetadataExposurePolicy.None` (default) keeps `Error.Metadata` server-side; the core `Result.Try` boundary already excludes exception messages by default for the same reason.
- `errorCode` values are part of your public contract — keep them free of sensitive information.

## Performance considerations

Conversion allocates only the `ProblemDetails` object, its extensions dictionary, and (for validation) the grouped errors dictionary. The status/title/type lookup tables are frozen dictionaries; log messages are source-generated (no boxing when the level is disabled). No reflection, no I/O.

## Thread safety

`KorasResultsOptions` is mutable only during configuration; once materialized by the options system it must be treated as read-only (standard options-pattern semantics). `IErrorMessageLocalizer` implementations are singletons and must be thread-safe. `Error` values are immutable.

## Testing applications using this feature

Use the eager overloads — no host required:

```csharp
[Fact]
public void Conflict_maps_to_409_with_error_code()
{
    var problem = Error.Conflict("User.DuplicateEmail", "Email taken.").ToProblemDetails();

    Assert.Equal(409, problem.Status);
    Assert.Equal("User.DuplicateEmail", problem.Extensions["errorCode"]);
}

[Fact]
public void Code_override_beats_type_override()
{
    var options = new KorasResultsOptions()
        .MapErrorType(ErrorType.Failure, 400)
        .MapErrorCode("Payment.Required", 402);

    Assert.Equal(402, options.GetStatusCode(Error.Failure("Payment.Required", "Pay up.")));
}
```

For end-to-end payload assertions, use `WebApplicationFactory<Program>` and assert on the JSON body.

## Complete example

```csharp
using Koras.Results;
using Koras.Results.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKorasResults(options =>
{
    options.MapErrorType(ErrorType.Failure, StatusCodes.Status400BadRequest);
    options.MapErrorCode("Payment.Required", StatusCodes.Status402PaymentRequired);
    options.ProblemTypeUriFactory = error => $"https://errors.example.com/{error.Code}";
});

var app = builder.Build();

app.MapGet("/orders/{id:int}", (int id) =>
{
    Result<string> order = id > 0
        ? $"order-{id}"
        : Error.NotFound("Order.NotFound", $"No order with id '{id}'.");
    return order.ToHttpResult();
});

app.Run();
```

## Common mistakes

- Calling `ToProblemDetails()` on a successful result — it throws; convert only failures, or use the endpoint adapters which branch for you.
- Expecting `MapErrorType` to beat `MapErrorCode` — exact-code mappings always win.
- Enabling `MetadataExposurePolicy.All` while error producers stash internal data (SQL, stack fragments) in metadata.
- Passing explicit `options` to `ToProblemDetails` in endpoints and wondering why `AddKorasResults` configuration is ignored — use the adapters for DI-resolved configuration.

## Troubleshooting

- **Status is 500 instead of my mapping** — the error's `Code` doesn't exactly match your `MapErrorCode` string (comparison is ordinal, case-sensitive), or the failing error is `Error.Uninitialized` from `default(Result)`.
- **No `traceId` in the payload** — `IncludeTraceId` was set to `false`, or the conversion ran outside a request with no current `Activity`.
- **`detail` says "An unexpected error occurred."** — working as intended for `Unexpected` errors; check the Warning log for the original message or opt in with `IncludeUnexpectedErrorDetails = true` (not recommended in production).
- **My custom `type` URI is missing** — `ProblemTypeUriFactory` returned `null` for that error, so the RFC 9110 default was used.

## Related features

- [Minimal API adapters](minimal-api.md) — `ToHttpResult` family.
- [MVC adapters](mvc.md) — `ToActionResult` family.
- [Localization](localization.md) — translate `detail` and validation messages.
- [FluentValidation integration](fluentvalidation.md) — the source of `ValidationError` payloads.
