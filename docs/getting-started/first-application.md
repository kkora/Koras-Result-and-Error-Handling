# Your First Application

This walkthrough builds the Minimal API todo service from [`samples/MinimalApi.Sample`](../../samples/MinimalApi.Sample/) from scratch: a small API where every endpoint ends in one conversion call and every failure — validation, missing resource, or domain rule — becomes a correct RFC 9457 `application/problem+json` response.

## Step 0 — Project setup

```bash
dotnet new web -n TodoApi
cd TodoApi
dotnet add package Koras.Results
dotnet add package Koras.Results.AspNetCore
dotnet add package Koras.Results.FluentValidation
dotnet add package FluentValidation.DependencyInjectionExtensions
```

## Step 1 — Model, command, validator

Create `Todos.cs`. The domain model is a plain record; the create command is validated with FluentValidation. Note the `WithErrorCode` calls — those codes flow all the way into the HTTP response:

```csharp
using FluentValidation;
using Koras.Results;

namespace TodoApi;

public sealed record Todo(Guid Id, string Title, bool Completed);

public sealed record CreateTodo(string? Title);

public sealed class CreateTodoValidator : AbstractValidator<CreateTodo>
{
    public CreateTodoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithErrorCode("Todo.TitleRequired");
        RuleFor(x => x.Title).MaximumLength(200).WithErrorCode("Todo.TitleTooLong");
    }
}
```

## Step 2 — The error catalog

Still in `Todos.cs`, list every failure the todo domain can produce as static factory methods. This is the single most important habit the library encourages: errors are a designed, reviewable surface, not ad-hoc strings scattered through the code.

```csharp
public static class TodoErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("Todo.NotFound", $"No todo with id '{id}'.");

    public static Error AlreadyCompleted(Guid id) =>
        Error.Failure("Todo.AlreadyCompleted", $"Todo '{id}' is already completed.");
}
```

- `Error.NotFound` sets `ErrorType.NotFound` → projected to HTTP 404.
- `Error.Failure` marks a domain-rule rejection → 422 by default (we will remap it to 400 in step 4, because this API's house rule is "business failures are 400s").
- Codes follow the `Subject.Condition` convention and never change meaning once shipped.

## Step 3 — The store: domain code with zero HTTP awareness

The store returns `Result<Todo>` / `Result` and never throws for expected conditions. Implicit conversions keep it readable: returning a `Todo` produces a success, returning an `Error` produces a failure.

```csharp
public sealed class TodoStore
{
    private readonly Dictionary<Guid, Todo> _todos = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<Todo> All()
    {
        lock (_gate) { return [.. _todos.Values]; }
    }

    public Result<Todo> Find(Guid id)
    {
        lock (_gate)
        {
            return _todos.TryGetValue(id, out var todo) ? todo : TodoErrors.NotFound(id);
        }
    }

    public Result<Todo> Create(string title)
    {
        var todo = new Todo(Guid.NewGuid(), title, Completed: false);
        lock (_gate) { _todos[todo.Id] = todo; }
        return todo;
    }

    public Result<Todo> Complete(Guid id)
    {
        lock (_gate)
        {
            if (!_todos.TryGetValue(id, out var todo)) return TodoErrors.NotFound(id);
            if (todo.Completed) return TodoErrors.AlreadyCompleted(id);

            var completed = todo with { Completed = true };
            _todos[id] = completed;
            return completed;
        }
    }

    public Result Delete(Guid id)
    {
        lock (_gate)
        {
            return _todos.Remove(id) ? Result.Success() : TodoErrors.NotFound(id);
        }
    }
}
```

Notice `Complete`: two distinct failures (`NotFound`, `AlreadyCompleted`) with different semantics, expressed in the return value instead of two exception types. `Delete` returns the non-generic `Result` because success carries no value.

## Step 4 — Wire up `Program.cs`

```csharp
using FluentValidation;
using Koras.Results;
using Koras.Results.AspNetCore;
using Koras.Results.FluentValidation;
using TodoApi;

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

// POST complete: domain rule failure -> 400 (remapped above from 422).
app.MapPost("/todos/{id:guid}/complete", (Guid id, TodoStore store) =>
    store.Complete(id).ToHttpResult());

// DELETE: void success -> 204.
app.MapDelete("/todos/{id:guid}", (Guid id, TodoStore store) =>
    store.Delete(id).ToHttpResult());

app.Run();
```

Three things to study in the POST endpoint:

1. **`ValidateToResultAsync`** runs the FluentValidation validator and returns `Result<CreateTodo>` — a `ValidationError` carrying per-field errors on failure, never a thrown `ValidationException`.
2. **`Bind`** chains the next fallible step. If validation failed, `store.Create` is never invoked and the validation error flows through untouched (short-circuit semantics).
3. **`ToCreatedHttpResult`** terminates the pipeline: success → `201 Created` with a `Location` header and the todo as the body; any failure → ProblemDetails.

## Step 5 — Exercise every path

```bash
dotnet run
BASE=http://localhost:5000

# Create (201 + Location header)
curl -si $BASE/todos -H 'content-type: application/json' -d '{"title":"Write docs"}'

# Validation failure (400 + errors dictionary)
curl -s $BASE/todos -H 'content-type: application/json' -d '{"title":""}'
# { "type": "https://errors.example.com/Validation.Failed", "title": "Bad Request",
#   "status": 400, "errors": { "Title": ["'Title' must not be empty."] },
#   "errorCode": "Validation.Failed", "traceId": "..." }

# Not found (404, errorCode "Todo.NotFound")
curl -s $BASE/todos/00000000-0000-0000-0000-000000000001

# Domain rule: complete twice (second call: 400, errorCode "Todo.AlreadyCompleted")
ID=$(curl -s $BASE/todos -H 'content-type: application/json' -d '{"title":"x"}' | jq -r .id)
curl -s -o /dev/null -w '%{http_code}\n' -X POST $BASE/todos/$ID/complete   # 200
curl -s -X POST $BASE/todos/$ID/complete | jq '.errorCode'                  # "Todo.AlreadyCompleted"

# Delete (204)
curl -s -o /dev/null -w '%{http_code}\n' -X DELETE $BASE/todos/$ID
```

## What you built

- A domain layer (`TodoStore`, `TodoErrors`) that depends only on the zero-dependency core and has no idea HTTP exists.
- An HTTP layer where every endpoint is one expression ending in a conversion call — no `try/catch`, no manual status-code selection, no exception filters.
- A consistent error contract: every failure response carries `status`, `errorCode`, and `traceId`, and validation failures include a per-field `errors` dictionary.

## Next steps

- [Dependency injection](dependency-injection.md) — what `AddKorasResults` registers and how to replace pieces
- [Configuration](configuration.md) — the full `KorasResultsOptions` tour
- [Concepts: lifecycle](../concepts/lifecycle.md) — how a result flows from creation to consumption
