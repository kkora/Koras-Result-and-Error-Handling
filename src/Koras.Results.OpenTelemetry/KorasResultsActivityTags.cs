namespace Koras.Results.OpenTelemetry;

/// <summary>
/// The activity tag names written by the Koras.Results OpenTelemetry integration. Public so that
/// custom instrumentation and dashboard queries can reference them without string literals.
/// </summary>
public static class KorasResultsActivityTags
{
    /// <summary>
    /// The error taxonomy value in snake_case (e.g. <c>not_found</c>), following the OpenTelemetry
    /// <c>error.type</c> semantic convention.
    /// </summary>
    public const string ErrorType = "error.type";

    /// <summary>The stable Koras.Results error code (e.g. <c>User.NotFound</c>).</summary>
    public const string ErrorCode = "koras.error.code";

    /// <summary>The number of child errors when the failure carries an <see cref="AggregateError"/>.</summary>
    public const string AggregateCount = "koras.error.aggregate_count";
}
