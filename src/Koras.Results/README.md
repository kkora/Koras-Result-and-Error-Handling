# Koras.Results

A lightweight, strongly typed Result pattern and standardized error-handling library for .NET — the zero-dependency core of the Koras.Results family.

```csharp
using Koras.Results;

public Result<Order> Place(PlaceOrder cmd) =>
    _catalog.Find(cmd.Sku)                                   // Result<Product>
        .Ensure(p => p.Stock >= cmd.Qty,
                p => Error.Conflict("Order.InsufficientStock", $"Not enough '{p.Sku}'."))
        .Map(p => Order.Create(p, cmd.Qty));

var message = result.Match(
    order => $"Placed {order.Id}",
    error => $"Rejected: {error.Code}");
```

## Features

- `Result` / `Result<T>` as allocation-free `readonly struct`s — success allocates nothing
- Semantic error taxonomy (`Validation`, `NotFound`, `Conflict`, `Unauthorized`, `Forbidden`, `Unavailable`, `Failure`, `Unexpected`) with stable error codes and metadata
- `ValidationError` with field-level errors
- Functional composition: `Map`, `Bind`, `Match`, `Ensure`, `Tap`, `MapError` — sync and async
- `Result.Try` / `Result.TryAsync` exception boundaries (cancellation always propagates)
- `Result.Combine` failure aggregation
- System.Text.Json serialization out of the box
- Full nullable-reference-type annotations

## Companion packages

| Package | Purpose |
|---|---|
| `Koras.Results.AspNetCore` | RFC 9457 ProblemDetails, Minimal API + MVC integration |
| `Koras.Results.FluentValidation` | FluentValidation → Result conversion |
| `Koras.Results.MediatR` | Validation pipeline behavior for MediatR 12.x |
| `Koras.Results.OpenTelemetry` | Activity error tagging |

Documentation: https://github.com/korastechnologies/koras-results/tree/main/docs
