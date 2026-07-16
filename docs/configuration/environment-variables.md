# Environment Variables

**The Koras.Results packages read no environment variables.** None of `Koras.Results`,
`Koras.Results.AspNetCore`, `Koras.Results.FluentValidation`, `Koras.Results.MediatR`, or
`Koras.Results.OpenTelemetry` inspects `Environment.GetEnvironmentVariable`, `IConfiguration`,
or any ambient setting. There are no `KORAS_*` variables to discover, and no hidden behavior
switches — configuration happens exclusively through `KorasResultsOptions` in code (or through
option properties you choose to bind yourself).

This is deliberate: a value library with environment-sensitive behavior would be untestable and
surprising. What follows is the *standard ASP.NET Core pattern* for driving Koras options from
the environment — wiring that your application owns.

## Pattern: toggling a bound flag per environment

ASP.NET Core's default host already layers configuration sources: `appsettings.json`, then
`appsettings.{Environment}.json`, then environment variables (with `__` as the section
separator), then command-line arguments. Bind the simple option flags to a section, and any of
those layers can set them:

```csharp
using Koras.Results.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<KorasResultsOptions>()
    .Bind(builder.Configuration.GetSection("KorasResults"));

builder.Services.AddKorasResults(options =>
{
    // Mappings and delegates cannot come from configuration — they stay in code.
    options.MapErrorType(ErrorType.Failure, StatusCodes.Status400BadRequest);
});
```

With that binding in place, the environment controls the flag:

```bash
# Linux / containers
export KorasResults__IncludeUnexpectedErrorDetails=true

# Windows PowerShell
$env:KorasResults__IncludeUnexpectedErrorDetails = "true"
```

```yaml
# Kubernetes / docker-compose
env:
  - name: KorasResults__IncludeUnexpectedErrorDetails
    value: "true"
```

Only the three bindable simple properties make sense here: `IncludeUnexpectedErrorDetails`,
`MetadataExposure` (`None`/`All`), and `IncludeTraceId`. See
[appsettings binding](appsettings.md) for why `MapErrorCode`/`MapErrorType` and
`ProblemTypeUriFactory` cannot be configuration-driven.

## Pattern: environment name instead of individual variables

Often you do not need per-flag variables at all — branching on `ASPNETCORE_ENVIRONMENT`
(surfaced as `IHostEnvironment`) is simpler and keeps production impossible to weaken with a
single stray variable:

```csharp
builder.Services.AddKorasResults(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        options.IncludeUnexpectedErrorDetails = true;    // developers see real messages
        options.MetadataExposure = MetadataExposurePolicy.All;
    }
    // Any other environment: secure defaults, nothing to set.
});
```

Or the file-based equivalent — put the flag in `appsettings.Development.json` only:

```json
// appsettings.Development.json (never appsettings.json)
{
  "KorasResults": {
    "IncludeUnexpectedErrorDetails": true
  }
}
```

The production file omits the section entirely, so production runs on the compiled-in secure
defaults.

## A word of caution

`IncludeUnexpectedErrorDetails` and `MetadataExposure` exist to *widen* what clients see. If you
make them environment-drivable, an operator can weaken your production error hygiene with one
misplaced variable. Two mitigations:

- Prefer the `IsDevelopment()` code branch over binding, so the widened behavior is structurally
  unreachable in production builds of your configuration.
- If you do bind, add a startup assertion:

```csharp
var opts = app.Services.GetRequiredService<IOptions<KorasResultsOptions>>().Value;
if (!app.Environment.IsDevelopment() && opts.IncludeUnexpectedErrorDetails)
{
    throw new InvalidOperationException(
        "IncludeUnexpectedErrorDetails must not be enabled outside Development.");
}
```

## Related packages' environment behavior

For completeness: the OpenTelemetry *SDK* (not `Koras.Results.OpenTelemetry`, which has no
dependencies) honors standard `OTEL_*` variables such as `OTEL_EXPORTER_OTLP_ENDPOINT`, and
ASP.NET Core itself honors `ASPNETCORE_*`/`DOTNET_*`. Those belong to their respective projects;
Koras.Results neither reads nor requires any of them.

## Related documentation

- [appsettings binding](appsettings.md)
- [Configuration guide](../guides/configuration.md)
- [Production configuration recipe](../recipes/production-configuration.md)
