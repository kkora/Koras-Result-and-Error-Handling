# Result Combination (`Result.Combine`)

Feature ID: KR-007 · Package: `Koras.Results` (Core)

## Overview

`Result.Combine` aggregates *independent* results so that every failure is reported, not just the
first. Where a `Bind` chain expresses "step B needs step A's output" and stops at the first
failure, `Combine` expresses "these checks do not depend on each other — run them all and tell me
everything that went wrong."

The aggregation rule is fixed and predictable:

- **0 failures** → success.
- **1 failure** → that exact error passes through unchanged.
- **≥ 2 failures** → if *all* errors are `ValidationError`s, their field errors merge into one
  `ValidationError`; otherwise an `AggregateError` is produced, carrying all child errors, with
  its `Type` set to the highest severity among them
  (`Unexpected > Unavailable > Forbidden > Unauthorized > Conflict > NotFound > Failure > Validation`),
  so downstream projections never under-report severity.

Generic overloads combine two to four `Result<T>` values into a single tuple-valued result with
named elements `First`, `Second`, `Third`, `Fourth`.

## When to use it

- Aggregating multiple validation checks so the caller sees every violation at once.
- Gathering the values of several independent lookups before constructing an object that needs
  all of them (tuple overloads).
- Collapsing the outcomes of a batch of operations (e.g. after awaiting several tasks) into one
  result.

## When not to use it

- Dependent steps, where a later operation needs an earlier value — use `Bind`
  ([functional-composition.md](functional-composition.md)); combining dependent steps just runs
  work whose input was never valid.
- Field-level checks within a single validator — build one
  [`ValidationError`](validation-errors.md) with all `FieldError`s directly; `Combine` is for
  combining *results*, not fields.
- More than four typed values — combine the non-generic `Result` projections and re-read values
  from the sources, or restructure into smaller groups.

## Installation

```bash
dotnet add package Koras.Results
```

Result combination is a core feature; no other package is required.

## Basic usage

```csharp
using Koras.Results;

public static class Program
{
    public static void Main()
    {
        // Non-generic: aggregate independent checks
        Result combined = Result.Combine(
            CheckName("Ada"),
            CheckAge(-3),
            CheckCountry(""));

        combined.Switch(
            onSuccess: () => Console.WriteLine("All checks passed."),
            onFailure: error =>
            {
                if (error is ValidationError validation)
                {
                    // All failures were ValidationErrors → merged into one
                    foreach (var field in validation.FieldErrors)
                    {
                        Console.WriteLine($"{field.PropertyName}: {field.Message}");
                    }
                }
            });

        // Generic: combine values into a named tuple
        Result<(string First, int Second)> pair = Result.Combine(
            Result.Success("Ada"),
            Result.Success(36));

        if (pair.IsSuccess)
        {
            Console.WriteLine($"{pair.Value.First} is {pair.Value.Second}");
        }
    }

    private static Result CheckName(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? new ValidationError(new FieldError("Name", "Name is required."))
            : Result.Success();

    private static Result CheckAge(int age) =>
        age < 0
            ? new ValidationError(new FieldError("Age", "Age cannot be negative."))
            : Result.Success();

    private static Result CheckCountry(string country) =>
        string.IsNullOrWhiteSpace(country)
            ? new ValidationError(new FieldError("Country", "Country is required."))
            : Result.Success();
}
```

When failures are heterogeneous, the merge produces an `AggregateError` instead:

```csharp
var combined = Result.Combine(
    Result.Failure(Error.NotFound("User.NotFound", "No such user.")),
    Result.Failure(Error.Unavailable("Db.Down", "Database offline.")));

// combined.Error is AggregateError:
//   Code  == "Errors.Multiple"
//   Type  == ErrorType.Unavailable  (highest severity of the children)
//   Errors contains both child errors, in order
```

## Dependency-injection usage

`Combine` is a static factory over plain values; nothing is registered. It typically aggregates
results coming from several injected services:

```csharp
using Koras.Results;

public sealed class EnrollmentService(
    IStudentDirectory students,
    ICourseCatalog courses,
    ITermCalendar terms)
{
    public Result<Enrollment> Enroll(int studentId, string courseCode, int termId)
    {
        var combined = Result.Combine(
            students.Find(studentId),   // Result<Student>
            courses.Find(courseCode),   // Result<Course>
            terms.Find(termId));        // Result<Term>

        return combined.Map(t => new Enrollment(t.First.Id, t.Second.Code, t.Third.Id));
    }
}

public interface IStudentDirectory { Result<Student> Find(int id); }
public interface ICourseCatalog    { Result<Course> Find(string code); }
public interface ITermCalendar     { Result<Term> Find(int id); }

public sealed record Student(int Id);
public sealed record Course(string Code);
public sealed record Term(int Id);
public sealed record Enrollment(int StudentId, string CourseCode, int TermId);
```

