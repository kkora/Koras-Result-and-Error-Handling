# MediatR integration

## Overview

`Koras.Results.MediatR` provides `ValidationBehavior<TRequest, TResponse>`, a MediatR pipeline behavior that runs all registered FluentValidation validators for a request and, when validation fails, **short-circuits with a failed `Result` / `Result<T>`** carrying a single aggregated `ValidationError` — instead of throwing `ValidationException`. Handlers never see invalid requests, and controllers/endpoints convert the failure like any other (`ToActionResult`, `ToHttpResult`).

> **Licensing note (ADR-0006):** the package pins MediatR to **`[12.4.0, 13.0.0)`** — the Apache-2.0 licensed 12.x line. MediatR 13+ moved to a commercial license; an MIT package silently pulling a commercially-licensed transitive dependency would be a trust violation, so users on MediatR 13+ get an explicit NuGet version conflict rather than silent license exposure. See `docs/architecture/decision-records/0006-mediatr-version-pin.md`.

## When to use it

- CQRS-style applications where MediatR handlers return `Result` / `Result<T>` and validation should happen once, in the pipeline, before any handler runs.
- You want validation failures to flow as values through the same Result-to-HTTP conversion as domain failures.
- Multiple validators per request whose failures should aggregate into one `ValidationError`.

## When not to use it

- You're on MediatR 13+ — the version pin will produce a NuGet conflict by design; port the ~100-line behavior yourself if you accept the licensing.
- Handlers that return non-`Result` responses and rely on `ValidationException` — this behavior deliberately throws `InvalidOperationException` in that combination instead.
- Non-MediatR designs — validate at the boundary with [`Koras.Results.FluentValidation`](fluentvalidation.md) directly.

## Installation

```bash
dotnet add package Koras.Results.MediatR
```

The core `Koras.Results` package, `Koras.Results.FluentValidation`, `FluentValidation`, and `MediatR [12.4.0, 13.0.0)` come transitively.

## Basic configuration

Register MediatR, your validators, and the behavior:

```csharp
using FluentValidation;
using Koras.Results.MediatR;

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddKorasResultsValidationBehavior();
```

`AddKorasResultsValidationBehavior` registers the open-generic behavior transiently via `TryAddEnumerable`, so it is safe to call multiple times and composes with your other pipeline behaviors (registration order defines pipeline order).

## Basic usage

```csharp
using FluentValidation;
using Koras.Results;
using MediatR;

public sealed record RegisterUser(string Email, string DisplayName) : IRequest<Result<UserDto>>;

public sealed class RegisterUserValidator : AbstractValidator<RegisterUser>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithErrorCode("User.EmailInvalid");
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(64);
    }
}

public sealed class RegisterUserHandler(UserRepository repository) : IRequestHandler<RegisterUser, Result<UserDto>>
{
    // Only ever invoked with a valid request.
    public Task<Result<UserDto>> Handle(RegisterUser request, CancellationToken cancellationToken) =>
        Task.FromResult(repository.Add(request.Email, request.DisplayName));
}
```

Sending an invalid `RegisterUser` returns `Result<UserDto>.IsFailure == true` with a `ValidationError` aggregating every validator's failures; the handler never runs.

## Dependency-injection usage

The behavior receives `IEnumerable<IValidator<TRequest>>` from the container — zero validators means straight pass-through, so requests without validators cost almost nothing. Both MediatR and the validators must be registered separately (the package registers only the behavior).

## ASP.NET Core usage

Controllers stay one-liners; validation failures become 400 ProblemDetails with the `errors` dictionary via `Koras.Results.AspNetCore`:

```csharp
[HttpPost]
public async Task<IActionResult> Register(RegisterUser command, CancellationToken cancellationToken) =>
    (await mediator.Send(command, cancellationToken))
        .ToActionResult(user => CreatedAtAction(nameof(Get), new { id = user.Id }, user));
```

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "errors": {
    "Email": ["'Email' is not a valid email address."],
    "DisplayName": ["'Display Name' must not be empty."]
  },
  "errorCode": "Validation.Failed",
  "traceId": "00-8f0a..."
}
```

## Console application usage

Works in any generic host — MediatR has no web dependency:

```csharp
using var host = Host.CreateApplicationBuilder(args).Build(); // after the registrations above
var mediator = host.Services.GetRequiredService<IMediator>();
var outcome = await mediator.Send(new RegisterUser("not-an-email", ""));
outcome.Switch(
    onSuccess: user => Console.WriteLine($"Created {user.Id}"),
    onFailure: error => Console.Error.WriteLine(error.Message));
```

## Advanced configuration

The behavior itself has no options. Control it through composition:

- **Pipeline order** — register logging/telemetry behaviors before `AddKorasResultsValidationBehavior()` if they should observe short-circuited failures.
- **Per-request opt-out** — simply don't register validators for that request type.
- **HTTP status of validation failures** — configured in `KorasResultsOptions` (`ErrorType.Validation` defaults to 400).

## Public API

```csharp
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators);
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}

