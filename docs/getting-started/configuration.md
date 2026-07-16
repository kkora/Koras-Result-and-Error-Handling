# Configuration

`KorasResultsOptions` (in `Koras.Results.AspNetCore`) controls how failed results are projected into HTTP responses. Every default is safe for production; you configure only what deviates from your house rules.

```csharp
builder.Services.AddKorasResults(options =>
{
    options.MapErrorType(ErrorType.Failure, StatusCodes.Status400BadRequest);
    options.MapErrorCode("Order.PaymentRequired", StatusCodes.Status402PaymentRequired);
    options.IncludeUnexpectedErrorDetails = false;              // default
    options.MetadataExposure = MetadataExposurePolicy.None;     // default
    options.IncludeTraceId = true;                              // default
    options.ProblemTypeUriFactory = e => $"https://errors.example.com/{e.Code}";
});
```

## Status-code mapping

### Built-in defaults

Every `ErrorType` has a default HTTP status, so failure mapping is total out of the box:

| `ErrorType` | Default status |
|---|---|
| `Failure` | 422 Unprocessable Entity |
| `Validation` | 400 Bad Request |
| `NotFound` | 404 Not Found |
| `Conflict` | 409 Conflict |
| `Unauthorized` | 401 Unauthorized |
| `Forbidden` | 403 Forbidden |
| `Unavailable` | 503 Service Unavailable |
| `Unexpected` | 500 Internal Server Error |

### `MapErrorType(ErrorType, int)`

Overrides the status for **every** error of a given type:

```csharp
options.MapErrorType(ErrorType.Failure, StatusCodes.Status400BadRequest);
```

### `MapErrorCode(string, int)`

Overrides the status for errors with **exactly** this code (ordinal comparison):

```csharp
options.MapErrorCode("Order.PaymentRequired", StatusCodes.Status402PaymentRequired);
```

### Precedence

`GetStatusCode(Error)` resolves in this order — most specific wins:

1. **Exact-code override** (`MapErrorCode`)
2. **Error-type override** (`MapErrorType`)
3. **Built-in default** for the error's type

Both mapping methods return the options instance, so calls chain fluently.

### Validation at configure time

Status codes are validated when you call the mapping methods, not when an error is projected:

- A status code outside **100–599** throws `ArgumentOutOfRangeException` immediately.
- A null or whitespace `errorCode` passed to `MapErrorCode` throws `ArgumentException`.

Misconfiguration therefore fails at startup, inside your `AddKorasResults` lambda, rather than surfacing as a wrong response in production.

## `IncludeUnexpectedErrorDetails` (default: `false`)

Controls whether the `detail` field of `ErrorType.Unexpected` errors is sent to clients. Unexpected errors usually originate from exceptions and may carry internal information (type names, infrastructure hints), so by default clients receive a generic message while the full message remains available for server-side logging.

Enable it only in environments where leaking internals is acceptable:

```csharp
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddKorasResults(o => o.IncludeUnexpectedErrorDetails = true);
}
```

## `MetadataExposure` (default: `MetadataExposurePolicy.None`)

Controls whether `Error.Metadata` is copied into the ProblemDetails `extensions["metadata"]`:

| Value | Behavior |
|---|---|
| `None` (default) | Metadata never leaves the server |
| `All` | All metadata entries are serialized to clients |

Before switching to `All`, audit every `WithMetadata` call in your codebase: metadata must never contain secrets, credentials, or PII (see [error handling](../concepts/error-handling.md#metadata-rules)).

## `IncludeTraceId` (default: `true`)

When enabled, the current trace identifier is added as the `traceId` extension on every problem response, letting clients quote an ID that you can correlate with server-side logs and distributed traces. Disable only if you must not reveal trace identifiers:

```csharp
options.IncludeTraceId = false;
```

## `ProblemTypeUriFactory` (default: `null`)

Produces the ProblemDetails `type` URI per error. When `null`, the standard RFC 9110 section URI for the mapped status code is used (e.g. `https://tools.ietf.org/html/rfc9110#section-15.5.5` for 404). Point it at your error-catalog documentation to give clients a stable, documented URI per error code:

```csharp
options.ProblemTypeUriFactory = error => $"https://errors.example.com/{error.Code}";
```

Returning `null` from the factory falls back to the default for that error.

## Reading the effective configuration

`GetStatusCode(Error)` is public, so you can assert your mapping rules directly in tests:

```csharp
var options = new KorasResultsOptions()
    .MapErrorType(ErrorType.Failure, 400)
    .MapErrorCode("Order.PaymentRequired", 402);

Assert.Equal(402, options.GetStatusCode(Error.Failure("Order.PaymentRequired", "m"))); // exact code wins
Assert.Equal(400, options.GetStatusCode(Error.Failure("Order.Rejected", "m")));        // type override
Assert.Equal(404, options.GetStatusCode(Error.NotFound("Order.NotFound", "m")));       // built-in default
```

Note that options instances are mutable only during configuration; once materialized by the options system, treat them as read-only.

## Next steps

- [Dependency injection](dependency-injection.md) — where the options live and how they are resolved
- [Concepts: error handling](../concepts/error-handling.md) — designing the error catalog these mappings project
