# OpenTelemetry Integration

`Koras.Results.OpenTelemetry` annotates your traces with failure data. It has **zero NuGet
dependencies** — it works against the in-box `System.Diagnostics.Activity` API, so it composes
with the OpenTelemetry SDK without coupling to any of its versions. The package never creates
activities and never tags successes; it only annotates the caller's current span on failure.

```bash
dotnet add package Koras.Results.OpenTelemetry
```

## API

```csharp
using Koras.Results.OpenTelemetry;

result.TagCurrentActivity();              // tags Activity.Current on failure; returns result unchanged
result.TagActivity(activity);             // explicit activity (null is a no-op)
await pipeline.TapActivityErrorAsync();   // combinator form for Task<Result>/Task<Result<T>> chains
```

All three return the receiver for chaining. Success results, a null `Activity.Current`, and
non-recording activities (`IsAllDataRequested == false`) are allocation-free no-ops.

## Tag reference

On failure, the current activity receives:

| Tag | Value | Notes |
|---|---|---|
| OTel status | `Error`, description = `Error.Code` | via `Activity.SetStatus(ActivityStatusCode.Error, code)` |
| `error.type` | `ErrorType` in snake_case: `failure`, `validation`, `not_found`, `conflict`, `unauthorized`, `forbidden`, `unavailable`, `unexpected` | follows the OTel `error.type` semantic convention |
| `koras.error.code` | `Error.Code`, e.g. `User.NotFound` | stable, custom |
| `koras.error.aggregate_count` | child error count | only when the failure is an `AggregateError` |

The tag names are exposed as constants on `KorasResultsActivityTags` (`ErrorType`, `ErrorCode`,
`AggregateCount`) so dashboards and custom instrumentation avoid string literals.

## Wiring with the OpenTelemetry SDK

The package tags spans; the SDK creates and exports them. Reference the SDK packages in the
application (not in your domain libraries):

```csharp
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("orders-api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddSource("OrdersApi.Domain")      // your own ActivitySource
        .AddOtlpExporter());
```

Then tag inside your operations:

```csharp
using System.Diagnostics;
using Koras.Results;
using Koras.Results.OpenTelemetry;

public sealed class OrderService
{
    private static readonly ActivitySource Activities = new("OrdersApi.Domain");

    public async Task<Result<Order>> PlaceAsync(PlaceOrder command, CancellationToken ct)
    {
        using var activity = Activities.StartActivity("place-order");

        return await ValidateAsync(command, ct)
            .BindAsync(valid => ReserveStockAsync(valid, ct))
            .BindAsync(reserved => ChargeAsync(reserved, ct))
            .TapActivityErrorAsync();   // one call tags the whole pipeline's outcome
    }
}
```

In ASP.NET Core, `AddAspNetCoreInstrumentation()` gives every request a span, so
`TagCurrentActivity()` inside request handling annotates the request span even if you create no
sources of your own. See [`samples/WorkerService.Sample`](../../samples/WorkerService.Sample)
for the non-HTTP variant.

## Building dashboards on error codes

Because `Error.Code` values are stable, machine-readable identifiers, `koras.error.code` is the
recommended dimension for failure dashboards and alerts:

- **Failure rate by code** — count spans where `status = ERROR`, grouped by `koras.error.code`.
  A spike in `Downstream.Timeout` reads very differently from a spike in `Order.InsufficientStock`.
- **Taxonomy roll-up** — group by `error.type` for a coarse health signal: growth in
  `unavailable` suggests infrastructure trouble; growth in `unexpected` suggests a bug.
- **Alerting** — alert on `error.type = unexpected` (bugs) and on sustained
  `error.type = unavailable` (dependency outage); treat `validation`/`not_found` as normal
  traffic.

Example Tempo/TraceQL query:

```
{ span.koras.error.code = "Downstream.Timeout" && status = error }
```

## Correlating client reports with traces

The ASP.NET Core package completes the loop: with `IncludeTraceId = true` (the default),
ProblemDetails responses carry a `traceId` extension populated from
`Activity.Current?.Id ?? HttpContext.TraceIdentifier` — the same W3C trace id your exporter
ships. A client-side error report containing

```json
{ "status": 503, "errorCode": "Downstream.Timeout", "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01" }
```

lets support staff paste the `traceId` into the tracing backend and land on the exact failing
span, already tagged with `koras.error.code`.

## Design rules (what the package will not do)

- Never creates activities — no hidden spans, no double instrumentation.
- Never tags successes — no tag spam on the 99% happy path.
- Never emits metrics — derive them from traces/logs or add a counter in `TapError`
  (see [logging guide](logging.md)).

## Related documentation

- [Observability architecture](../architecture/observability.md)
- [Worker Service guide](worker-service.md)
- [Logging guide](logging.md)