If any lookup fails, `Enroll` reports *all* the failures in one error.

## Advanced configuration

There is none. The aggregation rule (single failure passes through; all-validation merges;
otherwise `AggregateError` with highest-severity type) is a fixed contract, not a policy hook.
If you need different aggregation semantics, collect the errors yourself and construct the
combined error explicitly.

## Public API

Static factories on `Result`:

- `Result.Combine(params Result[] results)` — aggregates any number of void results.
- `Result.Combine(IEnumerable<Result> results)` — same, over an enumerable.
- `Result.Combine<T1, T2>(Result<T1> r1, Result<T2> r2)` — returns
  `Result<(T1 First, T2 Second)>`.
- `Result.Combine<T1, T2, T3>(Result<T1> r1, Result<T2> r2, Result<T3> r3)` — returns
  `Result<(T1 First, T2 Second, T3 Third)>`.
- `Result.Combine<T1, T2, T3, T4>(Result<T1> r1, Result<T2> r2, Result<T3> r3, Result<T4> r4)` —
  returns `Result<(T1 First, T2 Second, T3 Third, T4 Fourth)>`.

Supporting type:

- `AggregateError` (sealed class, `: Error`) — code `"Errors.Multiple"` (exposed as
  `AggregateError.DefaultCode`), message `"Multiple errors occurred."`;
  `Errors` is an `IReadOnlyList<Error>` of at least two children, flattened (nested aggregates
  are flattened into their children) and in supplied order; `Type` is the highest severity among
  the children. Constructor: `AggregateError(IEnumerable<Error> errors)`.

## Error handling

- A null `results` array/enumerable throws `ArgumentNullException`.
- Constructing an `AggregateError` directly with null entries or fewer than two errors (after
  flattening) throws `ArgumentException`; `Combine` itself never does this — it only builds an
  aggregate when it has two or more failures.
- Any `default(Result)` in the input counts as a failure carrying `Error.Uninitialized` and will
  appear among the aggregated errors — a useful tripwire for uninitialized values.
- Consumers unpack multi-failures by pattern matching:
  `error is AggregateError agg` → iterate `agg.Errors`; `error is ValidationError v` → iterate
  `v.FieldErrors`.

## Cancellation

`Combine` is synchronous pure data aggregation and takes no `CancellationToken`; there is nothing
to cancel. When combining the outcomes of asynchronous work, cancellation happens in the tasks
*before* combination — and per the package rule, a cancelled task surfaces as
`OperationCanceledException`, never as a result passed into `Combine`.

## Security considerations

An `AggregateError` carries its child errors verbatim, so everything in them (messages, metadata)
travels together toward logs and — under the ASP.NET Core projection rules — potentially clients.
The usual rule compounds here: no secrets or PII in any child error. The severity-precedence rule
is itself a safety property: a combined error containing one `Unexpected` child is typed
`Unexpected`, so HTTP projections apply the strictest (most suppressed) handling to the whole
aggregate.

## Performance considerations

- The all-success path allocates almost nothing: the input results are structs, and no error list
  is created until the first failure is seen.
- One failure passes through by identity — no aggregate allocation.
- Multiple failures allocate the merged `ValidationError` or `AggregateError` (failure path only).
- Tuple overloads return values inside the `Result<T>` struct; no boxing of the tuple.

## Thread safety

`Combine` is a pure static function over immutable inputs; `AggregateError` and merged
`ValidationError`s are immutable like all errors. Combining results produced concurrently by
different threads is safe once you have the values in hand.

## Testing applications using this feature

```csharp
using Koras.Results;
using Xunit;

public class CombineTests
{
    [Fact]
    public void AllSuccess_ReturnsSuccess()
    {
        var combined = Result.Combine(Result.Success(), Result.Success());

        Assert.True(combined.IsSuccess);
    }

    [Fact]
    public void SingleFailure_PassesThroughByIdentity()
    {
        var error = Error.NotFound("X.Missing", "missing");

        var combined = Result.Combine(Result.Success(), Result.Failure(error));

        Assert.True(combined.IsFailure);
        Assert.Same(error, combined.Error);
    }

    [Fact]
    public void MultipleValidationFailures_MergeIntoOneValidationError()
    {
        var combined = Result.Combine(
            Result.Failure(new ValidationError(new FieldError("Name", "Required."))),
            Result.Failure(new ValidationError(new FieldError("Age", "Invalid."))));

        var validation = Assert.IsType<ValidationError>(combined.Error);
        Assert.Equal(2, validation.FieldErrors.Count);
    }

    [Fact]
    public void MixedFailures_ProduceAggregateWithHighestSeverity()
    {
        var combined = Result.Combine(
            Result.Failure(Error.Validation("A.Invalid", "invalid")),
            Result.Failure(Error.Unavailable("B.Down", "down")));

        var aggregate = Assert.IsType<AggregateError>(combined.Error);
        Assert.Equal(ErrorType.Unavailable, aggregate.Type);
        Assert.Equal(2, aggregate.Errors.Count);
    }

    [Fact]
    public void TupleCombine_ExposesNamedElements()
    {
        var combined = Result.Combine(Result.Success("Ada"), Result.Success(36));

        Assert.True(combined.IsSuccess);
        Assert.Equal("Ada", combined.Value.First);
        Assert.Equal(36, combined.Value.Second);
    }
}
```

