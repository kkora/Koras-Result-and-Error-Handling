# FluentValidation integration

## Overview

`Koras.Results.FluentValidation` bridges FluentValidation and the Result pattern: a `ValidationResult` converts into a `Result` / `Result<T>` carrying a typed `ValidationError`, and validators can validate *directly* into results with `ValidateToResult` / `ValidateToResultAsync` — no `ValidationException`, no separate `IsValid` inspection. Property names, messages, and FluentValidation error codes are preserved on each `FieldError`, so downstream consumers (ProblemDetails, localization, telemetry) keep full fidelity.

## When to use it

- Application code that validates input and wants failures as values flowing through `Bind`/`Map` pipelines rather than exceptions.
- Minimal API endpoints validating request DTOs before invoking domain logic.
- Anywhere you need FluentValidation failures rendered as RFC 9457 `errors` dictionaries via `Koras.Results.AspNetCore`.

## When not to use it

- You don't use FluentValidation — construct `ValidationError` / `FieldError` from the core package directly.
- MediatR pipelines — prefer [`Koras.Results.MediatR`](mediatr.md), which runs the validators for you and short-circuits before the handler (it builds on this package).
- Throw-based validation flows where `ValidationException` is intentionally part of the contract.

## Installation

```bash
dotnet add package Koras.Results.FluentValidation
```

The core `Koras.Results` package and `FluentValidation` come transitively. Add `FluentValidation.DependencyInjectionExtensions` if you register validators through DI.

## Basic configuration

No options exist; the package is a set of pure extension methods. The only setup is registering your validators (any FluentValidation-supported way):

```csharp
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
```

## Basic usage

```csharp
using FluentValidation;
using Koras.Results;
using Koras.Results.FluentValidation;

public sealed record CreateTodo(string? Title);

public sealed class CreateTodoValidator : AbstractValidator<CreateTodo>
{
    public CreateTodoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithErrorCode("Todo.TitleRequired");
        RuleFor(x => x.Title).MaximumLength(200).WithErrorCode("Todo.TitleTooLong");
    }
}

var validator = new CreateTodoValidator();

// Validate directly into a Result<T>
Result<CreateTodo> result = validator.ValidateToResult(new CreateTodo(""));

if (result.IsFailure && result.Error is ValidationError validation)
{
    foreach (var field in validation.FieldErrors)
    {
        Console.WriteLine($"{field.PropertyName}: {field.Message} [{field.Code}]");
        // Title: 'Title' must not be empty. [Todo.TitleRequired]
    }
}
```

Mapping rules (per `ValidationFailure` → `FieldError`):

- `PropertyName` → `FieldError.PropertyName`; **model-level failures keep an empty `PropertyName` (`""`)** — they are not dropped or renamed.
- `ErrorMessage` → `FieldError.Message`.
- `ErrorCode` → `FieldError.Code`; empty codes become `null`.

## Dependency-injection usage

Inject `IValidator<T>` and validate at the boundary; the validated instance rides along on success:

```csharp
public sealed class TodoService(IValidator<CreateTodo> validator, TodoStore store)
{
    public async Task<Result<Todo>> CreateAsync(CreateTodo command, CancellationToken ct) =>
        await validator.ValidateToResultAsync(command, ct)
            .BindAsync(valid => Task.FromResult(store.Create(valid.Title!)));
}
```

## ASP.NET Core usage

From `samples/MinimalApi.Sample` — validation failure becomes a 400 with a grouped `errors` dictionary, with zero exception handling:

```csharp
app.MapPost("/todos", async (CreateTodo command, IValidator<CreateTodo> validator, TodoStore store, CancellationToken ct) =>
    (await validator.ValidateToResultAsync(command, ct))
        .Bind(valid => store.Create(valid.Title!))
        .ToCreatedHttpResult(todo => $"/todos/{todo.Id}"));
```

