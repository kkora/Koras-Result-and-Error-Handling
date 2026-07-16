# Recipes: Common Scenarios

Copy-paste patterns for everyday use of the `Koras.Results` core. Each recipe is self-contained.

## The error catalog pattern

Centralize error creation in static factories, one catalog per aggregate/module. Codes become
stable contracts you can assert in tests, map to HTTP status codes, and chart on dashboards.

```csharp
using Koras.Results;

public static class UserErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("User.NotFound", $"No user with id '{id}'.");

    public static Error DuplicateEmail(string email) =>
        Error.Conflict("User.DuplicateEmail", $"A user with email '{email}' already exists.");

    public static Error Suspended(Guid id) =>
        Error.Forbidden("User.Suspended", $"User '{id}' is suspended.");
}
```

Conventions that pay off: `Namespace.Reason` code format, one factory per distinct failure,
parameters only for message interpolation (equality is code + type, so messages can vary freely).

## Repository returning Result&lt;T&gt; instead of null

`Result<T>` never carries null, so "not found" becomes an explicit, typed outcome instead of a
nullable return every caller must remember to check:

```csharp
public sealed class UserRepository(AppDbContext db)
{
    public async Task<Result<User>> FindAsync(Guid id, CancellationToken ct) =>
        await db.Users.FindAsync([id], ct) is { } user
            ? user                              // implicit success
            : UserErrors.NotFound(id);          // implicit failure
}

// Callers compose instead of null-checking:
var greeting = await repository.FindAsync(id, ct)
    .MapAsync(user => $"Hello, {user.DisplayName}!");
```

## Guard-clause chains with Ensure

Replace stacked `if` guards with a declarative chain. The first failing predicate
short-circuits; later predicates never run.

```csharp
public Result<Order> Approve(Order order, User approver) =>
    Result.Success(order)
        .Ensure(o => o.Status == OrderStatus.Pending,
                o => Error.Conflict("Order.NotPending", $"Order is {o.Status}."))
        .Ensure(o => o.Total <= approver.ApprovalLimit,
                o => Error.Forbidden("Order.OverApprovalLimit", $"Total {o.Total:C} exceeds your limit."))
        .Ensure(o => !o.Items.Any(i => i.Discontinued),
                Error.Failure("Order.ContainsDiscontinuedItems", "Order contains discontinued items."))
        .Map(o => o.WithStatus(OrderStatus.Approved));
```

Both `Ensure` overloads are shown: a fixed `Error` and an error factory receiving the value.

## Converting legacy throwing code with Try

Wrap code you do not control at the boundary; classify exceptions into the taxonomy once, then
stay in result-land:

```csharp
public Result<Settings> LoadSettings(string path) =>
    Result.Try(
        () => JsonSerializer.Deserialize<Settings>(File.ReadAllText(path))
              ?? throw new InvalidDataException("Empty settings document."),
        ex => ex switch
        {
            FileNotFoundException or DirectoryNotFoundException =>
                Error.NotFound("Settings.FileMissing", $"Settings file '{path}' does not exist."),
            JsonException or InvalidDataException =>
                Error.Validation("Settings.Malformed", "The settings file is not valid JSON."),
            UnauthorizedAccessException =>
                Error.Forbidden("Settings.AccessDenied", $"Access to '{path}' was denied."),
            _ => Error.Unexpected("Settings.LoadFailed", "Failed to load settings."),
        });
```

Omit the mapper and you get the leak-safe default: `Error.Unexpected("Unexpected.Exception", ...)`
with the exception type in metadata but the message excluded. `OperationCanceledException` is
always rethrown.

## Paging + not-found

Distinguish "the page is empty" (a normal success) from "the thing you are paging under does not
exist" (a failure). Return a page envelope as the success value:

```csharp
public sealed record Page<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount);

public async Task<Result<Page<OrderSummary>>> GetOrdersForCustomerAsync(
    Guid customerId, int pageNumber, int pageSize, CancellationToken ct)
{
    if (pageNumber < 1 || pageSize is < 1 or > 200)
    {
        return new ValidationError(
            new FieldError("PageNumber", "Page number must be at least 1."),
            new FieldError("PageSize", "Page size must be between 1 and 200."));
    }

    if (!await db.Customers.AnyAsync(c => c.Id == customerId, ct))
    {
        return Error.NotFound("Customer.NotFound", $"No customer with id '{customerId}'.");
    }

    var query = db.Orders.Where(o => o.CustomerId == customerId).OrderByDescending(o => o.PlacedAt);
    var total = await query.CountAsync(ct);
    var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize)
        .Select(o => new OrderSummary(o.Id, o.Total)).ToListAsync(ct);

    return new Page<OrderSummary>(items, pageNumber, pageSize, total);   // empty page = success
}
```

Over HTTP: the empty page renders as 200 with `items: []`; the missing customer renders as a 404
problem response.

## Mapping infrastructure exceptions to Unavailable

`ErrorType.Unavailable` means "infrastructure / transient" — the taxonomy's retry signal (it also
maps to HTTP 503 by default). Establish one boundary per dependency:

```csharp
public Task<Result<PaymentReceipt>> ChargeAsync(PaymentRequest request, CancellationToken ct) =>
    Result.TryAsync(
        () => gatewayClient.ChargeAsync(request, ct),
        ex => ex switch
        {
            HttpRequestException or TimeoutException =>
                Error.Unavailable("Payments.GatewayUnavailable", "The payment gateway is unavailable."),
            _ => Error.Unexpected("Payments.ChargeFailed", "Charging the payment failed.")
                    .WithMetadata("exceptionType", ex.GetType().Name),
        });
```

Callers then branch on the taxonomy, not on exception types:

```csharp
if (result.IsFailure && result.Error.Type == ErrorType.Unavailable)
{
    // retry / requeue / back off — see the Worker Service guide
}
```

Caution: `TryAsync` always rethrows `OperationCanceledException` — and that includes
`TaskCanceledException`, which `HttpClient` throws on timeout. If you want client timeouts
classified as `Unavailable` rather than propagated as cancellation, catch the timeout inside the
delegate and rethrow it as `TimeoutException` (or check
`ex.InnerException is TimeoutException` there) before `TryAsync` sees it.

## Related documentation

- [Advanced scenarios](advanced-scenarios.md)
- [Worker Service guide](../guides/worker-service.md)
- [Console app guide](../guides/console-app.md)
