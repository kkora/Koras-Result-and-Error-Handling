# Minimal API adapters

## Overview

`Koras.Results.AspNetCore` provides one-line adapters that turn `Result` / `Result<T>` values into Minimal API `IResult` responses. Successes map to conventional status codes (204 No Content, 200 OK, 201 Created, or anything via a custom factory); failures become RFC 9457 `application/problem+json` responses driven by [KorasResultsOptions](problemdetails.md). Endpoints stay a single expression — no `if (result.IsFailure)` branching in handlers.

## When to use it

- Minimal API endpoints whose services return `Result`/`Result<T>`.
- You want the HTTP layer to be a pure projection: domain code carries no HTTP awareness, endpoints carry no error-mapping logic.
- You need per-application status remapping (e.g. business failures as 400 instead of 422) without touching endpoints.

## When not to use it

- MVC controllers — use the [`ToActionResult` adapters](mvc.md) instead.
- Endpoints that must return non-ProblemDetails failure payloads for contract reasons.
- Code outside an HTTP request pipeline (workers, console apps) — there is no `HttpContext` to execute an `IResult` against.

## Installation

```bash
dotnet add package Koras.Results.AspNetCore
```

The core `Koras.Results` package comes transitively.

## Basic configuration

None required. The adapters work **without** `AddKorasResults` — when the options are not registered, execution falls back to built-in defaults (default status map, pass-through localizer). Call `AddKorasResults` only to customize:

```csharp
builder.Services.AddKorasResults(options =>
    options.MapErrorType(ErrorType.Failure, StatusCodes.Status400BadRequest));
```

## Basic usage

Success mappings:

| Adapter | Success | Failure |
|---|---|---|
| `Result.ToHttpResult()` | 204 No Content | ProblemDetails |
| `Result<T>.ToHttpResult()` | 200 OK (JSON body) | ProblemDetails |
| `Result<T>.ToHttpResult(onSuccess)` | whatever `onSuccess` returns | ProblemDetails |
| `Result<T>.ToCreatedHttpResult(locationFactory)` | 201 Created + `Location` | ProblemDetails |

```csharp
app.MapGet("/todos/{id:guid}", (Guid id, TodoStore store) =>
    store.Find(id).ToHttpResult());               // 200 or problem

app.MapDelete("/todos/{id:guid}", (Guid id, TodoStore store) =>
    store.Delete(id).ToHttpResult());             // 204 or problem

app.MapPost("/todos", (CreateTodo cmd, TodoStore store) =>
    store.Create(cmd.Title!).ToCreatedHttpResult(todo => $"/todos/{todo.Id}"));

app.MapPost("/imports", (ImportService svc) =>
    svc.Enqueue().ToHttpResult(job => Results.Accepted($"/imports/{job.Id}", job)));
```

A `NotFound` failure produces (with a custom `ProblemTypeUriFactory` configured):

```json
{
  "type": "https://errors.example.com/Todo.NotFound",
  "title": "Not Found",
  "status": 404,
  "detail": "No todo with id '00000000-0000-0000-0000-000000000001'.",
  "errorCode": "Todo.NotFound",
  "traceId": "00-8f0a..."
}
```

## Dependency-injection usage

The adapters themselves take no dependencies; they defer to the request's service provider. Register options and an optional localizer at startup and every endpoint picks them up:

```csharp
builder.Services.AddKorasResults(options =>
{
    options.ProblemTypeUriFactory = error => $"https://errors.example.com/{error.Code}";
});
builder.Services.AddSingleton<IErrorMessageLocalizer, MyLocalizer>(); // optional
```

## ASP.NET Core usage

The `ToHttpResultAsync` overloads are sugar for `Task<Result>`-returning services, keeping endpoints expression-bodied:

```csharp
app.MapGet("/users/{id:guid}", (Guid id, IUserService users, CancellationToken ct) =>
    users.FindAsync(id, ct).ToHttpResultAsync());

app.MapPost("/todos", async (CreateTodo command, IValidator<CreateTodo> validator, TodoStore store, CancellationToken ct) =>
    (await validator.ValidateToResultAsync(command, ct))
        .Bind(valid => store.Create(valid.Title!))
        .ToCreatedHttpResult(todo => $"/todos/{todo.Id}"));
```

