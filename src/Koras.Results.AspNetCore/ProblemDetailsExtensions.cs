using Microsoft.AspNetCore.Mvc;

namespace Koras.Results.AspNetCore;

/// <summary>
/// Converts errors and failed results into RFC 9457 <see cref="ProblemDetails"/>. These overloads
/// build the ProblemDetails eagerly with explicit dependencies — ideal for unit tests, exception
/// middleware, and non-endpoint code. Endpoint code normally uses
/// <see cref="HttpResultExtensions.ToHttpResult(Result)"/> or
/// <see cref="ActionResultExtensions.ToActionResult(Result)"/>, which additionally resolve
/// configuration from the request's services.
/// </summary>
public static class ProblemDetailsExtensions
{
    /// <summary>Converts an error into ProblemDetails.</summary>
    /// <param name="error">The error to convert.</param>
    /// <param name="options">Mapping options; built-in defaults when omitted.</param>
    /// <param name="localizer">Message localizer; pass-through when omitted.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is null.</exception>
    public static ProblemDetails ToProblemDetails(
        this Error error,
        KorasResultsOptions? options = null,
        IErrorMessageLocalizer? localizer = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        return ProblemDetailsBuilder.Build(
            error,
            options ?? new KorasResultsOptions(),
            localizer ?? PassThroughErrorMessageLocalizer.Instance,
            httpContext: null,
            logger: null);
    }

    /// <summary>Converts a failed result into ProblemDetails.</summary>
    /// <param name="result">The failed result.</param>
    /// <param name="options">Mapping options; built-in defaults when omitted.</param>
    /// <param name="localizer">Message localizer; pass-through when omitted.</param>
    /// <exception cref="InvalidOperationException"><paramref name="result"/> is a success.</exception>
    public static ProblemDetails ToProblemDetails(
        this Result result,
        KorasResultsOptions? options = null,
        IErrorMessageLocalizer? localizer = null) =>
        result.Error.ToProblemDetails(options, localizer);

    /// <summary>Converts a failed result into ProblemDetails.</summary>
    /// <typeparam name="T">The result's value type.</typeparam>
    /// <param name="result">The failed result.</param>
    /// <param name="options">Mapping options; built-in defaults when omitted.</param>
    /// <param name="localizer">Message localizer; pass-through when omitted.</param>
    /// <exception cref="InvalidOperationException"><paramref name="result"/> is a success.</exception>
    public static ProblemDetails ToProblemDetails<T>(
        this Result<T> result,
        KorasResultsOptions? options = null,
        IErrorMessageLocalizer? localizer = null) =>
        result.Error.ToProblemDetails(options, localizer);
}
