# Functional Composition (Map, Bind, Match, Switch, Ensure, Tap, TapError, MapError)

Feature ID: KR-004 · Package: `Koras.Results` (Core)

## Overview

`ResultExtensions` provides the synchronous combinators that turn `Result` and `Result<T>` into a
pipeline: transform values (`Map`), chain fallible steps (`Bind`), assert post-conditions
(`Ensure`), run side effects (`Tap`/`TapError`), translate errors at layer boundaries
(`MapError`), and fold the two branches into a single value (`Match`/`Switch`).

The governing rule is **short-circuiting**: on a failure result, transformation delegates are
never invoked and the original error propagates by identity — no copying, no re-wrapping. This
replaces the repetitive `if (r.IsFailure) return r.Error;` boilerplate with a linear, readable
chain that stops at the first failure.

Delegates that throw are *not* caught. The combinators are pure plumbing; exception boundaries
belong to `Result.Try` (see [exception-conversion.md](exception-conversion.md)).

## When to use it

- Multi-step operations where each step may fail and later steps depend on earlier values.
- Enforcing invariants on a produced value (`Ensure`) without breaking the chain.
- Logging/metrics along the pipeline (`Tap`, `TapError`) without changing the outcome.
- Translating low-level errors into layer-appropriate ones at boundaries (`MapError`).
- Producing a final response from either branch (`Match`) — e.g. mapping to HTTP or a message.

## When not to use it

- When steps are asynchronous — use the async counterparts in
  [async-composition.md](async-composition.md).
- Around code that throws — wrap it with `Result.Try` first; a throwing `Map` delegate tears the
  pipeline down with an exception.
- When you need *all* failures from independent checks, not the first — use `Result.Combine`
  (see [result-combination.md](result-combination.md)); `Bind` chains stop at the first failure.
- For a single result with trivial handling; a plain `if (result.IsSuccess)` can be clearer than
  a one-step chain.

## Installation

```bash
dotnet add package Koras.Results
```

Functional composition is a core feature; the extensions live in the `Koras.Results` namespace,
next to the types, so they are IntelliSense-discoverable with a single `using`.

## Basic usage

```csharp
using Koras.Results;

public sealed record Order(Guid Id, string Sku, int Quantity, decimal Total);
public sealed record Product(string Sku, decimal Price, int Stock);

public sealed class OrderPipeline
{
    private readonly Dictionary<string, Product> _catalog = new()
    {
        ["SKU-1"] = new Product("SKU-1", 19.99m, 5),
    };

    public Result<Order> Place(string sku, int quantity) =>
        FindProduct(sku)
            .Ensure(p => p.Stock >= quantity,
                    p => Error.Failure("Order.InsufficientStock", $"Only {p.Stock} left of {p.Sku}."))
            .Map(p => new Order(Guid.NewGuid(), p.Sku, quantity, p.Price * quantity))
            .Tap(o => Console.WriteLine($"Placed {o.Id} for {o.Total:C}"))
            .TapError(e => Console.WriteLine($"Rejected: {e.Code}"));

    private Result<Product> FindProduct(string sku) =>
        _catalog.TryGetValue(sku, out var product)
            ? Result.Success(product)
            : Result.Failure<Product>(Error.NotFound("Product.NotFound", $"No product '{sku}'."));
}

public static class Program
{
    public static void Main()
    {
        var pipeline = new OrderPipeline();

        var message = pipeline.Place("SKU-1", 2).Match(
            onSuccess: order => $"OK: {order.Id}",
            onFailure: error => $"FAIL: {error.Code}");

        Console.WriteLine(message);
    }
}
```

`Map` transforms the value; `Bind` is for steps that themselves return a result:

```csharp
Result<Shipment> Ship(Order order) => /* another fallible step */ Result.Success(new Shipment(order.Id));

Result<Shipment> outcome = pipeline.Place("SKU-1", 1).Bind(Ship);

public sealed record Shipment(Guid OrderId);
```

## Dependency-injection usage

The combinators are static extension methods over plain values — nothing to register. They shine
when composing calls across injected services:

```csharp
using Koras.Results;

public sealed class CheckoutService(IProductCatalog catalog, IPricingService pricing)
{
    public Result<decimal> Quote(string sku, int quantity) =>
        catalog.Find(sku)                                   // Result<Product> from an injected service
            .Ensure(p => p.Stock >= quantity,
                    Error.Failure("Quote.InsufficientStock", "Not enough stock."))
            .Bind(p => pricing.PriceFor(p, quantity))       // Result<decimal> from another service
            .MapError(e => e.Type == ErrorType.Unavailable
                ? Error.Unavailable("Quote.Unavailable", "Quoting is temporarily unavailable.")
                : e);
}

public interface IProductCatalog { Result<Product> Find(string sku); }
public interface IPricingService { Result<decimal> PriceFor(Product product, int quantity); }
```

