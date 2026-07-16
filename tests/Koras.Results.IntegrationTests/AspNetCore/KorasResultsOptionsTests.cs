using Koras.Results.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Koras.Results.IntegrationTests.AspNetCore;

public class KorasResultsOptionsTests
{
    [Fact]
    public void Default_status_map_covers_every_error_type()
    {
        var options = new KorasResultsOptions();

        Assert.Equal(422, options.GetStatusCode(Error.Failure("A", "m")));
        Assert.Equal(400, options.GetStatusCode(Error.Validation("A", "m")));
        Assert.Equal(404, options.GetStatusCode(Error.NotFound("A", "m")));
        Assert.Equal(409, options.GetStatusCode(Error.Conflict("A", "m")));
        Assert.Equal(401, options.GetStatusCode(Error.Unauthorized("A", "m")));
        Assert.Equal(403, options.GetStatusCode(Error.Forbidden("A", "m")));
        Assert.Equal(503, options.GetStatusCode(Error.Unavailable("A", "m")));
        Assert.Equal(500, options.GetStatusCode(Error.Unexpected("A", "m")));
    }

    [Fact]
    public void Code_override_takes_precedence_over_type_override()
    {
        var options = new KorasResultsOptions()
            .MapErrorType(ErrorType.Failure, 400)
            .MapErrorCode("Special", 418);

        Assert.Equal(400, options.GetStatusCode(Error.Failure("Ordinary", "m")));
        Assert.Equal(418, options.GetStatusCode(Error.Failure("Special", "m")));
    }

    [Theory]
    [InlineData(99)]
    [InlineData(600)]
    [InlineData(-1)]
    public void Invalid_status_codes_are_rejected_at_configuration_time(int statusCode)
    {
        var options = new KorasResultsOptions();
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MapErrorType(ErrorType.Failure, statusCode));
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MapErrorCode("X", statusCode));
    }

    [Fact]
    public void Invalid_error_code_keys_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => new KorasResultsOptions().MapErrorCode(" ", 400));
        Assert.Throws<ArgumentNullException>(() => new KorasResultsOptions().GetStatusCode(null!));
    }

    [Fact]
    public void Secure_defaults_are_in_force()
    {
        var options = new KorasResultsOptions();

        Assert.False(options.IncludeUnexpectedErrorDetails);
        Assert.Equal(MetadataExposurePolicy.None, options.MetadataExposure);
        Assert.True(options.IncludeTraceId);
        Assert.Null(options.ProblemTypeUriFactory);
    }

    [Fact]
    public void AddKorasResults_registers_options_and_default_localizer_idempotently()
    {
        var services = new ServiceCollection();
        services.AddKorasResults(o => o.MapErrorCode("X", 418));
        services.AddKorasResults();

        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<KorasResultsOptions>>().Value;
        Assert.Equal(418, options.GetStatusCode(Error.Failure("X", "m")));
        Assert.IsType<PassThroughErrorMessageLocalizer>(provider.GetRequiredService<IErrorMessageLocalizer>());
    }

    [Fact]
    public void AddKorasResults_preserves_custom_localizer_registration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IErrorMessageLocalizer, FakeLocalizer>();
        services.AddKorasResults();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<FakeLocalizer>(provider.GetRequiredService<IErrorMessageLocalizer>());
    }

    [Fact]
    public void ToProblemDetails_builds_eagerly_with_defaults()
    {
        var problem = Error.NotFound("User.NotFound", "Missing user.").ToProblemDetails();

        Assert.Equal(404, problem.Status);
        Assert.Equal("Missing user.", problem.Detail);
        Assert.Equal("User.NotFound", problem.Extensions[ProblemDetailsBuilderTestAccessor.ErrorCodeExtension]);
    }

    [Fact]
    public void ToProblemDetails_projects_validation_errors_into_ValidationProblemDetails()
    {
        var error = new ValidationError(
            new FieldError("Email", "Required."),
            new FieldError("Email", "Malformed."));

        var problem = Assert.IsType<ValidationProblemDetails>(error.ToProblemDetails());

        Assert.Equal(400, problem.Status);
        Assert.Equal(2, problem.Errors["Email"].Length);
    }

    [Fact]
    public void ToProblemDetails_throws_on_success_results()
    {
        Assert.Throws<InvalidOperationException>(() => Result.Success().ToProblemDetails());
        Assert.Throws<InvalidOperationException>(() => Result.Success(1).ToProblemDetails());
    }

    private sealed class FakeLocalizer : IErrorMessageLocalizer
    {
        public string Localize(Error error, System.Globalization.CultureInfo culture) => error.Message;

        public string LocalizeField(FieldError fieldError, System.Globalization.CultureInfo culture) => fieldError.Message;
    }
}

/// <summary>Exposes internal extension-name constants to tests without duplicating literals.</summary>
internal static class ProblemDetailsBuilderTestAccessor
{
    internal const string ErrorCodeExtension = "errorCode";
}
