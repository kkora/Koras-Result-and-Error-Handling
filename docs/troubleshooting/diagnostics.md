# Diagnostics — Koras.Results

How to see what the library is doing in a running application: mapper logging, response↔trace
correlation, and activity tags. The full log-event reference is in [logging.md](logging.md).

## Turning on Debug logging for the HTTP mapper

The AspNetCore package logs under the category
**`Koras.Results.AspNetCore.ResultHttpMapper`**. At Debug level it records every error→status
mapping decision; at Warning it records suppressed `Unexpected` details. Default provider
configurations hide Debug, so enable it per category:

```json
// appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Koras.Results.AspNetCore.ResultHttpMapper": "Debug"
    }
  }
}
```

or in code:

```csharp
builder.Logging.AddFilter("Koras.Results.AspNetCore.ResultHttpMapper", LogLevel.Debug);
```

You will then see, for every failed result projected to HTTP:

```
dbug: Koras.Results.AspNetCore.ResultHttpMapper[1]
      Mapped error Order.InsufficientStock (Failure) to HTTP 422
```

This is the fastest way to answer "why did this request return 418?" — the log line shows the
error code, its `ErrorType`, and the resolved status after all `MapErrorType`/`MapErrorCode`
overrides. Remember the core package never logs (zero-dependency promise); mapping decisions are
the only library-emitted log source, and application-level failure logging belongs in your own
`TapError` calls.

Note: the logger is resolved from `HttpContext.RequestServices`; endpoints served without a DI
container (rare — e.g. calling `ToProblemDetails` manually with explicit options) produce no log
output.

## Reading traceId from problem responses and joining to traces

By default (`KorasResultsOptions.IncludeTraceId = true`) every ProblemDetails response carries a
`traceId` extension:

```json
{
  "status": 422,
  "title": "Unprocessable Entity",
  "detail": "Insufficient stock for SKU A-1.",
  "errorCode": "Order.InsufficientStock",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

The value is `Activity.Current?.Id ?? HttpContext.TraceIdentifier` — the same source ASP.NET
Core's own ProblemDetails uses. When a tracing SDK is active, it is the **W3C traceparent** form
(`00-{trace-id}-{span-id}-{flags}`), so:

1. Take the client-reported `traceId` and extract the 32-hex-character trace id segment
   (`4bf92f35…` above).
2. Search your trace backend (Jaeger/Tempo/Application Insights) for that trace id — you land on
   the exact request's trace.
3. Correlate with logs: with `ActivityTrackingOptions` (or any provider that stamps `TraceId` on
   log scopes), the mapper's Debug/Warning entries for that request carry the same trace id, so
   the suppressed-detail Warning (which contains the original `Unexpected` message withheld from
   the client) is directly joinable to the user's report.

If no tracing listener is active, the value falls back to `HttpContext.TraceIdentifier`
(Kestrel's connection-scoped id) — still joinable to server logs that include the request
identifier, but not to distributed traces. Support workflow: ask the reporting client for
`errorCode` + `traceId`; that pair usually pinpoints the failure without reproduction.

## Inspecting activity tags (Koras.Results.OpenTelemetry)

Where the application opts in (`result.TagCurrentActivity()`, `TagActivity(activity)`, or
`TapActivityErrorAsync()` in pipelines), failures annotate the current span:

| Tag | Example | Notes |
|---|---|---|
| `otel.status_code` / activity status | `Error`, with `StatusDescription` = error code | standard failure marking |
| `error.type` | `not_found` | `ErrorType` in snake_case (OTel `error.type` convention) |
| `koras.error.code` | `User.NotFound` | stable machine identifier |
| `koras.error.aggregate_count` | `3` | only for `AggregateError` |

To inspect: open the trace found via `traceId` above and look at the span's attributes; failed
result spans show status `ERROR` with the code as the status description. In dashboards, group by
`koras.error.code` for per-error rates and by `error.type` for taxonomy-level views.

Seeing **no tags**? Successes never tag (by design — no tag spam), null/absent activities are
no-ops, and *non-recording* activities are left untouched. See the checklist in
[provider-errors.md](provider-errors.md#opentelemetry-tags-missing).

## Quick triage recipe

Symptom: a client got an unexpected 500 with `"detail": "An unexpected error occurred."`.

1. Get `traceId` and `errorCode` from the response body.
2. Search logs for the Warning (event id 2) in category
   `Koras.Results.AspNetCore.ResultHttpMapper` with that trace id — it contains the **original
   suppressed message**.
3. Open the trace for the request; the failing span carries `koras.error.code`.
4. If more context is needed, enable Debug for the category (mapping decisions) and reproduce.
