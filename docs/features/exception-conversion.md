# Exception Conversion (`Result.Try`, `Result.TryAsync`)

Feature ID: KR-006 · Package: `Koras.Results` (Core)

## Overview

`Result.Try` and `Result.TryAsync` are the bridge between exception-based code and the Result
world. Third-party libraries and the BCL throw; your result-based pipelines expect failures as
values. `Try` executes an operation, returns a success on completion, and converts any thrown
exception into a failure — with two hard guarantees:

- **Cancellation is never converted.** `OperationCanceledException` is always rethrown; a
  cancelled operation is not a failed operation.
- **The default mapping is leak-safe.** When no custom mapper is supplied, the failure carries
  `Error.Unexpected("Unexpected.Exception", "An unexpected error occurred.")` with the exception's
  full type name in `metadata["exceptionType"]` — and deliberately *not* the exception message,
  which may contain sensitive details.

An optional `Func<Exception, Error>` mapper lets boundaries classify known exceptions into
precise, semantic errors.

## When to use it

- Wrapping BCL or third-party calls that throw (parsing, file I/O, HTTP clients, database
  drivers) at the edge of your result-based code.
- Adapter/infrastructure layers translating known exception types into semantic errors
  (`IOException` → `Unavailable`, unique-key violations → `Conflict`).
- Any place you are tempted to write try/catch inside a `Map`/`Bind` delegate — hoist that
  boundary into a `Try` call instead.

## When not to use it

- Around code that already returns `Result` — there is nothing to convert.
- To swallow programming bugs in your own code. A `NullReferenceException` from your own logic
  should crash loudly in development, not become an `Unexpected` failure; fix the bug.
- For flow you can check upfront cheaply (`int.TryParse`, `Dictionary.TryGetValue`) — prefer the
  non-throwing API and build the result directly.
- For cancellation handling. `Try` deliberately refuses to convert `OperationCanceledException`.

## Installation

```bash
dotnet add package Koras.Results
```

Exception conversion is a core feature; no other package is required.

## Basic usage

```csharp
using System.Text.Json;
using Koras.Results;

public static class Program
{
    public static void Main()
    {
        // Default mapping: Unexpected error, exception type in metadata, message withheld
        Result<Config> parsed = Result.Try(() => JsonSerializer.Deserialize<Config>("{ not json"))!;

        parsed.Switch(
            onSuccess: c => Console.WriteLine($"Loaded {c.Name}"),
            onFailure: e =>
            {
                Console.WriteLine(e.Code);                        // "Unexpected.Exception"
                Console.WriteLine(e.Metadata["exceptionType"]);   // "System.Text.Json.JsonException"
            });

        // Custom mapping: classify known exceptions into semantic errors
        Result<string> content = Result.Try(
            () => File.ReadAllText("settings.json"),
            mapError: ex => ex switch
            {
                FileNotFoundException => Error.NotFound("Config.FileMissing", "The settings file was not found."),
                UnauthorizedAccessException => Error.Forbidden("Config.AccessDenied", "Access to the settings file was denied."),
                IOException => Error.Unavailable("Config.IoFailure", "The settings file could not be read."),
                _ => Error.Unexpected("Config.ReadFailed", "Reading settings failed unexpectedly."),
            });

        Console.WriteLine(content.IsSuccess ? "loaded" : content.Error.Code);
    }
}

public sealed record Config(string Name);
```

The async variants mirror the sync ones:

```csharp
Result<string> body = await Result.TryAsync(
    () => httpClient.GetStringAsync("https://api.example.com/health"),
    mapError: ex => Error.Unavailable("Health.Unreachable", "The health endpoint is unreachable."));
```

## Dependency-injection usage

`Try` is a static factory over plain values — nothing to register. Its natural home is inside
injected infrastructure services, converting driver exceptions before results cross into the
application layer:

```csharp
using Koras.Results;

public interface IDocumentStore
{
    Task<Result<string>> LoadAsync(string key, CancellationToken ct);
}

public sealed class BlobDocumentStore(BlobClientFactory blobs) : IDocumentStore
{
    public Task<Result<string>> LoadAsync(string key, CancellationToken ct) =>
        Result.TryAsync(
            () => blobs.For(key).DownloadTextAsync(ct),
            mapError: ex => ex switch
            {
                KeyNotFoundException => Error.NotFound("Document.NotFound", $"No document '{key}'."),
                TimeoutException => Error.Unavailable("Document.StoreTimeout", "The document store timed out."),
                _ => Error.Unexpected("Document.LoadFailed", "Loading the document failed."),
            });
}
```

Callers of `IDocumentStore` now compose over `Result<string>` and never see storage exceptions.

## Advanced configuration