```json
{
  "type": "https://errors.example.com/Validation.Failed",
  "title": "Bad Request",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "errors": { "Title": ["'Title' must not be empty."] },
  "errorCode": "Validation.Failed",
  "traceId": "00-8f0a..."
}
```

## Console application usage

The package has no host dependencies — the same code works in console tools:

```csharp
using FluentValidation;
using Koras.Results.FluentValidation;

var result = new CreateTodoValidator().ValidateToResult(new CreateTodo(args.FirstOrDefault()));
return result.Match(
    onSuccess: todo => { Console.WriteLine($"OK: {todo.Title}"); return 0; },
    onFailure: error => { Console.Error.WriteLine(error.Message); return 1; });
```

## Advanced configuration

There are no package options; behavior is customized through FluentValidation itself — `WithErrorCode` for stable field codes, `WithMessage` for messages, custom property names via `OverridePropertyName`. To produce a `ValidationError` with a custom top-level code/message, construct one from the field errors:

```csharp
var validationResult = validator.Validate(instance);
if (!validationResult.IsValid)
{
    var fields = validationResult.ToValidationError().FieldErrors;
    Error error = new ValidationError("Todo.Invalid", "The todo request is invalid.", fields);
}
```

## Public API

```csharp
public static class ValidationResultExtensions
{
    public static Result ToResult(this ValidationResult validationResult);
    public static Result<T> ToResult<T>(this ValidationResult validationResult, T value);
    public static ValidationError ToValidationError(this ValidationResult validationResult); // throws if valid
}

public static class ValidatorExtensions
{
    public static Result<T> ValidateToResult<T>(this IValidator<T> validator, T instance);
    public static Task<Result<T>> ValidateToResultAsync<T>(this IValidator<T> validator, T instance,
        CancellationToken cancellationToken = default);
}
```

## Execution lifecycle

`ValidateToResult(Async)` runs the validator (`Validate` / `ValidateAsync`), then converts: a valid result becomes `Result.Success(instance)`; an invalid one becomes a failure whose error is a single `ValidationError` (code `Validation.Failed`, message `"One or more validation errors occurred."`) containing one `FieldError` per `ValidationFailure`, in order. Argument null checks happen eagerly, before any async state machine is created; the async path awaits with `ConfigureAwait(false)`.

## Error handling

- Invalid input is a **value**, never an exception: no `ValidationException` is thrown by this package.
- `ToValidationError` on a *valid* result throws `InvalidOperationException` ("check `IsValid` first").
- `ToResult<T>(value)` with a null value on the valid path throws `ArgumentNullException` — `Result<T>` never carries null.
- Exceptions thrown by your own validator rules are not caught; they propagate as usual.

## Cancellation

`ValidateToResultAsync` accepts a `CancellationToken` and passes it to FluentValidation's `ValidateAsync`, cancelling asynchronous rules (e.g. `MustAsync` database checks). Cancellation surfaces as `OperationCanceledException` — it is never converted into a failed result, so shutdown and abort semantics stay intact. The sync `ValidateToResult` has no token, as synchronous FluentValidation offers no cancellation.

## Logging

Not applicable: the package performs pure conversions and logs nothing; log validation outcomes yourself (e.g. via `TapError`) or rely on the HTTP mapper's Debug log when failures reach ASP.NET Core.

## Telemetry

Not applicable directly: no tags or metrics are emitted here; chain [`TagCurrentActivity` / `TapActivityErrorAsync`](opentelemetry.md) after validation to record failures on the current activity.

## Security considerations

Validation messages flow to clients when rendered as ProblemDetails — write rule messages that describe the input problem without echoing sensitive values (avoid interpolating secrets or full payloads into `WithMessage`). Field-level `ErrorCode`s become part of your public API contract.

## Performance considerations

Conversion is a single pass over `ValidationResult.Errors` allocating one `FieldError` per failure plus one `ValidationError`. The happy path allocates nothing beyond FluentValidation's own result. Prefer `ValidateToResultAsync` only when validators actually contain async rules.

