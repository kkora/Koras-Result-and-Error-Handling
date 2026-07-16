# Result Types (`Result`, `Result<T>`)

Feature ID: KR-001 · Package: `Koras.Results` (Core)

## Overview

`Result` and `Result<T>` are readonly structs that make failure an explicit part of a method's
signature. A `Result` represents the outcome of an operation that produces no value; a `Result<T>`
represents the outcome of an operation that produces a value. Both are always in exactly one of
two states: a success (optionally carrying a non-null value) or a failure carrying an
[`Error`](error-model.md). Callers can no longer ignore failure the way they can ignore an
exception or a null return — the type system forces the question "what if this failed?" at every
call site.

Two design decisions distinguish these types from ad-hoc result implementations:

- `default(Result)` and `default(Result<T>)` are **failures** carrying `Error.Uninitialized`,
  never successes. An uninitialized struct cannot masquerade as a valid outcome (ADR-0003).
- A success `Result<T>` **never carries null**. `Result.Success<T>(null!)` throws
  `ArgumentNullException`; model optionality in the domain type instead.

## When to use it

- Domain and application services where failure is an expected, meaningful outcome
  ("user not found", "insufficient stock", "duplicate email").
- Any method that today returns `null`, a `bool` + `out` pair, or throws exceptions for
  conditions the caller is expected to handle.
- Layered applications where errors must travel across layers as data and be projected to HTTP,
  logs, or telemetry at the edge.

## When not to use it

- Truly exceptional conditions (corrupted state, programming bugs, out-of-memory). Let those
  throw; `Result` is for *expected* failures.
- Cancellation. `OperationCanceledException` must propagate as an exception; it is never modeled
  as a failed result.
- Hot inner loops where even the branch on `IsSuccess` matters — but note the success path
  allocates nothing, so this is rarely a real constraint.
- As a general-purpose optional type. `Result<T>` is not `Option<T>`; a failure always carries a
  semantic `Error`, not "no value".

## Installation

```bash
dotnet add package Koras.Results
```

Result types are a core feature; no other package is required.

## Basic usage

```csharp
using Koras.Results;

public static class UserLookup
{
    private static readonly Dictionary<int, string> Users = new() { [1] = "Ada" };

    public static Result<string> FindUserName(int id) =>
        Users.TryGetValue(id, out var name)
            ? Result.Success(name)
            : Result.Failure<string>(Error.NotFound("User.NotFound", $"No user with id {id}."));
}

public static class Program
{
    public static void Main()
    {
        var result = UserLookup.FindUserName(1);

        if (result.IsSuccess)
        {
            Console.WriteLine($"Found: {result.Value}");
        }
        else
        {
            Console.WriteLine($"Failed: {result.Error.Code}");
        }

        // Or fold both branches in one expression:
        var message = UserLookup.FindUserName(42).Match(
            onSuccess: name => $"Found {name}",
            onFailure: error => $"Failed with {error.Code}");
        Console.WriteLine(message);
    }
}
```

Implicit conversions keep call sites terse: returning a `T` produces a success, returning an
`Error` produces a failure.

```csharp
public static Result<string> FindUserName(int id)
{
    if (id <= 0)
    {
        return Error.Validation("User.InvalidId", "Id must be positive."); // implicit failure
    }

    return "Ada"; // implicit success
}
```

## Dependency-injection usage

`Result` and `Result<T>` are plain immutable values. They are never registered in a container and
need no DI setup — you simply return them from injected services:

```csharp
using Koras.Results;

public interface IUserService
{
    Result<User> GetUser(int id);
}

public sealed class UserService(IUserRepository repository) : IUserService
{
    public Result<User> GetUser(int id) =>
        repository.Find(id) is { } user
            ? Result.Success(user)
            : Result.Failure<User>(Error.NotFound("User.NotFound", $"No user with id {id}."));
}

public sealed record User(int Id, string Name);
```

