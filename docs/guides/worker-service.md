# Worker Services and Background Jobs with Koras.Results

Results are just values, so they work anywhere — including hosted services with no HTTP pipeline.
This guide mirrors [`samples/WorkerService.Sample`](../../samples/WorkerService.Sample): a polling
worker with exception boundaries, taxonomy-driven retries, trace tagging, and clean cancellation.

## Packages

```bash
dotnet add package Koras.Results
dotnet add package Koras.Results.OpenTelemetry   # optional: activity tagging (zero NuGet deps)
```

## Host setup

Nothing Koras-specific to register — the core needs no DI:

```csharp
using WorkerServiceSample;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<FlakyDownstream>();
builder.Services.AddHostedService<SyncWorker>();

var host = builder.Build();
await host.RunAsync();
```

## Exception boundaries with Result.TryAsync

Infrastructure calls throw; the domain wants errors. Wrap the boundary once and classify each
exception into the taxonomy. Transient infrastructure problems become `Unavailable`; genuinely
unknown ones become `Unexpected`.

```csharp
private Task<Result<IReadOnlyList<string>>> FetchBatchAsync(CancellationToken cancellationToken) =>
    Result.TryAsync(
        () => downstream.FetchAsync(cancellationToken),
        ex => ex switch
        {
            TimeoutException => Error.Unavailable("Downstream.Timeout", "The downstream system timed out."),
            HttpRequestException => Error.Unavailable("Downstream.Unreachable", "The downstream system is unreachable."),
            _ => Error.Unexpected("Downstream.Unknown", "An unexpected sync error occurred.")
                    .WithMetadata("exceptionType", ex.GetType().Name),
        });
```

Two guarantees matter here:

- `TryAsync` always rethrows `OperationCanceledException`. Cancellation is a shutdown signal,
  never a failure, so a stopping host is not misreported as a downstream outage.
- Without a custom mapper, the default mapping is leak-safe: an
  `Error.Unexpected("Unexpected.Exception", ...)` whose message excludes the exception text
  (`metadata["exceptionType"]` carries the type name).

## Taxonomy-driven retry

The error taxonomy encodes retryability: `Unavailable` means "infrastructure / transient", so it
retries on the next cycle; everything else is terminal for that item.

```csharp
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
                onFailure: error =>
                {
                    if (error.Type == ErrorType.Unavailable)
                    {
                        logger.LogWarning("Downstream unavailable ({ErrorCode}); will retry next cycle", error.Code);
                    }
                    else
                    {
                        logger.LogError("Terminal sync failure {ErrorCode}: {ErrorMessage}", error.Code, error.Message);
                    }
                });

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break; // graceful shutdown
            }
        }
    }
}
```

This branch-on-`ErrorType` pattern generalizes to real retry policies: feed a Polly retry
predicate with `result.IsFailure && result.Error.Type == ErrorType.Unavailable`, or requeue a
message only for `Unavailable` and dead-letter the rest.

## Clean cancellation

Three pieces cooperate:

1. The `CancellationToken` flows into the downstream call inside the `TryAsync` delegate.
2. `TryAsync` rethrows `OperationCanceledException` instead of mapping it.
3. The loop catches `OperationCanceledException` around its delay and exits.

Press `Ctrl+C` on the sample and note the shutdown log contains no error results — cancellation
never masquerades as a downstream failure in your dashboards.

## Trace tagging

`TapActivityErrorAsync()` (from `Koras.Results.OpenTelemetry`) annotates the current `Activity`
on failure with `error.type`, `koras.error.code`, and OTel status `Error` — one call, no-op on
success or when no listener is recording. Add the OpenTelemetry SDK with an exporter and
subscribe to the `"WorkerServiceSample.Sync"` source to ship the spans; see the
[telemetry guide](telemetry.md).

## Related documentation

- [WorkerService.Sample source](../../samples/WorkerService.Sample)
- [Telemetry guide](telemetry.md)
- [Common scenarios: mapping infrastructure exceptions](../recipes/common-scenarios.md)
