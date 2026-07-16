# Public API Design — Koras.Results

This document is the contract for implementation. All signatures below are normative; deviations require updating this document in the same PR. Nullability annotations are exact.

## Design tenets

Predictable · IntelliSense-discoverable (extensions live in the same namespace as the types) · minimal (every member earns its place) · strongly typed · async-first · CancellationToken-aware · DI-friendly · test-friendly (pure values, no statics) · backward-compatible by construction (locked via PublicAPI files).

Prohibited patterns honored: no static mutable state, no service locator, no hidden I/O, no surprising defaults, no boolean-overload traps, no third-party types in core signatures.

---

## 1. `Koras.Results` — namespace `Koras.Results`

### 1.1 `ErrorType` (enum)

```csharp
public enum ErrorType
{
    Failure = 0,      // domain rule violation
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Unauthorized = 4,
    Forbidden = 5,
    Unavailable = 6,  // infrastructure / transient
    Unexpected = 7,   // bug / unclassified exception
}
```
Numeric values are a serialization contract; never reorder.

### 1.2 `Error` (immutable class)

```csharp
[JsonConverter(typeof(ErrorJsonConverter))]
public class Error : IEquatable<Error>
{
    public Error(string code, string message, ErrorType type,
                 IReadOnlyDictionary<string, object?>? metadata = null);

    public string Code { get; }          // non-empty, e.g. "User.NotFound"
    public string Message { get; }       // non-empty, human-readable
    public ErrorType Type { get; }
    public IReadOnlyDictionary<string, object?> Metadata { get; } // never null; empty by default

    // Factories (one per taxonomy entry)
    public static Error Failure(string code, string message);
    public static Error Validation(string code, string message);
    public static Error NotFound(string code, string message);
    public static Error Conflict(string code, string message);
    public static Error Unauthorized(string code, string message);
    public static Error Forbidden(string code, string message);
    public static Error Unavailable(string code, string message);
    public static Error Unexpected(string code, string message);

    // Copy-with (returns new instance)
    public Error WithMetadata(string key, object? value);
    public Error WithMetadata(IReadOnlyDictionary<string, object?> metadata);

    // Equality = Code + Type (message/metadata excluded)
    public bool Equals(Error? other);
    public override bool Equals(object? obj);
    public override int GetHashCode();
    public override string ToString();   // "NotFound: User.NotFound — message"

    public static readonly Error None;          // sentinel: success marker, internal use in serialization
    public static readonly Error Uninitialized; // "Result.Uninitialized", Unexpected — default(Result) error
}
```

- Guard: `code`/`message` null/whitespace → `ArgumentException`.
- `None` is never surfaced by a failure; accessing `Result.Error` on success throws instead of returning it.
- Not designed for user inheritance (documented); `ValidationError`/`AggregateError` are the only subclasses.

### 1.3 `FieldError` (sealed record)

```csharp
public sealed record FieldError(string PropertyName, string Message, string? Code = null);
```

### 1.4 `ValidationError` (sealed class)

```csharp
[JsonConverter(typeof(ErrorJsonConverter))]
public sealed class ValidationError : Error
{
    public ValidationError(params FieldError[] fieldErrors);                       // code = "Validation.Failed"
    public ValidationError(IEnumerable<FieldError> fieldErrors);
    public ValidationError(string code, string message, IEnumerable<FieldError> fieldErrors);

    public IReadOnlyList<FieldError> FieldErrors { get; }  // non-empty, order preserved
}
```
Default message: `"One or more validation errors occurred."` Guard: empty collection → `ArgumentException`.

### 1.5 `AggregateError` (sealed class)

```csharp
[JsonConverter(typeof(ErrorJsonConverter))]
public sealed class AggregateError : Error
{
    public AggregateError(IEnumerable<Error> errors);   // code "Errors.Multiple"; Type = highest severity
    public IReadOnlyList<Error> Errors { get; }         // ≥ 2, flattened (nested aggregates are flattened)
}
```
Severity precedence (highest wins): Unexpected > Unavailable > Forbidden > Unauthorized > Conflict > NotFound > Failure > Validation.