## Complete example

```csharp
using Koras.Results;

public sealed record Trip(string Flight, string Hotel, string Car);

public sealed class TripBooking
{
    public Result<Trip> Book(string origin, string destination)
    {
        var flight = ReserveFlight(origin, destination);
        var hotel = ReserveHotel(destination);
        var car = ReserveCar(destination);

        return Result.Combine(flight, hotel, car)
            .Map(t => new Trip(t.First, t.Second, t.Third))
            .TapError(e =>
            {
                if (e is AggregateError aggregate)
                {
                    Console.Error.WriteLine($"{aggregate.Errors.Count} bookings failed (severity {aggregate.Type}):");
                    foreach (var child in aggregate.Errors)
                    {
                        Console.Error.WriteLine($"  - {child.Code}: {child.Message}");
                    }
                }
                else
                {
                    Console.Error.WriteLine($"Booking failed: {e.Code}");
                }
            });
    }

    private static Result<string> ReserveFlight(string origin, string destination) =>
        origin == destination
            ? Result.Failure<string>(Error.Validation("Flight.SameCity", "Origin and destination are identical."))
            : Result.Success($"FL-{origin}-{destination}");

    private static Result<string> ReserveHotel(string city) =>
        city == "Nowhere"
            ? Result.Failure<string>(Error.NotFound("Hotel.NoneAvailable", $"No hotels in {city}."))
            : Result.Success($"HT-{city}");

    private static Result<string> ReserveCar(string city) =>
        Result.Success($"CAR-{city}");
}

public static class Program
{
    public static void Main()
    {
        var booking = new TripBooking();

        var outcome = booking.Book("Oslo", "Nowhere").Match(
            onSuccess: trip => $"Booked: {trip.Flight}, {trip.Hotel}, {trip.Car}",
            onFailure: error => $"Not booked ({error.Code}).");

        Console.WriteLine(outcome);
    }
}
```

## Common mistakes

1. **Combining dependent steps.** If step B needs A's value, `Combine` runs B against garbage or
   forces you to pre-compute it anyway. Dependent flow is `Bind`; `Combine` is for independent
   results.
2. **Expecting an `AggregateError` for a single failure.** One failure passes through as-is; only
   two or more failures aggregate. Handle both shapes (`error is AggregateError` and plain
   errors) at the consumption site.
3. **Ignoring the tuple element names.** The 2–4-arity overloads return
   `(First, Second, Third, Fourth)` — use the names (`t.First`) rather than `Item1`, which
   survives refactoring poorly.
4. **Reconstructing merged validation errors manually.** When all failures are
   `ValidationError`s, `Combine` already merges their `FieldErrors` in order into one
   `ValidationError`; do not unwrap and rebuild.
5. **Assuming input order changes the outcome type.** Order affects the order of aggregated
   errors, not the classification: severity precedence picks the aggregate's `Type` regardless of
   position.

## Troubleshooting

- **`combined.Error` is not an `AggregateError`** — either only one input failed (pass-through)
  or every failure was a `ValidationError` (merged into one `ValidationError`). Both are contract
  behavior.
- **Aggregate's `Type` is "worse" than any error you expected** — one child ranks higher on the
  severity scale (check for `Unexpected` from a leaked `default(Result)` or a `Result.Try`
  default mapping).
- **A child error with code `Result.Uninitialized` appears** — a `default(Result)` was in the
  input collection; find the uninitialized element (common with pre-sized arrays).
- **`ArgumentException` from `new AggregateError(...)`** — you constructed one directly with
  fewer than two errors or a null entry; let `Combine` build aggregates for you.

## Related features

- [result-types.md](result-types.md) — the results being combined.
- [error-model.md](error-model.md) — severity precedence and the error taxonomy.
- [validation-errors.md](validation-errors.md) — the merge target when all failures are validation.
- [functional-composition.md](functional-composition.md) — `Bind` for dependent steps; `Map` over combined tuples.
- [serialization.md](serialization.md) — how `AggregateError` serializes (`errors` array).