The single extension point is the `mapError` parameter. There is no global exception-to-error
mapping registry — by design: mappings are boundary-specific, and a shared reusable mapper is
just a named `Func<Exception, Error>` you pass where needed:

```csharp
public static class ExceptionMappers
{
    public static readonly Func<Exception, Error> Database = ex => ex switch
    {
        TimeoutException => Error.Unavailable("Db.Timeout", "The database timed out."),
        _ => Error.Unexpected("Db.Failure", "A database operation failed."),
    };
}

var result = Result.Try(() => connection.ExecuteScalar(), ExceptionMappers.Database);
```

## Public API

All members are static factories on `Result`:

- `Result.Try(Action action, Func<Exception, Error>? mapError = null)` — runs a void operation;
  returns `Result`.
- `Result.Try<T>(Func<T> func, Func<Exception, Error>? mapError = null)` — runs a value-producing
  operation; returns `Result<T>`.
- `Result.TryAsync(Func<Task> action, Func<Exception, Error>? mapError = null)` — awaits a void
  async operation; returns `Task<Result>`. `OperationCanceledException` always rethrown.
- `Result.TryAsync<T>(Func<Task<T>> func, Func<Exception, Error>? mapError = null)` — awaits a
  value-producing async operation; returns `Task<Result<T>>`.

Default mapper (when `mapError` is omitted):
`Error.Unexpected("Unexpected.Exception", "An unexpected error occurred.")` with
`metadata["exceptionType"]` set to the exception's full type name. The exception message is
deliberately excluded (leak-safe default).

## Error handling

- A null `action`/`func` throws `ArgumentNullException` — for the async variants, eagerly, before
  any task is created.
- `OperationCanceledException` (including `TaskCanceledException`) is rethrown, never converted.
- Every other exception becomes a failure via the mapper (or the default). The mapper receives
  the original exception and full discretion over the resulting `Error`.
- Note that `Result.Try<T>` returns whatever `func` returns through `Result.Success<T>(...)` — if
  `func` returns null, the success-null guard throws `ArgumentNullException`, which `Try` then
  converts like any other exception. Prefer funcs with non-null contracts.
- An exception thrown by your *mapper* itself is not caught — keep mappers trivial.

## Cancellation

This feature is where the package's cancellation rule is enforced: **cancellation is never
converted to a failure.** All four methods rethrow `OperationCanceledException` so that
cooperative cancellation composes correctly — `Task.WhenAny`, ASP.NET Core request aborts, and
`CancellationToken` semantics all rely on the exception reaching the caller. Pass tokens into the
work you run:

```csharp
var result = await Result.TryAsync(() => client.GetStringAsync(url, ct));
// If ct fires, OperationCanceledException propagates out of this await — result is never produced.
```

## Security considerations

- The default mapping is secure by default: the failure exposes only a generic message and the
  exception *type* name; the exception message and stack trace — which frequently contain paths,
  connection strings, or user data — are never copied into the error.
- When writing a custom `mapError`, you own this decision. Do not blindly copy
  `ex.Message` into `Error.Message` or metadata for errors that can reach clients or logs with
  broad access.
- The ASP.NET Core package additionally suppresses `Unexpected` error details in HTTP responses
  by default; the two layers of defense are independent.

## Performance considerations

- The success path adds only a try/catch frame around your operation — no allocations beyond the
  usual allocation-free `Result`/`Result<T>` structs.
- A caught exception is already the expensive event; the additional `Error` allocation (plus the
  one-entry metadata dictionary in the default mapping) is negligible next to the throw itself.
- Do not use `Try` as routine control flow around operations that fail frequently; exceptions are
  slow. Check first when a non-throwing API exists.

## Thread safety

The `Try` methods are pure static functions with no shared state, safe to call concurrently.
The results and errors they produce are immutable. Your operation and mapper delegates must be
as thread-safe as their own captured state requires.

## Testing applications using this feature

```csharp
using Koras.Results;
using Xunit;

public class ExceptionConversionTests
{
    [Fact]
    public void Try_Success_ReturnsValue()
    {
        var result = Result.Try(() => int.Parse("42"));

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Try_Throw_DefaultMapping_HidesMessageButKeepsType()
    {
        var result = Result.Try<int>(() => throw new FormatException("sensitive detail"));

        Assert.True(result.IsFailure);
        Assert.Equal("Unexpected.Exception", result.Error.Code);
        Assert.Equal(ErrorType.Unexpected, result.Error.Type);
        Assert.Equal("System.FormatException", result.Error.Metadata["exceptionType"]);
        Assert.DoesNotContain("sensitive detail", result.Error.Message);
    }

    [Fact]
    public void Try_CustomMapper_ReceivesOriginalException()
    {
        var result = Result.Try<int>(
            () => throw new TimeoutException(),
            mapError: ex => Error.Unavailable("Op.Timeout", $"Timed out ({ex.GetType().Name})."));

        Assert.Equal("Op.Timeout", result.Error.Code);
        Assert.Equal(ErrorType.Unavailable, result.Error.Type);
    }

    [Fact]
    public async Task TryAsync_Cancellation_IsRethrown_NotConverted()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Result.TryAsync(() => Task.Delay(1000, cts.Token)));
    }
}
```

