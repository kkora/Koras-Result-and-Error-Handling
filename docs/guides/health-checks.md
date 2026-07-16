# Health Checks and Koras.Results

Let's be upfront: **the Koras.Results packages ship no health checks and no health-check API.**
There is nothing to probe — the packages are value libraries with no connections, caches, or
background state that could be healthy or unhealthy. Any `AddKorasResultsHealthChecks(...)` you
might expect does not exist, by design.

What the library *does* offer is a clean way to write your application's own health checks:
probe logic that returns results internally, plus a taxonomy that maps naturally onto
`HealthStatus`.

## Probes that return results

Give your infrastructure probes the same result-shaped contract as the rest of the app, using
`Result.TryAsync` as the exception boundary:

```csharp
using Koras.Results;

public sealed class PaymentGatewayProbe(HttpClient http)
{
    public Task<Result<TimeSpan>> PingAsync(CancellationToken cancellationToken) =>
        Result.TryAsync(
            async () =>
            {
                var started = System.Diagnostics.Stopwatch.GetTimestamp();
                try
                {
                    using var response = await http.GetAsync("/status", cancellationToken);
                    response.EnsureSuccessStatusCode();
                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // HttpClient timeout — surface as TimeoutException so TryAsync maps it
                    // (genuine cancellation still propagates and is rethrown by TryAsync).
                    throw new TimeoutException("The status probe timed out.");
                }

                return System.Diagnostics.Stopwatch.GetElapsedTime(started);
            },
            ex => ex switch
            {
                TimeoutException =>
                    Error.Unavailable("PaymentGateway.Timeout", "The payment gateway timed out."),
                HttpRequestException =>
                    Error.Unavailable("PaymentGateway.Unreachable", "The payment gateway is unreachable."),
                _ => Error.Unexpected("PaymentGateway.ProbeFailed", "The payment gateway probe failed.")
                        .WithMetadata("exceptionType", ex.GetType().Name),
            });
}
```

Note: `TryAsync` rethrows `OperationCanceledException`, so a shutting-down host cancels the probe
instead of reporting the dependency as down.

## Mapping errors to health statuses

Wrap the probe in a standard `IHealthCheck` (from
`Microsoft.Extensions.Diagnostics.HealthChecks`, part of ASP.NET Core) and let the taxonomy drive
the status. A sensible policy: `Unavailable` is a known, transient dependency problem —
**Degraded**; anything else coming out of a probe is **Unhealthy**.

```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;

public sealed class PaymentGatewayHealthCheck(PaymentGatewayProbe probe) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = await probe.PingAsync(cancellationToken);

        return result.Match(
            latency => HealthCheckResult.Healthy(
                $"Payment gateway responded in {latency.TotalMilliseconds:F0} ms."),
            error => error.Type == ErrorType.Unavailable
                ? HealthCheckResult.Degraded(
                    error.Message,
                    data: new Dictionary<string, object> { ["errorCode"] = error.Code })
                : HealthCheckResult.Unhealthy(
                    error.Message,
                    data: new Dictionary<string, object> { ["errorCode"] = error.Code }));
    }
}
```

Putting the stable `error.Code` in the health-report `data` dictionary means your monitoring can
distinguish `PaymentGateway.Timeout` from `PaymentGateway.Unreachable` without parsing messages —
the same identifier discipline used in logs and traces.

## Registration

Standard ASP.NET Core wiring; nothing Koras-specific:

```csharp
builder.Services.AddHttpClient<PaymentGatewayProbe>(client =>
{
    client.BaseAddress = new Uri("https://payments.example.com");
    client.Timeout = TimeSpan.FromSeconds(2);   // probes should fail fast
});

builder.Services.AddHealthChecks()
    .AddCheck<PaymentGatewayHealthCheck>("payment-gateway", tags: ["ready"]);

var app = builder.Build();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready", new()
{
    Predicate = registration => registration.Tags.Contains("ready"),
});
```

## Choosing Degraded vs Unhealthy

| Probe outcome | Suggested status | Rationale |
|---|---|---|
| Success | Healthy | dependency reachable |
| `ErrorType.Unavailable` | Degraded | known transient failure; taxonomy says "retryable", orchestrators should usually not restart the pod for it |
| `ErrorType.Unexpected` (or anything else) | Unhealthy | the probe itself misbehaved or an unclassified failure occurred; investigate |

Tune per dependency: a *required* dependency being `Unavailable` on the readiness probe may
deserve Unhealthy so traffic is routed away, while the same error on a liveness probe should
almost never be Unhealthy (restarting your process will not fix someone else's outage).

## Why the package will not ship health checks

- A value library has no state to check; a built-in check would always report Healthy and add a
  false sense of coverage.
- Health policy (which dependencies matter, Degraded vs Unhealthy thresholds) is irreducibly
  application-specific.
- It would drag `Microsoft.Extensions.Diagnostics.HealthChecks` into the dependency graph of
  every consumer, breaking the zero/minimal-dependency promise.

## Related documentation

- [Worker Service guide](worker-service.md) — the same `TryAsync` + taxonomy pattern for retries
- [Common scenarios: mapping infrastructure exceptions](../recipes/common-scenarios.md)
- [Observability architecture](../architecture/observability.md)
