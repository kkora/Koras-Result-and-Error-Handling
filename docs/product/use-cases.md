# Primary Use Cases — Koras.Results

## UC-1: Domain operation results

A domain service returns `Result<Order>` instead of throwing `OrderNotFoundException` or returning `null`. Callers must acknowledge the failure path; the compiler and nullability annotations enforce it.

```csharp
public Result<Order> PlaceOrder(PlaceOrderCommand command)
{
    if (!_inventory.Has(command.Sku, command.Quantity))
        return OrderErrors.InsufficientStock(command.Sku);

    var order = Order.Create(command);
    return order; // implicit conversion to success
}
```

## UC-2: Validation errors with field-level detail

Input validation produces a `ValidationError` carrying per-field failures that later become a ProblemDetails `errors` dictionary — the same shape ASP.NET Core model validation produces.

```csharp
Result<User> result = Result.Failure<User>(
    new ValidationError(
        new FieldError("Email", "Email is required."),
        new FieldError("Age", "Age must be at least 18.")));
```

## UC-3: API error responses (Minimal API)

One extension method converts any failure into a correct RFC 9457 response; success becomes 200/201/204 as configured.

```csharp
app.MapGet("/users/{id}", async (Guid id, IUserService svc, CancellationToken ct)
    => (await svc.GetUserAsync(id, ct)).ToHttpResult());
// NotFound error -> 404 + application/problem+json, automatically
```

## UC-4: API error responses (MVC)

```csharp
[HttpGet("{id}")]
public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    => (await _users.GetUserAsync(id, ct)).ToActionResult(this);
```

## UC-5: Exception boundary conversion

Wrapping I/O that throws into the Result world at the infrastructure boundary:

```csharp
Result<Customer> result = await Result.TryAsync(
    () => _db.Customers.SingleAsync(c => c.Id == id, ct),
    ex => Error.Unavailable("Db.Unavailable", "The customer store is unreachable."));
```

## UC-6: Functional composition (railway style)

```csharp
var result = await ParseId(rawId)
    .Bind(id => _repository.Find(id))
    .Ensure(order => order.Status == OrderStatus.Open,
            OrderErrors.NotOpen)
    .MapAsync(order => _pricing.PriceAsync(order, ct))
    .TapAsync(priced => _audit.RecordAsync(priced, ct));
```

## UC-7: Aggregating independent failures

```csharp
var combined = Result.Combine(ValidateName(cmd), ValidateEmail(cmd), ValidateAge(cmd));
// Failure aggregates all errors, not just the first.
```

## UC-8: FluentValidation at the application boundary

```csharp
Result<CreateUserCommand> validated =
    await _validator.ValidateToResultAsync(command, ct);
```

## UC-9: MediatR pipeline short-circuit

`ValidationBehavior<TRequest, TResponse>` runs registered validators and returns a failed `Result` — no `ValidationException`, no exception middleware.

## UC-10: Observability

Failures tag the current `Activity` with `error.type`, `error.code`, `otel.status_code` so failure rates by error code appear in traces without manual instrumentation.

## Secondary use cases

- Library authors returning `Result<T>` from public APIs.
- Worker services distinguishing retryable (`Unavailable`) from terminal (`Validation`) failures.
- Serializing `Result<T>` in internal messaging (with documented caveats — prefer ProblemDetails at public boundaries).
