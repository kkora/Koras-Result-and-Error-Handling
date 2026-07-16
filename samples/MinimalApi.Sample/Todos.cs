using FluentValidation;
using Koras.Results;

namespace MinimalApiSample;

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

public static class TodoErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("Todo.NotFound", $"No todo with id '{id}'.");

    public static Error AlreadyCompleted(Guid id) =>
        Error.Failure("Todo.AlreadyCompleted", $"Todo '{id}' is already completed.");
}

public sealed class TodoStore
{
    private readonly Dictionary<Guid, Todo> _todos = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<Todo> All()
    {
        lock (_gate)
        {
            return [.. _todos.Values];
        }
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
        lock (_gate)
        {
            _todos[todo.Id] = todo;
        }

        return todo;
    }

    public Result<Todo> Complete(Guid id)
    {
        lock (_gate)
        {
            if (!_todos.TryGetValue(id, out var todo))
            {
                return TodoErrors.NotFound(id);
            }

            if (todo.Completed)
            {
                return TodoErrors.AlreadyCompleted(id);
            }

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
