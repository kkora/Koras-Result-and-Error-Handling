# MVC (controller) adapters

## Overview

`Koras.Results.AspNetCore` includes MVC adapters that convert `Result` / `Result<T>` values into `IActionResult` / `ActionResult<T>` responses. Successes map to 204 No Content or 200 OK (or any result via a custom factory); failures become RFC 9457 ProblemDetails responses written with the `application/problem+json` content type, using the same builder and [KorasResultsOptions](problemdetails.md) as the Minimal API adapters — so both styles produce byte-for-byte consistent error payloads.

## When to use it

- Controller-based (`[ApiController]`) APIs whose services or MediatR handlers return `Result`/`Result<T>`.
- You want controllers that never inspect errors: one conversion call per action.
- Actions using `ActionResult<T>` for OpenAPI type inference — `ToActionResultOf<T>` keeps the generic signature.

## When not to use it

- Minimal API endpoints — use the [`ToHttpResult` adapters](minimal-api.md), which return `IResult`.
- Razor Pages / server-rendered MVC views, where a failure should render a view rather than a JSON problem document.
- APIs contractually bound to a non-RFC-9457 error envelope.

## Installation

```bash
dotnet add package Koras.Results.AspNetCore
```

The core `Koras.Results` package comes transitively.

## Basic configuration

```csharp
builder.Services.AddControllers();
builder.Services.AddKorasResults(); // optional callback for status remapping etc.
```

`AddKorasResults` is optional: without it, execution falls back to the built-in default status map and pass-through localizer.

## Basic usage

| Adapter | Success | Failure |
|---|---|---|
| `Result.ToActionResult()` | 204 No Content | ProblemDetails |
| `Result<T>.ToActionResult()` | 200 OK (JSON body) | ProblemDetails |
| `Result<T>.ToActionResult(onSuccess)` | whatever `onSuccess` returns | ProblemDetails |
| `Result<T>.ToActionResultOf()` | `ActionResult<T>` carrying the value (200) | ProblemDetails |

```csharp
[HttpGet("{id:guid}")]
public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
    (await mediator.Send(new GetUser(id), cancellationToken)).ToActionResult();

[HttpPost]
public async Task<IActionResult> Register(RegisterUser command, CancellationToken cancellationToken) =>
    (await mediator.Send(command, cancellationToken))
        .ToActionResult(user => CreatedAtAction(nameof(Get), new { id = user.Id }, user));
```

A domain `Conflict` failure produces a 409 with `Content-Type: application/problem+json`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "A user with email 'ada@example.com' already exists.",
  "errorCode": "User.DuplicateEmail",
  "traceId": "00-8f0a..."
}
```

## Dependency-injection usage

The adapters resolve `IOptions<KorasResultsOptions>`, `IErrorMessageLocalizer`, and `ILoggerFactory` from the request's services at execution time; controllers need no extra dependencies. Configure once at startup:

```csharp
builder.Services.AddKorasResults(options =>
    options.MapErrorCode("User.DuplicateEmail", StatusCodes.Status409Conflict)); // 409 is already the Conflict default; shown as an example
