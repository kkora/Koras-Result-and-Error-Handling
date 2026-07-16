# OpenTelemetry integration

## Overview

`Koras.Results.OpenTelemetry` annotates `System.Diagnostics.Activity` spans with failure information from `Result` / `Result<T>` values, following OpenTelemetry semantic conventions. On failure it sets the activity status to `ActivityStatusCode.Error` (with the error code as the description) and writes `error.type` (snake_case taxonomy value), `koras.error.code`, and — for aggregates — `koras.error.aggregate_count` tags. Successes, absent activities, and non-recording activities are allocation-free no-ops.

Two design guarantees: the package **never creates activities** (it only annotates the caller's), and it has **zero NuGet dependencies** beyond the core package — `Activity` ships in-box, so it composes with any OpenTelemetry SDK setup (or none).

## When to use it

- You emit distributed traces (OpenTelemetry SDK, Application Insights, any `ActivityListener`-based backend) and want failed results visible as errored spans with queryable error codes.
- Background workers and services where failures are values, not exceptions, and would otherwise leave spans looking "green".
- Dashboards/alerts keyed on stable error codes (`koras.error.code`) or taxonomy classes (`error.type`).

## When not to use it

- No tracing is configured anywhere — the calls are harmless no-ops, but they add noise to the code.
- You need metrics or logs rather than span attributes — this package only tags activities.
- You want spans *created* per operation — start activities yourself with an `ActivitySource`; this package will only tag them.

## Installation

```bash
dotnet add package Koras.Results.OpenTelemetry
```

The core `Koras.Results` package comes transitively. There is deliberately no dependency on the OpenTelemetry SDK — `System.Diagnostics.Activity` is part of the base class library.

## Basic configuration

None — there are no options and no DI registrations. Tracing itself is configured by *your* OpenTelemetry setup (exporters, samplers, `ActivitySource` registration); this package works with whatever is active.

## Basic usage

```csharp
using System.Diagnostics;
using Koras.Results;
using Koras.Results.OpenTelemetry;

using var activity = MyActivities.StartActivity("place-order");

Result<Order> result = orderService.Place(command)
    .TagCurrentActivity(); // failure -> status Error + error.type/koras.error.code tags
```

On failure the activity receives:

| Tag | Value |
|---|---|
| `error.type` | `failure`, `validation`, `not_found`, `conflict`, `unauthorized`, `forbidden`, `unavailable`, or `unexpected` |
| `koras.error.code` | the stable error code, e.g. `Order.NotFound` |
| `koras.error.aggregate_count` | child-error count, only when the error is an `AggregateError` |

and its status becomes `ActivityStatusCode.Error` with the error code as the status description. On success, nothing changes.

## Dependency-injection usage

Not applicable: the package is pure extension methods over values and `Activity` — there is nothing to register; it simply coexists with your DI-configured OpenTelemetry pipeline.

## ASP.NET Core usage

Inside a request, `Activity.Current` is the request (or your custom) span — tag it before converting to HTTP:

```csharp
app.MapGet("/orders/{id:guid}", (Guid id, OrderStore store) =>
    store.Find(id)
        .TagCurrentActivity()
        .ToHttpResult());
```

The ProblemDetails `traceId` extension and the tagged span then share the same trace, closing the loop from client error report to backend span.

## Worker Service usage

From `samples/WorkerService.Sample` — the async combinator form slots into result pipelines:

```csharp
using System.Diagnostics;
using Koras.Results;
using Koras.Results.OpenTelemetry;

public sealed class SyncWorker(FlakyDownstream downstream, ILogger<SyncWorker> logger) : BackgroundService
{
    private static readonly ActivitySource Activities = new("WorkerServiceSample.Sync");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var activity = Activities.StartActivity("sync-batch");

            var result = await FetchBatchAsync(stoppingToken)
                .TapActivityErrorAsync(); // failure -> error.type / koras.error.code tags on the trace

            result.Switch(
                onSuccess: batch => logger.LogInformation("Synced {Count} records", batch.Count),
                onFailure: error => logger.LogWarning("Sync failed ({ErrorCode})", error.Code));

            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private Task<Result<IReadOnlyList<string>>> FetchBatchAsync(CancellationToken cancellationToken) =>
        Result.TryAsync(
            () => downstream.FetchAsync(cancellationToken),
            ex => ex switch
            {
                TimeoutException => Error.Unavailable("Downstream.Timeout", "The downstream system timed out."),
                HttpRequestException => Error.Unavailable("Downstream.Unreachable", "The downstream system is unreachable."),
                _ => Error.Unexpected("Downstream.Unknown", "An unexpected sync error occurred."),
            });
}
```

## Advanced configuration

There are no options. For explicit control over *which* span is tagged (e.g. a parent rather than `Activity.Current`), use the `TagActivity(Activity?)` overloads; tag names are exposed as constants on `KorasResultsActivityTags` so dashboards and custom instrumentation never hardcode strings.

## Public API

```csharp
public static class KorasResultsActivityTags
{
    public const string ErrorType = "error.type";
    public const string ErrorCode = "koras.error.code";
    public const string AggregateCount = "koras.error.aggregate_count";
}

public static class ActivityResultExtensions
{
    public static Result TagCurrentActivity(this Result result);
    public static Result<T> TagCurrentActivity<T>(this Result<T> result);
    public static Result TagActivity(this Result result, Activity? activity);
    public static Result<T> TagActivity<T>(this Result<T> result, Activity? activity);
    public static Task<Result> TapActivityErrorAsync(this Task<Result> resultTask);
    public static Task<Result<T>> TapActivityErrorAsync<T>(this Task<Result<T>> resultTask);
}
```

All methods return the receiver (or its awaited value) unchanged, for chaining.

## Execution lifecycle

1. Success → return the result immediately; nothing is touched.
2. Failure → resolve the target activity (`Activity.Current` for the current/async forms; the argument for `TagActivity`).
3. If the activity is `null` or `IsAllDataRequested` is `false` (the sampler chose not to record), return — a no-op.
4. Otherwise: `SetStatus(ActivityStatusCode.Error, error.Code)`, set `error.type` (snake_cased `ErrorType`), set `koras.error.code`, and, if the error is an `AggregateError`, set `koras.error.aggregate_count` to its child count.

`TapActivityErrorAsync` awaits the task (`ConfigureAwait(false)`) and then applies `TagCurrentActivity` — note that the "current" activity is captured *after* the await, in the continuation's context.

## Error handling

Tagging never throws for absent/non-recording activities and never alters the result — the same failure flows onward untouched. The only exceptions are eager `ArgumentNullException`s when a null `Task` is passed to `TapActivityErrorAsync`. Exceptions are out of scope entirely: convert them to results first with `Result.Try` / `Result.TryAsync`.

## Cancellation

Not applicable: the methods take no `CancellationToken` — tagging is synchronous, in-memory work; cancellation belongs to the operation that produced the result (note that `Result.TryAsync` always rethrows `OperationCanceledException`, so cancelled work is never tagged as a failure).

## Logging

Not applicable: the package writes span attributes only; pair with your own logging (e.g. `Switch`/`TapError`) as the worker sample does.

## Telemetry

This *is* the telemetry surface: `error.type` follows the OpenTelemetry semantic convention of that name with snake_case taxonomy values (`failure`, `validation`, `not_found`, `conflict`, `unauthorized`, `forbidden`, `unavailable`, `unexpected`); `koras.error.code` and `koras.error.aggregate_count` are Koras-namespaced attributes. Span status becomes `Error` with the error code as description, so errored-span queries work out of the box in any backend.

## Security considerations

Only `Error.Code`, the taxonomy value, and an aggregate count are exported — never `Error.Message` or `Error.Metadata`, so free-text or sensitive content cannot leak into traces through this package. Keep error *codes* themselves free of sensitive data (they also appear in HTTP payloads and logs).

## Performance considerations

Success and unsampled/absent-activity paths are allocation-free no-ops, making the calls safe on hot paths. Failure tagging costs three or four `SetTag`/`SetStatus` calls on an already-recording span. The snake_case conversion is a switch over the enum — no string manipulation.

## Thread safety

The extension methods are stateless; `Result`/`Error` are immutable. `Activity` tag-setting is safe under `Activity`'s own concurrency guarantees; as usual, avoid tagging an activity concurrently with disposing it.

## Testing applications using this feature

Use an `ActivityListener` that samples everything, then assert on tags:

```csharp
[Fact]
public void Failure_tags_the_current_activity()
{
    using var source = new ActivitySource("test");
    using var listener = new ActivityListener
    {
        ShouldListenTo = s => s.Name == "test",
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
    };
    ActivitySource.AddActivityListener(listener);

    using var activity = source.StartActivity("op");
    Result result = Error.NotFound("User.NotFound", "missing");
    result.TagCurrentActivity();

    Assert.Equal(ActivityStatusCode.Error, activity!.Status);
    Assert.Equal("User.NotFound", activity.StatusDescription);
    Assert.Equal("not_found", activity.GetTagItem("error.type"));
    Assert.Equal("User.NotFound", activity.GetTagItem("koras.error.code"));
}

[Fact]
public void Success_is_a_no_op_even_without_an_activity()
{
    var result = Result.Success(42).TagCurrentActivity(); // no listener, no activity: safe
    Assert.True(result.IsSuccess);
}
```

## Complete example

A minimal worker with the OpenTelemetry SDK exporting the tagged spans:

```csharp
using System.Diagnostics;
using Koras.Results;
using Koras.Results.OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("sync-worker"))
    .WithTracing(t => t.AddSource("SyncWorker").AddOtlpExporter());
builder.Services.AddHostedService<Worker>();
await builder.Build().RunAsync();

public sealed class Worker : BackgroundService
{
    private static readonly ActivitySource Activities = new("SyncWorker");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var activity = Activities.StartActivity("sync-batch");

            await Result.TryAsync(
                    () => Task.FromResult<IReadOnlyList<string>>(["a", "b"]),
                    ex => Error.Unavailable("Downstream.Timeout", "The downstream system timed out."))
                .TapActivityErrorAsync();

            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
```

The `OpenTelemetry.*` packages here belong to *your* application — `Koras.Results.OpenTelemetry` itself needs none of them.

## Common mistakes

- Expecting the package to start spans — it never creates activities; without `StartActivity` (and a listener/sampler recording it), tagging is a silent no-op.
- Calling `TagCurrentActivity()` after the `using var activity` scope has disposed — `Activity.Current` has already reverted to the parent (or null).
- Tagging before the failure exists, e.g. `.TagCurrentActivity().Ensure(...)` — order matters; tag at the end of the pipeline.
- Treating a missing tag as a bug when the sampler dropped the span — `IsAllDataRequested == false` intentionally skips all work.

## Troubleshooting

- **No tags appear in my backend** — verify an `ActivityListener`/OpenTelemetry SDK actually samples the source (`activity` is non-null and `IsAllDataRequested` is true), and that the failure occurred before the tagging call.
- **Tags land on the wrong span** — with `TapActivityErrorAsync`, `Activity.Current` is read after the await; if the producing code changes the current activity, use `TagActivity(activity)` with an explicit reference.
- **`error.type` shows `unexpected` for my custom flow** — the error was created via the default `Result.Try` mapper or `Error.Unexpected`; use the taxonomy factory that matches the situation.
- **Status description isn't my message** — by design, the description is `Error.Code`, not `Error.Message` (codes are stable and safe to export).

## Related features

- [ProblemDetails conversion](problemdetails.md) — the `traceId` extension links client-visible errors to these spans.
- [Minimal API](minimal-api.md) / [MVC](mvc.md) adapters — tag before converting to HTTP.
- Worker sample — `samples/WorkerService.Sample` (taxonomy-driven retries + tagging).