Register the service normally (`services.AddScoped<IUserService, UserService>()`); the results it
returns are just values flowing through method returns.

## Advanced configuration

There is none. Result types are pure data with no options, no global state, and no configurable
behavior. Configuration exists only in the optional `Koras.Results.AspNetCore` package, which
controls how results are *projected* to HTTP.

## Public API

- `Result` (readonly struct) — outcome of a void operation.
  - `IsSuccess` / `IsFailure` — mutually exclusive state flags.
  - `Error` — the failure's error; throws `InvalidOperationException` on success.
  - `Result.Success()` — creates a success.
  - `Result.Failure(Error error)` — creates a failure; `ArgumentNullException` on null.
  - `Result.Success<T>(T value)` — creates a generic success; `ArgumentNullException` on null value.
  - `Result.Failure<T>(Error error)` — creates a generic failure.
  - `implicit operator Result(Error error)` — an error converts to a failure.
- `Result<T>` (readonly struct) — outcome of a value-producing operation.
  - `IsSuccess` (`[MemberNotNullWhen(true)]` — the compiler knows `Value` is non-null) / `IsFailure`.
  - `Value` — the success value; throws `InvalidOperationException` on failure.
  - `Error` — the failure's error; throws `InvalidOperationException` on success.
  - `TryGetValue(out T value)` / `TryGetError(out Error error)` — non-throwing accessors.
  - `GetValueOrDefault()` / `GetValueOrDefault(T fallback)` — value or fallback.
  - `ToResult()` — drops the value, keeping the outcome and error.
  - `implicit operator Result<T>(T value)` / `implicit operator Result<T>(Error error)` /
    `implicit operator Result(Result<T> result)`.

## Error handling

- Accessing `Value` on a failure throws `InvalidOperationException` with a message naming the
  error code. Accessing `Error` on a success throws `InvalidOperationException`.
- `Result.Failure(null!)` and `Result.Failure<T>(null!)` throw `ArgumentNullException`.
- `Result.Success<T>(null!)` (and the implicit conversion from a null value) throws
  `ArgumentNullException` — success never carries null.
- `default(Result)` / `default(Result<T>)` are failures carrying `Error.Uninitialized`
  (`"Result.Uninitialized"`, `ErrorType.Unexpected`).

## Cancellation

Result types are pure data and never interact with `CancellationToken`. The core rule of the
package applies everywhere: cancellation is never converted to a failed result —
`OperationCanceledException` always propagates as an exception (see
[exception-conversion.md](exception-conversion.md)).

## Security considerations

Results are pure data with no I/O, deserialization, or reflection surface. The security burden
lives in the `Error` you attach: never place secrets, credentials, or PII in `Error.Message` or
`Error.Metadata`, because errors are designed to travel to logs and — through the ASP.NET Core
package — potentially to clients.

## Performance considerations

- `Result` and `Result<T>` are `readonly struct`s; the **success path allocates nothing**.
- Failures allocate only the `Error` instance (and its metadata dictionary, if used).
- Copying a result is a small struct copy (a bool plus one or two references).

## Thread safety

`Result`, `Result<T>`, and the whole `Error` family are immutable. Instances can be freely
shared, cached, and read from any number of threads without synchronization.

## Testing applications using this feature

Results are values, so tests assert on them directly — no mocking infrastructure required:

```csharp
using Koras.Results;
using Xunit;

public class UserLookupTests
{
    [Fact]
    public void FindUserName_KnownId_ReturnsSuccess()
    {
        var result = UserLookup.FindUserName(1);

        Assert.True(result.IsSuccess);
        Assert.Equal("Ada", result.Value);
    }

    [Fact]
    public void FindUserName_UnknownId_ReturnsNotFound()
    {
        var result = UserLookup.FindUserName(999);

        Assert.True(result.IsFailure);
        Assert.Equal("User.NotFound", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public void DefaultResult_IsUninitializedFailure()
    {
        var result = default(Result<string>);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.Uninitialized, result.Error);
    }
}
```