## Console application usage

Not applicable: `IResult` executes against an `HttpContext`, which only exists inside an ASP.NET Core request.

## Advanced configuration

All failure-shaping knobs live on `KorasResultsOptions` — status precedence (code > type > default), `IncludeUnexpectedErrorDetails`, `MetadataExposure`, `IncludeTraceId`, `ProblemTypeUriFactory`. See the [ProblemDetails guide](problemdetails.md) for the full matrix. For success responses beyond 200/201/204, the `onSuccess` factory overload accepts any `IResult` (`Results.Accepted`, `Results.File`, custom results).

## Public API

```csharp
public static class HttpResultExtensions
{
    public static IResult ToHttpResult(this Result result);                          // 204 / problem
    public static IResult ToHttpResult<T>(this Result<T> result);                    // 200 / problem
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess);
    public static IResult ToCreatedHttpResult<T>(this Result<T> result, Func<T, string> locationFactory);
    public static Task<IResult> ToHttpResultAsync(this Task<Result> resultTask);
    public static Task<IResult> ToHttpResultAsync<T>(this Task<Result<T>> resultTask);
    public static Task<IResult> ToHttpResultAsync<T>(this Task<Result<T>> resultTask, Func<T, IResult> onSuccess);
}
```

## Execution lifecycle

Failure conversion is **deferred**. The adapter does not build the response; it returns an internal `KorasProblemHttpResult` (an `IResult` that also implements `IStatusCodeHttpResult` with `StatusCode == null`, because the code is unknown until execution). When ASP.NET Core executes it:

1. `ExecuteAsync(HttpContext)` resolves `IOptions<KorasResultsOptions>` (or built-in defaults when absent), `IErrorMessageLocalizer` (or the pass-through default), and an `ILoggerFactory` logger from `HttpContext.RequestServices`.
2. The shared ProblemDetails builder projects the error (status precedence, localization, `errorCode`/`traceId`/`metadata` extensions, suppression of `Unexpected` details).
3. The payload is written via `Results.Problem(problemDetails)`, giving the standard `application/problem+json` response.

This is why the adapters work with or without `AddKorasResults`, and why configuration changes never require touching endpoints.

## Error handling

Failure mapping is total: every `ErrorType` has a default status (Validation 400, Unauthorized 401, Forbidden 403, NotFound 404, Conflict 409, Failure 422, Unavailable 503, Unexpected 500). `onSuccess` / `locationFactory` delegates and awaited `resultTask` arguments are null-checked eagerly (`ArgumentNullException`). Exceptions thrown *inside* your success factory are not caught — they surface to ASP.NET Core's exception handling, as any endpoint exception would.

## Cancellation

The adapters take no `CancellationToken`: they are pure projections of an already-computed result. Cancellation belongs to the operation producing the result — inject `CancellationToken` into the endpoint and pass it to your service (see the POST example above). ASP.NET Core aborts response writing itself when the client disconnects.

## Logging

At execution time, under category `Koras.Results.AspNetCore.ResultHttpMapper`:

- **Debug**: `Mapped error {ErrorCode} ({ErrorType}) to HTTP {StatusCode}` — one line per converted failure.
- **Warning**: emitted when an `Unexpected` error's details are suppressed from the response (includes the original message).

Successful conversions log nothing.

## Telemetry

The ProblemDetails payload carries a `traceId` extension (from `Activity.Current?.Id`, falling back to `HttpContext.TraceIdentifier`) so clients can correlate error reports with server traces. Combine with [Koras.Results.OpenTelemetry](opentelemetry.md) to tag the server-side span with `error.type` / `koras.error.code`.

## Security considerations

Secure defaults apply automatically: `Unexpected` error details are replaced with a generic message (`IncludeUnexpectedErrorDetails = false`) and error metadata is never sent (`MetadataExposurePolicy.None`). Location URIs from `ToCreatedHttpResult` are emitted verbatim — build them from server-generated identifiers, not raw client input.

## Performance considerations

