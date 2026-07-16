using Koras.Results.AspNetCore;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace WebApiSample.Users;

[ApiController]
[Route("users")]
public sealed class UsersController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Register(RegisterUser command, CancellationToken cancellationToken) =>
        (await mediator.Send(command, cancellationToken))
            .ToActionResult(user => CreatedAtAction(nameof(Get), new { id = user.Id }, user));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        (await mediator.Send(new GetUser(id), cancellationToken)).ToActionResult();

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        (await mediator.Send(new DeleteUser(id), cancellationToken)).ToActionResult();
}
