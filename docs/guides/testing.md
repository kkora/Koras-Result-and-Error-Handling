# Testing Applications That Use Koras.Results

Results are plain immutable values, which makes them unusually pleasant to test: no mocking
frameworks, no exception-assertion gymnastics, no shared state. This guide covers unit-testing
result-returning code, integration-testing HTTP endpoints, and testing MediatR pipelines. For
copy-paste snippets (snapshot tests, delegate-invocation counts, `JsonElement` assertions), see
the [testing recipes](../recipes/testing-recipes.md).

## No mocking needed for results

You never mock a `Result` — you construct one. `Result.Success(value)`, `Result.Failure<T>(error)`,
and the implicit conversions build any state a test needs:

```csharp
Result<Order> success = new Order(id, "Pending", 42m);            // implicit success
Result<Order> failure = Error.NotFound("Order.NotFound", "gone"); // implicit failure
```

If a *dependency* returns results, a hand-rolled fake is usually simpler than a mock:

```csharp
public sealed class StubOrderRepository(Result<Order> result) : IOrderRepository
{
    public Task<Result<Order>> FindAsync(Guid id, CancellationToken ct) => Task.FromResult(result);
}
```

## Unit tests: assert on IsSuccess, Value, and Error.Code

Assert the code (the stable contract), not the message (display text that may change or be
localized):

```csharp
[Fact]
public async Task Placing_order_with_insufficient_stock_fails_with_conflict()
{
    var service = new OrderService(new StubCatalog(stock: 1));

    var result = await service.PlaceAsync(new PlaceOrder("book-1", quantity: 5));

    Assert.True(result.IsFailure);
    Assert.Equal("Order.InsufficientStock", result.Error.Code);
    Assert.Equal(ErrorType.Conflict, result.Error.Type);
}

[Fact]
public async Task Placing_valid_order_succeeds()
{
    var service = new OrderService(new StubCatalog(stock: 10));

    var result = await service.PlaceAsync(new PlaceOrder("book-1", quantity: 2));

    Assert.True(result.IsSuccess);
    Assert.Equal(2, result.Value.Quantity);
}
```

Remember the access rules: `result.Value` throws `InvalidOperationException` on failure and
`result.Error` throws on success — so assert `IsSuccess`/`IsFailure` first (or use
`TryGetValue`/`TryGetError`). For `ValidationError`, pattern-match and assert on `FieldErrors`.

## Integration tests: WebApplicationFactory and problem+json

`Microsoft.AspNetCore.Mvc.Testing` hosts the real pipeline — including your `AddKorasResults`
options — in-memory via `TestServer`, so tests exercise the actual wire shape:

```csharp
public sealed class TodoEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Unknown_todo_returns_404_problem_json()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/todos/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(404, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Todo.NotFound", problem.RootElement.GetProperty("errorCode").GetString());
        Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task Invalid_create_returns_400_with_errors_dictionary()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/todos", new { title = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = problem.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("Title", out var titleErrors));
        Assert.True(titleErrors.GetArrayLength() > 0);
    }
}
```

(For Minimal API apps, add `public partial class Program;` at the end of `Program.cs` so the
factory can reference the entry point.)

To test with different options, override registrations in `WithWebHostBuilder`:

```csharp
var strictFactory = factory.WithWebHostBuilder(builder =>
    builder.ConfigureServices(services =>
        services.Configure<KorasResultsOptions>(o => o.IncludeUnexpectedErrorDetails = false)));
```

## Testing MediatR pipelines

`ValidationBehavior<,>` can be tested directly — construct it with validators and a `next`
delegate:

```csharp
[Fact]
public async Task Invalid_command_short_circuits_before_the_handler()
{
    var behavior = new ValidationBehavior<RegisterUser, Result<UserDto>>([new RegisterUserValidator()]);
    var handlerRan = false;

    var response = await behavior.Handle(
        new RegisterUser(Email: "not-an-email", DisplayName: ""),
        next: () => { handlerRan = true; return Task.FromResult(Result.Success(SomeUser)); },
        CancellationToken.None);

    Assert.False(handlerRan);                       // short-circuited
    Assert.True(response.IsFailure);
    var validation = Assert.IsType<ValidationError>(response.Error);
    Assert.Contains(validation.FieldErrors, f => f.PropertyName == "Email");
}
```

For end-to-end pipeline tests, build a real container and send through `IMediator`:

```csharp
var services = new ServiceCollection();
services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<RegisterUserHandler>());
services.AddValidatorsFromAssemblyContaining<RegisterUserValidator>();
services.AddKorasResultsValidationBehavior();
services.AddSingleton<UserRepository>();

await using var provider = services.BuildServiceProvider();
var mediator = provider.GetRequiredService<IMediator>();

var result = await mediator.Send(new RegisterUser("ada@example.com", "Ada"));
Assert.True(result.IsSuccess);
```

## Testing ProblemDetails mapping without a server

The explicit-parameter overload of `ToProblemDetails` needs no `HttpContext`, which makes option
mappings unit-testable:

```csharp
[Fact]
public void Custom_code_mapping_wins_over_type_default()
{
    var options = new KorasResultsOptions().MapErrorCode("Order.PaymentRequired", 402);

    var problem = Error.Failure("Order.PaymentRequired", "Payment required.").ToProblemDetails(options);

    Assert.Equal(402, problem.Status);
    Assert.Equal("Order.PaymentRequired", problem.Extensions["errorCode"]);
}
```

## Related documentation

- [Testing recipes](../recipes/testing-recipes.md)
- [Configuration validation](../configuration/validation.md)
- [ASP.NET Core guide](aspnet-core.md)
