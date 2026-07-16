# The Result Pattern

Koras.Results is built on one premise: **expected failures are values, not exceptions.** This page explains what that means, when exceptions are still the right tool, and how the library carries a failure from a domain rule all the way to an HTTP response.

## Expected failures are part of the contract

Consider a method that looks up a user:

```csharp
public User GetUser(Guid id);   // what happens when the user doesn't exist?
```

"User not found" is not exceptional — it is one of the *normal, expected outcomes* of a lookup. Modeling it with an exception has real costs:

- **Invisible contracts.** The signature says nothing about failure; callers learn about `UserNotFoundException` from production logs.
- **No compiler help.** Nothing forces a caller to handle the failure path; forgotten catch blocks are the default state.
- **Control flow by stack unwinding.** Exceptions used for routine branching make code harder to follow and pull failure handling far from the call site.
- **Cost on the failure path.** Throwing captures stack traces and unwinds frames — expensive for something that happens on every cache miss or duplicate submission.

With a result type, the failure is in the signature and in the type system:

```csharp
public Result<User> GetUser(Guid id);
```

```csharp
var result = users.GetUser(id);
if (result.IsFailure)
{
    // The compiler made you look. result.Error carries code, message, type, metadata.
    return result.Error;
}
var user = result.Value;
```

Failures become first-class data: immutable `Error` values with a stable machine-readable `Code` (`"User.NotFound"`), a human-readable `Message`, a semantic `ErrorType`, and optional `Metadata`. They can be compared, logged, aggregated, serialized, and — crucially — projected into HTTP responses without any per-endpoint mapping code.

## When exceptions remain correct

Koras.Results is explicitly **not** exception-replacement dogma. Exceptions are the right tool for conditions that are genuinely exceptional — situations no caller can meaningfully handle as a branch of normal logic:

| Use a `Result` | Keep throwing |
|---|---|
| Resource not found | Programming bugs (`ArgumentNullException`, `InvalidOperationException`) |
| Validation failure | Violated invariants ("this can never happen") |
| Business rule rejection ("insufficient stock") | Corrupted state, failed assertions |
| Authorization denial | `OperationCanceledException` — cancellation is *not* failure and always propagates |
| A dependency being down (as a handled, retryable condition) | Truly unrecoverable infrastructure failure at startup |

The library itself follows this split: `Result.Value` on a failure throws `InvalidOperationException` (accessing it is a bug, not a failure), and `Result.Try`/`Result.TryAsync` — the bridge that converts thrown exceptions into failure results at the boundary between exception-based and result-based code — always rethrows `OperationCanceledException` (see [cancellation](cancellation.md)).

Plain `if (result.IsFailure)` checks are a first-class usage pattern. The combinators (`Map`, `Bind`, `Ensure`, …) are optional sugar for those who want pipeline-style composition — see [lifecycle](lifecycle.md).

## The whole-path story

Most result libraries stop at the domain layer, leaving you to hand-write the translation into HTTP responses. Koras.Results covers the entire path:

### 1. Domain — produce typed failures

```csharp
public static class OrderErrors
{
    public static Error InsufficientStock(string sku) =>
        Error.Failure("Order.InsufficientStock", $"Not enough stock for '{sku}'.");
}

public Result<Order> Place(PlaceOrder cmd) =>
    _catalog.Find(cmd.Sku)                                  // Result<Product>
        .Ensure(p => p.Stock >= cmd.Qty,
                p => OrderErrors.InsufficientStock(p.Sku))
        .Map(p => Order.Create(p, cmd.Qty));
```

The domain classifies failures by *business meaning* (`ErrorType.Failure`, `NotFound`, `Conflict`, …) and never references HTTP. It depends only on the zero-dependency core package.

### 2. Composition — failures short-circuit

`Ensure`, `Bind`, and `Map` only run on success; a failure flows through the pipeline untouched, preserving its identity. Validation (FluentValidation) and cross-cutting pipelines (MediatR) plug into the same shape.

### 3. Edge — one call projects the outcome

```csharp
app.MapPost("/orders", (PlaceOrder cmd, IOrderService svc) =>
    svc.Place(cmd).ToCreatedHttpResult(o => $"/orders/{o.Id}"));
```

The AspNetCore package turns any failure into an RFC 9457 `application/problem+json` response: the `ErrorType` picks the status code (configurable), the `Code` ships as `errorCode`, validation errors become a per-field `errors` dictionary, and a `traceId` links the response to your telemetry. Unexpected errors have their details suppressed by default so internals never leak.

The same error value can simultaneously feed OpenTelemetry activity tags, structured logs, and retry decisions in background workers — one classification, many projections.

## What this buys you

- **Honest signatures** — every fallible operation says so in its return type.
- **Uniform error contracts** — one taxonomy across services enables shared dashboards and client-side handling by `errorCode`.
- **Zero per-endpoint mapping code** — the edge collapses to one extension-method call.
- **An allocation-free happy path** — `Result` and `Result<T>` are `readonly struct`s; success allocates nothing.

## Further reading

- [Core abstractions](core-abstractions.md) — the types in detail
- [Lifecycle](lifecycle.md) — creation → composition → consumption
- [Error handling](error-handling.md) — designing error catalogs
- [Architecture](architecture.md) — how the packages fit together
