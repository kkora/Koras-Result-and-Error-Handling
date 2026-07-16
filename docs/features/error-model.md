# Typed Error Model (`Error`, `ErrorType`, codes, metadata)

Feature ID: KR-002 · Package: `Koras.Results` (Core)

## Overview

Every failed result carries an `Error`: an immutable, serializable value describing *why* the
operation failed. An error has four parts:

- `Code` — a stable, dot-separated, machine-readable identifier (`"User.NotFound"`,
  `"Order.InsufficientStock"`). Codes are the machine contract; they never change meaning.
- `Message` — a human-readable description. Messages are presentation and may be localized.
- `Type` — one of eight semantic categories in the closed `ErrorType` enum.
- `Metadata` — an optional read-only, string-keyed dictionary of structured extra data.

The taxonomy is deliberately **closed** (ADR-0004): a fixed set of categories is what makes
uniform dashboards, HTTP status mapping, and cross-team contracts possible. Extensibility lives
in `Code` (unbounded) and `Metadata`, not in inventing new categories. Classification is semantic,
not transport-shaped — HTTP status codes are a projection owned by the ASP.NET Core package.

## When to use it

- Every failure in your system. Any `Result.Failure(...)` requires an `Error`.
- Defining a per-module error catalog: static factory methods returning well-known errors with
  stable codes (`OrderErrors.InsufficientStock(sku)`).
- Attaching structured diagnostic data (`WithMetadata("sku", sku)`) that survives serialization
  and telemetry.

## When not to use it

- Cancellation — never model `OperationCanceledException` as an error.
- Programming bugs in your own code — throw; reserve `ErrorType.Unexpected` for exceptions
  converted at boundaries via `Result.Try`.
- Rich domain objects. `Error` is a description of a failure, not a place to smuggle entities;
  metadata values should be JSON-primitive-representable (string, number, bool, null, or arrays
  thereof).

## Installation

```bash
dotnet add package Koras.Results
```

The error model is a core feature; no other package is required.

## Basic usage

```csharp
using Koras.Results;

public static class Program
{
    public static void Main()
    {
        // Factory per taxonomy entry
        var notFound = Error.NotFound("User.NotFound", "The requested user does not exist.");
        var conflict = Error.Conflict("User.DuplicateEmail", "That email address is already registered.");

        // Attach structured data without mutating the original (copy-with)
        var enriched = conflict
            .WithMetadata("email", "ada@example.com")
            .WithMetadata("attempt", 3L);

        Console.WriteLine(notFound);            // "NotFound: User.NotFound — The requested user does not exist."
        Console.WriteLine(enriched.Metadata.Count); // 2
        Console.WriteLine(conflict.Metadata.Count); // 0 — the original is unchanged

        // Equality is Code + Type only; message and metadata are presentation
        var same = Error.Conflict("User.DuplicateEmail", "different message");
        Console.WriteLine(conflict.Equals(same));     // True
        Console.WriteLine(conflict.Equals(enriched)); // True — metadata excluded too

        // An error converts implicitly into a failed result
        Result result = notFound;
        Console.WriteLine(result.IsFailure); // True
    }
}
```

## Dependency-injection usage

Errors are plain immutable values and need no DI registration. Services obtained from the
container create and return them directly; a common pattern is a static error catalog per module:

```csharp
using Koras.Results;

public static class AccountErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("Account.NotFound", $"Account '{id}' does not exist.");

    public static readonly Error Suspended =
        Error.Forbidden("Account.Suspended", "The account is suspended.");
}

public sealed class AccountService(IAccountRepository repository)
{
    public Result<Account> Get(Guid id) =>
        repository.Find(id) is { } account
            ? Result.Success(account)
            : Result.Failure<Account>(AccountErrors.NotFound(id));
}

public sealed record Account(Guid Id, string Owner);
```

Because errors are immutable, sharing a `static readonly Error` across all threads and requests
is safe.

## Advanced configuration

The error model itself has no configuration — it is pure data. How errors are *projected*
(HTTP status per `ErrorType` or per code, metadata exposure, message localization) is configured
in the `Koras.Results.AspNetCore` package via `KorasResultsOptions`.

## Public API