## Advanced configuration

There is none. The combinators are deterministic pure functions with no options or global state.
Behavioral guarantees (short-circuit, identity propagation, delegates invoked at most once) are
fixed by contract and locked by tests.

## Public API

All members are on the static class `ResultExtensions`:

- `Map<TIn, TOut>(this Result<TIn>, Func<TIn, TOut>)` — transforms the value of a success.
- `Map<TOut>(this Result, Func<TOut>)` — produces a value from a void success.
- `Bind<TIn, TOut>(this Result<TIn>, Func<TIn, Result<TOut>>)` — chains a result-returning step.
- `Bind<TIn>(this Result<TIn>, Func<TIn, Result>)` — chains a void-result step.
- `Bind<TOut>(this Result, Func<Result<TOut>>)` — chains a value-producing step onto a void result.
- `Bind(this Result, Func<Result>)` — chains a void step onto a void result.
- `Match<TIn, TOut>(this Result<TIn>, Func<TIn, TOut> onSuccess, Func<Error, TOut> onFailure)` — exhaustive fold.
- `Match<TOut>(this Result, Func<TOut> onSuccess, Func<Error, TOut> onFailure)` — fold for void results.
- `Switch<TIn>(this Result<TIn>, Action<TIn> onSuccess, Action<Error> onFailure)` — action-based fold.
- `Switch(this Result, Action onSuccess, Action<Error> onFailure)` — action-based fold, void.
- `Ensure<T>(this Result<T>, Func<T, bool> predicate, Error error)` — post-condition with a fixed error.
- `Ensure<T>(this Result<T>, Func<T, bool> predicate, Func<T, Error> errorFactory)` — post-condition with a value-aware error.
- `Tap<T>(this Result<T>, Action<T>)` / `Tap(this Result, Action)` — side effect on success; passes through.
- `TapError<T>(this Result<T>, Action<Error>)` / `TapError(this Result, Action<Error>)` — side effect on failure; passes through.
- `MapError<T>(this Result<T>, Func<Error, Error>)` / `MapError(this Result, Func<Error, Error>)` — translate the error of a failure.

Semantics: on failure, `Map`/`Bind`/`Ensure`/`Tap` return the same error identity without invoking
delegates; on success, `TapError`/`MapError` pass through. Delegate exceptions are NOT caught.
Null delegates throw `ArgumentNullException` eagerly.

## Error handling

- **Null delegates** throw `ArgumentNullException` immediately (eagerly, before inspecting the
  result), so misconfiguration surfaces at the call site, not deep in a pipeline.
- **Delegates that throw are not caught.** An exception in a `Map`/`Bind`/`Tap` delegate
  propagates and aborts the chain. This is deliberate: silently converting delegate bugs into
  failures would hide defects. Bridge throwing code with `Result.Try` first.
- **Errors propagate by identity.** A failure entering a chain exits it as the same `Error`
  instance (unless `MapError` translates it), preserving type, metadata, and subclass shape
  (`ValidationError`, `AggregateError`).
- `Ensure` also null-guards the `error` / `errorFactory` argument.

## Cancellation

The combinators are synchronous plumbing and take no `CancellationToken`. If a delegate
internally observes cancellation and throws `OperationCanceledException`, that exception
propagates unchanged — the package never converts cancellation into a failed result.

## Security considerations

Nothing in this feature performs I/O, serialization, or reflection. The relevant caution is in
`MapError` and `Ensure` error factories: errors you construct there travel toward the edge, so
follow the [error-model.md](error-model.md) rules — no secrets or PII in messages or metadata.
`MapError` at layer boundaries is in fact a security tool: translate internal, detail-rich errors
into client-safe ones before they leave your service layer.

## Performance considerations

- Success-path composition allocates nothing beyond what your own delegates allocate; results are
  readonly structs, and failures short-circuit without invoking delegates.
- Failures allocate only the `Error` at the point of creation; propagation reuses the instance.
- Delegates are invoked at most once per combinator; lambdas that capture locals allocate a
  closure, as in any C# code — use static lambdas or method groups where that matters.

## Thread safety

All combinators are pure static methods over immutable values; there is no shared state.
Concurrent pipelines over the same source result are safe. Your delegates must be as thread-safe
as their own side effects require.

## Testing applications using this feature

Test pipelines end-to-end as values, and short-circuit behavior with invocation counters:

```csharp
using Koras.Results;
using Xunit;

public class CompositionTests
{
    [Fact]
    public void Pipeline_Success_TransformsValue()
    {
        var result = Result.Success(2)
            .Map(x => x * 10)
            .Ensure(x => x > 0, Error.Failure("Num.NotPositive", "Must be positive."))
            .Bind(x => Result.Success(x + 1));

        Assert.True(result.IsSuccess);
        Assert.Equal(21, result.Value);
    }

    [Fact]
    public void Pipeline_Failure_ShortCircuits_WithoutInvokingDelegates()
    {
        var error = Error.NotFound("X.Missing", "missing");
        var invoked = 0;

        var result = Result.Failure<int>(error)
            .Map(x => { invoked++; return x * 10; })
            .Bind(x => { invoked++; return Result.Success(x); });

        Assert.True(result.IsFailure);
        Assert.Equal(0, invoked);
        Assert.Same(error, result.Error); // identity, not a copy
    }

    [Fact]
    public void Ensure_FailingPredicate_ProducesTheGivenError()
    {
        var result = Result.Success(-5)
            .Ensure(x => x > 0, x => Error.Validation("Num.NotPositive", $"{x} is not positive."));

        Assert.True(result.IsFailure);
        Assert.Equal("Num.NotPositive", result.Error.Code);
    }
}
```

## Complete example

```csharp
using Koras.Results;

public sealed record Draft(string Title, string Body);
public sealed record Article(Guid Id, string Title, string Slug);

public static class Publishing
{
    public static Result<Article> Publish(Draft draft) =>
        Result.Success(draft)
            .Ensure(d => !string.IsNullOrWhiteSpace(d.Title),
                    Error.Validation("Draft.TitleRequired", "A title is required."))
            .Ensure(d => d.Body.Length >= 100,
                    d => Error.Validation("Draft.BodyTooShort", $"Body has {d.Body.Length} chars; 100 required."))
            .Map(d => new Article(Guid.NewGuid(), d.Title, Slugify(d.Title)))
            .Bind(CheckSlugAvailable)
            .Tap(a => Console.WriteLine($"Published '{a.Title}' as /{a.Slug}"))
            .MapError(e => e.WithMetadata("stage", "publishing"));

    private static Result<Article> CheckSlugAvailable(Article article) =>
        article.Slug == "reserved"
            ? Result.Failure<Article>(Error.Conflict("Article.SlugTaken", "That slug is already in use."))
            : Result.Success(article);

    private static string Slugify(string title) =>
        title.Trim().ToLowerInvariant().Replace(' ', '-');
}

public static class Program
{
    public static void Main()
    {
        var draft = new Draft("Hello World", new string('x', 120));

        var exitCode = Publishing.Publish(draft).Match(
            onSuccess: article => 0,
            onFailure: error =>
            {
                Console.Error.WriteLine(error);
                return 1;
            });

        Environment.Exit(exitCode);
    }
}
```

## Common mistakes

1. **Catching exceptions inside `Map`/`Bind` delegates.** Wrapping delegate bodies in try/catch
   and hand-building failures re-implements `Result.Try`, badly. Bridge throwing code with
   `Result.Try(...)` / `Result.TryAsync(...)` and keep combinator delegates exception-free.
2. **Using `Map` where `Bind` is needed.** `Map` with a result-returning delegate yields the
   awkward `Result<Result<T>>`. If the step returns a `Result`, use `Bind`.
3. **Doing side effects in `Map`.** `Map` is for transformation; hiding I/O or logging in it makes
   pipelines hard to reason about. Use `Tap`/`TapError` for effects — they pass the result through
   unchanged.
4. **Expecting `Bind` to aggregate failures.** Chains stop at the *first* failure. When
   independent checks should all report, evaluate them separately and `Result.Combine` them.
5. **Handling only one branch.** `result.Match(onSuccess, onFailure)` forces both; ad-hoc
   `if (result.IsSuccess)` blocks without an else quietly ignore failures.

## Troubleshooting

- **`ArgumentNullException` naming a delegate parameter (`map`, `bind`, `predicate` …)** — a null
  delegate was passed; guards fire eagerly even when the result is a failure.
- **An exception aborts the pipeline** — one of your delegates threw. That is contract behavior;
  move the throwing call behind `Result.Try`.
- **`Tap` did not run** — `Tap` runs only on success (and `TapError` only on failure). Check which
  branch the result was actually on.
- **The error at the edge is not the one you created** — some `MapError` upstream translated it;
  search the chain for `MapError` calls. Errors otherwise propagate by identity.

## Related features

- [result-types.md](result-types.md) — the structs these combinators operate on.
- [async-composition.md](async-composition.md) — the same combinators over `Task<Result<T>>`.
- [exception-conversion.md](exception-conversion.md) — `Result.Try` for delegates that throw.
- [result-combination.md](result-combination.md) — aggregating independent results.
- [error-model.md](error-model.md) — crafting errors for `Ensure` and `MapError`.
