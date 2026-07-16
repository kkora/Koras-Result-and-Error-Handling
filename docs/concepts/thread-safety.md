# Thread Safety

Every public type in the Koras.Results family is safe to share across threads. This is not an accident of implementation — it falls out of one design rule: **everything public is immutable.** This page explains what that guarantees, and the few places where the usual .NET caveats still apply.

## Immutability of the core types

| Type | Shape | After construction |
|---|---|---|
| `Result`, `Result<T>` | `readonly struct` | No member can mutate state; all fields are readonly |
| `Error` | Immutable class | `Code`, `Message`, `Type` fixed; `Metadata` is `IReadOnlyDictionary` |
| `ValidationError` | Immutable class | `FieldErrors` is a read-only, order-preserving list |
| `AggregateError` | Immutable class | `Errors` is a read-only, flattened list |
| `FieldError` | `sealed record` (positional) | Value object; `with` produces copies |

There are no setters, no `Add` methods, no lazily-populated state. "Modification" APIs are copy-with operations:

```csharp
var basic    = Error.Conflict("User.EmailTaken", "Email already registered.");
var enriched = basic.WithMetadata("conflictingField", "email");

// basic is untouched — WithMetadata returned a new instance.
ReferenceEquals(basic, enriched);   // false
basic.Metadata.Count;               // 0
```

Consequences you can rely on:

- **Share freely.** A `Result<T>` or `Error` can be captured by multiple tasks, stored in caches, passed between threads, and read concurrently without any synchronization.
- **No defensive copies needed.** Nothing anyone does to "their" reference can affect yours.
- **No torn observation of the error model.** All `Error` state is fixed at construction.

The one thing immutability does *not* extend to is **your value inside `Result<T>`**: the result never mutates the value, but if `T` itself is a mutable class, concurrent mutation of that object is your concern, exactly as it would be outside a result. Prefer records and immutable models for values that cross threads.

## Struct copy semantics

`Result` and `Result<T>` are structs, so assignment copies:

```csharp
Result<Todo> a = store.Find(id);
Result<Todo> b = a;                 // independent copy of the (flag, value, error) triple
```

Both copies reference the *same* `Error`/value objects (a shallow copy), which is safe precisely because those objects are immutable. Two standard .NET struct caveats apply:

- **A shared *field* of struct type is not atomic to update.** `Result<T>` is larger than a machine word, so racing writes to the same field from multiple threads can produce a torn read. Sharing results by *passing* them (parameters, return values, closures) is always safe; publishing one via a mutable shared field requires the same care as any multi-word struct — and remember that a torn or defaulted read still fails safe: `default(Result<T>)` is a failure carrying `Error.Uninitialized`, never a phantom success.
- **Avoid boxing surprises.** Converting a result to `object`/interface boxes a copy; that copy is as immutable as the original, so this is a performance note, not a safety one.

## Static error catalogs are safe

Because `Error` is immutable, the recommended catalog pattern is inherently thread-safe — both `static readonly` fields and factory methods:

```csharp
public static class UserErrors
{
    // Shared singleton error instance: safe — nothing can mutate it.
    public static readonly Error SignupsClosed =
        Error.Forbidden("User.SignupsClosed", "New registrations are currently disabled.");

    // Factory: returns a fresh immutable instance per call — also safe.
    public static Error NotFound(Guid id) =>
        Error.NotFound("User.NotFound", $"No user with id '{id}'.");
}
```

The library uses the same pattern internally for its sentinels (`Error.None`, `Error.Uninitialized`) — static readonly instances shared by every thread in the process. It holds no other static state, mutable or otherwise.

## Singleton safety in the ASP.NET Core package

| Service | Lifetime | Thread-safety contract |
|---|---|---|
| `KorasResultsOptions` | Options singleton | Mutable **only during configuration** (inside the `AddKorasResults` lambda, which the options system runs once). Afterwards, `GetStatusCode` and property reads are served concurrently to every request; treat the instance as read-only per standard options-pattern semantics. Do not call `MapErrorType`/`MapErrorCode` at request time. |
| `IErrorMessageLocalizer` | Singleton | **Implementations must be thread-safe** — one instance serves all concurrent requests. The default `PassThroughErrorMessageLocalizer` is stateless. If yours caches, use `ConcurrentDictionary` or immutable snapshots; resource-manager-based lookups are naturally safe. |
| `ValidationBehavior<,>` (MediatR package) | Transient | Stateless; a new instance per request, nothing shared. |

The extension methods themselves (`ToHttpResult`, `ToActionResult`, `ToProblemDetails`, the combinators, `TagCurrentActivity`) are static and stateless — they read immutable inputs and produce new outputs, so concurrent calls never interfere.

## Async pipelines

Combinators capture results by value and never store shared state, so concurrent pipelines over the same source result are safe:

```csharp
Result<Order> order = LoadOrder(id);

// Both tasks read independent copies of the same immutable data — no locks needed.
var t1 = Task.Run(() => order.Map(o => Summarize(o)));
var t2 = Task.Run(() => order.Map(o => Audit(o)));
```

All library async code awaits with `ConfigureAwait(false)` and never blocks on tasks, so there is no sync-over-async deadlock surface inside the library.

## Summary

- Every public Koras.Results type is deeply immutable; share instances freely without synchronization.
- Struct assignment copies; passing results around is safe, racing writes to one shared field follows normal multi-word-struct rules — and even then, `default` fails safe.
- Static `Error` catalogs (fields or factories) are the recommended pattern and are thread-safe by construction.
- Configure `KorasResultsOptions` only at startup; make custom `IErrorMessageLocalizer` implementations thread-safe.
- The thread-safety of the *values you put inside* `Result<T>` remains your responsibility.

## Further reading

- [Core abstractions](core-abstractions.md) — the types and their guarantees
- [Dependency injection](../getting-started/dependency-injection.md) — lifetimes table and registration