### 1.6 `Result` (readonly struct)

```csharp
[JsonConverter(typeof(ResultJsonConverter))]
public readonly struct Result : IEquatable<Result>
{
    public bool IsSuccess { get; }
    public bool IsFailure { get; }
    public Error Error { get; }          // throws InvalidOperationException on success

    public static Result Success();
    public static Result Failure(Error error);                 // ArgumentNullException on null
    public static Result<T> Success<T>(T value);               // ArgumentNullException on null value
    public static Result<T> Failure<T>(Error error);

    public static implicit operator Result(Error error);       // failure

    // Exception boundary (sync)
    public static Result Try(Action action, Func<Exception, Error>? mapError = null);
    public static Result<T> Try<T>(Func<T> func, Func<Exception, Error>? mapError = null);
    // Exception boundary (async) — OperationCanceledException always rethrown
    public static Task<Result> TryAsync(Func<Task> action, Func<Exception, Error>? mapError = null);
    public static Task<Result<T>> TryAsync<T>(Func<Task<T>> func, Func<Exception, Error>? mapError = null);

    // Combination
    public static Result Combine(params Result[] results);
    public static Result Combine(IEnumerable<Result> results);
    public static Result<(T1, T2)> Combine<T1, T2>(Result<T1> r1, Result<T2> r2);
    public static Result<(T1, T2, T3)> Combine<T1, T2, T3>(Result<T1> r1, Result<T2> r2, Result<T3> r3);
    public static Result<(T1, T2, T3, T4)> Combine<T1, T2, T3, T4>(Result<T1> r1, Result<T2> r2, Result<T3> r3, Result<T4> r4);
}
```

- `default(Result)`: `IsFailure == true`, `Error == Error.Uninitialized`.
- Combine aggregation: 0 failures → success; 1 → that error; ≥2 → all-`ValidationError` merges into one `ValidationError`, else `AggregateError`.
- Default `Try` mapper: `Error.Unexpected("Unexpected.Exception", "An unexpected error occurred.")` with `metadata["exceptionType"]` = full type name. Exception message deliberately excluded (leak-safe default).

### 1.7 `Result<T>` (readonly struct)

```csharp
[JsonConverter(typeof(ResultJsonConverterFactory))]
public readonly struct Result<T> : IEquatable<Result<T>>
{
    [MemberNotNullWhen(true, nameof(...))] // NRT: IsSuccess=true guarantees Value non-null
    public bool IsSuccess { get; }
    public bool IsFailure { get; }
    public T Value { get; }              // throws InvalidOperationException on failure
    public Error Error { get; }          // throws InvalidOperationException on success

    public bool TryGetValue([MaybeNullWhen(false)] out T value);
    public bool TryGetError([MaybeNullWhen(false)] out Error error);
    public T? GetValueOrDefault();
    public T GetValueOrDefault(T fallback);

    public static implicit operator Result<T>(T value);        // success (null → failure guard: throws)
    public static implicit operator Result<T>(Error error);    // failure
    public static implicit operator Result(Result<T> result);  // drops value, keeps error/success

    public Result ToResult();
}
```

Null policy: `Result<T>` never carries null. Optional values are modeled as `Result<Option>`-style domain types or nullable domain design — documented. `Success<T>(null)` throws `ArgumentNullException` (compile-time discouraged by NRT, runtime enforced).

### 1.8 `ResultExtensions` (static class — sync combinators)

