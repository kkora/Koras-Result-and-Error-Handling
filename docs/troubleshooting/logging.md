# Logging Reference — Koras.Results

The complete inventory of everything this library will ever write to `ILogger`. It is short by
design: the **core package never logs** (it has no logger and never will — the zero-dependency
promise), and the satellites other than AspNetCore emit nothing either. All library logging comes
from one source.

## Category

```
Koras.Results.AspNetCore.ResultHttpMapper
```

Defined as `ProblemDetailsBuilder.LoggerCategory` and used for every event below. The logger is
created via `ILoggerFactory.CreateLogger(category)` from `HttpContext.RequestServices` during
result→HTTP projection; if no logger factory is registered (non-DI usage of the explicit-options
overloads), logging is skipped entirely.

## Events

Source: `src/Koras.Results.AspNetCore/ResultHttpMapperLog.cs` (source-generated via
`[LoggerMessage]`, so the event ids and message templates below are compile-time constants).

| Event id | Name | Level | Message template |
|---|---|---|---|
| **1** | `MappedError` | **Debug** | `Mapped error {ErrorCode} ({ErrorType}) to HTTP {StatusCode}` |
| **2** | `SuppressedUnexpectedDetails` | **Warning** | `Suppressed details of unexpected error {ErrorCode} from the HTTP response; original message: {ErrorMessage}` |

### Event 1 — MappedError (Debug)

Emitted once per failed result projected to a ProblemDetails response, **after** all
`MapErrorType`/`MapErrorCode` overrides are resolved. Structured parameters: `ErrorCode`
(string), `ErrorType` (enum), `StatusCode` (int). Fires for every failure, including suppressed
ones (event 2 precedes it in the same projection).

### Event 2 — SuppressedUnexpectedDetails (Warning)

Emitted only when an `ErrorType.Unexpected` error is projected while
`IncludeUnexpectedErrorDetails` is `false` (the default): the response `detail` was replaced with
generic text, and this entry preserves the withheld information server-side. Structured
parameters: `ErrorCode`, `ErrorMessage` — note **`ErrorMessage` is the original, unsanitized
message**; treat this log stream accordingly (see `docs/security/data-protection.md`).

If you see event 2 in production frequently, something is routinely producing `Unexpected` errors
— that is a bug signal worth alerting on, independent of the disclosure aspect.

## Sample output

Console provider, Debug enabled for the category:

```
dbug: Koras.Results.AspNetCore.ResultHttpMapper[1]
      Mapped error User.NotFound (NotFound) to HTTP 404

warn: Koras.Results.AspNetCore.ResultHttpMapper[2]
      Suppressed details of unexpected error Db.Crash from the HTTP response; original message: Connection to replica-3 timed out after 30s
dbug: Koras.Results.AspNetCore.ResultHttpMapper[1]
      Mapped error Db.Crash (Unexpected) to HTTP 500
```

(The second block shows the pairing: a suppressed `Unexpected` failure produces the Warning and
then the Debug mapping entry.)

These exact behaviors are pinned by integration test
`MinimalApiIntegrationTests.Unexpected_error_details_are_suppressed_by_default_and_logged`, which
asserts the category, the Warning level, and the presence of the error code in the message via
the test suite's `CapturingLoggerProvider`.

## Filtering configuration

Defaults matter: most provider configurations show Warning and above, so **event 2 is visible out
of the box** and **event 1 is hidden** — enable Debug for the category when diagnosing mapping
decisions:

```json
{
  "Logging": {
    "LogLevel": {
      "Koras.Results.AspNetCore.ResultHttpMapper": "Debug"
    }
  }
}
```

```csharp
// equivalent in code
builder.Logging.AddFilter("Koras.Results.AspNetCore.ResultHttpMapper", LogLevel.Debug);
```

To silence the library entirely (not recommended — you lose the suppression audit trail):

```json
"Koras.Results.AspNetCore.ResultHttpMapper": "None"
```

Because the ids and templates are stable compile-time constants, alerting rules can key on
`{Category = 'Koras.Results.AspNetCore.ResultHttpMapper', EventId = 2}` safely; changing either
would be a behavioral break governed by the compatibility policy.

## Application-level logging

Wanting *more* than the two events above is an application concern, done with the combinators —
typically at the layer boundary where the failure is decided:

```csharp
return await _orders.PlaceAsync(cmd)
    .TapErrorAsync(e => _logger.LogWarning("Order rejected: {ErrorCode} ({ErrorType})", e.Code, e.Type));
```

Log-safety rule (from `docs/architecture/observability.md`): `Code` and `Type` are always safe to
log; treat `Message` and `Metadata` as potentially sensitive at Information level and above.
