# Performance Guide — Koras.Results

The library's performance model in one sentence: **success costs nothing, failure costs one
object, and composition costs delegate calls — so keep delegates cheap and allocation-free.**
Verified numbers are in [benchmarks.md](benchmarks.md); memory details in
[memory-management.md](memory-management.md).

## The allocation model

### Success paths are allocation-free

`Result` and `Result<T>` are `readonly struct`s (ADR-0003). A success is encoded as the struct's
own fields — a `bool`, the value, and a `null` error reference — so `Result.Success(42)` performs
**zero heap allocations** and benchmarks indistinguishably from returning the raw value
(sub-nanosecond, 0 B). There is no boxing: the structs implement no interfaces that would invite
it in normal use, and the combinators are generic over the concrete struct.

`default(Result)` is also allocation-free: the uninitialized-failure behavior reuses the static
`Error.Uninitialized` sentinel rather than constructing anything.

### Failure allocates the Error — once

Creating a failure allocates only the `Error` object (a class, by design — ADR-0003: failures
carry rich data and are comparatively rare). Everything downstream reuses it:

- `Result.Failure(error)` stores the reference; no copies.
- Combinators short-circuit **by identity**: `Map`/`Bind`/`Ensure`/`Tap` on a failure return a
  new struct carrying the *same* `Error` reference without invoking your delegate (pinned by
  tests using `Assert.Same` and invocation counters). A four-step failed chain allocates nothing
  beyond the original error.
- Error objects are immutable and freely shareable: declare recurring errors as
  `static readonly` fields (an error-catalog class) and failure creation becomes allocation-free
  too:

```csharp
public static class OrderErrors
{
    public static readonly Error NotFound = Error.NotFound("Order.NotFound", "The order does not exist.");
}
// later — zero allocations:
return Result.Failure<Order>(OrderErrors.NotFound);
```

Note `Error.Metadata` starts as a shared empty dictionary; metadata costs appear only when you
add entries (see [memory-management.md](memory-management.md)).

## Delegates and closures on hot paths

The combinators (`Map`, `Bind`, `Ensure`, …) take delegates. Delegates themselves are cheap to
*invoke*; what costs is **allocating** them per call, which happens whenever a lambda captures
local state (a closure) — every execution allocates a closure object plus a delegate.

- **Prefer static lambdas** on hot paths. `static v => v * 2` cannot capture, the compiler caches
  the delegate instance, and the `static` keyword makes accidental capture a compile error:

  ```csharp
  result.Map(static v => v * 2)                       // cached delegate, 0 B per call
        .Ensure(static v => v > 0, OrderErrors.Invalid);
  ```

- **Non-capturing (but not marked static) lambdas** are also cached by the compiler — `static` is
  the guard rail, not the mechanism.
- **Capturing lambdas allocate per call.** `result.Map(v => v * factor)` allocates a closure for
  `factor` each time. Acceptable in request-scoped code; avoid in per-item loops. Alternatives:
  fold the captured value into the pipeline (carry a tuple through `Map`), use a method group on
  a type that already holds the state, or accept the allocation consciously.
- **`Ensure` error arguments**: prefer the `Error` overload with a static-readonly error over the
  `Func<T, Error>` factory when the error doesn't need the value — the factory overload's whole
  point is lazy, value-dependent construction; on the success path neither allocates an error,
  but a capturing factory lambda still allocates its closure.
- Delegate *exceptions* are not caught by the combinators — there is no hidden try/catch cost on
  these paths.

## When the async overloads allocate

The sync combinators over `Result<T>` are allocation-free. The async surface
(`MapAsync`, `BindAsync`, `MatchAsync`, …) rides on `Task`, and `Task` machinery allocates:

- Each `async` combinator invocation involves the state machine + `Task<Result<T>>` boxes; the
  benchmarked three-step async pipeline (`Task.FromResult → MapAsync → MatchAsync`) allocates
  ~304 B. That is Task plumbing, not result plumbing.
- The public surface deliberately uses `Task`, not `ValueTask` (ADR-0003 notes): simpler for
  consumers, and at library level the difference is dwarfed by the awaited I/O these pipelines
  exist to wrap. If a code path is hot enough that Task allocations matter *and* it is not doing
  I/O, use the synchronous combinators — mixing is trivial since sync combinators compose with
  `Task<Result<T>>` receivers via the `*Async` bridge overloads.
- All implementations use `ConfigureAwait(false)`; there is no context-capture overhead inside
  the library.

Rule of thumb: **use async combinators only around genuinely asynchronous work.** Wrapping
synchronous logic in `MapAsync(v => Task.FromResult(...))` buys allocations for nothing.

## Combine's params-array allocation

`Result.Combine(params Result[] results)` allocates the params array at the call site (~88 B
measured for four results, including aggregation bookkeeping) — this is C# params semantics, not
avoidable inside the method. Guidance:

- On hot paths combining a *fixed small number* of typed results, prefer the tuple overloads
  (`Combine(r1, r2)` … up to four) or sequential `Bind`, which avoid the array.
- The `IEnumerable<Result>` overload avoids the params array only if you already have a
  collection; materializing one just to call it is strictly worse.
- On failure, `Combine` additionally allocates the merged `ValidationError`/`AggregateError` —
  proportional to the failure count, and inherent to its contract.

## Projection-layer costs (AspNetCore)

`ToHttpResult`/`ToActionResult` on success defer to ASP.NET Core's own result types (whose cost
you pay with or without this library). On failure, building ProblemDetails allocates the response
object, extensions dictionary, and (for validation errors) the grouped `errors` dictionary —
once per failed request, at the edge, where an error response is being built anyway. Status-code
lookup uses a `FrozenDictionary` and per-options dictionaries; no reflection, no LINQ on the
success path.

## Failure vs exceptions

The benchmarked gap (~2.4 ns vs ~2,506 ns, 0 B vs 320 B; see
[benchmarks.md](benchmarks.md)) is the reason expected failures should be values. The gap comes
from stack-trace capture and unwinding, which exceptions pay and results don't. Keep exceptions
for the genuinely exceptional; convert at boundaries with `Result.Try` (whose success path adds
only a try/catch scope, costing nothing when nothing throws).