```csharp
public static class ResultExtensions
{
    // Map — transform value
    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> map);
    public static Result<TOut> Map<TOut>(this Result result, Func<TOut> map);

    // Bind — chain fallible operations
    public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> result, Func<TIn, Result<TOut>> bind);
    public static Result Bind<TIn>(this Result<TIn> result, Func<TIn, Result> bind);
    public static Result<TOut> Bind<TOut>(this Result result, Func<Result<TOut>> bind);
    public static Result Bind(this Result result, Func<Result> bind);

    // Match — exhaustive fold
    public static TOut Match<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> onSuccess, Func<Error, TOut> onFailure);
    public static TOut Match<TOut>(this Result result, Func<TOut> onSuccess, Func<Error, TOut> onFailure);
    public static void Switch<TIn>(this Result<TIn> result, Action<TIn> onSuccess, Action<Error> onFailure);
    public static void Switch(this Result result, Action onSuccess, Action<Error> onFailure);

    // Ensure — post-condition
    public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, Error error);
    public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, Func<T, Error> errorFactory);

    // Tap — side effects, result passes through unchanged
    public static Result<T> Tap<T>(this Result<T> result, Action<T> action);
    public static Result Tap(this Result result, Action action);
    public static Result<T> TapError<T>(this Result<T> result, Action<Error> action);
    public static Result TapError(this Result result, Action<Error> action);

    // MapError — translate error (e.g., at layer boundaries)
    public static Result<T> MapError<T>(this Result<T> result, Func<Error, Error> map);
    public static Result MapError(this Result result, Func<Error, Error> map);
}
```

Semantics: on failure, `Map`/`Bind`/`Ensure`/`Tap` return the same error identity without invoking delegates; on success, `TapError`/`MapError` pass through. Delegate exceptions are NOT caught. Null delegates throw `ArgumentNullException` eagerly.

### 1.9 `ResultAsyncExtensions` (static class — async combinators)

Complete overload matrix over three receiver/delegate axes (receiver: `Result<T>` or `Task<Result<T>>`; delegate: sync or `Task`-returning). Naming: async suffix on the *method* whenever the return is a `Task`:

```csharp
public static class ResultAsyncExtensions
{
    // Map
    public static Task<Result<TOut>> MapAsync<TIn, TOut>(this Result<TIn> result, Func<TIn, Task<TOut>> map);
    public static Task<Result<TOut>> MapAsync<TIn, TOut>(this Task<Result<TIn>> resultTask, Func<TIn, TOut> map);
    public static Task<Result<TOut>> MapAsync<TIn, TOut>(this Task<Result<TIn>> resultTask, Func<TIn, Task<TOut>> map);

    // Bind (same 3-way matrix, plus non-generic bridges)
    public static Task<Result<TOut>> BindAsync<TIn, TOut>(this Result<TIn> result, Func<TIn, Task<Result<TOut>>> bind);
    public static Task<Result<TOut>> BindAsync<TIn, TOut>(this Task<Result<TIn>> resultTask, Func<TIn, Result<TOut>> bind);
    public static Task<Result<TOut>> BindAsync<TIn, TOut>(this Task<Result<TIn>> resultTask, Func<TIn, Task<Result<TOut>>> bind);
    public static Task<Result> BindAsync<TIn>(this Task<Result<TIn>> resultTask, Func<TIn, Task<Result>> bind);

    // Match
    public static Task<TOut> MatchAsync<TIn, TOut>(this Task<Result<TIn>> resultTask, Func<TIn, TOut> onSuccess, Func<Error, TOut> onFailure);
    public static Task<TOut> MatchAsync<TIn, TOut>(this Task<Result<TIn>> resultTask, Func<TIn, Task<TOut>> onSuccess, Func<Error, Task<TOut>> onFailure);

    // Ensure
    public static Task<Result<T>> EnsureAsync<T>(this Task<Result<T>> resultTask, Func<T, bool> predicate, Error error);
    public static Task<Result<T>> EnsureAsync<T>(this Result<T> result, Func<T, Task<bool>> predicate, Error error);
    public static Task<Result<T>> EnsureAsync<T>(this Task<Result<T>> resultTask, Func<T, Task<bool>> predicate, Error error);

    // Tap / TapError
    public static Task<Result<T>> TapAsync<T>(this Result<T> result, Func<T, Task> action);
    public static Task<Result<T>> TapAsync<T>(this Task<Result<T>> resultTask, Action<T> action);
    public static Task<Result<T>> TapAsync<T>(this Task<Result<T>> resultTask, Func<T, Task> action);
    public static Task<Result<T>> TapErrorAsync<T>(this Task<Result<T>> resultTask, Action<Error> action);
    public static Task<Result<T>> TapErrorAsync<T>(this Task<Result<T>> resultTask, Func<Error, Task> action);

    // Non-generic Result variants for Map/Bind/Match/Tap mirrored where meaningful
    public static Task<Result> TapAsync(this Task<Result> resultTask, Action action);
    public static Task<Result> BindAsync(this Task<Result> resultTask, Func<Task<Result>> bind);
    public static Task<TOut> MatchAsync<TOut>(this Task<Result> resultTask, Func<TOut> onSuccess, Func<Error, TOut> onFailure);
    public static Task<Result<TOut>> MapAsync<TOut>(this Task<Result> resultTask, Func<TOut> map);
}
```

