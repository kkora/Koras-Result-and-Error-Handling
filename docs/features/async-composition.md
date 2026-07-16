# Async Composition (MapAsync, BindAsync, MatchAsync, EnsureAsync, TapAsync, TapErrorAsync)

Feature ID: KR-005 · Package: `Koras.Results` (Core)

## Overview

`ResultAsyncExtensions` extends the [synchronous combinators](functional-composition.md) to
asynchronous pipelines. Real applications await repositories, HTTP clients, and message brokers;
without these overloads every step would need an intermediate `await` and a temporary variable.
With them, a pipeline over `Task<Result<T>>` reads exactly like its synchronous counterpart and
is awaited once at the end.

The overloads cover three axes:

- **sync receiver + async delegate** — `Result<T>` with a `Task`-returning delegate;
- **task receiver + sync delegate** — `Task<Result<T>>` with an ordinary delegate;
- **task receiver + async delegate** — `Task<Result<T>>` with a `Task`-returning delegate.

The method name carries the `Async` suffix whenever the return type is a `Task`. All awaits use
`ConfigureAwait(false)`, and the short-circuit rule is unchanged: delegates are never invoked on
failures and the original error propagates by identity.

## When to use it

- Pipelines where any step performs I/O: database lookups, HTTP calls, file access, queues.
- Chaining onto a `Task<Result<T>>` returned by another method without an intermediate `await`.
- Mixing sync and async steps in one chain — the overload matrix covers every combination.

## When not to use it

- Fully synchronous pipelines — use the sync combinators; wrapping sync work in tasks adds
  overhead for nothing.
- Around async code that throws — bridge it with `Result.TryAsync` first (see
  [exception-conversion.md](exception-conversion.md)); async combinator delegates that throw or
  fault propagate their exceptions unchanged.
- To run independent operations concurrently. The chain is sequential by design; start the tasks
  yourself, await them, then aggregate with `Result.Combine`
  ([result-combination.md](result-combination.md)).

## Installation

```bash
dotnet add package Koras.Results
```

Async composition is a core feature; the extensions live in the `Koras.Results` namespace.

## Basic usage

```csharp
using Koras.Results;

public sealed record User(int Id, string Email);
public sealed record Invoice(Guid Id, int UserId, decimal Amount);

public sealed class BillingPipeline
{
    public async Task<Invoice> LoadInvoiceAsync(Guid id) =>
        new Invoice(id, 1, 42m); // stands in for a database call

    public async Task<User> LoadUserAsync(int id) =>
        new User(id, "ada@example.com"); // stands in for a database call

    public Task<Result<string>> DescribeInvoiceAsync(Guid invoiceId) =>
        FindInvoiceAsync(invoiceId)                                      // Task<Result<Invoice>>
            .EnsureAsync(i => i.Amount > 0,
                         Error.Validation("Invoice.EmptyAmount", "Invoice amount must be positive."))
            .BindAsync(async i =>                                        // async delegate on task receiver
            {
                var user = await LoadUserAsync(i.UserId);
                return Result.Success((Invoice: i, User: user));
            })
            .MapAsync(pair => $"{pair.User.Email} owes {pair.Invoice.Amount:C}") // sync delegate
            .TapAsync(text => Console.WriteLine(text));

    private async Task<Result<Invoice>> FindInvoiceAsync(Guid id)
    {
        var invoice = await LoadInvoiceAsync(id);
        return invoice is not null
            ? Result.Success(invoice)
            : Result.Failure<Invoice>(Error.NotFound("Invoice.NotFound", $"No invoice {id}."));
    }
}

public static class Program
{
    public static async Task Main()
    {
        var pipeline = new BillingPipeline();

        var message = await pipeline.DescribeInvoiceAsync(Guid.NewGuid())
            .MatchAsync(
                onSuccess: text => $"OK: {text}",
                onFailure: error => $"FAIL: {error.Code}");

        Console.WriteLine(message);
    }
}
```

Note the single `await` at the end: each combinator returns the next `Task<Result<...>>`, so the
whole chain composes fluently.

## Dependency-injection usage

As with all core features, nothing is registered — async combinators compose calls across
injected services:

```csharp
using Koras.Results;

public interface IUserRepository { Task<Result<User>> FindAsync(int id, CancellationToken ct); }
public interface IEmailSender    { Task SendAsync(string to, string body, CancellationToken ct); }

public sealed class WelcomeService(IUserRepository users, IEmailSender email)
{
    public Task<Result<User>> SendWelcomeAsync(int userId, CancellationToken ct) =>
        users.FindAsync(userId, ct)
            .EnsureAsync(u => u.Email.Contains('@'),
                         Error.Validation("User.InvalidEmail", "User has no valid email address."))
            .TapAsync(u => email.SendAsync(u.Email, "Welcome!", ct));
}
```

The `CancellationToken` is captured by the delegate closures and flows into the I/O calls — see
"Cancellation" below.

## Advanced configuration

There is none. Two fixed, deliberate design decisions stand in for configuration:

