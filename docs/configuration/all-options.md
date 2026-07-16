# KorasResultsOptions — Complete Reference

`Koras.Results.AspNetCore.KorasResultsOptions` is the single options type in the package family.
It controls how failures are projected into RFC 9457 ProblemDetails responses. Configure via
`AddKorasResults(options => ...)`; instances are mutable only during configuration (standard
options-pattern semantics).

## Members at a glance

| Member | Kind | Type | Default | Effect |
|---|---|---|---|---|
| `IncludeUnexpectedErrorDetails` | property | `bool` | `false` | Whether `Unexpected` error messages reach clients |
| `MetadataExposure` | property | `MetadataExposurePolicy` | `None` | Whether `Error.Metadata` is sent in the `metadata` extension |
| `ProblemTypeUriFactory` | property | `Func<Error, string?>?` | `null` | Custom ProblemDetails `type` URI; `null` → RFC 9110 section URIs |
| `IncludeTraceId` | property | `bool` | `true` | Whether the `traceId` extension is added |
| `MapErrorType(ErrorType, int)` | method | fluent | — | Status-code override for every error of a type |
| `MapErrorCode(string, int)` | method | fluent | — | Status-code override for one exact error code |
| `GetStatusCode(Error)` | method | `int` | — | Resolves the effective status code (see precedence) |

## IncludeUnexpectedErrorDetails

- **Type / default:** `bool`, `false` — the secure default.
- **Effect:** `Unexpected` errors usually originate from exceptions and may carry internal
  details. When `false`, the response `detail` is replaced with
  `"An unexpected error occurred."`; the original message is logged server-side at Warning
  (category `Koras.Results.AspNetCore.ResultHttpMapper`). All other error types are unaffected.

```csharp
options.IncludeUnexpectedErrorDetails = builder.Environment.IsDevelopment();
```

## MetadataExposure

- **Type / default:** `MetadataExposurePolicy`, `None`.
- **Values:** `None` (metadata never sent) | `All` (entire `Error.Metadata` dictionary sent in
  the `metadata` extension when non-empty).
- **Effect:** metadata often carries diagnostic values (exception type names, upstream codes).
  Only set `All` when every error producer treats metadata as client-safe.

```csharp
options.MetadataExposure = MetadataExposurePolicy.All;   // opt-in only
```

## ProblemTypeUriFactory

- **Type / default:** `Func<Error, string?>?`, `null`.
- **Effect:** when `null`, the ProblemDetails `type` is the standard RFC 9110 section URI for
  the mapped status (e.g. 404 → `https://tools.ietf.org/html/rfc9110#section-15.5.5`), matching
  ASP.NET Core's own defaults. When set, a non-null return replaces `type`; returning `null`
  for a given error keeps the RFC default. Being a delegate, it cannot be expressed in
  appsettings — see [appsettings binding](appsettings.md).

```csharp
options.ProblemTypeUriFactory = error => $"https://errors.example.com/{error.Code}";
```

## IncludeTraceId

- **Type / default:** `bool`, `true`.
- **Effect:** adds the `traceId` extension from `Activity.Current?.Id ?? HttpContext.TraceIdentifier`,
  so clients can quote an identifier that joins their report to server telemetry. With the
  OpenTelemetry SDK active this is the W3C trace id of the request span.

## MapErrorType

```csharp
public KorasResultsOptions MapErrorType(ErrorType type, int statusCode)
```

Overrides the status code for *every* error of the given type. Fluent — returns the options for
chaining. Throws `ArgumentOutOfRangeException` when `statusCode` is outside 100–599.

```csharp
options.MapErrorType(ErrorType.Failure, StatusCodes.Status400BadRequest);
```

## MapErrorCode

```csharp
public KorasResultsOptions MapErrorCode(string errorCode, int statusCode)
```

Overrides the status code for errors whose `Code` matches exactly (ordinal comparison). Takes
precedence over `MapErrorType`. Throws `ArgumentException` for a null/whitespace code and
`ArgumentOutOfRangeException` for a status outside 100–599.

```csharp
options.MapErrorCode("Billing.PaymentRequired", StatusCodes.Status402PaymentRequired);
```

## GetStatusCode — resolution precedence

```csharp
public int GetStatusCode(Error error)
```

Resolution order (first hit wins):

1. **Exact-code override** registered via `MapErrorCode`.
2. **Error-type override** registered via `MapErrorType`.
3. **Built-in default** for the error's `ErrorType` (500 as the final fallback).

Pure and side-effect-free — ideal for asserting your configuration in unit tests. Throws
`ArgumentNullException` for a null error.

```csharp
var options = new KorasResultsOptions()
    .MapErrorType(ErrorType.Failure, 400)
    .MapErrorCode("Billing.PaymentRequired", 402);

options.GetStatusCode(Error.Failure("Billing.PaymentRequired", "m")); // 402 (code beats type)
options.GetStatusCode(Error.Failure("Other.Rule", "m"));              // 400 (type override)
options.GetStatusCode(Error.NotFound("X.Y", "m"));                    // 404 (built-in default)
```

## Built-in default status map

| `ErrorType` | HTTP status |
|---|---|
| `Failure` | 422 Unprocessable Entity |
| `Validation` | 400 Bad Request |
| `NotFound` | 404 Not Found |
| `Conflict` | 409 Conflict |
| `Unauthorized` | 401 Unauthorized |
| `Forbidden` | 403 Forbidden |
| `Unavailable` | 503 Service Unavailable |
| `Unexpected` | 500 Internal Server Error |

Every `ErrorType` has a default, so failure mapping is total — no error can fall through
unmapped.

## Related response behavior (not options, but adjacent)

Regardless of configuration, problem responses always include `extensions["errorCode"]`
(`Error.Code`), and `ValidationError` failures additionally produce the ASP.NET Core-shaped
`errors` dictionary grouped by property name.

## Related documentation

- [Configuration guide](../guides/configuration.md)
- [appsettings binding](appsettings.md)
- [Configuration validation](validation.md)
- [Production configuration recipe](../recipes/production-configuration.md)
