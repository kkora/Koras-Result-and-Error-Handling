# Using Koras.Results in Blazor (Server and WebAssembly)

There is no Blazor-specific Koras package, and none is needed: `Koras.Results` is a
zero-dependency value library, so it runs unchanged in Blazor Server and in the browser under
WebAssembly. This guide shows the patterns that work well in components.

```bash
dotnet add package Koras.Results
```

## Services return Result&lt;T&gt;

Whether the service talks to a database (Server) or an HTTP API (WASM), give it a result-shaped
contract so components never need try/catch:

```csharp
using Koras.Results;

public sealed record Order(Guid Id, string Status, decimal Total);

public interface IOrderService
{
    Task<Result<Order>> GetOrderAsync(Guid id, CancellationToken cancellationToken = default);
}
```

A Blazor Server implementation calls the domain directly. A WASM implementation wraps
`HttpClient` — see below.

## Rendering results in components with Match

`Match` folds a result into a `RenderFragment`, which makes the failure branch impossible to
forget:

```razor
@page "/orders/{Id:guid}"
@inject IOrderService Orders

@if (_result is { } result)
{
    @result.Match<Order, RenderFragment>(
        order => @<div class="order">
                     <h2>Order @order.Id</h2>
                     <p>@order.Status — @order.Total.ToString("C")</p>
                  </div>,
        error => @<div class="alert alert-danger" role="alert">
                     @FriendlyMessage(error)
                  </div>)
}
else
{
    <p>Loading…</p>
}

@code {
    [Parameter] public Guid Id { get; set; }

    private Result<Order>? _result;

    protected override async Task OnParametersSetAsync() =>
        _result = await Orders.GetOrderAsync(Id);

    private static string FriendlyMessage(Error error) => error.Type switch
    {
        ErrorType.NotFound => "We couldn't find that order.",
        ErrorType.Unavailable => "The service is temporarily unavailable. Please try again.",
        _ => error.Message,
    };
}
```

Branching on `error.Type` (or `error.Code` for specific cases) keeps UI copy decoupled from
server messages. For form-style UIs, pattern-match `ValidationError` and render its
`FieldErrors` next to the corresponding inputs.

## WASM: mapping ProblemDetails responses back to errors

When the backend uses `Koras.Results.AspNetCore`, failures arrive as
`application/problem+json` with an `errorCode` extension and a status code derived from the
error type. On the client, translate that wire shape back into an `Error` so the rest of the app
stays in result-land:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Koras.Results;

public sealed class ApiOrderService(HttpClient http) : IOrderService
{
    public async Task<Result<Order>> GetOrderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"orders/{id}", cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var order = await response.Content.ReadFromJsonAsync<Order>(cancellationToken: cancellationToken);
            return order is null
                ? Error.Unexpected("Api.EmptyBody", "The server returned an empty response.")
                : order;
        }

        return await ReadProblemAsync(response, cancellationToken);
    }

    private static async Task<Error> ReadProblemAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var type = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => ErrorType.Validation,
            HttpStatusCode.Unauthorized => ErrorType.Unauthorized,
            HttpStatusCode.Forbidden => ErrorType.Forbidden,
            HttpStatusCode.NotFound => ErrorType.NotFound,
            HttpStatusCode.Conflict => ErrorType.Conflict,
            HttpStatusCode.UnprocessableEntity => ErrorType.Failure,
            HttpStatusCode.ServiceUnavailable => ErrorType.Unavailable,
            _ => ErrorType.Unexpected,
        };

        try
        {
            using var problem = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = problem.RootElement;
            var code = root.TryGetProperty("errorCode", out var c) ? c.GetString() : null;
            var detail = root.TryGetProperty("detail", out var d) ? d.GetString() : null;

            var error = new Error(code ?? $"Http.{(int)response.StatusCode}",
                                  detail ?? response.ReasonPhrase ?? "The request failed.",
                                  type);
            return root.TryGetProperty("traceId", out var t) && t.GetString() is { } traceId
                ? error.WithMetadata("traceId", traceId)
                : error;
        }
        catch (JsonException)
        {
            return new Error($"Http.{(int)response.StatusCode}", "The request failed.", type);
        }
    }
}
```

Because the server always sets `errorCode`, the client can branch on exactly the same stable
codes the domain uses (`"Todo.NotFound"`, `"User.DuplicateEmail"`, …). Keeping the `traceId` in
metadata lets a "report a problem" button hand support staff a trace to look up.

## Blazor Server nuance

In Server components the service call happens on the server, so you can also use
`Koras.Results.OpenTelemetry` there (`TagCurrentActivity`) and normal `ILogger` side effects via
`Tap`/`TapError`. In WASM there is no server-side activity; keep telemetry on the API side and
correlate through the `traceId` extension.

## Related documentation

- [Minimal API guide](minimal-api.md) — the server side of this contract
- [Telemetry guide](telemetry.md) — how `traceId` joins client reports to traces
- [Public API design](../api/public-api-design.md)
