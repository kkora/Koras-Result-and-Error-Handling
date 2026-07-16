# Binding KorasResultsOptions from appsettings.json

The packages expose no `IConfiguration`-specific API — `AddKorasResults` takes only a code
delegate. But `KorasResultsOptions` is a plain options class, so the standard ASP.NET Core
options binder can populate its *simple* properties from a configuration section. This page
shows the supported pattern and its hard limits.

## What can and cannot be bound

| Member | Bindable from JSON? | Why |
|---|---|---|
| `IncludeUnexpectedErrorDetails` (`bool`) | yes | settable simple property |
| `MetadataExposure` (`MetadataExposurePolicy`) | yes | enum binds from its name (`"None"`/`"All"`) |
| `IncludeTraceId` (`bool`) | yes | settable simple property |
| `ProblemTypeUriFactory` (`Func<Error, string?>?`) | **no** | a delegate — JSON cannot carry code |
| `MapErrorType(...)` / `MapErrorCode(...)` | **no** | methods populating private state — the binder has no property to target |
| `GetStatusCode(...)` | n/a | read-side resolution method |

There is deliberately no string-based JSON schema for mappings: it would fail silently on typos
at runtime, whereas the code API fails fast at startup (see [validation](validation.md)).

## The pattern

Bind the simple flags, then apply mappings and delegates in code. Both feed the same options
instance through the options pipeline:

```json
// appsettings.json
{
  "KorasResults": {
    "IncludeUnexpectedErrorDetails": false,
    "MetadataExposure": "None",
    "IncludeTraceId": true
  }
}
```

```csharp
using Koras.Results;
using Koras.Results.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Bind the simple flags from configuration.
builder.Services.AddOptions<KorasResultsOptions>()
    .Bind(builder.Configuration.GetSection("KorasResults"));

// 2. Register the package services and apply everything JSON cannot express.
builder.Services.AddKorasResults(options =>
{
    options.ProblemTypeUriFactory = error => $"https://errors.example.com/{error.Code}";
    options
        .MapErrorType(ErrorType.Failure, StatusCodes.Status400BadRequest)
        .MapErrorCode("Billing.PaymentRequired", StatusCodes.Status402PaymentRequired);
});
```

Both steps are additive: `AddOptions<T>()` is idempotent, `Bind` registers a configuration step,
and `AddKorasResults`'s delegate registers another `Configure` step. The options system runs all
of them (in registration order) against one `KorasResultsOptions` when it is first resolved.

Environment-specific overlays then work as usual — `appsettings.Development.json` can flip
`IncludeUnexpectedErrorDetails` to `true` without touching code (see
[environment variables](environment-variables.md)).

## Cautions

- **Do not attempt mappings in JSON.** A section like
  `"MapErrorCode": { "Order.NotFound": 404 }` binds to nothing — the binder silently ignores
  properties that do not exist, so the mapping would appear configured but never take effect.
  Keep mappings in code where the compiler and the fail-fast guards can see them.
- **Last writer wins per property.** If both a bound section and a `Configure` delegate set the
  *same* property, the later-registered step wins. Keep each property owned by exactly one
  source: flags in JSON, mappings/delegates in code.
- **Watch what you make configurable.** `IncludeUnexpectedErrorDetails = true` and
  `MetadataExposure = "All"` widen client-visible output; binding them means a configuration
  change can weaken production hygiene. Consider the `IsDevelopment()` code branch instead, or
  add a startup assertion — see [environment variables](environment-variables.md).
- **`ValidateOnStart` pairs well with binding:**

```csharp
builder.Services.AddOptions<KorasResultsOptions>()
    .Bind(builder.Configuration.GetSection("KorasResults"))
    .Validate(o => !o.IncludeUnexpectedErrorDetails || builder.Environment.IsDevelopment(),
              "IncludeUnexpectedErrorDetails must not be enabled outside Development.")
    .ValidateOnStart();
```

## Testing the combined configuration

Resolve the effective options from a built container to verify JSON and code compose the way you
expect:

```csharp
[Fact]
public void Bound_flags_and_code_mappings_compose()
{
    var config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["KorasResults:IncludeTraceId"] = "false",
        })
        .Build();

    var services = new ServiceCollection();
    services.AddOptions<KorasResultsOptions>().Bind(config.GetSection("KorasResults"));
    services.AddKorasResults(o => o.MapErrorType(ErrorType.Failure, 400));

    using var provider = services.BuildServiceProvider();
    var options = provider.GetRequiredService<IOptions<KorasResultsOptions>>().Value;

    Assert.False(options.IncludeTraceId);                                    // from JSON
    Assert.Equal(400, options.GetStatusCode(Error.Failure("X.Y", "m")));     // from code
}
```

## Related documentation

- [Configuration guide](../guides/configuration.md)
- [All options reference](all-options.md)
- [Environment variables](environment-variables.md)
- [Configuration validation](validation.md)
