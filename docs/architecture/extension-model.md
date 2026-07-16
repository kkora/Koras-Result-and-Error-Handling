# Extension Model — Koras.Results

Koras.Results is not a provider framework (there is no I/O to abstract), so its extension model is deliberately lean: **extension points are seams where applications customize projection and presentation**, not plugin registries.

## Extension points

### 1. Error codes and factories (userland, unbounded)
Applications define their own error catalogs as static classes:

```csharp
public static class OrderErrors
{
    public static Error InsufficientStock(string sku) =>
        Error.Conflict("Order.InsufficientStock", $"Not enough stock for '{sku}'.")
             .WithMetadata("sku", sku);
}
```

This is the primary extension mechanism — no interfaces required.

### 2. Custom error types (constrained)
`Error` can be instantiated directly with any `ErrorType`; `ValidationError` covers structured field errors. Subclassing beyond the shipped hierarchy is intentionally **not** supported (constructors are internal/protected-internal) so that serialization and projection remain closed over known shapes (ADR-0005).

### 3. HTTP status mapping (`KorasResultsOptions`)
```csharp
services.AddKorasResults(o =>
{
    o.MapErrorType(ErrorType.Failure, StatusCodes.Status400BadRequest); // house style
    o.MapErrorCode("Order.InsufficientStock", StatusCodes.Status409Conflict);
    o.ProblemTypeUriFactory = error => new Uri($"https://errors.contoso.com/{error.Code}");
});
```
Precedence: exact code override → error-type override → built-in default.

### 4. Message localization (`IErrorMessageLocalizer`)
One interface, one method (`Localize(Error, CultureInfo)` returning the display message). Registered via DI; the default implementation is pass-through. FieldError messages localize individually.

### 5. Success-result shaping (Minimal API / MVC overloads)
`ToHttpResult(mapSuccess: value => Results.Created(...))` style overloads let endpoints customize success projection without touching failure handling.

### 6. Exception mapping (`Result.Try` mapper delegates)
Boundary code supplies `Func<Exception, Error>`; teams typically centralize these as static mappers (documented recipe).

### 7. Telemetry tagging
Tag names are public constants (`KorasResultsActivityTags`) so custom instrumentation can stay consistent; `TagActivity` overloads accept an explicit `Activity` for non-ambient scenarios.

## Non-extension points (deliberate)

| Not extensible | Why |
|---|---|
| `ErrorType` enum | Closed taxonomy is the contract (ADR-0004) |
| Result structs (no interfaces, sealed behavior) | Struct identity + API stability; interfaces would box |
| Serialization wire shape | Stability across services outweighs customization; use ProblemDetails for public contracts |
| Combinator pipeline (no middleware) | Results are values; a middleware pipeline would reintroduce the framework-weight this package exists to avoid |

## Patterns used (and where)

- **Options pattern:** `KorasResultsOptions` (AspNetCore), validated on start.
- **Builder pattern:** not needed — options object is flat; a builder would add ceremony (documented decision).
- **Factory pattern:** static `Error.*` factories; `ProblemDetailsFactory` from ASP.NET Core is respected in MVC path.
- **Extension methods:** the entire composition surface, kept in `ResultExtensions`/`ResultAsyncExtensions` within the `Koras.Results` namespace so IntelliSense discovers them with the type.
