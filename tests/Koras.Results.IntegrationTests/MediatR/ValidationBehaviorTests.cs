using FluentValidation;
using Koras.Results.MediatR;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Koras.Results.IntegrationTests.MediatR;

/// <summary>
/// Exercises <see cref="ValidationBehavior{TRequest, TResponse}"/> through a real MediatR
/// pipeline resolved from dependency injection.
/// </summary>
public class ValidationBehaviorTests
{
    private static ServiceProvider BuildProvider() =>
        new ServiceCollection()
            .AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ValidationBehaviorTests).Assembly))
            .AddValidatorsFromAssemblyContaining<ValidationBehaviorTests>()
            .AddKorasResultsValidationBehavior()
            .BuildServiceProvider();

    [Fact]
    public async Task Valid_requests_reach_the_handler()
    {
        await using var provider = BuildProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new CreateUser("ada@example.com", 30));

        Assert.True(result.IsSuccess);
        Assert.Equal("ada@example.com", result.Value.Email);
        Assert.True(CreateUserHandler.WasInvoked);
    }

    [Fact]
    public async Task Invalid_requests_short_circuit_with_a_failed_result()
    {
        CreateUserHandler.WasInvoked = false;
        await using var provider = BuildProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new CreateUser(string.Empty, 10));

        Assert.True(result.IsFailure);
        var error = Assert.IsType<ValidationError>(result.Error);
        Assert.Equal(2, error.FieldErrors.Count);
        Assert.False(CreateUserHandler.WasInvoked, "the handler must not run for invalid requests");
    }

    [Fact]
    public async Task Multiple_validators_aggregate_their_failures()
    {
        await using var provider = BuildProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new RenameUser(string.Empty));

        var error = Assert.IsType<ValidationError>(result.Error);
        // Two validators contribute one failure each.
        Assert.Equal(2, error.FieldErrors.Count);
    }

    [Fact]
    public async Task Void_result_responses_short_circuit_too()
    {
        await using var provider = BuildProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new DeleteUser(Guid.Empty));

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task Requests_without_validators_pass_through()
    {
        await using var provider = BuildProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new Ping());

        Assert.True(result.IsSuccess);
        Assert.Equal("pong", result.Value);
    }

    [Fact]
    public async Task Non_result_responses_throw_on_validation_failure_instead_of_swallowing()
    {
        await using var provider = BuildProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.Send(new LegacyCreate(string.Empty)));

        Assert.Contains("not Result or Result<T>", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancellation_propagates_through_the_behavior()
    {
        await using var provider = BuildProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => mediator.Send(new SlowValidated("x"), cts.Token));
    }

    // ── Requests, handlers, validators ─────────────────────────────────────

    public sealed record UserDto(string Email);

    public sealed record CreateUser(string Email, int Age) : IRequest<Result<UserDto>>;

    public sealed class CreateUserValidator : AbstractValidator<CreateUser>
    {
        public CreateUserValidator()
        {
            RuleFor(x => x.Email).NotEmpty();
            RuleFor(x => x.Age).GreaterThanOrEqualTo(18);
        }
    }

    public sealed class CreateUserHandler : IRequestHandler<CreateUser, Result<UserDto>>
    {
        internal static bool WasInvoked;

        public Task<Result<UserDto>> Handle(CreateUser request, CancellationToken cancellationToken)
        {
            WasInvoked = true;
            return Task.FromResult(Result.Success(new UserDto(request.Email)));
        }
    }

    public sealed record RenameUser(string NewName) : IRequest<Result<string>>;

    public sealed class RenameUserValidatorA : AbstractValidator<RenameUser>
    {
        public RenameUserValidatorA()
        {
            RuleFor(x => x.NewName).NotEmpty().WithMessage("Name required (A).");
        }
    }

    public sealed class RenameUserValidatorB : AbstractValidator<RenameUser>
    {
        public RenameUserValidatorB()
        {
            RuleFor(x => x.NewName).MinimumLength(1).WithMessage("Name too short (B).");
        }
    }

    public sealed class RenameUserHandler : IRequestHandler<RenameUser, Result<string>>
    {
        public Task<Result<string>> Handle(RenameUser request, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(request.NewName));
    }

    public sealed record DeleteUser(Guid Id) : IRequest<Result>;

    public sealed class DeleteUserValidator : AbstractValidator<DeleteUser>
    {
        public DeleteUserValidator()
        {
            RuleFor(x => x.Id).NotEmpty();
        }
    }

    public sealed class DeleteUserHandler : IRequestHandler<DeleteUser, Result>
    {
        public Task<Result> Handle(DeleteUser request, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());
    }

    public sealed record Ping : IRequest<Result<string>>;

    public sealed class PingHandler : IRequestHandler<Ping, Result<string>>
    {
        public Task<Result<string>> Handle(Ping request, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success("pong"));
    }

    public sealed record LegacyCreate(string Email) : IRequest<string>;

    public sealed class LegacyCreateValidator : AbstractValidator<LegacyCreate>
    {
        public LegacyCreateValidator()
        {
            RuleFor(x => x.Email).NotEmpty();
        }
    }

    public sealed class LegacyCreateHandler : IRequestHandler<LegacyCreate, string>
    {
        public Task<string> Handle(LegacyCreate request, CancellationToken cancellationToken) =>
            Task.FromResult("created");
    }

    public sealed record SlowValidated(string Value) : IRequest<Result<string>>;

    public sealed class SlowValidatedValidator : AbstractValidator<SlowValidated>
    {
        public SlowValidatedValidator()
        {
            RuleFor(x => x.Value).MustAsync(async (_, ct) =>
            {
                await Task.Delay(5000, ct);
                return true;
            });
        }
    }

    public sealed class SlowValidatedHandler : IRequestHandler<SlowValidated, Result<string>>
    {
        public Task<Result<string>> Handle(SlowValidated request, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(request.Value));
    }
}
