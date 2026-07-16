# Dependency Injection with Koras.Results

Most of Koras.Results needs no dependency injection at all. `Result`, `Result<T>`, and the
`Error` family are immutable values; the combinators are static extension methods. This guide
covers the two places DI does appear and exactly what gets registered.

## What needs registration (and what does not)

| Package | Registration | What it adds |
|---|---|---|
| `Koras.Results` (core) | none | values only — nothing to register |
| `Koras.Results.AspNetCore` | `AddKorasResults(...)` | `KorasResultsOptions` + default `IErrorMessageLocalizer` |
| `Koras.Results.FluentValidation` | none (extension methods) | you register your own validators as usual |
| `Koras.Results.MediatR` | `AddKorasResultsValidationBehavior()` | the open-generic `ValidationBehavior<,>` pipeline behavior |
| `Koras.Results.OpenTelemetry` | none | extension methods over `System.Diagnostics.Activity` |

## AddKorasResults

```csharp
using Koras.Results.AspNetCore;

builder.Services.AddKorasResults(options =>
{
    options.MapErrorType(ErrorType.Failure, StatusCodes.Status400BadRequest);
});
```

What it does, precisely:

```csharp
services.AddOptions<KorasResultsOptions>();
if (configure is not null)
{
    services.Configure(configure);
}
services.TryAddSingleton<IErrorMessageLocalizer, PassThroughErrorMessageLocalizer>();
```

- Options use the standard options pattern (`IOptions<KorasResultsOptions>`), so multiple
  `Configure` delegates compose — each call layers on top of the previous ones.
- The localizer uses `TryAddSingleton`: the first registration wins.

Even without `AddKorasResults`, `ToHttpResult`/`ToActionResult` work: at execution time they
resolve `IOptions<KorasResultsOptions>` and `IErrorMessageLocalizer` from
`HttpContext.RequestServices` and fall back to built-in defaults when absent.

## AddKorasResultsValidationBehavior

```csharp
using Koras.Results.MediatR;

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
builder.Services.AddValidatorsFromAssemblyContaining<Program>();   // FluentValidation
builder.Services.AddKorasResultsValidationBehavior();
```

What it does, precisely:

```csharp
services.TryAddEnumerable(
    ServiceDescriptor.Transient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>)));
```

MediatR and the FluentValidation validators must be registered separately — the behavior only
consumes `IEnumerable<IValidator<TRequest>>` from the container.

## Custom localizer

To translate client-facing messages, register your implementation **before or instead of**
relying on the default (thanks to `TryAddSingleton`, an existing registration is preserved):

```csharp
public sealed class ResxErrorMessageLocalizer(IStringLocalizer<ErrorMessages> localizer) : IErrorMessageLocalizer
{
    public string Localize(Error error, CultureInfo culture) =>
        localizer[error.Code].ResourceNotFound ? error.Message : localizer[error.Code];

    public string LocalizeField(FieldError fieldError, CultureInfo culture) =>
        fieldError.Code is { } code && !localizer[code].ResourceNotFound
            ? localizer[code]
            : fieldError.Message;
}

builder.Services.AddSingleton<IErrorMessageLocalizer, ResxErrorMessageLocalizer>();
builder.Services.AddKorasResults();   // TryAddSingleton keeps your registration
```

Implementations must be thread-safe — they are resolved as singletons.

## Lifetimes

| Service | Lifetime | Notes |
|---|---|---|
| `KorasResultsOptions` | options singleton | mutate only during configuration; read-only afterwards |
| `IErrorMessageLocalizer` | singleton | must be thread-safe |
| `ValidationBehavior<,>` | transient | stateless; per-MediatR-request instantiation |
| `Result`, `Result<T>`, `Error` | n/a | values, never registered |

## Multiple-registration safety

Both registration methods are explicitly safe to call more than once:

- `AddKorasResults()` — `TryAddSingleton` preserves an existing localizer; extra `configure`
  delegates simply layer via the options pattern. Calling it from both a library module and
  `Program.cs` is fine.
- `AddKorasResultsValidationBehavior()` — `TryAddEnumerable` deduplicates by implementation
  type, so the behavior is never added to the pipeline twice no matter how often you call it.

## Services that return results

Your own services need nothing special — register them normally and return results:

```csharp
builder.Services.AddScoped<IOrderService, OrderService>();

public sealed class OrderService(AppDbContext db) : IOrderService
{
    public async Task<Result<Order>> FindAsync(Guid id, CancellationToken ct) =>
        await db.Orders.FindAsync([id], ct) is { } order
            ? order
            : Error.NotFound("Order.NotFound", $"No order with id '{id}'.");
}
```

## Related documentation

- [Configuration guide](configuration.md)
- [ASP.NET Core guide](aspnet-core.md)
- [All options reference](../configuration/all-options.md)
