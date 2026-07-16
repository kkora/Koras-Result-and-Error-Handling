# Error Handling

The library gives you the mechanics — `Error`, `ErrorType`, `Result` — but the quality of your error handling is decided by design habits: a curated error catalog, correct classification, disciplined metadata, and clean layer boundaries. This page covers those habits.

## Design an error catalog

Do not create errors inline at call sites. Collect every failure a subject can produce into one static class:

```csharp
using Koras.Results;

public static class OrderErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("Order.NotFound", $"No order with id '{id}'.");

    public static Error InsufficientStock(string sku) =>
        Error.Failure("Order.InsufficientStock", $"Not enough stock for '{sku}'.")
             .WithMetadata("sku", sku);

    public static Error AlreadyShipped(Guid id) =>
        Error.Conflict("Order.AlreadyShipped", $"Order '{id}' has already shipped.");

    public static Error PaymentGatewayDown() =>
        Error.Unavailable("Order.PaymentGatewayDown", "The payment provider is unavailable.");
}
```

Why this pattern works:

- **One reviewable surface.** The catalog *is* the failure contract of the subject — reviewers, testers, and API consumers can read it top to bottom.
- **Stable codes.** Codes follow `Subject.Condition` (`"Order.InsufficientStock"`, `"User.EmailTaken"`): dot-separated, PascalCase segments, specific enough to switch on. Once shipped, a code **never changes meaning** — messages may be reworded or localized; codes are the machine contract.
- **Parameterized messages, fixed identity.** Factories take parameters for the message and metadata, but the `(Code, Type)` pair — which is also what `Error` equality compares — stays constant.
- **Safe as statics.** `Error` is immutable, so catalog factories (and even `static readonly Error` fields for parameterless errors) are thread-safe by construction.

## Choose the right `ErrorType`

Classification is semantic — describe *what kind of condition occurred*, never the HTTP status you want. (Status codes are a configurable projection; see [configuration](../getting-started/configuration.md).)

| Ask yourself | Type |
|---|---|
| Did a business rule reject an otherwise valid request? | `Failure` |
| Is the input itself malformed or invalid? | `Validation` |
| Does a referenced resource not exist? | `NotFound` |
| Does the current state conflict (duplicate, concurrency, version)? | `Conflict` |
| Is the caller's identity missing or invalid? | `Unauthorized` |
| Is the caller known but not permitted? | `Forbidden` |
| Is a dependency down, throttling, or timing out (retry may help)? | `Unavailable` |
| Is this a bug or an unclassified exception? | `Unexpected` |

Guidelines:

- `Failure` is the catch-all for *domain* semantics; `Unexpected` for *technical* ones. Do not use `Unexpected` for anything you designed.
- The taxonomy is closed on purpose: uniform dashboards, HTTP mapping, and cross-team contracts depend on it. When you feel the urge for a new category, express the nuance in `Code` and `Metadata` instead.
- `Unavailable` is the retry signal — background workers can branch on it (`error.Type == ErrorType.Unavailable` ⇒ retry next cycle, anything else ⇒ terminal).

## Metadata rules

`Error.Metadata` carries structured context (`WithMetadata` returns a new instance; errors are immutable):

```csharp
Error.Conflict("User.EmailTaken", "That email address is already registered.")
     .WithMetadata("conflictingField", "email")
     .WithMetadata("retryAfterSeconds", 30);
```

Three rules, all enforced by convention and reviewed like API surface:

1. **Keys are camelCase** (`"sku"`, `"retryAfterSeconds"`) — they may end up in JSON payloads.
2. **Values must be JSON-primitive-representable**: string, number, bool, null, or arrays thereof. No entities, no exceptions, no domain objects.
3. **Never secrets or PII.** No credentials, connection strings, tokens, or personal data — in metadata *or* in messages. Even though `MetadataExposure` defaults to `None` (metadata stays server-side), errors travel through logs and telemetry; write every error as if a client will eventually see it.

## Translate errors at layer boundaries with `MapError`

Inner-layer errors often should not leak upward verbatim: an infrastructure detail means nothing to an API client, and a generic library error means nothing on a dashboard. Use `MapError` where layers meet:

```csharp
// Application layer: translate an infrastructure failure into the domain's vocabulary.
public Result<Order> Place(PlaceOrder cmd) =>
    _gateway.Charge(cmd)                              // may fail Payments.Timeout (Unavailable)
        .MapError(inner => inner.Code.StartsWith("Payments.", StringComparison.Ordinal)
            ? OrderErrors.PaymentGatewayDown()        // re-express in this layer's catalog
            : inner)                                  // pass through what already fits
        .Bind(receipt => CreateOrder(cmd, receipt));

// Async pipelines: await the inner call, then translate with the same sync MapError.
public async Task<Result<Order>> PlaceAsync(PlaceOrder cmd, CancellationToken ct)
{
    var charged = await _gateway.ChargeAsync(cmd, ct);
    return charged
        .MapError(inner => inner.Type == ErrorType.Unavailable
            ? OrderErrors.PaymentGatewayDown()
            : inner)
        .Bind(receipt => CreateOrder(cmd, receipt));
}
```

Rules of thumb:

- Preserve severity: do not downgrade an `Unavailable` or `Unexpected` into a `Failure` just to make dashboards greener.
- Keep the inner code in metadata when the translation would otherwise lose diagnostic value: `.WithMetadata("innerCode", inner.Code)`.
- On success, `MapError` is a no-op — it is always safe to leave in the pipeline.

## Exceptions still have a job

Results model **expected** failures. Keep throwing for:

- **Programming bugs and violated invariants** — `ArgumentNullException`, `InvalidOperationException`. The library itself throws these (e.g. accessing `Value` on a failure, passing a null delegate, `Success<T>(null)`).
- **Cancellation** — `OperationCanceledException` always propagates; never convert it to an error ([cancellation](cancellation.md)).
- **Unrecoverable startup failures** — missing critical configuration should crash the process, not return a result.

At the boundary between exception-throwing code (BCL, third-party SDKs) and your result-based code, use `Result.Try`/`Result.TryAsync` with an explicit mapper that classifies known exception types into your catalog:

```csharp
var saved = await Result.TryAsync(
    () => _db.SaveChangesAsync(ct),
    ex => ex is DbUpdateConcurrencyException
        ? OrderErrors.ConcurrentModification()
        : Error.Unexpected("Order.SaveFailed", "The order could not be saved."));
```

Without a mapper, the default is leak-safe (`"Unexpected.Exception"`, generic message, exception *type* in metadata, message excluded) — good enough at the outermost edge, but an explicit mapper into your catalog is better anywhere you can anticipate the exception.

## Further reading

- [Core abstractions](core-abstractions.md) — `Error`, `ValidationError`, `AggregateError` mechanics
- [Error model (deep)](../architecture/error-model.md) — taxonomy rationale and lifecycle
- [Configuration](../getting-started/configuration.md) — projecting the catalog to HTTP
