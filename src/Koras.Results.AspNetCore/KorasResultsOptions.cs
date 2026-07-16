using Microsoft.AspNetCore.Http;

namespace Koras.Results.AspNetCore;

/// <summary>
/// Configures how Koras.Results failures are projected into HTTP responses. Register via
/// <see cref="KorasResultsServiceCollectionExtensions.AddKorasResults"/>; all defaults are safe
/// for production.
/// </summary>
/// <remarks>
/// Instances are mutable only during configuration; once options are materialized by the options
/// system they must be treated as read-only (standard options-pattern semantics).
/// </remarks>
public sealed class KorasResultsOptions
{
    internal static readonly IReadOnlyDictionary<ErrorType, int> DefaultStatusMap = new Dictionary<ErrorType, int>
    {
        [ErrorType.Failure] = StatusCodes.Status422UnprocessableEntity,
        [ErrorType.Validation] = StatusCodes.Status400BadRequest,
        [ErrorType.NotFound] = StatusCodes.Status404NotFound,
        [ErrorType.Conflict] = StatusCodes.Status409Conflict,
        [ErrorType.Unauthorized] = StatusCodes.Status401Unauthorized,
        [ErrorType.Forbidden] = StatusCodes.Status403Forbidden,
        [ErrorType.Unavailable] = StatusCodes.Status503ServiceUnavailable,
        [ErrorType.Unexpected] = StatusCodes.Status500InternalServerError,
    };

    private readonly Dictionary<ErrorType, int> _typeOverrides = [];
    private readonly Dictionary<string, int> _codeOverrides = new(StringComparer.Ordinal);

    /// <summary>
    /// Whether the <c>detail</c> of <see cref="ErrorType.Unexpected"/> errors is sent to clients.
    /// Default <see langword="false"/>: unexpected errors usually originate from exceptions and
    /// may carry internal details; clients receive a generic message instead. The full message is
    /// still logged server-side.
    /// </summary>
    public bool IncludeUnexpectedErrorDetails { get; set; }

    /// <summary>How much error metadata is exposed to clients. Default: <see cref="MetadataExposurePolicy.None"/>.</summary>
    public MetadataExposurePolicy MetadataExposure { get; set; } = MetadataExposurePolicy.None;

    /// <summary>
    /// Optional factory for the ProblemDetails <c>type</c> URI, e.g.
    /// <c>error =&gt; $"https://errors.example.com/{error.Code}"</c>. When null (default), the
    /// standard RFC 9110 section URI for the mapped status code is used.
    /// </summary>
    public Func<Error, string?>? ProblemTypeUriFactory { get; set; }

    /// <summary>
    /// Whether the current trace identifier is added as the <c>traceId</c> extension so clients
    /// can correlate error reports with server telemetry. Default <see langword="true"/>.
    /// </summary>
    public bool IncludeTraceId { get; set; } = true;

    /// <summary>Overrides the status code for every error of <paramref name="type"/>.</summary>
    /// <param name="type">The error type to remap.</param>
    /// <param name="statusCode">The HTTP status code (100–599).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="statusCode"/> is outside 100–599.</exception>
    public KorasResultsOptions MapErrorType(ErrorType type, int statusCode)
    {
        ValidateStatusCode(statusCode);
        _typeOverrides[type] = statusCode;
        return this;
    }

    /// <summary>
    /// Overrides the status code for errors with exactly <paramref name="errorCode"/>. Takes
    /// precedence over <see cref="MapErrorType"/>.
    /// </summary>
    /// <param name="errorCode">The exact error code (ordinal comparison).</param>
    /// <param name="statusCode">The HTTP status code (100–599).</param>
    /// <exception cref="ArgumentException"><paramref name="errorCode"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="statusCode"/> is outside 100–599.</exception>
    public KorasResultsOptions MapErrorCode(string errorCode, int statusCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            throw new ArgumentException("'errorCode' must be a non-empty string.", nameof(errorCode));
        }

        ValidateStatusCode(statusCode);
        _codeOverrides[errorCode] = statusCode;
        return this;
    }

    /// <summary>
    /// Resolves the status code for <paramref name="error"/>: exact-code override, then
    /// error-type override, then the built-in default.
    /// </summary>
    /// <param name="error">The error to resolve.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is null.</exception>
    public int GetStatusCode(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (_codeOverrides.TryGetValue(error.Code, out var byCode))
        {
            return byCode;
        }

        if (_typeOverrides.TryGetValue(error.Type, out var byType))
        {
            return byType;
        }

        return DefaultStatusMap.TryGetValue(error.Type, out var byDefault)
            ? byDefault
            : StatusCodes.Status500InternalServerError;
    }

    private static void ValidateStatusCode(int statusCode)
    {
        if (statusCode is < 100 or > 599)
        {
            throw new ArgumentOutOfRangeException(
                nameof(statusCode), statusCode, "HTTP status codes must be between 100 and 599.");
        }
    }
}