The success paths delegate directly to `Results.NoContent()` / `Results.Ok(value)` / `Results.Created(...)`. The failure path allocates one small `KorasProblemHttpResult` wrapper; all ProblemDetails work happens once, at execution. Async overloads use `ConfigureAwait(false)` and avoid state-machine allocation when arguments are invalid (eager null checks).

## Thread safety

`Result`/`Result<T>`/`Error` are immutable values. Resolved `KorasResultsOptions` is a singleton treated as read-only after configuration; `IErrorMessageLocalizer` implementations must be thread-safe singletons. The adapters hold no shared state.

## Testing applications using this feature

Success results are plain ASP.NET Core results you can assert on directly; failure payloads are best asserted end-to-end:

```csharp
[Fact]
public void Success_maps_to_Ok()
{
    var result = Result.Success(new Todo(Guid.NewGuid(), "x", false));
    var httpResult = result.ToHttpResult();
    Assert.IsType<Ok<Todo>>(httpResult); // Microsoft.AspNetCore.Http.HttpResults
}

[Fact]
public async Task NotFound_produces_problem_json()
{
    await using var factory = new WebApplicationFactory<Program>();
    using var client = factory.CreateClient();

    var response = await client.GetAsync($"/todos/{Guid.Empty}");

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
}
```

For unit-testing the payload shape without a host, call `error.ToProblemDetails(options)` directly.

## Complete example

From `samples/MinimalApi.Sample`:

```csharp
using FluentValidation;
using Koras.Results;
using Koras.Results.AspNetCore;
using Koras.Results.FluentValidation;
using MinimalApiSample;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKorasResults(options =>
{
    options.MapErrorType(ErrorType.Failure, StatusCodes.Status400BadRequest);
    options.ProblemTypeUriFactory = error => $"https://errors.example.com/{error.Code}";
});
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddSingleton<TodoStore>();

var app = builder.Build();

app.MapGet("/todos/{id:guid}", (Guid id, TodoStore store) =>
    store.Find(id).ToHttpResult());

app.MapPost("/todos", async (CreateTodo command, IValidator<CreateTodo> validator, TodoStore store, CancellationToken ct) =>
    (await validator.ValidateToResultAsync(command, ct))
        .Bind(valid => store.Create(valid.Title!))
        .ToCreatedHttpResult(todo => $"/todos/{todo.Id}"));

app.MapPost("/todos/{id:guid}/complete", (Guid id, TodoStore store) =>
    store.Complete(id).ToHttpResult());

app.MapDelete("/todos/{id:guid}", (Guid id, TodoStore store) =>
    store.Delete(id).ToHttpResult());

app.Run();
```

## Common mistakes

- Using `ToHttpResult` in MVC controllers — controllers return `IActionResult`; use [`ToActionResult`](mvc.md).
- Inspecting `IStatusCodeHttpResult.StatusCode` on a failure result in a filter and getting `null` — the status is intentionally unknown until execution, when options are resolved.
- Wrapping the adapters in `try/catch` for error flow — failures are values; only genuine exceptions escape.
- Forgetting that `ToCreatedHttpResult` also serializes the value as the 201 body — return a DTO, not an entity, if they differ.

## Troubleshooting

- **Options configured in `AddKorasResults` are ignored** — the response executed outside the app's request services (e.g. you called `ExecuteAsync` with a hand-built `HttpContext` lacking those registrations); defaults were used by design.
- **`Failure` errors return 422, expected 400** — 422 is the default for `ErrorType.Failure`; remap with `options.MapErrorType(ErrorType.Failure, 400)`.
- **No Debug log lines** — set `"Koras.Results.AspNetCore": "Debug"` (or the full category `Koras.Results.AspNetCore.ResultHttpMapper`) in your logging configuration.
- **`default(Result)` returned 500 with `Result.Uninitialized`** — an endpoint returned an uninitialized struct; always construct results via `Result.Success()` / factories / implicit conversions.

## Related features

- [ProblemDetails conversion](problemdetails.md) — payload shape and all options.
- [MVC adapters](mvc.md) — the controller equivalent.
- [FluentValidation integration](fluentvalidation.md) — `ValidateToResultAsync` in endpoints.
- [Localization](localization.md) — translating client-facing messages.
