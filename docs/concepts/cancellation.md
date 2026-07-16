# Cancellation

**Cancellation is not failure.** Nothing went wrong when a request is cancelled — the caller simply stopped wanting the answer. Koras.Results therefore never represents cancellation as an `Error`, and its API shape is deliberately built around that rule.

## The rule: `OperationCanceledException` always propagates

`Result.Try` and `Result.TryAsync` exist to convert thrown exceptions into failure results at the boundary between exception-based and result-based code. They make exactly one exception to that conversion — literally:

```csharp
public static Result Try(Action action, Func<Exception, Error>? mapError = null)
{
    try
    {
        action();
        return Success();
    }
    catch (OperationCanceledException)
    {
        throw;                    // cancellation is not failure — always rethrown
    }
    catch (Exception exception)
    {
        return Failure(MapException(exception, mapError));
    }
}
```

All four overloads (`Try`, `Try<T>`, `TryAsync`, `TryAsync<T>`) behave identically: `OperationCanceledException` (which includes `TaskCanceledException`) is rethrown, never mapped — even if you supply your own `mapError`, it is never invoked for cancellation.

Why this matters:

- **Hosts understand cancellation.** ASP.NET Core, `BackgroundService`, and `Task`-based pipelines all treat `OperationCanceledException` as cooperative shutdown. Swallowing it into a failure result turns clean shutdowns into phantom 5xx errors, poisons retry logic, and pollutes error dashboards with non-errors.
- **No caller can "handle" cancellation as a failure.** There is no meaningful recovery branch — the correct behavior is to stop, promptly, all the way up the stack. Stack unwinding is exactly the right mechanism.

Follow the same rule in your own code: never write `catch (OperationCanceledException) { return SomeError; }`, and never define an `Error` for cancellation in your catalog.

## Why combinators take no `CancellationToken`

You will notice that `Map`, `Bind`, `Ensure`, `Tap`, and their async variants accept no token. This is a deliberate, documented design decision, not an omission:

- **Combinators are pure plumbing.** They route a value or an error between your delegates; there is nothing inside them to cancel.
- **The token belongs to the I/O inside your delegates.** Delegates close over the token they need — this keeps cancellation where the actual work happens, and keeps the overload matrix (already three axes: receiver kind × delegate kind × generic arity) from doubling for zero benefit.

```csharp
public Task<Result<ReceiptDto>> CheckoutAsync(Guid orderId, CancellationToken ct) =>
    LoadOrderAsync(orderId, ct)                              // token passed to the I/O call
        .EnsureAsync(o => !o.Shipped, OrderErrors.AlreadyShipped(orderId))
        .BindAsync(o => _payments.ChargeAsync(o, ct))        // closure carries ct to the gateway
        .MapAsync(p => ReceiptDto.From(p));                  // pure transform: no token needed
```

If cancellation fires mid-pipeline, the in-flight delegate throws `OperationCanceledException`; combinators do not catch delegate exceptions, so it unwinds straight through the pipeline to the host. The result value is simply never produced — which is correct, because nobody is waiting for it anymore.

## APIs that do accept a token

Wherever a Koras.Results API *itself* initiates awaitable work on your behalf, it takes a token like any well-behaved async API. The FluentValidation adapter is the canonical example:

```csharp
public static Task<Result<T>> ValidateToResultAsync<T>(
    this IValidator<T> validator, T instance,
    CancellationToken cancellationToken = default);
```

```csharp
app.MapPost("/todos", async (CreateTodo cmd, IValidator<CreateTodo> validator, TodoStore store, CancellationToken ct) =>
    (await validator.ValidateToResultAsync(cmd, ct))         // request-aborted token flows in
        .Bind(valid => store.Create(valid.Title!))
        .ToCreatedHttpResult(t => $"/todos/{t.Id}"));
```

Minimal APIs bind `CancellationToken` to `HttpContext.RequestAborted` automatically, so an aborted request cancels validation instead of producing a `ValidationError`.

## Pattern: cancellation-aware background work

The `WorkerService.Sample` shows the full discipline — errors drive retry decisions, cancellation shuts down cleanly:

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        // OperationCanceledException from inside the delegate is NOT converted:
        // it escapes TryAsync and ends the loop via the catch below.
        var result = await Result.TryAsync(
            () => SyncBatchAsync(stoppingToken),
            ex => SyncErrors.FromException(ex));

        result.Switch(
            onSuccess: () => _logger.LogInformation("Batch synced."),
            onFailure: error =>
            {
                if (error.Type == ErrorType.Unavailable)
                    _logger.LogWarning("Dependency down ({Code}); retrying next cycle.", error.Code);
                else
                    _logger.LogError("Terminal failure: {Code}", error.Code);
            });

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            break;                        // clean shutdown — not an error, not logged as one
        }
    }
}
```

## Summary

| Situation | Behavior |
|---|---|
| `OperationCanceledException` inside `Result.Try` / `TryAsync` | Rethrown — never converted to a failure, custom `mapError` never invoked |
| Cancellation mid-combinator-pipeline | Delegate throws; exception unwinds through the pipeline (combinators never catch) |
| `Map` / `Bind` / `Ensure` / `Tap` and async variants | Take no token by design — pass the token into your delegates via closure |
| `ValidateToResultAsync` | Accepts a `CancellationToken` and forwards it to FluentValidation |
| Your error catalogs | Should contain no "Cancelled" error — ever |

## Further reading

- [Lifecycle](lifecycle.md) — where the exception boundary sits in a result's life
- [Error handling](error-handling.md) — what *does* belong in the error catalog
