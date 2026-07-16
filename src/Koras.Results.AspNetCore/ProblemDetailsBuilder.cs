using System.Collections.Frozen;
using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Koras.Results.AspNetCore;

/// <summary>
/// Internal single source of truth for projecting an <see cref="Error"/> into RFC 9457
/// ProblemDetails. Both the Minimal API and MVC adapters delegate here.
/// </summary>
internal static class ProblemDetailsBuilder
{
    internal const string ErrorCodeExtension = "errorCode";
    internal const string TraceIdExtension = "traceId";
    internal const string MetadataExtension = "metadata";
    internal const string SuppressedUnexpectedDetail = "An unexpected error occurred.";
    internal const string LoggerCategory = "Koras.Results.AspNetCore.ResultHttpMapper";

    // Matches ASP.NET Core's ProblemDetailsDefaults (RFC 9110 section links).
    private static readonly FrozenDictionary<int, (string Title, string Type)> Defaults =
        new Dictionary<int, (string Title, string Type)>
        {
            [400] = ("Bad Request", "https://tools.ietf.org/html/rfc9110#section-15.5.1"),
            [401] = ("Unauthorized", "https://tools.ietf.org/html/rfc9110#section-15.5.2"),
            [403] = ("Forbidden", "https://tools.ietf.org/html/rfc9110#section-15.5.4"),
            [404] = ("Not Found", "https://tools.ietf.org/html/rfc9110#section-15.5.5"),
            [405] = ("Method Not Allowed", "https://tools.ietf.org/html/rfc9110#section-15.5.6"),
            [406] = ("Not Acceptable", "https://tools.ietf.org/html/rfc9110#section-15.5.7"),
            [408] = ("Request Timeout", "https://tools.ietf.org/html/rfc9110#section-15.5.9"),
            [409] = ("Conflict", "https://tools.ietf.org/html/rfc9110#section-15.5.10"),
            [422] = ("Unprocessable Entity", "https://tools.ietf.org/html/rfc9110#section-15.5.21"),
            [426] = ("Upgrade Required", "https://tools.ietf.org/html/rfc9110#section-15.5.22"),
            [500] = ("An error occurred while processing your request.", "https://tools.ietf.org/html/rfc9110#section-15.6.1"),
            [502] = ("Bad Gateway", "https://tools.ietf.org/html/rfc9110#section-15.6.3"),
            [503] = ("Service Unavailable", "https://tools.ietf.org/html/rfc9110#section-15.6.4"),
        }.ToFrozenDictionary();

    internal static ProblemDetails Build(
        Error error,
        KorasResultsOptions options,
        IErrorMessageLocalizer localizer,
        HttpContext? httpContext,
        ILogger? logger)
    {
        var statusCode = options.GetStatusCode(error);
        var culture = CultureInfo.CurrentUICulture;

        var problemDetails = error is ValidationError validationError
            ? BuildValidationProblem(validationError, localizer, culture)
            : new ProblemDetails();

        problemDetails.Status = statusCode;
        if (Defaults.TryGetValue(statusCode, out var defaults))
        {
            problemDetails.Title = defaults.Title;
            problemDetails.Type = defaults.Type;
        }

        var typeUri = options.ProblemTypeUriFactory?.Invoke(error);
        if (typeUri is not null)
        {
            problemDetails.Type = typeUri;
        }

        if (error.Type == ErrorType.Unexpected && !options.IncludeUnexpectedErrorDetails)
        {
            problemDetails.Detail = SuppressedUnexpectedDetail;
            if (logger is not null)
            {
                ResultHttpMapperLog.SuppressedUnexpectedDetails(logger, error.Code, error.Message);
            }
        }
        else
        {
            problemDetails.Detail = localizer.Localize(error, culture);
        }

        problemDetails.Extensions[ErrorCodeExtension] = error.Code;

        if (options.IncludeTraceId)
        {
            var traceId = Activity.Current?.Id ?? httpContext?.TraceIdentifier;
            if (traceId is not null)
            {
                problemDetails.Extensions[TraceIdExtension] = traceId;
            }
        }

        if (options.MetadataExposure == MetadataExposurePolicy.All && error.Metadata.Count > 0)
        {
            problemDetails.Extensions[MetadataExtension] = error.Metadata;
        }

        if (logger is not null)
        {
            ResultHttpMapperLog.MappedError(logger, error.Code, error.Type, statusCode);
        }

        return problemDetails;
    }

    internal static ProblemDetails Build(Error error, HttpContext httpContext)
    {
        var services = httpContext.RequestServices;
        var options = services.GetService<IOptions<KorasResultsOptions>>()?.Value ?? new KorasResultsOptions();
        var localizer = services.GetService<IErrorMessageLocalizer>() ?? PassThroughErrorMessageLocalizer.Instance;
        var logger = services.GetService<ILoggerFactory>()?.CreateLogger(LoggerCategory);
        return Build(error, options, localizer, httpContext, logger);
    }

    private static ValidationProblemDetails BuildValidationProblem(
        ValidationError validationError,
        IErrorMessageLocalizer localizer,
        CultureInfo culture)
    {
        // Grouped by property name to match ASP.NET Core's HttpValidationProblemDetails shape.
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var group in validationError.FieldErrors.GroupBy(f => f.PropertyName, StringComparer.Ordinal))
        {
            errors[group.Key] = group.Select(f => localizer.LocalizeField(f, culture)).ToArray();
        }

        return new ValidationProblemDetails(errors);
    }
}
