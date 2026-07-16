using Microsoft.Extensions.Logging;

namespace Koras.Results.AspNetCore;

/// <summary>Source-generated log messages for the result-to-HTTP mapping path.</summary>
internal static partial class ResultHttpMapperLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Mapped error {ErrorCode} ({ErrorType}) to HTTP {StatusCode}")]
    internal static partial void MappedError(ILogger logger, string errorCode, ErrorType errorType, int statusCode);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Suppressed details of unexpected error {ErrorCode} from the HTTP response; original message: {ErrorMessage}")]
    internal static partial void SuppressedUnexpectedDetails(ILogger logger, string errorCode, string errorMessage);
}
