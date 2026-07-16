using Microsoft.AspNetCore.Mvc;

namespace Koras.Results.AspNetCore;

/// <summary>
/// An <see cref="ActionResult"/> that projects an <see cref="Error"/> into an RFC 9457 response
/// at execution time, resolving <see cref="KorasResultsOptions"/>, the localizer, and logging
/// from the request's services. Created by the <see cref="ActionResultExtensions"/> adapters.
/// </summary>
internal sealed class KorasProblemActionResult : ActionResult
{
    private readonly Error _error;

    internal KorasProblemActionResult(Error error) => _error = error;

    public override Task ExecuteResultAsync(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var problemDetails = ProblemDetailsBuilder.Build(_error, context.HttpContext);
        var objectResult = new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status,
            ContentTypes = { "application/problem+json" },
        };

        return objectResult.ExecuteResultAsync(context);
    }
}
