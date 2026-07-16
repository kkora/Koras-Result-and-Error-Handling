# Minimal APIs with Koras.Results

With `Koras.Results.AspNetCore`, every Minimal API endpoint ends in a single conversion call and
every failure becomes a correct RFC 9457 `application/problem+json` response. This guide mirrors
[`samples/MinimalApi.Sample`](../../samples/MinimalApi.Sample).

## Packages

```bash
dotnet add package Koras.Results.AspNetCore      # brings in Koras.Results
dotnet add package Koras.Results.FluentValidation # optional, for validator integration
```

## Registration and options

```csharp
using FluentValidation;
using Koras.Results;
using Koras.Results.AspNetCore;
using Koras.Results.FluentValidation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKorasResults(options =>
{
    // House rule: business failures are 400s in this API (default is 422).
    options.MapErrorType(ErrorType.Failure, StatusCodes.Status400BadRequest);
    options.ProblemTypeUriFactory = error => $"https://errors.example.com/{error.Code}";
});
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddSingleton<TodoStore>();

var app = builder.Build();
```

## Endpoints: the ToHttpResult family

```csharp
// GET one: NotFound error -> 404 problem details.
app.MapGet("/todos/{id:guid}", (Guid id, TodoStore store) =>
    store.Find(id).ToHttpResult());

// GET all: always succeeds -> 200 with a JSON array.
app.MapGet("/todos", (TodoStore store) =>
    Result.Success(store.All()).ToHttpResult());

// POST: validation failure -> 400 with an errors dictionary; success -> 201 with Location.
app.MapPost("/todos", async (CreateTodo command, IValidator<CreateTodo> validator, TodoStore store, CancellationToken ct) =>
    (await validator.ValidateToResultAsync(command, ct))
        .Bind(valid => store.Create(valid.Title!))
        .ToCreatedHttpResult(todo => $"/todos/{todo.Id}"));

// POST complete: domain rule failure -> 400 (remapped above from the default 422).
app.MapPost("/todos/{id:guid}/complete", (Guid id, TodoStore store) =>
    store.Complete(id).ToHttpResult());

// DELETE: void success -> 204.
app.MapDelete("/todos/{id:guid}", (Guid id, TodoStore store) =>
    store.Delete(id).ToHttpResult());

app.Run();
```

The complete family:

| Method | Success | Failure |
|---|---|---|
| `Result.ToHttpResult()` | 204 No Content | ProblemDetails |
| `Result<T>.ToHttpResult()` | 200 OK (JSON) | ProblemDetails |
| `Result<T>.ToHttpResult(Func<T, IResult>)` | your factory (e.g. `Results.Accepted()`) | ProblemDetails |
| `Result<T>.ToCreatedHttpResult(Func<T, string>)` | 201 Created + Location + body | ProblemDetails |
| `Task<Result>.ToHttpResultAsync()` and `Task<Result<T>>` overloads | awaits, then as above | ProblemDetails |

The async overloads let a pipeline flow straight into the response without an intermediate
variable:

```csharp
app.MapGet("/todos/{id:guid}/title", (Guid id, TodoStore store) =>
    Task.FromResult(store.Find(id))
        .MapAsync(todo => todo.Title)
        .ToHttpResultAsync());
```

## The domain stays HTTP-free

`TodoStore` returns `Result`/`Result<T>` with zero HTTP awareness, using an error catalog with
stable codes:

```csharp
public static class TodoErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("Todo.NotFound", $"No todo with id '{id}'.");

    public static Error AlreadyCompleted(Guid id) =>
        Error.Failure("Todo.AlreadyCompleted", $"Todo '{id}' is already completed.");
}

public Result<Todo> Complete(Guid id)
{
    if (!_todos.TryGetValue(id, out var todo)) return TodoErrors.NotFound(id);
    if (todo.Completed) return TodoErrors.AlreadyCompleted(id);
    var completed = todo with { Completed = true };
    _todos[id] = completed;
    return completed;
}
```

## What failures look like on the wire

```bash
curl -s $BASE/todos -H 'content-type: application/json' -d '{"title":""}' | jq
```

```json
{
  "type": "https://errors.example.com/Validation.Failed",
  "title": "Bad Request",
  "status": 400,
  "errors": { "Title": ["'Title' must not be empty."] },
  "errorCode": "Validation.Failed",
  "traceId": "00-..."
}
```

- `errorCode` is always present — clients can branch on it without parsing messages.
- `traceId` (on by default) joins the client report to your server traces.
- `ValidationError` failures additionally produce the ASP.NET Core-shaped `errors` dictionary.
- `Error.Unexpected` details are suppressed by default; clients see a generic message while the
  original message is logged server-side at Warning.

## Execution model

`ToHttpResult` does not build the response eagerly. It returns an `IResult` that resolves
`KorasResultsOptions`, the `IErrorMessageLocalizer`, and logging from the request's services at
execution time. If `AddKorasResults` was never called, built-in defaults apply — the conversion
never fails for lack of registration.

## Related documentation

- [MinimalApi.Sample source](../../samples/MinimalApi.Sample)
- [MVC controllers guide](aspnet-core.md)
- [Configuration reference](../configuration/all-options.md)
- [Logging guide](logging.md)
