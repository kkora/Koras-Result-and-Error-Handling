# ASP.NET Core MVC Controllers with Koras.Results

This guide covers the full MVC stack end-to-end: MediatR handlers returning `Result<T>`, the
validation behavior short-circuiting invalid commands, and controllers converting with
`ToActionResult`. It mirrors [`samples/WebApi.Sample`](../../samples/WebApi.Sample).

## Packages

```bash
dotnet add package Koras.Results.AspNetCore     # brings in Koras.Results
dotnet add package Koras.Results.FluentValidation
dotnet add package Koras.Results.MediatR        # pinned to MediatR [12.4, 13.0) — the Apache-2.0 line
```

## Registration

```csharp
using FluentValidation;
using Koras.Results.AspNetCore;
using Koras.Results.MediatR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddKorasResults();   // options + default IErrorMessageLocalizer
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddKorasResultsValidationBehavior();
builder.Services.AddSingleton<UserRepository>();

var app = builder.Build();
app.MapControllers();
app.Run();
```

`AddKorasResults()` accepts an optional `Action<KorasResultsOptions>` for status-code remapping,
custom `type` URIs, and exposure policies — see the [configuration reference](../configuration/all-options.md).

## Handlers return results, never throw for expected failures

```csharp
using Koras.Results;
using MediatR;

public sealed record UserDto(Guid Id, string Email, string DisplayName);

public static class UserErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("User.NotFound", $"No user with id '{id}'.");

    public static Error DuplicateEmail(string email) =>
        Error.Conflict("User.DuplicateEmail", $"A user with email '{email}' already exists.");
}

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
```

Invalid commands never reach the handler: `ValidationBehavior` runs all registered validators and
short-circuits with a failed result carrying a `ValidationError`.

## Controllers: the ToActionResult family

Controllers stay one-liners. They never inspect errors — the extension methods project failures
into RFC 9457 `application/problem+json` using the registered options.

```csharp
using Koras.Results.AspNetCore;
using MediatR;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("users")]
public sealed class UsersController(IMediator mediator) : ControllerBase
{
    // Custom success factory: 201 Created with a Location header via CreatedAtAction.
    [HttpPost]
    public async Task<IActionResult> Register(RegisterUser command, CancellationToken cancellationToken) =>
        (await mediator.Send(command, cancellationToken))
            .ToActionResult(user => CreatedAtAction(nameof(Get), new { id = user.Id }, user));

    // Result<T> -> 200 OK with JSON body, or a problem response.
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        (await mediator.Send(new GetUser(id), cancellationToken)).ToActionResult();

    // Result -> 204 No Content, or a problem response.
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        (await mediator.Send(new DeleteUser(id), cancellationToken)).ToActionResult();
}
```

The complete family in `Koras.Results.AspNetCore`:

| Method | Success | Failure |
|---|---|---|
| `Result.ToActionResult()` | 204 No Content | ProblemDetails |
| `Result<T>.ToActionResult()` | 200 OK (JSON) | ProblemDetails |
| `Result<T>.ToActionResult(Func<T, IActionResult>)` | your factory (e.g. `CreatedAtAction`) | ProblemDetails |
| `Result<T>.ToActionResultOf<T>()` | `ActionResult<T>` carrying the value | ProblemDetails |
| `Task<Result>.ToActionResultAsync()` / `Task<Result<T>>.ToActionResultAsync()` | awaits, then as above | ProblemDetails |

`ToActionResultOf<T>` suits actions declared as `ActionResult<T>` (useful for OpenAPI metadata):

```csharp
[HttpGet("{id:guid}")]
public async Task<ActionResult<UserDto>> GetTyped(Guid id, CancellationToken ct) =>
    (await mediator.Send(new GetUser(id), ct)).ToActionResultOf();
```

## What failures look like on the wire

| Scenario | Status | Payload highlights |
|---|---|---|
| Validation short-circuit | 400 | `errors` dictionary keyed by property, `errorCode: "Validation.Failed"` |
| `User.NotFound` | 404 | `errorCode: "User.NotFound"`, `traceId` |
| `User.DuplicateEmail` (Conflict) | 409 | `errorCode: "User.DuplicateEmail"` |
| `Error.Unexpected` | 500 | detail replaced by a generic message (secure default) |

```bash
curl -s $BASE/users -H 'content-type: application/json' \
  -d '{"email":"not-an-email","displayName":""}' | jq '.errors'
```

## Notes

- Options and the localizer are resolved from `HttpContext.RequestServices` when the response
  executes; if `AddKorasResults` was never called, built-in defaults apply.
- Each mapping is logged at Debug under the category
  `Koras.Results.AspNetCore.ResultHttpMapper` — see the [logging guide](logging.md).

## Related documentation

- [WebApi.Sample source](../../samples/WebApi.Sample)
- [Minimal API guide](minimal-api.md)
- [Dependency injection guide](dependency-injection.md)
- [Configuration reference](../configuration/all-options.md)