Note that `Result<T>` has value equality: two successes are equal when their values are equal,
two failures when their errors are equal (by code and type), so `Assert.Equal(expected, actual)`
works on whole results.

## Complete example

```csharp
using Koras.Results;

public sealed record Order(Guid Id, string Sku, int Quantity);

public sealed class OrderService
{
    private readonly Dictionary<string, int> _stock = new() { ["SKU-1"] = 5 };

    public Result<Order> Place(string sku, int quantity)
    {
        if (quantity <= 0)
        {
            return Error.Validation("Order.InvalidQuantity", "Quantity must be positive.");
        }

        if (!_stock.TryGetValue(sku, out var available))
        {
            return Error.NotFound("Product.NotFound", $"No product with SKU '{sku}'.");
        }

        if (available < quantity)
        {
            return Error
                .Failure("Order.InsufficientStock", "Not enough stock to fulfil the order.")
                .WithMetadata("available", available);
        }

        _stock[sku] = available - quantity;
        return new Order(Guid.NewGuid(), sku, quantity);
    }
}

public static class Program
{
    public static void Main()
    {
        var service = new OrderService();

        service.Place("SKU-1", 2).Switch(
            onSuccess: order => Console.WriteLine($"Placed order {order.Id}"),
            onFailure: error => Console.WriteLine($"Rejected: {error}"));

        service.Place("SKU-1", 99).Switch(
            onSuccess: order => Console.WriteLine($"Placed order {order.Id}"),
            onFailure: error => Console.WriteLine($"Rejected: {error}"));
    }
}
```

## Common mistakes

1. **Accessing `.Value` without checking `IsSuccess`.** On a failure this throws
   `InvalidOperationException`. Check `IsSuccess`, use `TryGetValue`, or fold with `Match`.
2. **Expecting `default(Result)` to be a success.** It is a failure carrying
   `Error.Uninitialized` by design. Always construct results through `Result.Success()` /
   `Result.Failure(...)` or the implicit conversions.
3. **Returning `Result.Success<string?>(null)`.** Success never carries null; this throws
   `ArgumentNullException`. Model "absent" as a `NotFound` failure or a domain type with an
   explicit empty representation.
4. **Using `Result` for bugs.** Null arguments, broken invariants, and impossible states should
   throw. Reserving results for expected failures keeps both channels meaningful.
5. **Swallowing the result.** Calling a `Result`-returning method and discarding the return value
   silently drops failures. Always observe the result — via `IsSuccess`, `Match`, `Switch`, or by
   returning it to your caller.

## Troubleshooting

- **`InvalidOperationException: Cannot access Value on a failure result (error '...')`** — you
  read `Value` on a failure; the message names the error code that was actually carried. Guard
  with `IsSuccess` or `TryGetValue`.
- **`InvalidOperationException: Cannot access Error on a success result`** — the symmetric
  mistake; guard with `IsFailure` or `TryGetError`.
- **A failure appears with code `Result.Uninitialized`** — somewhere a `default(Result)` /
  `default(Result<T>)` leaked into your flow: an unassigned field, `new Result<T>[n]` array slots,
  or a struct default in a dictionary. Find the uninitialized value and construct it explicitly.
- **`ArgumentNullException` from an implicit conversion** — you assigned a null `T` or null
  `Error` to a result. The guard is intentional.

## Related features

- [error-model.md](error-model.md) — the `Error` carried by every failure.
- [functional-composition.md](functional-composition.md) — chaining results with Map/Bind/Match.
- [async-composition.md](async-composition.md) — the same pipelines over `Task<Result<T>>`.
- [exception-conversion.md](exception-conversion.md) — bridging exception-throwing code.
- [result-combination.md](result-combination.md) — aggregating independent results.
- [serialization.md](serialization.md) — the JSON wire shape of results.