All implementations `ConfigureAwait(false)`. Cancellation: delegates close over their own `CancellationToken` (the combinators are pure plumbing; adding a token parameter to every overload doubles the matrix for no benefit — the token belongs to the I/O call inside the delegate). This is a documented, deliberate decision.

### 1.10 Serialization types

```csharp
public sealed class ErrorJsonConverter : JsonConverter<Error>;
public sealed class ResultJsonConverter : JsonConverter<Result>;
public sealed class ResultJsonConverterFactory : JsonConverterFactory;   // handles Result<T>
```
Public so users can register them explicitly in custom `JsonSerializerOptions`; attribute-wired so they normally never need to.

---

## 2. `Koras.Results.AspNetCore` — namespace `Koras.Results.AspNetCore`

```csharp
public sealed class KorasResultsOptions
{
    public bool IncludeUnexpectedErrorDetails { get; set; }              // default false (secure)
    public MetadataExposurePolicy MetadataExposure { get; set; }         // None (default) | All
    public Func<Error, string?>? ProblemTypeUriFactory { get; set; }     // default: about:blank family
    public bool IncludeTraceId { get; set; } = true;
    public KorasResultsOptions MapErrorType(ErrorType type, int statusCode);
    public KorasResultsOptions MapErrorCode(string errorCode, int statusCode);
    public int GetStatusCode(Error error);                               // resolution incl. precedence
}

public enum MetadataExposurePolicy { None = 0, All = 1 }

public interface IErrorMessageLocalizer
{
    string Localize(Error error, CultureInfo culture);
    string LocalizeField(FieldError fieldError, CultureInfo culture);
}

public static class KorasResultsServiceCollectionExtensions
{
    public static IServiceCollection AddKorasResults(this IServiceCollection services,
        Action<KorasResultsOptions>? configure = null);
}

// ProblemDetails conversion
public static class ProblemDetailsExtensions
{
    public static ProblemDetails ToProblemDetails(this Error error, KorasResultsOptions? options = null, IErrorMessageLocalizer? localizer = null);
    public static ProblemDetails ToProblemDetails(this Result result, ...);       // throws on success
    public static ProblemDetails ToProblemDetails<T>(this Result<T> result, ...); // throws on success
}

// Minimal API
public static class HttpResultExtensions
{
    public static IResult ToHttpResult(this Result result);                         // 204 / problem
    public static IResult ToHttpResult<T>(this Result<T> result);                   // 200 / problem
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess);
    public static IResult ToCreatedHttpResult<T>(this Result<T> result, Func<T, string> locationFactory);
    public static Task<IResult> ToHttpResultAsync<T>(this Task<Result<T>> resultTask);            // sugar
    public static Task<IResult> ToHttpResultAsync(this Task<Result> resultTask);
    public static Task<IResult> ToHttpResultAsync<T>(this Task<Result<T>> resultTask, Func<T, IResult> onSuccess);
}

// MVC
public static class ActionResultExtensions
{
    public static IActionResult ToActionResult(this Result result);                 // 204 / problem
    public static IActionResult ToActionResult<T>(this Result<T> result);           // 200 / problem
    public static IActionResult ToActionResult<T>(this Result<T> result, Func<T, IActionResult> onSuccess);
    public static ActionResult<T> ToActionResultOf<T>(this Result<T> result);
    public static Task<IActionResult> ToActionResultAsync<T>(this Task<Result<T>> resultTask);
    public static Task<IActionResult> ToActionResultAsync(this Task<Result> resultTask);
}
```