- `ErrorType` (enum) — `Failure = 0`, `Validation = 1`, `NotFound = 2`, `Conflict = 3`,
  `Unauthorized = 4`, `Forbidden = 5`, `Unavailable = 6`, `Unexpected = 7`. Numeric values are a
  serialization contract; never reorder.
- `Error` (immutable class, `IEquatable<Error>`)
  - `Error(string code, string message, ErrorType type, IReadOnlyDictionary<string, object?>? metadata = null)`
    — public constructor for custom scenarios; the metadata dictionary is defensively copied.
  - `Code` / `Message` / `Type` / `Metadata` — see overview; `Metadata` is never null.
  - Factories, one per taxonomy entry: `Error.Failure(code, message)`,
    `Error.Validation(code, message)`, `Error.NotFound(code, message)`,
    `Error.Conflict(code, message)`, `Error.Unauthorized(code, message)`,
    `Error.Forbidden(code, message)`, `Error.Unavailable(code, message)`,
    `Error.Unexpected(code, message)`.
  - `WithMetadata(string key, object? value)` / `WithMetadata(IReadOnlyDictionary<string, object?> metadata)`
    — copy-with helpers returning a new instance; existing keys are overwritten on merge.
  - `Equals` / `GetHashCode` — value equality on `Code` + `Type` only.
  - `ToString()` — `"NotFound: User.NotFound — message"`.
  - `Error.None` — internal-use success sentinel; never surfaced by a failure.
  - `Error.Uninitialized` — the error carried by `default(Result)` / `default(Result<T>)`
    (code `"Result.Uninitialized"`, type `Unexpected`).

`Error` is not designed for user inheritance; [`ValidationError`](validation-errors.md) and
`AggregateError` ([result-combination.md](result-combination.md)) are the only subclasses.

## Error handling

- Null, empty, or whitespace `code` or `message` → `ArgumentException` from the constructor and
  every factory.
- `WithMetadata(key, value)` with a null/whitespace key → `ArgumentException`;
  `WithMetadata((IReadOnlyDictionary<string, object?>)null!)` → `ArgumentNullException`.
- `Error.None` is never returned from `Result.Error`; accessing `Error` on a success throws
  `InvalidOperationException` instead of returning the sentinel.

Choosing a category: `Failure` is the catch-all for domain-rule rejections, `Unexpected` for
technical/unclassified conditions, `Unavailable` for transient infrastructure trouble (the only
category callers should generally retry).

## Cancellation

Errors are pure data and take no part in cancellation. `OperationCanceledException` is never
converted into an `Error` anywhere in the package — cancellation always propagates as an
exception (see [exception-conversion.md](exception-conversion.md)).

## Security considerations

- **Never place secrets, credentials, connection strings, or PII in `Message` or `Metadata`.**
  Errors are built to travel: into logs, telemetry, serialized payloads, and — via the
  ASP.NET Core package — potentially to clients.
- The ASP.NET Core projection suppresses `Unexpected` error details by default
  (`IncludeUnexpectedErrorDetails = false`) and exposes metadata only when
  `MetadataExposure` allows it (default: none) — but defense in depth starts with never putting
  sensitive data in the error at all.
- Metadata values should stay JSON-primitive-representable; the serializer never resolves
  polymorphic types from them.

## Performance considerations

- Errors allocate — on the failure path only, which is acceptable and by design. The success path
  of `Result`/`Result<T>` is allocation-free.
- The metadata dictionary is allocated only when metadata is actually used; an error without
  metadata shares a single empty read-only dictionary.
- `WithMetadata` copies the dictionary (immutability over micro-optimization); add all entries in
  one `WithMetadata(dictionary)` call when attaching several.
- Reuse well-known errors as `static readonly` fields; equality and hashing are cheap
  (`Code` + `Type`).

## Thread safety

`Error` and its subclasses are deeply immutable: constructor inputs are defensively copied and
`WithMetadata` returns new instances. Instances — including shared `static readonly` catalogs —
are safe to use concurrently from any thread.

## Testing applications using this feature

Assert on `Code` and `Type` — the stable machine contract — rather than on messages:

```csharp
using Koras.Results;
using Xunit;

public class ErrorModelTests
{
    [Fact]
    public void Factories_SetTheMatchingType()
    {
        Assert.Equal(ErrorType.NotFound, Error.NotFound("X.Missing", "missing").Type);
        Assert.Equal(ErrorType.Unavailable, Error.Unavailable("Db.Down", "database offline").Type);
    }

    [Fact]
    public void Equality_IgnoresMessageAndMetadata()
    {
        var a = Error.Conflict("Order.Duplicate", "first message");
        var b = Error.Conflict("Order.Duplicate", "second message").WithMetadata("orderId", 42L);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void WithMetadata_ReturnsNewInstance_OriginalUnchanged()
    {
        var original = Error.Failure("Order.Rejected", "rejected");
        var enriched = original.WithMetadata("reason", "credit");

        Assert.Empty(original.Metadata);
        Assert.Equal("credit", enriched.Metadata["reason"]);
    }

    [Fact]
    public void EmptyCode_Throws()
    {
        Assert.Throws<ArgumentException>(() => Error.Failure(" ", "message"));
    }
}
```

## Complete example

```csharp
using Koras.Results;

public static class PaymentErrors
{
    public static Error CardDeclined(string last4) =>
        Error.Failure("Payment.CardDeclined", "The card was declined.")
             .WithMetadata("cardLast4", last4);

    public static readonly Error GatewayUnavailable =
        Error.Unavailable("Payment.GatewayUnavailable", "The payment gateway is not responding.");
}

public sealed class PaymentService
{
    public Result Charge(string cardLast4, decimal amount)
    {
        if (amount <= 0)
        {
            return Error.Validation("Payment.InvalidAmount", "Amount must be positive.");
        }

        if (cardLast4 == "0000")
        {
            return PaymentErrors.CardDeclined(cardLast4);
        }

        return Result.Success();
    }
}

public static class Program
{
    public static void Main()
    {
        var service = new PaymentService();

        var outcome = service.Charge("0000", 25m);

        outcome.Switch(
            onSuccess: () => Console.WriteLine("Charged."),
            onFailure: error =>
            {
                Console.WriteLine($"[{error.Type}] {error.Code}: {error.Message}");
                foreach (var (key, value) in error.Metadata)
                {
                    Console.WriteLine($"  {key} = {value}");
                }

                if (error.Type == ErrorType.Unavailable)
                {
                    Console.WriteLine("  (transient — safe to retry)");
                }
            });
    }
}
```

## Common mistakes

1. **Putting sensitive data in messages or metadata.** Connection strings, tokens, and PII in an
   error will eventually reach a log or a client. Keep messages generic; keep metadata to safe
   identifiers.
2. **Asserting or branching on `Message`.** Messages are presentation and may change or be
   localized. Branch and assert on `Code` (and `Type`).
3. **Expecting metadata to participate in equality.** Two errors with the same `Code` and `Type`
   are equal even with different messages and metadata — errors are identities.
4. **Mutating expectations around `WithMetadata`.** It returns a *new* error; the original is
   unchanged. Capture the return value.
5. **Abusing `Unexpected` for domain failures.** `Unexpected` means "bug or unclassified
   exception" and maps to HTTP 500 with details suppressed. Use `Failure` for domain-rule
   rejections.

## Troubleshooting

- **`ArgumentException: 'code' must be a non-empty string.`** — a factory or constructor received
  a null/whitespace code or message. Error identity requires both.
- **Two "different" errors compare equal** — remember equality is `Code` + `Type` only. If two
  failures must be distinguishable, give them distinct codes.
- **Metadata missing after enrichment** — `WithMetadata` is copy-with; assign the result
  (`error = error.WithMetadata(...)`).
- **An error with code `Result.Uninitialized` appears** — a `default(Result)` leaked into your
  flow; see [result-types.md](result-types.md).

## Related features

- [result-types.md](result-types.md) — the structs that carry errors.
- [validation-errors.md](validation-errors.md) — the field-level `ValidationError` subclass.
- [result-combination.md](result-combination.md) — how `AggregateError` merges multiple errors.
- [exception-conversion.md](exception-conversion.md) — mapping exceptions to errors.
- [serialization.md](serialization.md) — the error wire shape, including metadata fidelity.
