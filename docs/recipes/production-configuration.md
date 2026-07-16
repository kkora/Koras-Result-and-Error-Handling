# Recipe: Production Configuration

A consolidated, reviewed setup for running Koras.Results in production: options, logging levels,
telemetry wiring, and a go-live checklist.

## Recommended KorasResultsOptions

The defaults are already the secure production posture — the recommended configuration mostly
consists of *not* changing them:

```csharp
using Koras.Results;
using Koras.Results.AspNetCore;

builder.Services.AddKorasResults(options =>
{
    // ── Keep the secure defaults (shown explicitly for review clarity) ──
    options.IncludeUnexpectedErrorDetails = false;           // clients never see exception-derived text
    options.MetadataExposure = MetadataExposurePolicy.None;  // metadata stays server-side
    options.IncludeTraceId = true;                           // clients can quote a trace id to support

    // ── Custom type URIs: point clients at your error documentation ──
    options.ProblemTypeUriFactory = error => $"https://errors.example.com/{error.Code}";

    // ── Code-level overrides: your API's house rules ──
    options
        .MapErrorType(ErrorType.Failure, StatusCodes.Status400BadRequest)      // if your API treats
                                                                               // domain failures as 400
        .MapErrorCode("Billing.PaymentRequired", StatusCodes.Status402PaymentRequired)
        .MapErrorCode("Api.RateLimited", StatusCodes.Status429TooManyRequests);
});
```

Notes:

- `ProblemTypeUriFactory` should return stable, documented URLs — ideally each resolves to a page
  describing the error code, remediation, and retryability. Returning `null` from the factory
  falls back to the RFC 9110 default for that status.
- Exact-code mappings beat type mappings, which beat the built-in defaults — audit your
  `MapErrorCode` list against the error catalog at review time.
- If Development needs richer output, flip `IncludeUnexpectedErrorDetails`/`MetadataExposure`
  inside an `if (builder.Environment.IsDevelopment())` block — never via a production-reachable
  setting. See the [environment variables guide](../configuration/environment-variables.md).

## Logging levels

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Koras.Results.AspNetCore.ResultHttpMapper": "Warning"
    }
  }
}
```

- **Production:** `Warning` for the mapper category. You keep the important signal — the
  suppressed-`Unexpected`-detail event, which logs the original message that clients did not see
  — without a Debug line per failed request.
- **Investigations:** temporarily lower the category to `Debug` to see every
  `Mapped error {ErrorCode} ({ErrorType}) to HTTP {StatusCode}` decision.
- The core packages never log; your own failure logging lives in `Tap`/`TapError` and follows
  the [log-safety rules](../guides/logging.md) (`Code`/`Type` freely; `Message`/`Metadata` with
  care).

## OpenTelemetry wiring

```csharp
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName: "orders-api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("OrdersApi.Domain")     // every ActivitySource you tag from
        .AddOtlpExporter());
```

Then ensure failure paths actually tag: `TapActivityErrorAsync()` at the end of significant
pipelines, or `TagCurrentActivity()` at operation boundaries. Failed spans carry `error.type`,
`koras.error.code`, and OTel status `Error`; build dashboards and alerts on
`koras.error.code` (see the [telemetry guide](../guides/telemetry.md)).

The `traceId` extension in ProblemDetails uses `Activity.Current?.Id`, so with the SDK active,
client-reported errors join server traces directly.

## Go-live checklist

**Response hygiene**

- [ ] `IncludeUnexpectedErrorDetails` is `false` (or unset) in production configuration paths.
- [ ] `MetadataExposure` is `None` — or, if `All`, every error producer has been audited to put
      only client-safe values in metadata.
- [ ] Error `Message` texts across the catalog were reviewed as client-visible copy (they are
      sent for every non-`Unexpected` error type).
- [ ] `ProblemTypeUriFactory` URLs resolve to real documentation (or the factory is left `null`).

**Mapping correctness**

- [ ] Every `MapErrorCode` entry matches a code that actually exists in the error catalog
      (ordinal comparison — typos fail silently at lookup, so cover them with tests:
      `Assert.Equal(402, options.GetStatusCode(BillingErrors.PaymentRequired()))`).
- [ ] The default map was consciously accepted or overridden: Failure→422, Validation→400,
      NotFound→404, Conflict→409, Unauthorized→401, Forbidden→403, Unavailable→503,
      Unexpected→500.

**Observability**

- [ ] Mapper category at `Warning`; an alert exists on the suppressed-details Warning event
      (it means unclassified exceptions are reaching clients).
- [ ] Dashboards/alerts exist for `error.type = unexpected` (bugs) and sustained
      `error.type = unavailable` (dependency outage).
- [ ] Support tooling knows to ask users for the `errorCode` and `traceId` fields from problem
      responses.

**Composition**

- [ ] `AddKorasResults` is called exactly where intended (multiple calls are safe, but each
      `configure` delegate layers — verify the effective options with a startup-time assertion
      or an options unit test).
- [ ] MediatR apps: `AddKorasResultsValidationBehavior()` registered, and all request types that
      have validators return `Result`/`Result<T>` (other response types throw
      `InvalidOperationException` on validation failure — by design).

## Related documentation

- [All options reference](../configuration/all-options.md)
- [Configuration guide](../guides/configuration.md)
- [Logging guide](../guides/logging.md)
- [Telemetry guide](../guides/telemetry.md)
