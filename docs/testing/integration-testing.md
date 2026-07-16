# Integration Testing — Koras.Results

The integration suite (`tests/Koras.Results.IntegrationTests`) exercises the satellites against
the *real* frameworks they integrate with: an in-memory ASP.NET Core server for the HTTP
adapters, and a real MediatR pipeline resolved from dependency injection. Nothing is mocked.

## The TestServer harness

`tests/Koras.Results.IntegrationTests/AspNetCore/TestHost.cs` contains `TestHostFactory`, the
single entry point every HTTP test uses:

```csharp
internal static async Task<IHost> StartAsync(
    Action<IEndpointRouteBuilder> mapEndpoints,
    Action<KorasResultsOptions>? configureOptions = null,
    Action<IServiceCollection>? configureServices = null,
    bool addControllers = false,
    ILoggerProvider? loggerProvider = null)
```

What it builds:

- A `HostBuilder` with `ConfigureWebHost(webHost => webHost.UseTestServer()...)` — the
  **TestServer** from `Microsoft.AspNetCore.TestHost` hosts the full ASP.NET Core pipeline
  in-memory. Requests issued through `host.GetTestClient()` traverse real routing, endpoint
  execution, result serialization, and response writing — no network sockets, no port conflicts.
- `AddRouting()` + `AddKorasResults(configureOptions)` are always registered, so every test runs
  against the same registration path a real application uses.
- `mapEndpoints` lets each test map its own Minimal API endpoints inline.
- `addControllers: true` additionally calls `AddControllers().AddApplicationPart(...)` with the
  test assembly and `MapControllers()`, which is how `MvcIntegrationTests` picks up the real
  `[ApiController]` class `MvcTestController` defined next to the tests.
- `configureServices` allows service overrides (e.g. registering a custom
  `IErrorMessageLocalizer`, as `Custom_localizer_translates_messages_and_field_messages` does).
- `loggerProvider` swaps the logging pipeline for a capturing provider (below), with
  `SetMinimumLevel(LogLevel.Debug)` so the mapper's Debug events are observable.

The per-TFM `Microsoft.AspNetCore.Mvc.Testing` package (which brings the TestServer types)
matches the framework under test via conditions in `Directory.Packages.props` — see
[compatibility-testing.md](compatibility-testing.md).

## CapturingLoggerProvider

Also in `TestHost.cs`. A minimal, thread-safe `ILoggerProvider` that records every log entry as a
`(string Category, LogLevel Level, string Message)` tuple into a lock-guarded list, exposed
through an `Entries` snapshot property. `IsEnabled` always returns true, so nothing is filtered
before capture.

It exists so tests can assert the AspNetCore package's logging contract, e.g.
`MinimalApiIntegrationTests.Unexpected_error_details_are_suppressed_by_default_and_logged`:

```csharp
var warning = loggerProvider.Entries.Single(e => e.Level == LogLevel.Warning);
Assert.Contains("Db.Crash", warning.Message, StringComparison.Ordinal);
Assert.Equal("Koras.Results.AspNetCore.ResultHttpMapper", warning.Category);
```

This pins the category name, the level, and the content of the suppression warning — the exact
things `docs/troubleshooting/logging.md` documents for consumers.

## Writing a new HTTP integration test

1. Add a `[Fact]`/`[Theory]` to `MinimalApiIntegrationTests` (Minimal API surface) or
   `MvcIntegrationTests` (MVC surface), or create a new class in
   `tests/Koras.Results.IntegrationTests/AspNetCore/` for a new concern.
2. Start a host with only what the scenario needs:

   ```csharp
   using var host = await TestHostFactory.StartAsync(
       endpoints => endpoints.MapGet("/thing", () =>
           Result.Failure<int>(Error.Conflict("Thing.Locked", "m")).ToHttpResult()),
       options => options.MapErrorCode("Thing.Locked", StatusCodes.Status423Locked));
   using var client = host.GetTestClient();
   ```

3. Assert on the *HTTP* level — status code, `Content-Type`
   (`application/problem+json`), and the parsed body via
   `response.Content.ReadFromJsonAsync<JsonElement>()`. Prefer asserting the response the client
   actually sees over any intermediate object.
4. For MVC scenarios, add an action to `MvcTestController` (or a new controller in the test
   assembly) and start the host with `addControllers: true`.
5. Dispose the host and client (`using`), and keep each test's host configuration self-contained
   — hosts are cheap to start and tests must not share state.

## MediatR pipeline testing

`tests/Koras.Results.IntegrationTests/MediatR/ValidationBehaviorTests.cs` verifies
`ValidationBehavior<,>` through a **real MediatR pipeline resolved from DI**, not by invoking
`Handle` directly:

```csharp
private static ServiceProvider BuildProvider() =>
    new ServiceCollection()
        .AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ValidationBehaviorTests).Assembly))
        .AddValidatorsFromAssemblyContaining<ValidationBehaviorTests>()
        .AddKorasResultsValidationBehavior()
        .BuildServiceProvider();
```

Requests, handlers, and FluentValidation validators are declared as nested types in the test
class (e.g. `CreateUser`/`CreateUserValidator`/`CreateUserHandler`), so assembly scanning finds
them exactly as it would in an application. The suite covers:

- valid requests reaching the handler (observed via a `WasInvoked` flag);
- invalid requests short-circuiting with a `ValidationError` — and the handler *not* running;
- multiple validators for one request aggregating their failures;
- `IRequest<Result>` (void) responses short-circuiting the same way;
- requests with no validators passing straight through;
- the deliberate `InvalidOperationException` when the response type is not `Result`/`Result<T>`
  (message asserted to contain the guidance text);
- cancellation propagating through async validator rules.

To add a MediatR scenario: define the request/handler/validator as nested types in the test class
(they are auto-registered by the assembly scan) and send through
`provider.GetRequiredService<IMediator>()`.

## Running the suite

```bash
dotnet test tests/Koras.Results.IntegrationTests -c Release          # all TFMs
dotnet test tests/Koras.Results.IntegrationTests -c Release -f net8.0 # one TFM
```

CI runs it on every push and pull request across net8.0/net9.0/net10.0 with coverage collection
(`.github/workflows/test.yml`).