- Every await uses `ConfigureAwait(false)`; the library never captures a synchronization context.
- No overload accepts a `CancellationToken`. The combinators are pure plumbing; a token parameter
  on every overload would double the matrix for no benefit, because the token belongs to the I/O
  call inside the delegate.

## Public API

All members are on the static class `ResultAsyncExtensions`:

- Map
  - `MapAsync<TIn, TOut>(this Result<TIn>, Func<TIn, Task<TOut>>)` — async delegate, sync receiver.
  - `MapAsync<TIn, TOut>(this Task<Result<TIn>>, Func<TIn, TOut>)` — sync delegate, task receiver.
  - `MapAsync<TIn, TOut>(this Task<Result<TIn>>, Func<TIn, Task<TOut>>)` — async delegate, task receiver.
  - `MapAsync<TOut>(this Task<Result>, Func<TOut>)` — value factory over an awaited void result.
- Bind
  - `BindAsync<TIn, TOut>(this Result<TIn>, Func<TIn, Task<Result<TOut>>>)`.
  - `BindAsync<TIn, TOut>(this Task<Result<TIn>>, Func<TIn, Result<TOut>>)`.
  - `BindAsync<TIn, TOut>(this Task<Result<TIn>>, Func<TIn, Task<Result<TOut>>>)`.
  - `BindAsync<TIn>(this Task<Result<TIn>>, Func<TIn, Task<Result>>)` — bridge to non-generic `Result`.
  - `BindAsync(this Task<Result>, Func<Task<Result>>)` — non-generic chaining.
- Match
  - `MatchAsync<TIn, TOut>(this Task<Result<TIn>>, Func<TIn, TOut>, Func<Error, TOut>)` — sync folds.
  - `MatchAsync<TIn, TOut>(this Task<Result<TIn>>, Func<TIn, Task<TOut>>, Func<Error, Task<TOut>>)` — async folds.
  - `MatchAsync<TOut>(this Task<Result>, Func<TOut>, Func<Error, TOut>)` — non-generic fold.
- Ensure
  - `EnsureAsync<T>(this Task<Result<T>>, Func<T, bool>, Error)` — sync predicate, task receiver.
  - `EnsureAsync<T>(this Result<T>, Func<T, Task<bool>>, Error)` — async predicate, sync receiver.
  - `EnsureAsync<T>(this Task<Result<T>>, Func<T, Task<bool>>, Error)` — async predicate, task receiver.
- Tap / TapError
  - `TapAsync<T>(this Result<T>, Func<T, Task>)` — async side effect on success.
  - `TapAsync<T>(this Task<Result<T>>, Action<T>)` / `TapAsync<T>(this Task<Result<T>>, Func<T, Task>)`.
  - `TapAsync(this Task<Result>, Action)` — non-generic.
  - `TapErrorAsync<T>(this Task<Result<T>>, Action<Error>)` / `TapErrorAsync<T>(this Task<Result<T>>, Func<Error, Task>)` — side effects on failure.

## Error handling

- Null receivers (`resultTask`) and null delegates throw `ArgumentNullException` eagerly —
  before any await — so the exception surfaces at the composition site.
- Failure short-circuits: async delegates are never started for a failed result, and the original
  `Error` propagates by identity.
- Delegate exceptions and faulted delegate tasks propagate unchanged; the combinators never
  convert them to failures. Use `Result.TryAsync` at exception boundaries.
- Expected error categories are whatever your steps produce; the combinators add none of their
  own.

## Cancellation

Cancellation tokens belong to the I/O calls *inside* your delegates — capture them by closure, as
in the DI example above. The combinators deliberately take no token.

If a delegate's task is cancelled, the resulting `OperationCanceledException` propagates out of
the awaited chain exactly as it would from any awaited call. It is **never** converted into a
failed result: cancellation is not failure. Catch it (or let it flow) at the same place you would
for any async method.

## Security considerations

The combinators perform no I/O of their own; security properties are those of your delegates and
the errors they produce. The same boundary rule applies as in the sync case: use `MapError`
(sync) or an error-translating `BindAsync` step to replace detail-rich internal errors with
client-safe ones before results leave your service layer, and keep secrets out of error messages
and metadata.

## Performance considerations

- Where no await is needed — a failure short-circuiting, or a sync receiver with a failed result
  — the implementations return a completed task (`Task.FromResult`) rather than paying for an
  async state machine.
- `ConfigureAwait(false)` throughout avoids synchronization-context capture and the associated
  scheduling cost.
- The success path of the underlying structs remains allocation-free; the tasks themselves and
  delegate closures allocate as ordinary async C# does.
- Failures allocate the `Error` once at creation; propagation reuses the instance.

## Thread safety

The combinators are pure static methods over immutable values. Chains may be built and awaited
from any thread; because of `ConfigureAwait(false)`, continuations run on the thread pool. Your
delegates must be safe for whatever concurrency your application introduces.

## Testing applications using this feature

Async pipelines test like any async code — await the chain and assert on the result:

