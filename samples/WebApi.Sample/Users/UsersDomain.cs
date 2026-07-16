using FluentValidation;
using Koras.Results;
using MediatR;

namespace WebApiSample.Users;

public sealed record UserDto(Guid Id, string Email, string DisplayName);

public static class UserErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("User.NotFound", $"No user with id '{id}'.");

    public static Error DuplicateEmail(string email) =>
        Error.Conflict("User.DuplicateEmail", $"A user with email '{email}' already exists.");
}

// ── Commands & queries ─────────────────────────────────────────────────────

public sealed record RegisterUser(string Email, string DisplayName) : IRequest<Result<UserDto>>;

public sealed class RegisterUserValidator : AbstractValidator<RegisterUser>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithErrorCode("User.EmailInvalid");
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(64);
    }
}

public sealed class RegisterUserHandler(UserRepository repository) : IRequestHandler<RegisterUser, Result<UserDto>>
{
    public Task<Result<UserDto>> Handle(RegisterUser request, CancellationToken cancellationToken) =>
        Task.FromResult(repository.Add(request.Email, request.DisplayName));
}

public sealed record GetUser(Guid Id) : IRequest<Result<UserDto>>;

public sealed class GetUserHandler(UserRepository repository) : IRequestHandler<GetUser, Result<UserDto>>
{
    public Task<Result<UserDto>> Handle(GetUser request, CancellationToken cancellationToken) =>
        Task.FromResult(repository.Find(request.Id));
}

public sealed record DeleteUser(Guid Id) : IRequest<Result>;

public sealed class DeleteUserHandler(UserRepository repository) : IRequestHandler<DeleteUser, Result>
{
    public Task<Result> Handle(DeleteUser request, CancellationToken cancellationToken) =>
        Task.FromResult(repository.Delete(request.Id));
}

// ── Infrastructure ─────────────────────────────────────────────────────────

public sealed class UserRepository
{
    private readonly Dictionary<Guid, UserDto> _users = [];
    private readonly Lock _gate = new();

    public Result<UserDto> Add(string email, string displayName)
    {
        lock (_gate)
        {
            if (_users.Values.Any(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)))
            {
                return UserErrors.DuplicateEmail(email);
            }

            var user = new UserDto(Guid.NewGuid(), email, displayName);
            _users[user.Id] = user;
            return user;
        }
    }

    public Result<UserDto> Find(Guid id)
    {
        lock (_gate)
        {
            return _users.TryGetValue(id, out var user) ? user : UserErrors.NotFound(id);
        }
    }

    public Result Delete(Guid id)
    {
        lock (_gate)
        {
            return _users.Remove(id) ? Result.Success() : UserErrors.NotFound(id);
        }
    }
}
