using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Koras.Results.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Koras.Results.IntegrationTests.AspNetCore;

public class MinimalApiIntegrationTests
{
    [Fact]
    public async Task Success_with_value_returns_200_with_json_body()
    {
        using var host = await TestHostFactory.StartAsync(endpoints =>
            endpoints.MapGet("/ok", () => Result.Success(new { Name = "Ada" }).ToHttpResult()));
        using var client = host.GetTestClient();

        var response = await client.GetAsync("/ok");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Ada", body.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Void_success_returns_204()
    {
        using var host = await TestHostFactory.StartAsync(endpoints =>
            endpoints.MapPost("/no-content", () => Result.Success().ToHttpResult()));
        using var client = host.GetTestClient();

        var response = await client.PostAsync("/no-content", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Created_result_returns_201_with_location()
    {
        using var host = await TestHostFactory.StartAsync(endpoints =>
            endpoints.MapPost("/orders", () =>
                Result.Success(new OrderDto("o-1")).ToCreatedHttpResult(o => $"/orders/{o.Id}")));
        using var client = host.GetTestClient();

        var response = await client.PostAsync("/orders", content: null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/orders/o-1", response.Headers.Location?.ToString());
    }

    [Theory]
    [InlineData("failure", HttpStatusCode.UnprocessableEntity)]
    [InlineData("validation", HttpStatusCode.BadRequest)]
    [InlineData("notFound", HttpStatusCode.NotFound)]
    [InlineData("conflict", HttpStatusCode.Conflict)]
    [InlineData("unauthorized", HttpStatusCode.Unauthorized)]
    [InlineData("forbidden", HttpStatusCode.Forbidden)]
    [InlineData("unavailable", HttpStatusCode.ServiceUnavailable)]
    [InlineData("unexpected", HttpStatusCode.InternalServerError)]
    public async Task Every_error_type_maps_to_its_default_status_with_problem_content_type(
        string kind, HttpStatusCode expectedStatus)
    {
        using var host = await TestHostFactory.StartAsync(endpoints =>
            endpoints.MapGet("/fail/{kind}", (string kind) =>
                Result.Failure<int>(MakeError(kind)).ToHttpResult()));
        using var client = host.GetTestClient();

        var response = await client.GetAsync($"/fail/{kind}");

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)expectedStatus, problem.GetProperty("status").GetInt32());
        Assert.Equal($"Test.{kind}", problem.GetProperty("errorCode").GetString());
        Assert.True(problem.TryGetProperty("traceId", out _), "traceId extension expected by default");
    }

    [Fact]
    public async Task Validation_error_produces_errors_dictionary_matching_aspnetcore_shape()
    {
        var error = new ValidationError(
            new FieldError("Email", "Email is required."),
            new FieldError("Email", "Email is malformed."),
            new FieldError("Age", "Age must be at least 18."));

        using var host = await TestHostFactory.StartAsync(endpoints =>
            endpoints.MapPost("/signup", () => Result.Failure<int>(error).ToHttpResult()));
        using var client = host.GetTestClient();

        var response = await client.PostAsync("/signup", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = problem.GetProperty("errors");
        Assert.Equal(2, errors.GetProperty("Email").GetArrayLength());
        Assert.Equal("Age must be at least 18.", errors.GetProperty("Age")[0].GetString());
        Assert.Equal("Validation.Failed", problem.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Unexpected_error_details_are_suppressed_by_default_and_logged()
    {
        var loggerProvider = new CapturingLoggerProvider();
        using var host = await TestHostFactory.StartAsync(
            endpoints => endpoints.MapGet("/boom", () =>
                Result.Failure<int>(Error.Unexpected("Db.Crash", "Connection string for server X leaked")).ToHttpResult()),
            loggerProvider: loggerProvider);
        using var client = host.GetTestClient();

        var response = await client.GetAsync("/boom");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("An unexpected error occurred.", problem.GetProperty("detail").GetString());
        Assert.DoesNotContain("leaked", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var warning = loggerProvider.Entries.Single(e => e.Level == LogLevel.Warning);
        Assert.Contains("Db.Crash", warning.Message, StringComparison.Ordinal);
        Assert.Equal("Koras.Results.AspNetCore.ResultHttpMapper", warning.Category);
    }

    [Fact]
    public async Task Unexpected_error_details_can_be_opted_in()
    {
        using var host = await TestHostFactory.StartAsync(
            endpoints => endpoints.MapGet("/boom", () =>
                Result.Failure<int>(Error.Unexpected("Db.Crash", "the actual detail")).ToHttpResult()),
            options => options.IncludeUnexpectedErrorDetails = true);
        using var client = host.GetTestClient();

        var problem = await (await client.GetAsync("/boom")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("the actual detail", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Status_mapping_overrides_apply_with_code_over_type_precedence()
    {
        using var host = await TestHostFactory.StartAsync(
            endpoints =>
            {
                endpoints.MapGet("/by-type", () => Result.Failure<int>(Error.Failure("Any.Code", "m")).ToHttpResult());
                endpoints.MapGet("/by-code", () => Result.Failure<int>(Error.Failure("Special.Code", "m")).ToHttpResult());
            },
            options => options
                .MapErrorType(ErrorType.Failure, StatusCodes.Status400BadRequest)
                .MapErrorCode("Special.Code", StatusCodes.Status418ImATeapot));
        using var client = host.GetTestClient();

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/by-type")).StatusCode);
        Assert.Equal((HttpStatusCode)418, (await client.GetAsync("/by-code")).StatusCode);
    }

    [Fact]
    public async Task Metadata_is_hidden_by_default_and_exposed_under_All_policy()
    {
        static IResult Fail() =>
            Result.Failure<int>(Error.Conflict("Order.Duplicate", "dup").WithMetadata("sku", "A-1")).ToHttpResult();

        using (var host = await TestHostFactory.StartAsync(e => e.MapGet("/f", Fail)))
        {
            using var client = host.GetTestClient();
            var problem = await (await client.GetAsync("/f")).Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(problem.TryGetProperty("metadata", out _));
        }

        using (var host = await TestHostFactory.StartAsync(
            e => e.MapGet("/f", Fail),
            options => options.MetadataExposure = MetadataExposurePolicy.All))
        {
            using var client = host.GetTestClient();
            var problem = await (await client.GetAsync("/f")).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("A-1", problem.GetProperty("metadata").GetProperty("sku").GetString());
        }
    }

    [Fact]
    public async Task Custom_type_uri_factory_is_used()
    {
        using var host = await TestHostFactory.StartAsync(
            endpoints => endpoints.MapGet("/f", () =>
                Result.Failure<int>(Error.NotFound("User.NotFound", "m")).ToHttpResult()),
            options => options.ProblemTypeUriFactory = error => $"https://errors.example.com/{error.Code}");
        using var client = host.GetTestClient();

        var problem = await (await client.GetAsync("/f")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("https://errors.example.com/User.NotFound", problem.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Custom_localizer_translates_messages_and_field_messages()
    {
        using var host = await TestHostFactory.StartAsync(
            endpoints =>
            {
                endpoints.MapGet("/f", () => Result.Failure<int>(Error.NotFound("User.NotFound", "original")).ToHttpResult());
                endpoints.MapGet("/v", () => Result.Failure<int>(
                    new ValidationError(new FieldError("Email", "original", "Email.Required"))).ToHttpResult());
            },
            configureServices: services =>
                services.AddSingleton<IErrorMessageLocalizer, PrefixLocalizer>());
        using var client = host.GetTestClient();

        var problem = await (await client.GetAsync("/f")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LOC:User.NotFound", problem.GetProperty("detail").GetString());

        var validation = await (await client.GetAsync("/v")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LOC-FIELD:Email.Required", validation.GetProperty("errors").GetProperty("Email")[0].GetString());
    }

    [Fact]
    public async Task Works_without_AddKorasResults_registration_using_defaults()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureServices(s => s.AddRouting())
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(e => e.MapGet("/f", () =>
                        Result.Failure<int>(Error.NotFound("X.Y", "m")).ToHttpResult()));
                }))
            .StartAsync();
        using var client = host.GetTestClient();

        var response = await client.GetAsync("/f");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Task_sugar_overloads_map_like_their_sync_counterparts()
    {
        using var host = await TestHostFactory.StartAsync(endpoints =>
        {
            endpoints.MapGet("/async-ok", () => Task.FromResult(Result.Success(1)).ToHttpResultAsync());
            endpoints.MapGet("/async-fail", () => Task.FromResult(Result.Failure<int>(Error.NotFound("A", "m"))).ToHttpResultAsync());
            endpoints.MapGet("/async-void", () => Task.FromResult(Result.Success()).ToHttpResultAsync());
            endpoints.MapGet("/async-custom", () => Task.FromResult(Result.Success(5))
                .ToHttpResultAsync(v => Microsoft.AspNetCore.Http.Results.Accepted(value: v)));
        });
        using var client = host.GetTestClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/async-ok")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/async-fail")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/async-void")).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await client.GetAsync("/async-custom")).StatusCode);
    }

    private static Error MakeError(string kind) => kind switch
    {
        "failure" => Error.Failure("Test.failure", "m"),
        "validation" => Error.Validation("Test.validation", "m"),
        "notFound" => Error.NotFound("Test.notFound", "m"),
        "conflict" => Error.Conflict("Test.conflict", "m"),
        "unauthorized" => Error.Unauthorized("Test.unauthorized", "m"),
        "forbidden" => Error.Forbidden("Test.forbidden", "m"),
        "unavailable" => Error.Unavailable("Test.unavailable", "m"),
        _ => Error.Unexpected("Test.unexpected", "m"),
    };

    private sealed record OrderDto(string Id);

    private sealed class PrefixLocalizer : IErrorMessageLocalizer
    {
        public string Localize(Error error, System.Globalization.CultureInfo culture) => $"LOC:{error.Code}";

        public string LocalizeField(FieldError fieldError, System.Globalization.CultureInfo culture) =>
            $"LOC-FIELD:{fieldError.Code ?? fieldError.PropertyName}";
    }
}
