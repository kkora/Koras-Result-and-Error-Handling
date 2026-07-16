# Logging with Koras.Results

The design rule is simple: **the core never logs**. `Koras.Results` has no logger dependency and
never will (zero-dependency promise). Failure information is data; your application decides what
becomes log output. The one place the package family logs is the ASP.NET Core HTTP mapping path.

## The ResultHttpMapper category

When `ToHttpResult`/`ToActionResult` execute inside a request, they log under the category:

```
Koras.Results.AspNetCore.ResultHttpMapper
```

| Level | Event id | Message |
|---|---|---|
| Debug | 1 | `Mapped error {ErrorCode} ({ErrorType}) to HTTP {StatusCode}` |
| Warning | 2 | `Suppressed details of unexpected error {ErrorCode} from the HTTP response; original message: {ErrorMessage}` |

- The Debug event fires for every failure-to-status mapping — useful when diagnosing "why did
  this error become a 422?".
- The Warning event fires when an `Unexpected` error's detail is withheld from the client
  (`IncludeUnexpectedErrorDetails = false`, the default). The original message is preserved in
  the log so nothing is lost server-side while the client sees only
  `"An unexpected error occurred."`.

Enable or silence the category like any other:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Koras.Results.AspNetCore.ResultHttpMapper": "Debug"
    }
  }
}
```

Logging is resolved from the request's `ILoggerFactory` at response-execution time; if no logging
is configured (or you call `ToProblemDetails` directly with explicit options), nothing is logged.

## Application-level logging: Tap and TapError

Because the core is silent, you attach logging where *you* decide it belongs, as side effects
that pass the result through unchanged:

```csharp
var result = await orderService.PlaceAsync(command)
    .TapAsync(order => logger.LogInformation("Order {OrderId} placed", order.Id))
    .TapErrorAsync(error => logger.LogWarning("Order rejected: {ErrorCode}", error.Code));
```

Sync variants exist too:

```csharp
repository.Find(id)
    .Tap(user => logger.LogDebug("Loaded user {UserId}", user.Id))
    .TapError(error => logger.LogWarning("Lookup failed: {ErrorCode} ({ErrorType})", error.Code, error.Type));
```

`Tap` runs only on success, `TapError` only on failure, and neither changes the result — chains
continue exactly as before. Note that delegate exceptions are not caught by the combinators, so
keep logging delegates trivial.

## Log-safety rules

Not every part of an `Error` is equally safe to log:

| Member | Safety | Guidance |
|---|---|---|
| `Error.Code` | always safe | stable machine identifier, designed for logs, metrics, and dashboards |
| `Error.Type` | always safe | closed enum, eight values |
| `Error.Message` | potentially sensitive | human-readable; may embed identifiers, emails, or upstream text. Treat with care at Information level and above |
| `Error.Metadata` | potentially sensitive | arbitrary key-values from error producers; audit before logging wholesale |

A safe default pattern for warning-level logs:

```csharp
result.TapError(e => logger.LogWarning("Operation failed: {ErrorCode} ({ErrorType})", e.Code, e.Type));
```

Reserve `Message`/`Metadata` for Debug-level logs or sinks with appropriate access control. This
mirrors the HTTP side, where `Unexpected` messages are suppressed from responses by default and
metadata is only exposed when you opt in with `MetadataExposure`.

## Structured logging tips

- Always log `ErrorCode` as a named structured property (not interpolated into the message
  string) so you can aggregate and alert on it: `logger.LogWarning("... {ErrorCode}", e.Code)`.
- Pair log-based alerting with trace tagging (`koras.error.code` from
  `Koras.Results.OpenTelemetry`) so dashboards built on either signal use the same identifiers —
  see the [telemetry guide](telemetry.md).
- For `ValidationError`, prefer logging the code and field count over dumping all field
  messages: `((ValidationError)e).FieldErrors.Count`.

## What about metrics?

The packages ship no meters. The documented recipe is to derive metrics from logs or traces, or
increment your own counter inside a `TapError`:

```csharp
result.TapError(e => failureCounter.Add(1,
    new KeyValuePair<string, object?>("error.code", e.Code),
    new KeyValuePair<string, object?>("error.type", e.Type.ToString())));
```

## Related documentation

- [Observability architecture](../architecture/observability.md)
- [Telemetry guide](telemetry.md)
- [Production configuration recipe](../recipes/production-configuration.md)