## Complete example

```csharp
using System.Text.Json;
using Koras.Results;

public sealed record Settings(string Endpoint, int TimeoutSeconds);

public static class SettingsLoader
{
    public static Result<Settings> Load(string path) =>
        Result.Try(
                () => File.ReadAllText(path),
                mapError: ex => ex switch
                {
                    FileNotFoundException or DirectoryNotFoundException =>
                        Error.NotFound("Settings.FileMissing", "The settings file was not found."),
                    UnauthorizedAccessException =>
                        Error.Forbidden("Settings.AccessDenied", "Access to the settings file was denied."),
                    IOException =>
                        Error.Unavailable("Settings.IoFailure", "The settings file could not be read."),
                    _ => Error.Unexpected("Settings.ReadFailed", "Reading settings failed unexpectedly."),
                })
            .Bind(json => Result.Try(
                () => JsonSerializer.Deserialize<Settings>(json)
                      ?? throw new JsonException("Settings deserialized to null."),
                mapError: _ => Error.Validation("Settings.Malformed", "The settings file is not valid JSON.")))
            .Ensure(s => s.TimeoutSeconds > 0,
                    Error.Validation("Settings.InvalidTimeout", "TimeoutSeconds must be positive."));
}

public static class Program
{
    public static void Main(string[] args)
    {
        var path = args.Length > 0 ? args[0] : "settings.json";

        var exitCode = SettingsLoader.Load(path).Match(
            onSuccess: s =>
            {
                Console.WriteLine($"Endpoint: {s.Endpoint}, timeout: {s.TimeoutSeconds}s");
                return 0;
            },
            onFailure: e =>
            {
                Console.Error.WriteLine($"[{e.Type}] {e.Code}: {e.Message}");
                return 1;
            });

        Environment.Exit(exitCode);
    }
}
```

## Common mistakes

1. **Catching exceptions in `Map`/`Bind` delegates instead of using `Try`.** Hand-rolled
   try/catch inside combinator lambdas duplicates `Try` without its guarantees (cancellation
   rethrow, leak-safe default). Put the boundary in a `Try`/`TryAsync` call.
2. **Catching `OperationCanceledException` in a custom mapper's caller and converting it.** The
   library rethrows it before your mapper ever runs; do not undo that with an outer catch that
   turns cancellation into a failure.
3. **Copying `ex.Message` into client-facing errors.** Exception messages routinely leak paths,
   server names, and query text. Classify by exception *type* and write your own safe message.
4. **Wrapping everything in `Try` "just in case".** Blanket wrapping converts genuine bugs into
   quiet `Unexpected` failures. Use `Try` at real exception boundaries only, and let bugs throw.
5. **Forgetting `await` on `TryAsync`.** `Result.TryAsync(...)` returns a `Task<Result>`; using
   it where a `Result` is expected fails to compile only sometimes (e.g. `var` chains). Awaiting
   is mandatory before inspecting the outcome.

## Troubleshooting

- **A failure with code `Unexpected.Exception` and no useful detail** — the default mapper fired.
  Check `metadata["exceptionType"]` to identify the exception, then add a custom `mapError` at
  that boundary to classify it properly.
- **`OperationCanceledException` escapes `Try`** — by design; handle it where you handle
  cancellation. If you genuinely see it during normal (non-cancelled) operation, some library is
  throwing it incorrectly.
- **`ArgumentNullException: value` from `Try<T>`** — your `func` returned null and the
  success-null guard fired inside the try block, so it surfaced as a converted failure or (with a
  custom mapper) whatever the mapper produced. Ensure the func returns non-null, or model absence
  explicitly.
- **Mapper exception crashes the call** — mapper exceptions are not caught. Keep mappers to a
  simple `switch` over exception types.

## Related features

- [result-types.md](result-types.md) — the results `Try` produces.
- [error-model.md](error-model.md) — choosing the right `ErrorType` in custom mappers.
- [functional-composition.md](functional-composition.md) — pipelines that stay exception-free.
- [async-composition.md](async-composition.md) — chaining onto `Task<Result<T>>` from `TryAsync`.
- [serialization.md](serialization.md) — how the `exceptionType` metadata round-trips.
