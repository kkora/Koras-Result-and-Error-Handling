using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Koras.Results.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;

namespace Koras.Results.IntegrationTests.AspNetCore;

public class MvcIntegrationTests
{
    private static Task<Microsoft.Extensions.Hosting.IHost> StartHostAsync() =>
        TestHostFactory.StartAsync(_ => { }, addControllers: true);

    [Fact]
    public async Task Success_returns_200_with_body()
    {
        using var host = await StartHostAsync();
        using var client = host.GetTestClient();

        var response = await client.GetAsync("/mvc-test/ok");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Ada", body.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Void_success_returns_204()
    {
        using var host = await StartHostAsync();
        using var client = host.GetTestClient();

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/mvc-test/gone")).StatusCode);
    }

    [Fact]
    public async Task NotFound_error_returns_404_problem_details()
    {
        using var host = await StartHostAsync();
        using var client = host.GetTestClient();

        var response = await client.GetAsync("/mvc-test/missing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("User.NotFound", problem.GetProperty("errorCode").GetString());
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Validation_error_returns_400_with_errors_dictionary()
    {
        using var host = await StartHostAsync();
        using var client = host.GetTestClient();

        var response = await client.PostAsync("/mvc-test/validate", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Email is required.", problem.GetProperty("errors").GetProperty("Email")[0].GetString());
    }

    [Fact]
    public async Task ActionResultOf_returns_value_or_problem()
    {
        using var host = await StartHostAsync();
        using var client = host.GetTestClient();

        var ok = await client.GetAsync("/mvc-test/typed/1");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var body = await ok.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("id").GetInt32());

        var notFound = await client.GetAsync("/mvc-test/typed/0");
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        Assert.Equal("application/problem+json", notFound.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Custom_success_factory_is_used()
    {
        using var host = await StartHostAsync();
        using var client = host.GetTestClient();

        var response = await client.PostAsync("/mvc-test/created", content: null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/mvc-test/typed/9", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Async_sugar_overloads_work_in_actions()
    {
        using var host = await StartHostAsync();
        using var client = host.GetTestClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/mvc-test/async-ok")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync("/mvc-test/async-conflict")).StatusCode);
    }
}

[ApiController]
[Route("mvc-test")]
public sealed class MvcTestController : ControllerBase
{
    [HttpGet("ok")]
    public IActionResult GetOk() => Result.Success(new PersonDto("Ada")).ToActionResult();

    [HttpDelete("gone")]
    public IActionResult Delete() => Result.Success().ToActionResult();

    [HttpGet("missing")]
    public IActionResult GetMissing() =>
        Result.Failure<PersonDto>(Error.NotFound("User.NotFound", "The user does not exist.")).ToActionResult();

    [HttpPost("validate")]
    public IActionResult Validate() =>
        Result.Failure<PersonDto>(new ValidationError(new FieldError("Email", "Email is required."))).ToActionResult();

    [HttpGet("typed/{id:int}")]
    public ActionResult<ItemDto> GetTyped(int id) =>
        (id > 0
            ? Result.Success(new ItemDto(id))
            : Result.Failure<ItemDto>(Error.NotFound("Item.NotFound", "No such item.")))
        .ToActionResultOf();

    [HttpPost("created")]
    public IActionResult Create() =>
        Result.Success(new ItemDto(9)).ToActionResult(item =>
            new CreatedResult($"/mvc-test/typed/{item.Id}", item));

    [HttpGet("async-ok")]
    public Task<IActionResult> GetAsyncOk() =>
        Task.FromResult(Result.Success(new ItemDto(1))).ToActionResultAsync();

    [HttpGet("async-conflict")]
    public Task<IActionResult> GetAsyncConflict() =>
        Task.FromResult(Result.Failure(Error.Conflict("X.Y", "conflict"))).ToActionResultAsync();

    public sealed record PersonDto(string Name);

    public sealed record ItemDto(int Id);
}
