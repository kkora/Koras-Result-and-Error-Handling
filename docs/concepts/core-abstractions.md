# Core Abstractions

The core package (`Koras.Results`, namespace `Koras.Results`) defines two result structs and a small immutable error hierarchy. Everything on this page is zero-dependency and safe to use in any layer.

## `Result` and `Result<T>`

Both are **`readonly struct`s**: success results allocate nothing, copies are safe, and instances are immutable by construction.

```csharp
Result ok = Result.Success();                       // outcome without a value
Result<Todo> found = Result.Success(todo);          // outcome carrying a value
Result<Todo> missing = Result.Failure<Todo>(TodoErrors.NotFound(id));

// Implicit conversions keep call sites clean:
Result<Todo> a = todo;                              // T -> success
Result<Todo> b = TodoErrors.NotFound(id);           // Error -> failure
Result c = b;                                       // Result<T> -> Result (drops value)
```

### `default` is a failure, never a fake success

An uninitialized result — `default(Result)`, `new Result<T>()`, an unset field — is a **failure** carrying `Error.Uninitialized` (code `"Result.Uninitialized"`, type `Unexpected`). A forgotten initialization can therefore never masquerade as success (ADR-0003).

### Success never carries null

`Result<T>` never holds a null value: `Result.Success<T>(null)` throws `ArgumentNullException`, and the implicit `T → Result<T>` conversion enforces the same guard. When `IsSuccess` is true, `Value` is guaranteed non-null (annotated for the compiler's nullable analysis).

### Guarded accessors

Accessing the wrong side is a programming bug and throws:

```csharp
result.Value;   // InvalidOperationException if IsFailure (message includes the error code)
result.Error;   // InvalidOperationException if IsSuccess
```

Safe access patterns that never throw:

```csharp
if (result.TryGetValue(out var todo)) { /* success path */ }
if (result.TryGetError(out var error)) { /* failure path */ }

Todo? maybe   = result.GetValueOrDefault();          // null/default on failure
Todo fallback = result.GetValueOrDefault(Todo.Empty); // explicit fallback on failure
```

`Result<T>.ToResult()` converts to the non-generic form, preserving success/failure and the error.

## `Error`

An immutable class with four properties:

```csharp
var error = Error.Conflict("User.EmailTaken", "That email address is already registered.")
                 .WithMetadata("email", "a***@example.com");

error.Code;      // "User.EmailTaken"  — stable, machine-readable, dot-separated
error.Message;   // human-readable; may be localized at the edge
error.Type;      // ErrorType.Conflict — drives HTTP mapping, dashboards, retry policy
error.Metadata;  // IReadOnlyDictionary<string, object?> — never null, empty by default
```

- **Construction:** one static factory per taxonomy entry (`Error.Failure`, `Error.Validation`, `Error.NotFound`, `Error.Conflict`, `Error.Unauthorized`, `Error.Forbidden`, `Error.Unavailable`, `Error.Unexpected`) or the public constructor. Null/whitespace `code` or `message` throws `ArgumentException`.
- **Copy-with:** `WithMetadata(key, value)` and `WithMetadata(dictionary)` return new instances; the original is never mutated.
- **Equality = `Code` + `Type`.** Message and metadata are excluded: errors are identities, messages are presentation. Two `"User.NotFound"` errors with differently formatted messages are equal.
- **Sentinels:** `Error.None` (internal success marker used by serialization — never surfaced by a failure) and `Error.Uninitialized` (carried by default-constructed results).
- **Not for user inheritance.** `ValidationError` and `AggregateError` are the only subclasses.

## `ErrorType` taxonomy

A closed enum of eight semantic categories (numeric values are a serialization contract and never reordered). Extensibility lives in `Code` and `Metadata`, not in new categories.

| Value | Meaning | Default HTTP mapping |
|---|---|---|
| `Failure` (0) | A domain/business rule rejected the operation | 422 Unprocessable Entity |
| `Validation` (1) | Input is syntactically/semantically invalid | 400 Bad Request |
| `NotFound` (2) | A referenced resource does not exist | 404 Not Found |
| `Conflict` (3) | State conflict (duplicate, concurrency, version) | 409 Conflict |
| `Unauthorized` (4) | Caller identity missing or invalid | 401 Unauthorized |
| `Forbidden` (5) | Caller known but not permitted | 403 Forbidden |
| `Unavailable` (6) | A dependency is down, throttling, or timing out | 503 Service Unavailable |
| `Unexpected` (7) | A bug or unclassified exception | 500 Internal Server Error |

HTTP mappings are defaults owned by the AspNetCore package and are overridable per type and per exact code — see [configuration](../getting-started/configuration.md).

## `ValidationError` and `FieldError`

`ValidationError` is the `Error` subclass for input validation, carrying per-field detail:

```csharp
public sealed record FieldError(string PropertyName, string Message, string? Code = null);

var invalid = new ValidationError(
    new FieldError("Title", "'Title' must not be empty.", "Todo.TitleRequired"),
    new FieldError("Title", "Title is too long.", "Todo.TitleTooLong"));

invalid.Code;         // "Validation.Failed" (default; a custom-code constructor exists)
invalid.Type;         // ErrorType.Validation
invalid.FieldErrors;  // IReadOnlyList<FieldError> — non-empty, order preserved
```

The default message is `"One or more validation errors occurred."`; constructing with an empty field-error collection throws `ArgumentException`. At the HTTP edge, a `ValidationError` produces the familiar `errors` dictionary grouped by property name. The `Koras.Results.FluentValidation` package builds these from FluentValidation results automatically.

## `AggregateError`

When `Result.Combine` observes two or more heterogeneous failures, they merge into an `AggregateError`:

```csharp
var combined = Result.Combine(CheckInventory(), CheckPayment(), CheckAddress());
// 0 failures -> success; 1 -> that error unchanged;
// >= 2 all-ValidationError -> merged ValidationError; otherwise -> AggregateError
```

- `Code` is `"Errors.Multiple"`; `Errors` is a flattened, read-only list (nested aggregates are flattened) of at least 2 entries.
- Its `Type` is the **highest severity** among the children, so a projection never under-reports severity:

```
Unexpected > Unavailable > Forbidden > Unauthorized > Conflict > NotFound > Failure > Validation
```

An aggregate containing one `Unavailable` and three `Validation` errors is therefore `Unavailable` (→ 503), not `Validation` (→ 400).

## Serialization

All core types serialize with System.Text.Json via attribute-wired converters (`ErrorJsonConverter`, `ResultJsonConverter`, `ResultJsonConverterFactory`) — no setup required. The converter types are public for explicit registration in custom `JsonSerializerOptions`.

## Further reading

- [Lifecycle](lifecycle.md) — how these types flow through creation, composition, and consumption
- [Error handling](error-handling.md) — designing error catalogs on top of this model
- [Public API design](../api/public-api-design.md) — the normative signature contract
