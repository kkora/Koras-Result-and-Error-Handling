# Recipes: Advanced Scenarios

Patterns for composing results across parallel work, transactions, bounded contexts, and custom
HTTP success shapes.

## Combining parallel independent operations

`Bind` chains are sequential by design. When operations are independent, run them concurrently
with `Task.WhenAll`, then merge with `Result.Combine` — the tuple overloads keep everything
strongly typed:

```csharp
public async Task<Result<CheckoutContext>> LoadCheckoutContextAsync(Guid userId, Guid cartId, CancellationToken ct)
{
    var userTask = users.FindAsync(userId, ct);        // Task<Result<User>>
    var cartTask = carts.FindAsync(cartId, ct);        // Task<Result<Cart>>
    var ratesTask = taxService.GetRatesAsync(ct);      // Task<Result<TaxRates>>

    await Task.WhenAll(userTask, cartTask, ratesTask);

    return Result.Combine(userTask.Result, cartTask.Result, ratesTask.Result)
        .Map(t => new CheckoutContext(t.Item1, t.Item2, t.Item3));
}
```

Combine semantics: all successes → a success carrying the tuple; exactly one failure → that
error; two or more → merged (`ValidationError` when all are validation failures, otherwise an
`AggregateError` whose `Type` is the highest severity among the children). The caller sees
*every* failure, not just the first — better UX and better diagnostics than sequential
short-circuiting for independent work.

For a dynamic number of homogeneous operations, use the collection overload:

```csharp
Result[] results = await Task.WhenAll(ids.Select(id => notifier.NotifyAsync(id, ct)));
Result outcome = Result.Combine(results);
```

## Transactional pipelines with Bind

`Bind` gives a transaction script natural short-circuiting: any failing step skips the rest, and
the commit only happens on overall success.

```csharp
public async Task<Result<OrderConfirmation>> PlaceOrderAsync(PlaceOrder command, CancellationToken ct)
{
    await using var transaction = await db.Database.BeginTransactionAsync(ct);

    var result = await validator.ValidateToResultAsync(command, ct)
        .BindAsync(valid => inventory.ReserveAsync(valid.Sku, valid.Quantity, ct))
        .BindAsync(reservation => payments.ChargeAsync(command.PaymentToken, reservation.Total, ct)
            .MapAsync(receipt => (reservation, receipt)))
        .BindAsync(pair => orders.CreateAsync(pair.reservation, pair.receipt, ct))
        .MapAsync(order => new OrderConfirmation(order.Id, order.Total));

    if (result.IsSuccess)
    {
        await transaction.CommitAsync(ct);
    }
    // else: dispose rolls back; the failed result already says why.

    return result;
}
```

Because failures are values, the rollback path needs no catch block — exceptions remain reserved
for genuinely exceptional situations (and would still trigger the rollback via disposal).

## Error translation between bounded contexts with MapError

An inner context's error codes are its private vocabulary. Translate at the seam with
`MapError` so consumers only ever see the outer context's catalog:

```csharp
public async Task<Result<Shipment>> ScheduleShipmentAsync(Guid orderId, CancellationToken ct) =>
    (await warehouseClient.RequestPickupAsync(orderId, ct))
        .MapError(error => error.Code switch
        {
            "Wh.SlotUnavailable" =>
                Error.Conflict("Shipping.NoCapacity", "No shipping capacity is available for this order.")
                     .WithMetadata("warehouseCode", error.Code),
            "Wh.HubOffline" =>
                Error.Unavailable("Shipping.ProviderDown", "The shipping provider is temporarily offline."),
            _ => error,   // pass through anything we don't specifically translate
        });
```

Keeping the original code in metadata preserves the diagnostic trail without leaking the inner
vocabulary into your public contract. The same technique renames overly generic errors on the
way up: `error => Error.NotFound("Order.NotFound", ...)` instead of a bare `Entity.NotFound`.

## Aggregating validation across multiple commands

Batch endpoints should validate every item and report all failures at once. Validate each item,
tag field errors with the item index, and merge:

```csharp
public async Task<Result> ValidateBatchAsync(IReadOnlyList<CreateTodo> commands,
    IValidator<CreateTodo> validator, CancellationToken ct)
{
    var results = new List<Result>(commands.Count);
    for (var i = 0; i < commands.Count; i++)
    {
        var index = i;
        var itemResult = (await validator.ValidateToResultAsync(commands[i], ct))
            .ToResult()
            .MapError(error => error is ValidationError validation
                ? new ValidationError(validation.FieldErrors.Select(f =>
                      f with { PropertyName = $"[{index}].{f.PropertyName}" }))
                : error);
        results.Add(itemResult);
    }

    return Result.Combine(results);
}
```

Because all failures are `ValidationError`s, `Combine` merges them into a *single*
`ValidationError` whose field errors carry indexed property names (`[0].Title`, `[3].Title`).
Over HTTP that renders as one 400 with a complete `errors` dictionary — clients fix everything
in one round trip.

## Custom success mapping: 202 Accepted, file downloads

Failure mapping is fixed and correct by default; success mapping is yours via the
`ToHttpResult(onSuccess)` overload (Minimal APIs) or `ToActionResult(onSuccess)` (MVC).

**202 Accepted** for long-running work:

```csharp
app.MapPost("/reports", async (RequestReport command, IReportQueue queue, CancellationToken ct) =>
    (await queue.EnqueueAsync(command, ct))                       // Task<Result<ReportTicket>>
        .ToHttpResultAsync(ticket =>
            Results.AcceptedAtRoute("report-status", new { id = ticket.Id }, ticket)));
```

**File downloads**:

```csharp
app.MapGet("/invoices/{id:guid}/pdf", async (Guid id, IInvoiceService invoices, CancellationToken ct) =>
    (await invoices.RenderPdfAsync(id, ct))                       // Task<Result<InvoicePdf>>
        .ToHttpResultAsync(pdf =>
            Results.File(pdf.Content, "application/pdf", $"invoice-{id}.pdf")));
```

In both cases a failure (`Invoice.NotFound`, `Reports.QueueFull`, …) still produces the standard
problem+json response with the right status code — only the success branch is customized. The
201-with-Location case is common enough to have its own sugar: `ToCreatedHttpResult(v => url)`.

## Related documentation

- [Common scenarios](common-scenarios.md)
- [Minimal API guide](../guides/minimal-api.md)
- [Public API design](../api/public-api-design.md)