public static class KorasResultsMediatRServiceCollectionExtensions
{
    public static IServiceCollection AddKorasResultsValidationBehavior(this IServiceCollection services);
}
```

## Execution lifecycle

1. With no validators registered for `TRequest`, the behavior immediately awaits `next()` — pass-through.
2. Otherwise, every validator runs sequentially via `ValidateAsync`. Each validator gets a **fresh `ValidationContext<TRequest>`** — FluentValidation accumulates failures on a shared context, which would duplicate earlier validators' failures into later results.
3. All failures are collected; zero failures → `next()` runs the handler.
4. On failure, the failures aggregate into one `ValidationError` (via `Koras.Results.FluentValidation`). If `TResponse` is `Result` or `Result<T>`, a cached per-closed-generic failure factory returns the failed result — the handler is never invoked. Otherwise the behavior throws `InvalidOperationException` with guidance (fail-fast beats silently swallowing validation).

## Error handling

- Validation failure with a `Result`/`Result<T>` response: returned as a failed result — never an exception.
- Validation failure with any other response type: `InvalidOperationException` naming the request and response types; change the handler to return a `Result`, or validate before sending.
- Exceptions thrown by validator rules themselves are not caught and propagate through MediatR.
- Null `request`/`next` throw `ArgumentNullException`.

## Cancellation

The pipeline's `CancellationToken` is passed to every `ValidateAsync` call, so async rules cancel promptly. Cancellation surfaces as `OperationCanceledException` and is never converted into a failed result.

## Logging

Not applicable: the behavior logs nothing itself; add a logging pipeline behavior around it, or rely on the ASP.NET Core mapper's Debug event when the failure is converted to HTTP.

## Telemetry

Not applicable directly: no tags are emitted here; combine with [`Koras.Results.OpenTelemetry`](opentelemetry.md) (e.g. `TapActivityErrorAsync` around `mediator.Send`) to record short-circuited failures on the current activity.

## Security considerations

Validation messages aggregated here typically reach clients through ProblemDetails — keep rule messages free of sensitive data. The dependency pin `[12.4.0, 13.0.0)` is itself a supply-chain/licensing safeguard: upgrading to commercially-licensed MediatR 13+ must be an explicit, visible decision in *your* project, never a silent transitive change.

## Performance considerations

- The `Result`/`Result<T>` failure factory is resolved once per closed generic type in a static field; the `Result<T>` path binds `Result.Failure<T>(Error)` reflection once, then each failure is a delegate invocation.
- Zero-validator requests short-circuit to `next()` after one length check.
- Validators run sequentially (deterministic aggregation order), each with a fresh context allocation — the cost of correctness over shared-context reuse.

## Thread safety

The behavior is stateless per request and registered **transient** (MediatR's convention); the cached failure factory is a static readonly delegate, safe for concurrent use. Validators should be thread-safe (standard FluentValidation guidance).

## Testing applications using this feature

Test the behavior directly — no host needed:

```csharp
[Fact]
public async Task Invalid_request_short_circuits_with_ValidationError()
{
    var behavior = new ValidationBehavior<RegisterUser, Result<UserDto>>(
        [new RegisterUserValidator()]);
    var handlerRan = false;

    var response = await behavior.Handle(
        new RegisterUser("not-an-email", ""),
        () => { handlerRan = true; return Task.FromResult(Result.Success(SomeUser())); },
        CancellationToken.None);

    Assert.False(handlerRan);
    Assert.True(response.IsFailure);
    Assert.IsType<ValidationError>(response.Error);
}

[Fact]
public async Task Valid_request_reaches_the_handler()
{
    var behavior = new ValidationBehavior<RegisterUser, Result<UserDto>>(
        [new RegisterUserValidator()]);

    var response = await behavior.Handle(
        new RegisterUser("ada@example.com", "Ada"),
        () => Task.FromResult(Result.Success(SomeUser())),
        CancellationToken.None);

    Assert.True(response.IsSuccess);
}
```

## Complete example

From `samples/WebApi.Sample`:

```csharp
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

With `RegisterUser` / `RegisterUserValidator` / `RegisterUserHandler` as shown under **Basic usage**, an invalid POST returns a 400 ProblemDetails and the handler never executes; a valid POST reaches the handler, which may still return domain failures (e.g. `User.DuplicateEmail` → 409).

## Common mistakes

- Handler request declared as `IRequest<UserDto>` instead of `IRequest<Result<UserDto>>` — validation failure then throws `InvalidOperationException` by design; return `Result`-typed responses.
- Forgetting `AddValidatorsFromAssemblyContaining` — no validators are resolved, so *everything* passes through unvalidated (no error, no warning).
- Forgetting `AddKorasResultsValidationBehavior()` — validators exist but never run in the pipeline.
- Adding `MediatR` 13+ directly to your project alongside this package and being surprised by a NuGet version conflict — that conflict is the intended licensing guardrail (ADR-0006).
- Re-validating inside the handler — the pipeline already guarantees validity.

## Troubleshooting

- **`InvalidOperationException: Request 'X' failed validation, but its response type 'Y' is not Result or Result<T>...`** — exactly what it says: change the handler's response to `Result`/`Result<T>`, or validate before `Send`.
- **NuGet error NU1107 (version conflict) on MediatR** — another package or your project demands MediatR ≥ 13.0; this package pins `[12.4.0, 13.0.0)` deliberately. Align on 12.x or drop `Koras.Results.MediatR` and port the behavior.
- **Duplicate validation messages** — you also registered FluentValidation's own or another validation behavior; keep one.
- **Failures from only one validator appear** — check validators are all registered for the *same* `TRequest` type (not a base type); MediatR resolves `IValidator<TRequest>` exactly.

## Related features

- [FluentValidation integration](fluentvalidation.md) — the conversion layer this behavior uses.
- [ProblemDetails conversion](problemdetails.md) — how the aggregated `ValidationError` renders over HTTP.
- [MVC adapters](mvc.md) — `ToActionResult` in controllers dispatching MediatR requests.
- ADR-0006 — `docs/architecture/decision-records/0006-mediatr-version-pin.md`.
