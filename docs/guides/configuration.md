# Configuring KorasResultsOptions

`KorasResultsOptions` controls how `Koras.Results.AspNetCore` projects failures into HTTP
responses. This guide covers configuring it in code — the primary and recommended mechanism —
and explains how to combine it with `IConfiguration` when you need environment-specific flags.

## Configure in code

Pass a delegate to `AddKorasResults`:

```csharp
using Koras.Results;
using Koras.Results.AspNetCore;

builder.Services.AddKorasResults(options =>
{
    // Simple flags
    options.IncludeUnexpectedErrorDetails = false;         // default; keep in production
    options.MetadataExposure = MetadataExposurePolicy.None; // default; keep in production
    options.IncludeTraceId = true;                          // default

    // Custom ProblemDetails "type" URIs (default: RFC 9110 section links)
    options.ProblemTypeUriFactory = error => $"https://errors.example.com/{error.Code}";

    // Status-code remapping — fluent, chainable
    options
        .MapErrorType(ErrorType.Failure, StatusCodes.Status400BadRequest)
        .MapErrorCode("Order.PaymentRequired", StatusCodes.Status402PaymentRequired);
});
```

Resolution precedence in `GetStatusCode(error)`: exact-code override → error-type override →
built-in default map. See the [full options reference](../configuration/all-options.md).

Because the options pattern is used, multiple `Configure` calls compose. A shared library can
establish house rules and the application can layer more on top:

```csharp
builder.Services.AddKorasResults(o => o.MapErrorType(ErrorType.Failure, 400)); // platform defaults
builder.Services.Configure<KorasResultsOptions>(o => o.MapErrorCode("Legacy.Teapot", 418));
```

## Why appsettings alone cannot express this

Two of the most important members are not data:

- `MapErrorType` / `MapErrorCode` are **method calls** that populate private override
  dictionaries — there is no settable property for the configuration binder to target.
- `ProblemTypeUriFactory` is a **delegate** (`Func<Error, string?>`), and JSON cannot carry code.

So a raw `Bind` of an appsettings section can only ever reach the three simple boolean/enum
properties. Attempting to express mappings in JSON would require inventing a parallel
string-based schema that the package deliberately does not have (typos would fail silently at
runtime; the code-based API fails fast at startup instead — see
[configuration validation](../configuration/validation.md)).

## The recommended pattern: bind flags, map in code

Put only the environment-varying flags in configuration, and keep mappings and delegates in code:

```json
// appsettings.Development.json
{
  "KorasResults": {
    "IncludeUnexpectedErrorDetails": true
  }
}
```

```csharp
builder.Services.AddOptions<KorasResultsOptions>()
    .Bind(builder.Configuration.GetSection("KorasResults"));   // simple flags only

builder.Services.AddKorasResults(options =>
{
    // Everything JSON can't express lives here — identical in every environment.
    options.ProblemTypeUriFactory = error => $"https://errors.example.com/{error.Code}";
    options.MapErrorType(ErrorType.Failure, StatusCodes.Status400BadRequest);
});
```

Order does not matter for distinct members: `Bind` sets the flag properties, the `configure`
delegate adds mappings, and the options system applies both to the same
`KorasResultsOptions` instance. If both touch the *same* property, last registration wins —
keep each property owned by exactly one source to avoid surprises.

An equivalent alternative reads configuration inside the delegate, which keeps everything in one
place:

```csharp
builder.Services.AddKorasResults(options =>
{
    options.IncludeUnexpectedErrorDetails =
        builder.Configuration.GetValue<bool>("KorasResults:IncludeUnexpectedErrorDetails");
    options.MapErrorType(ErrorType.Failure, StatusCodes.Status400BadRequest);
});
```

Note: the packages themselves never read `IConfiguration` or environment variables — the wiring
above is standard ASP.NET Core composition that you own. See
[appsettings binding](../configuration/appsettings.md) and
[environment variables](../configuration/environment-variables.md).

## Environment-conditional configuration

The idiomatic switch for developer convenience without weakening production:

```csharp
builder.Services.AddKorasResults(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        options.IncludeUnexpectedErrorDetails = true;   // see real exception-derived messages locally
        options.MetadataExposure = MetadataExposurePolicy.All;
    }
    // Production: secure defaults, nothing to do.
});
```

## Testing your configuration

Options validation happens at call time (`MapErrorType(type, 700)` throws immediately), and
`GetStatusCode` is a pure function — assert your house rules directly:

```csharp
[Fact]
public void Failure_maps_to_400_per_house_rule()
{
    var options = new KorasResultsOptions().MapErrorType(ErrorType.Failure, 400);
    Assert.Equal(400, options.GetStatusCode(Error.Failure("X.Y", "m")));
}
```

## Related documentation

- [All options reference](../configuration/all-options.md)
- [appsettings binding](../configuration/appsettings.md)
- [Configuration validation](../configuration/validation.md)
- [Production configuration recipe](../recipes/production-configuration.md)
