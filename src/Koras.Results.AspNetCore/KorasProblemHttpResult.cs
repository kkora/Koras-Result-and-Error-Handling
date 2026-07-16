using Microsoft.AspNetCore.Http;

using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Koras.Results.AspNetCore;

/// <summary>
/// An <see cref="IResult"/> that projects an <see cref="Error"/> into an RFC 9457 response at
/// execution time, resolving <see cref="KorasResultsOptions"/>, the localizer, and logging from
/// the request's services. Created by the <see cref="HttpResultExtensions"/> adapters.
/// </summary>
internal sealed class KorasProblemHttpResult : IResult, IStatusCodeHttpResult
{
    private readonly Error _error;

    internal KorasProblemHttpResult(Error error) => _error = error;

    /// <summary>
    /// Unknown until execution: the status code depends on options resolved from request services.
    /// </summary>
    public int? StatusCode => null;

    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var problemDetails = ProblemDetailsBuilder.Build(_error, httpContext);
        return HttpResults.Problem(problemDetails).ExecuteAsync(httpContext);
    }
}
