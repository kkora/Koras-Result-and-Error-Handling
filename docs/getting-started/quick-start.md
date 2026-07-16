# Quick Start

From zero to a correct RFC 9457 `application/problem+json` response in five minutes. You will define typed errors, return `Result<T>` from a service, and let one extension method turn every outcome into the right HTTP response.

## 1. Create a project and install packages

```bash
dotnet new web -n QuickStart
cd QuickStart
dotnet add package Koras.Results
dotnet add package Koras.Results.AspNetCore
```

## 2. Define an error catalog

Errors are immutable values with a stable machine-readable `Code`, a human-readable `Message`, and a semantic `ErrorType`. Group them in a static class per subject so every failure your domain can produce is discoverable in one place:

```csharp
using Koras.Results;

public static class TodoErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("Todo.NotFound", $"No todo with id '{id}'.");

    public static Error AlreadyCompleted(Guid id) =>
        Error.Failure("Todo.AlreadyCompleted", $"Todo '{id}' is already completed.");
}
```

`Error.NotFound(...)`, `Error.Failure(...)`, `Error.Conflict(...)` and the other factories set the `ErrorType`, which later drives the HTTP status code — the domain itself never mentions HTTP.

## 3. Return `Result<T>` from a service

```csharp
using Koras.Results;

public sealed record Todo(Guid Id, string Title, bool Completed);

public sealed class TodoStore
{
    private readonly Dictionary<Guid, Todo> _todos = [];

    public Result<Todo> Create(string title)
    {
        var todo = new Todo(Guid.NewGuid(), title, Completed: false);
        _todos[todo.Id] = todo;
        return todo;                          // implicit conversion: T -> success
    }

    public Result<Todo> Find(Guid id) =>
        _todos.TryGetValue(id, out var todo)
            ? todo                            // success
            : TodoErrors.NotFound(id);        // implicit conversion: Error -> failure
}
```

No exceptions, no null returns: the signature `Result<Todo>` tells every caller that this operation can fail and forces them to deal with it.

## 4. Map endpoints with `ToHttpResult()`

```csharp
using Koras.Results.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddKorasResults();           // options + localizer (defaults are production-safe)
builder.Services.AddSingleton<TodoStore>();

var app = builder.Build();

app.MapGet("/todos/{id:guid}", (Guid id, TodoStore store) =>
    store.Find(id).ToHttpResult());           // 200 OK or problem+json

app.MapPost("/todos", (CreateTodo command, TodoStore store) =>
    store.Create(command.Title)
         .ToCreatedHttpResult(t => $"/todos/{t.Id}"));  // 201 + Location or problem+json

app.Run();

public sealed record CreateTodo(string Title);
```

That is the entire HTTP mapping. Success on `Result<T>` becomes `200 OK` (or `201 Created` with `ToCreatedHttpResult`); success on a non-generic `Result` becomes `204 No Content`; every failure becomes ProblemDetails with a status derived from its `ErrorType`.

## 5. See the failure output

```bash
dotnet run
curl -s http://localhost:5000/todos/00000000-0000-0000-0000-000000000001
```

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "No todo with id '00000000-0000-0000-0000-000000000001'.",
  "errorCode": "Todo.NotFound",
  "traceId": "00-8f1c6c2a5f9e4b1e9d3a7c0b2e5f8a1d-4b2e5f8a1d9d3a7c-01"
}
```

What you get for free:

- **`status: 404`** — projected from `ErrorType.NotFound` (Failure → 422, Validation → 400, Conflict → 409, Unauthorized → 401, Forbidden → 403, Unavailable → 503, Unexpected → 500).
- **`errorCode`** — the stable machine contract clients can switch on; messages may change, codes never change meaning.
- **`traceId`** — the current trace identifier for correlating client reports with server telemetry (on by default).
- **Leak safety** — `Unexpected` errors have their `detail` replaced with generic text by default, so internal exception messages never reach clients.

## Where to go next

- [Your first application](first-application.md) — the same API, built out with FluentValidation and domain rules
- [Configuration](configuration.md) — remap status codes, custom `type` URIs, metadata exposure
- [Concepts: overview](../concepts/overview.md) — why results instead of exceptions
