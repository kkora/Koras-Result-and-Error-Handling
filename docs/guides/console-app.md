# Using Koras.Results in Console Applications

The core `Koras.Results` package has zero dependencies, so a plain console application is the
smallest possible host. Everything in this guide mirrors [`samples/Console.Sample`](../../samples/Console.Sample),
which you can run with `dotnet run --project samples/Console.Sample`.

## Setup

```bash
dotnet new console -n OrderCli
dotnet add OrderCli package Koras.Results
```

No registration, no configuration — `Result`, `Result<T>`, and `Error` are plain immutable values.

## Returning results instead of throwing

Model expected failures as `Error` values with stable codes. Implicit conversions keep the happy
path clean: returning a `T` produces a success, returning an `Error` produces a failure.

```csharp
using Koras.Results;

public sealed record Product(string Sku, string Name, decimal Price, int Stock);

public static class Catalog
{
    private static readonly Dictionary<string, Product> Products = new()
    {
        ["book-1"] = new Product("book-1", "The Pragmatic Programmer", 42.00m, 3),
    };

    public static Result<Product> Find(string sku) =>
        Products.TryGetValue(sku, out var product)
            ? product                                                        // implicit success
            : Error.NotFound("Product.NotFound", $"No product with SKU '{sku}'."); // implicit failure
}
```

## Consuming results with Match and Switch

`Match` folds both branches into a value; `Switch` runs side effects. Both force you to handle
the failure case — there is no way to "forget" it.

```csharp
var found = Catalog.Find("book-1");
Console.WriteLine(found.Match(
    product => $"Found: {product.Name} ({product.Price:C})",
    error => $"Failed: {error.Code}"));
```

## Railway composition

Chain fallible steps with `Bind`, transform values with `Map`, and enforce post-conditions with
`Ensure`. The first failure short-circuits: later delegates are never invoked, and the original
error travels to the end of the chain untouched.

```csharp
var outcome = Order.Parse(input)                                       // Result<Order>
    .Bind(order => Catalog.Find(order.Sku).Map(product => (order, product)))
    .Ensure(pair => pair.product.Stock >= pair.order.Quantity,
            pair => Error.Conflict("Order.InsufficientStock", $"Only {pair.product.Stock} left."))
    .Map(pair => pair.product.Price * pair.order.Quantity);           // Result<decimal>

Console.WriteLine(outcome.Match(
    total => $"total {total:C}",
    error => $"{error.Type}: {error.Code}"));
```

## Field-level validation errors

`ValidationError` (a subclass of `Error`) carries a list of `FieldError` values. It works
anywhere an `Error` does:

```csharp
public static Result ValidateSignup(string email, int age)
{
    var fieldErrors = new List<FieldError>();
    if (string.IsNullOrWhiteSpace(email))
        fieldErrors.Add(new FieldError("Email", "Email is required."));
    if (age < 18)
        fieldErrors.Add(new FieldError("Age", "You must be at least 18."));

    return fieldErrors.Count == 0 ? Result.Success() : new ValidationError(fieldErrors);
}

ValidateSignup(email: "", age: 15).Switch(
    onSuccess: () => Console.WriteLine("signup valid"),
    onFailure: error =>
    {
        foreach (var field in ((ValidationError)error).FieldErrors)
            Console.WriteLine($"{field.PropertyName}: {field.Message}");
    });
```

## Exception boundaries with Result.Try

Wrap legacy or BCL code that throws. The mapper turns exceptions into taxonomy errors; without a
mapper, `Try` produces a leak-safe `Error.Unexpected("Unexpected.Exception", ...)` that excludes
the exception message.

```csharp
var parsed = Result.Try(
    () => int.Parse(userInput, CultureInfo.InvariantCulture),
    ex => Error.Validation("Input.NotANumber", "The input is not a valid integer."));
```

`OperationCanceledException` is always rethrown by `Try`/`TryAsync` — cancellation is not a failure.

## Combining independent checks

`Result.Combine` aggregates: zero failures yields success, one failure yields that error, and two
or more merge (all-validation failures merge into one `ValidationError`; mixed failures become an
`AggregateError`).

```csharp
var combined = Result.Combine(
    ValidateSignup("ada@example.com", 30),
    ValidateSignup("", 12));

Console.WriteLine(combined.IsFailure ? combined.Error.Code : "all valid"); // "Validation.Failed"
```

## Async pipelines

Every combinator has an async form that composes over `Task<Result<T>>`:

```csharp
var report = await Catalog.FindAsync("book-2")
    .MapAsync(product => $"{product.Name} => {product.Price:C}")
    .MatchAsync(line => $"report line: {line}", error => $"failed: {error.Code}");
```

## App-level logging

The core never logs. When a console app needs logging, attach it as a side effect with
`Tap`/`TapError` — the result passes through unchanged:

```csharp
var result = Catalog.Find(sku)
    .Tap(p => logger.LogInformation("Located {Sku}", p.Sku))
    .TapError(e => logger.LogWarning("Lookup failed: {ErrorCode}", e.Code));
```

## Related documentation

- [Console.Sample source](../../samples/Console.Sample)
- [Public API design](../api/public-api-design.md)
- [Common scenarios recipes](../recipes/common-scenarios.md)
