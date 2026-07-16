// Minimal API sample: Results become HTTP responses with one extension method per endpoint.
// Failures turn into RFC 9457 application/problem+json automatically.
using FluentValidation;
using Koras.Results;
using Koras.Results.AspNetCore;
using Koras.Results.FluentValidation;
using MinimalApiSample;

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

// GET: NotFound error -> 404 problem details.
app.MapGet("/todos/{id:guid}", (Guid id, TodoStore store) =>
    store.Find(id).ToHttpResult());

// GET all: always succeeds.
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