Notes:
- Extension methods resolve options/localizer from `HttpContext.RequestServices` when invoked in a request (overloads taking explicit options exist for unit tests / non-DI use with sensible defaults when absent). Concretely: the parameterless overloads use built-in defaults unless `AddKorasResults` was called — both paths tested.
- Success mapping defaults: `Result` → 204 NoContent; `Result<T>` → 200 OK JSON.
- Failure mapping is total: every `ErrorType` has a default status; `ValidationError` produces `errors` dictionary; `extensions["errorCode"]`, optional `extensions["traceId"]`, `extensions["metadata"]` (policy-gated).

## 3. `Koras.Results.FluentValidation` — namespace `Koras.Results.FluentValidation`

```csharp
public static class ValidationResultExtensions
{
    public static Result ToResult(this ValidationResult validationResult);
    public static Result<T> ToResult<T>(this ValidationResult validationResult, T value);
    public static ValidationError ToValidationError(this ValidationResult validationResult); // throws if valid
}

public static class ValidatorExtensions
{
    public static Task<Result<T>> ValidateToResultAsync<T>(this IValidator<T> validator, T instance,
        CancellationToken cancellationToken = default);
    public static Result<T> ValidateToResult<T>(this IValidator<T> validator, T instance);
}
```
Mapping: `ValidationFailure.PropertyName/ErrorMessage/ErrorCode` → `FieldError`; empty `PropertyName` (model-level) preserved as `""`.

## 4. `Koras.Results.MediatR` — namespace `Koras.Results.MediatR`

```csharp
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators);
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct);
}

public static class KorasResultsMediatRServiceCollectionExtensions
{
    public static IServiceCollection AddKorasResultsValidationBehavior(this IServiceCollection services);
}
```
Behavior contract: `TResponse` must be `Result` or `Result<T>`; for other response types the behavior passes through after validation *succeeds* and **throws `InvalidOperationException`** at failure time with guidance (fail-fast beats silently swallowing validation). All validators run; failures aggregate into one `ValidationError`.

## 5. `Koras.Results.OpenTelemetry` — namespace `Koras.Results.OpenTelemetry`

```csharp
public static class KorasResultsActivityTags
{
    public const string ErrorType = "error.type";
    public const string ErrorCode = "koras.error.code";
    public const string AggregateCount = "koras.error.aggregate_count";
}

public static class ActivityResultExtensions
{
    public static Result TagCurrentActivity(this Result result);
    public static Result<T> TagCurrentActivity<T>(this Result<T> result);
    public static Result TagActivity(this Result result, Activity? activity);
    public static Result<T> TagActivity<T>(this Result<T> result, Activity? activity);
    public static Task<Result<T>> TapActivityErrorAsync<T>(this Task<Result<T>> resultTask);
    public static Task<Result> TapActivityErrorAsync(this Task<Result> resultTask);
}
```
All return the receiver for chaining; success and null/non-recording activity are allocation-free no-ops.

---

## Sample usage (canonical)

```csharp
// Domain
public Result<Order> Place(PlaceOrder cmd) =>
    _catalog.Find(cmd.Sku)                                  // Result<Product>
        .Ensure(p => p.Stock >= cmd.Qty,
                p => OrderErrors.InsufficientStock(p.Sku))
        .Map(p => Order.Create(p, cmd.Qty));

// Endpoint (Minimal API)
app.MapPost("/orders", (PlaceOrder cmd, IOrderService svc) =>
    svc.Place(cmd).ToCreatedHttpResult(o => $"/orders/{o.Id}"));
```

## Thread-safety & lifetime summary

| Type | Thread-safe | DI lifetime |
|---|---|---|
| `Result`, `Result<T>`, `Error` family | yes (immutable) | n/a (values) |
| `KorasResultsOptions` | mutate only during configuration | Options singleton |
| `IErrorMessageLocalizer` impls | must be thread-safe | singleton |
| `ValidationBehavior<,>` | stateless | transient (per MediatR) |