## Thread safety

All extension methods are stateless and thread-safe. FluentValidation validators are themselves thread-safe once constructed (standard FluentValidation guidance) and are typically registered as singletons; `FieldError` and `ValidationError` are immutable.

## Testing applications using this feature

```csharp
[Fact]
public void Invalid_input_produces_field_errors_with_codes()
{
    var result = new CreateTodoValidator().ValidateToResult(new CreateTodo(""));

    Assert.True(result.IsFailure);
    var validation = Assert.IsType<ValidationError>(result.Error);
    var field = Assert.Single(validation.FieldErrors);
    Assert.Equal("Title", field.PropertyName);
    Assert.Equal("Todo.TitleRequired", field.Code);
}

[Fact]
public void Valid_input_carries_the_instance()
{
    var command = new CreateTodo("Write docs");
    var result = new CreateTodoValidator().ValidateToResult(command);

    Assert.True(result.IsSuccess);
    Assert.Same(command, result.Value);
}
```

No mocking is needed — validators and conversions are pure.

## Complete example

```csharp
using FluentValidation;
using Koras.Results;
using Koras.Results.AspNetCore;
using Koras.Results.FluentValidation;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddKorasResults();
builder.Services.AddScoped<IValidator<Signup>, SignupValidator>();

var app = builder.Build();

app.MapPost("/signups", async (Signup signup, IValidator<Signup> validator, CancellationToken ct) =>
    (await validator.ValidateToResultAsync(signup, ct))
        .Map(valid => new { valid.Email })
        .ToHttpResult());

app.Run();

public sealed record Signup(string Email, string Password);

public sealed class SignupValidator : AbstractValidator<Signup>
{
    public SignupValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithErrorCode("Signup.EmailInvalid");
        RuleFor(x => x.Password).MinimumLength(12).WithErrorCode("Signup.PasswordTooShort");
        RuleFor(x => x).Must(s => !s.Password.Contains(s.Email, StringComparison.OrdinalIgnoreCase))
            .WithErrorCode("Signup.PasswordContainsEmail")
            .WithMessage("Password must not contain the email address.");
        // The model-level rule above produces a FieldError with PropertyName == ""
    }
}
```

## Common mistakes

- Calling `ToValidationError()` without checking `IsValid` — it throws on valid results; use `ToResult`/`ToResult<T>` for the branching form.
- Ignoring the returned `Result` and re-checking `validationResult.IsValid` — pick one style; the point of `ValidateToResult` is a single flow.
- Forgetting `WithErrorCode` and then wondering why `FieldError.Code` is FluentValidation's default validator code (e.g. `NotEmptyValidator`) rather than your catalog code.
- Expecting model-level failures under a property key: they group under the empty string `""` in the ProblemDetails `errors` dictionary — by design, matching ASP.NET Core conventions.

## Troubleshooting

- **`ArgumentNullException` from `ValidateToResult`** — the instance (or validator) was null; validate a constructed DTO, model "missing body" as its own error.
- **`OperationCanceledException` during validation** — the token you passed was cancelled; this is correct behavior, don't convert it to a failure.
- **Duplicate field errors when composing validators manually** — reuse of a shared `ValidationContext<T>` accumulates failures across validators; create a fresh context per validator (the [MediatR behavior](mediatr.md) does this for you).
- **`errors` dictionary missing in the HTTP response** — the failure reaching the adapter was not a `ValidationError` (e.g. it was re-wrapped by `MapError` into a plain `Error`).

## Related features

- [MediatR validation behavior](mediatr.md) — runs validators in the pipeline using these conversions.
- [ProblemDetails conversion](problemdetails.md) — how `ValidationError` renders as the `errors` dictionary.
- [Localization](localization.md) — `LocalizeField` translates each `FieldError` message.
- [Minimal API adapters](minimal-api.md) — endpoint wiring.
