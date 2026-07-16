# Dependency Injection

Only the ASP.NET Core integration participates in dependency injection. The core package is pure values — `Result`, `Result<T>`, and `Error` are never resolved from a container, never registered, and need no lifetime management.

## Registering with `AddKorasResults`

```csharp
using Koras.Results.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKorasResults();
```

`AddKorasResults` registers exactly two things:

1. **`KorasResultsOptions`** via the standard options system (`services.AddOptions<KorasResultsOptions>()`), plus your configuration lambda if you pass one.
2. **`IErrorMessageLocalizer`** via `TryAddSingleton`, defaulting to `PassThroughErrorMessageLocalizer`, which returns error messages unchanged.

It is safe to call multiple times; existing registrations are preserved.

Calling `AddKorasResults` is itself optional: the `ToHttpResult` / `ToActionResult` adapters fall back to built-in defaults when nothing is registered. Register it whenever you want to configure options or localize messages.

## Configuring options

Pass a lambda to configure the HTTP projection. The lambda runs through the options system, so it composes with any other `Configure<KorasResultsOptions>` calls:

```csharp
builder.Services.AddKorasResults(options =>
{
    options.MapErrorType(ErrorType.Failure, StatusCodes.Status400BadRequest);
    options.MapErrorCode("Order.PaymentRequired", StatusCodes.Status402PaymentRequired);
    options.ProblemTypeUriFactory = error => $"https://errors.example.com/{error.Code}";
    options.IncludeTraceId = true;                      // default
    options.MetadataExposure = MetadataExposurePolicy.None; // default
});
```

See [Configuration](configuration.md) for the full option reference. Options are mutable only during configuration; once materialized they must be treated as read-only (standard options-pattern semantics).

## Replacing the localizer

`IErrorMessageLocalizer` translates `Error` and `FieldError` messages into the request culture before they reach clients:

```csharp
using System.Globalization;
using Koras.Results;
using Koras.Results.AspNetCore;

public sealed class ResourceErrorMessageLocalizer : IErrorMessageLocalizer
{
    public string Localize(Error error, CultureInfo culture) =>
        ErrorMessages.ResourceManager.GetString(error.Code, culture) ?? error.Message;

    public string LocalizeField(FieldError fieldError, CultureInfo culture) =>
        fieldError.Code is { } code
            ? ErrorMessages.ResourceManager.GetString(code, culture) ?? fieldError.Message
            : fieldError.Message;
}
```

Because `AddKorasResults` uses **`TryAddSingleton`** semantics — it only registers the default if no `IErrorMessageLocalizer` is present — you have two equally correct ways to install yours:

```csharp
// Option A: register yours BEFORE AddKorasResults; TryAddSingleton then keeps it.
builder.Services.AddSingleton<IErrorMessageLocalizer, ResourceErrorMessageLocalizer>();
builder.Services.AddKorasResults();

// Option B: rely on TryAdd semantics in the other order — TryAddSingleton never
// overwrites, and the container resolves your later AddSingleton registration.
builder.Services.AddKorasResults();
builder.Services.AddSingleton<IErrorMessageLocalizer, ResourceErrorMessageLocalizer>();
```

Option A is the recommended, unambiguous form. Implementations **must be thread-safe**: the localizer is a singleton serving concurrent requests.

## Service lifetimes

| Service | Lifetime | Notes |
|---|---|---|
| `Result`, `Result<T>`, `Error` family | n/a (values) | Immutable; never registered in DI |
| `KorasResultsOptions` | Options singleton (`IOptions<T>`) | Mutate only inside the configure lambda |
| `IErrorMessageLocalizer` | Singleton | Must be thread-safe; `TryAddSingleton` — first registration wins |
| `ValidationBehavior<,>` (MediatR package) | Transient | Stateless; instantiated per MediatR request |

## How the adapters resolve services

`ToHttpResult` and `ToActionResult` resolve `KorasResultsOptions` and `IErrorMessageLocalizer` from `HttpContext.RequestServices` when they execute inside a request. Outside a request — or when `AddKorasResults` was never called — they use built-in defaults. `ToProblemDetails` additionally has overloads taking explicit `options` and `localizer` arguments for non-DI use.

## Testing with `ServiceCollection`

The registration is plain `Microsoft.Extensions.DependencyInjection`, so you can verify your wiring in a unit test without a web host:

```csharp
using Koras.Results;
using Koras.Results.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

[Fact]
public void AddKorasResults_registers_options_and_localizer()
{
    var services = new ServiceCollection();
    services.AddSingleton<IErrorMessageLocalizer, ResourceErrorMessageLocalizer>();
    services.AddKorasResults(o => o.MapErrorType(ErrorType.Failure, 400));

    using var provider = services.BuildServiceProvider();

    var options = provider.GetRequiredService<IOptions<KorasResultsOptions>>().Value;
    Assert.Equal(400, options.GetStatusCode(Error.Failure("X.Y", "m")));

    // TryAddSingleton preserved the custom localizer.
    Assert.IsType<ResourceErrorMessageLocalizer>(
        provider.GetRequiredService<IErrorMessageLocalizer>());
}
```

For testing the full HTTP projection (status codes, `errorCode`, `traceId` extensions), use `WebApplicationFactory<Program>` and assert on the `application/problem+json` body — see the repository's own integration tests for the pattern.

## Next steps

- [Configuration](configuration.md) — every `KorasResultsOptions` member in detail
- [Concepts: thread safety](../concepts/thread-safety.md) — why singletons are safe here