```

## ASP.NET Core usage

With `ActionResult<T>` signatures (nice for Swagger/OpenAPI response types):

```csharp
[HttpGet("{id:guid}")]
[ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<ActionResult<UserDto>> Get(Guid id, CancellationToken ct) =>
    (await mediator.Send(new GetUser(id), ct)).ToActionResultOf();
```

The `ToActionResultAsync` overloads keep `Task<Result>`-returning calls expression-bodied:

```csharp
[HttpDelete("{id:guid}")]
public Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
    mediator.Send(new DeleteUser(id), ct).ToActionResultAsync();
```

## Console application usage

Not applicable: `IActionResult` executes against an MVC `ActionContext`, which only exists inside an ASP.NET Core request.

## Advanced configuration

All failure shaping is centralized in `KorasResultsOptions` — status precedence (exact code > error type > default), `IncludeUnexpectedErrorDetails` (default `false`), `MetadataExposure` (default `None`), `IncludeTraceId` (default `true`), and `ProblemTypeUriFactory`. See the [ProblemDetails guide](problemdetails.md). For success responses other than 200/204, pass an `onSuccess` factory returning any `IActionResult` (`CreatedAtAction`, `AcceptedAtAction`, `File`, ...).

## Public API

```csharp
public static class ActionResultExtensions
{
    public static IActionResult ToActionResult(this Result result);               // 204 / problem
    public static IActionResult ToActionResult<T>(this Result<T> result);         // 200 / problem
    public static IActionResult ToActionResult<T>(this Result<T> result, Func<T, IActionResult> onSuccess);
    public static ActionResult<T> ToActionResultOf<T>(this Result<T> result);
    public static Task<IActionResult> ToActionResultAsync(this Task<Result> resultTask);
    public static Task<IActionResult> ToActionResultAsync<T>(this Task<Result<T>> resultTask);
}
```

## Execution lifecycle

Failure conversion is deferred. The adapters return an internal `KorasProblemActionResult` (an `ActionResult`); when MVC executes it:

1. `ExecuteResultAsync(ActionContext)` resolves `IOptions<KorasResultsOptions>`, `IErrorMessageLocalizer`, and a logger from `context.HttpContext.RequestServices` (built-in defaults when not registered).
2. The shared ProblemDetails builder projects the error — status precedence, localized `detail`, `errorCode`/`traceId`/`metadata` extensions, `Unexpected`-detail suppression.
3. The payload is written through an `ObjectResult` with `StatusCode = problemDetails.Status` and `ContentTypes = { "application/problem+json" }`, so content negotiation always emits the problem media type.

Success paths are eager: plain `NoContentResult` / `OkObjectResult` / your factory's result.

## Error handling

Failure mapping is total (Validation 400, Unauthorized 401, Forbidden 403, NotFound 404, Conflict 409, Failure 422, Unavailable 503, Unexpected 500 by default). Null `onSuccess` delegates and null `resultTask` arguments throw `ArgumentNullException` eagerly. Exceptions thrown inside your success factory are not caught and flow to MVC's exception filters/middleware.

## Cancellation

The adapters take no `CancellationToken`; they project an already-computed result. Bind a `CancellationToken` parameter in the action and pass it to the service or `mediator.Send` producing the result — ASP.NET Core wires it to request abort.

## Logging

At execution time, under category `Koras.Results.AspNetCore.ResultHttpMapper`: a **Debug** event `Mapped error {ErrorCode} ({ErrorType}) to HTTP {StatusCode}` per converted failure, and a **Warning** when an `Unexpected` error's details are suppressed from the response (the original message is included in the log entry).

## Telemetry

Failure payloads carry the `traceId` extension (from `Activity.Current?.Id`, falling back to `HttpContext.TraceIdentifier`). Pair with [Koras.Results.OpenTelemetry](opentelemetry.md) to tag the server-side request span with `error.type` and `koras.error.code`.

## Security considerations

Secure defaults are inherited from the ProblemDetails builder: `Unexpected` messages are replaced with a generic detail and metadata is never exposed unless you opt in. Prefer returning DTOs (not entities) from `ToActionResult<T>` so 200/201 bodies expose only intended fields.

## Performance considerations

Success conversion allocates the same `NoContentResult`/`OkObjectResult` you would write by hand. Failure conversion allocates one small deferred `ActionResult` wrapper; ProblemDetails construction happens once at execution with frozen-dictionary lookups and source-generated logging. `ToActionResultAsync` uses `ConfigureAwait(false)`.

## Thread safety

Results and errors are immutable values; the extension classes are stateless. `KorasResultsOptions` is a read-only singleton after configuration, and `IErrorMessageLocalizer` implementations must be thread-safe singletons.

## Testing applications using this feature

Unit-test controllers by asserting on the returned action result type; assert failure payloads end-to-end:

```csharp
[Fact]
public void Success_maps_to_OkObjectResult()
{
    var result = Result.Success(new UserDto(Guid.NewGuid(), "ada@example.com", "Ada"));
    var actionResult = result.ToActionResult();
    var ok = Assert.IsType<OkObjectResult>(actionResult);
    Assert.IsType<UserDto>(ok.Value);
}

[Fact]
public async Task Unknown_user_returns_problem_json_404()
{
    await using var factory = new WebApplicationFactory<Program>();
    using var client = factory.CreateClient();

    var response = await client.GetAsync($"/users/{Guid.Empty}");

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
}
```

## Complete example

From `samples/WebApi.Sample` (controllers + MediatR + validation behavior):

```csharp
// Program.cs
using FluentValidation;
using Koras.Results.AspNetCore;
using Koras.Results.MediatR;
using WebApiSample.Users;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddKorasResults();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddKorasResultsValidationBehavior();
builder.Services.AddSingleton<UserRepository>();

var app = builder.Build();
app.MapControllers();
app.Run();
```

```csharp
// UsersController.cs
using Koras.Results.AspNetCore;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace WebApiSample.Users;

[ApiController]
[Route("users")]
public sealed class UsersController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Register(RegisterUser command, CancellationToken cancellationToken) =>
        (await mediator.Send(command, cancellationToken))
            .ToActionResult(user => CreatedAtAction(nameof(Get), new { id = user.Id }, user));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        (await mediator.Send(new GetUser(id), cancellationToken)).ToActionResult();

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        (await mediator.Send(new DeleteUser(id), cancellationToken)).ToActionResult();
}
```

## Common mistakes

- Returning `ToHttpResult()` (an `IResult`) from a controller action — MVC expects `IActionResult`/`ActionResult<T>`; use `ToActionResult`/`ToActionResultOf`.
- Manually re-mapping errors in the controller (`if (result.IsFailure) return NotFound()`) — this bypasses the configured status mapping, `errorCode`, and `traceId`.
- Expecting `[ApiController]` model-state validation to produce Koras error codes — that pipeline is ASP.NET Core's own; route validation through FluentValidation + `ValidationError` for consistent payloads.
- Forgetting `CreatedAtAction`'s action name must match an existing action when using the `onSuccess` factory (MVC throws at execution otherwise).

## Troubleshooting

- **Response is `application/json` instead of `application/problem+json`** — you built and returned your own `ObjectResult` from `ToProblemDetails` without setting content types; the adapters set it for you.
- **Configured options ignored** — verify `AddKorasResults(...)` runs in the same service collection your host builds; the deferred result resolves options from the *request's* services.
- **Status is 422 for domain failures** — `ErrorType.Failure` defaults to 422; remap via `MapErrorType(ErrorType.Failure, 400)` if your API treats business failures as 400.
- **`InvalidOperationException` from `CreatedAtAction`** — the referenced action or route values don't match; this happens inside your success factory, not in the adapter.

## Related features

- [ProblemDetails conversion](problemdetails.md) — payload shape and options reference.
- [Minimal API adapters](minimal-api.md) — the `IResult` equivalent.
- [MediatR integration](mediatr.md) — validation short-circuiting before handlers.
- [Localization](localization.md) — translating client-facing messages.