```csharp
using Koras.Results;
using Xunit;

public class AsyncCompositionTests
{
    [Fact]
    public async Task Chain_Success_TransformsValue()
    {
        var result = await Task.FromResult(Result.Success(5))
            .MapAsync(x => x * 2)
            .EnsureAsync(x => x > 0, Error.Validation("Num.NotPositive", "Must be positive."))
            .BindAsync(x => Task.FromResult(Result.Success(x + 1)));

        Assert.True(result.IsSuccess);
        Assert.Equal(11, result.Value);
    }

    [Fact]
    public async Task Chain_Failure_DoesNotInvokeAsyncDelegates()
    {
        var error = Error.NotFound("X.Missing", "missing");
        var invoked = false;

        var result = await Task.FromResult(Result.Failure<int>(error))
            .BindAsync(async x =>
            {
                invoked = true;
                await Task.Yield();
                return Result.Success(x);
            });

        Assert.True(result.IsFailure);
        Assert.False(invoked);
        Assert.Same(error, result.Error);
    }

    [Fact]
    public async Task Cancellation_PropagatesAsException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Task.FromResult(Result.Success(1))
                .TapAsync(async _ => await Task.Delay(1000, cts.Token)));
    }
}
```

## Complete example

```csharp
using Koras.Results;

public sealed record Customer(int Id, string Email, bool Active);
public sealed record Report(int CustomerId, int OrderCount);

public sealed class ReportService
{
    public async Task<Result<Report>> BuildReportAsync(int customerId, CancellationToken ct) =>
        await FindCustomerAsync(customerId, ct)
            .EnsureAsync(c => c.Active,
                         Error.Forbidden("Customer.Inactive", "Reports are only available for active customers."))
            .BindAsync(async c =>
            {
                var count = await CountOrdersAsync(c.Id, ct);
                return Result.Success(new Report(c.Id, count));
            })
            .TapAsync(r => Console.WriteLine($"Report ready for customer {r.CustomerId}"))
            .TapErrorAsync(e => Console.Error.WriteLine($"Report failed: {e}"));

    private async Task<Result<Customer>> FindCustomerAsync(int id, CancellationToken ct)
    {
        await Task.Delay(10, ct); // stands in for a database query
        return id > 0
            ? Result.Success(new Customer(id, "ada@example.com", Active: true))
            : Result.Failure<Customer>(Error.NotFound("Customer.NotFound", $"No customer {id}."));
    }

    private async Task<int> CountOrdersAsync(int customerId, CancellationToken ct)
    {
        await Task.Delay(10, ct); // stands in for a database query
        return 7;
    }
}

public static class Program
{
    public static async Task Main()
    {
        var service = new ReportService();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var summary = await service.BuildReportAsync(1, cts.Token)
            .MatchAsync(
                onSuccess: r => $"{r.OrderCount} orders",
                onFailure: e => $"failed: {e.Code}");

        Console.WriteLine(summary);
    }
}
```

## Common mistakes

1. **Awaiting mid-chain unnecessarily.** `var r = await step1(); var r2 = await r.BindAsync(...)`
   works, but the task-receiver overloads exist so you can write
   `await step1().BindAsync(...).MapAsync(...)` with one await at the end.
2. **Catching exceptions in async delegates instead of using `Result.TryAsync`.** Faulted delegate
   tasks propagate; the boundary belongs in `Result.TryAsync`, not scattered try/catch blocks
   inside `MapAsync` lambdas.
3. **Treating cancellation as failure.** Do not catch `OperationCanceledException` in a delegate
   and return a failed result; let it propagate. The library never converts cancellation, and
   neither should you.
4. **Forgetting to pass the token into delegate I/O.** Because the combinators take no token, the
   only path for cancellation is the closure: `users.FindAsync(id, ct)` inside the delegate. A
   delegate that ignores `ct` cannot be cancelled.
5. **Expecting concurrency from a chain.** `BindAsync` steps run strictly one after another. For
   parallel independent calls, start the tasks first, await them, then combine the results.

## Troubleshooting

- **`ArgumentNullException` before anything runs** — a null `resultTask` or delegate; guards fire
  eagerly at composition time.
- **The chain returns before delegates run** — the receiver was a failure and everything
  short-circuited; inspect the error with `TapErrorAsync` or a `MatchAsync` failure branch.
- **`OperationCanceledException` surfaces from an awaited chain** — expected behavior when a
  delegate's token fires; handle it where you handle cancellation for any async call.
- **Deadlock in a UI/legacy sync context** — the library itself uses `ConfigureAwait(false)`, but
  blocking on the chain with `.Result`/`.Wait()` from a context-bound thread can still deadlock
  in *your* delegates. Stay async end to end.

## Related features

- [functional-composition.md](functional-composition.md) — the synchronous combinators and shared semantics.
- [exception-conversion.md](exception-conversion.md) — `Result.TryAsync` for async code that throws.
- [result-combination.md](result-combination.md) — aggregating results of concurrent operations.
- [result-types.md](result-types.md) — the underlying structs.
- [error-model.md](error-model.md) — the errors flowing through the pipeline.
