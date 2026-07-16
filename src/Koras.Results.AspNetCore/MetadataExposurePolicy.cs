namespace Koras.Results.AspNetCore;

/// <summary>
/// Controls whether <see cref="Error.Metadata"/> is exposed to HTTP clients in ProblemDetails
/// responses.
/// </summary>
public enum MetadataExposurePolicy
{
    /// <summary>Metadata is never sent to clients. This is the secure default.</summary>
    None = 0,

    /// <summary>
    /// All metadata is sent in the <c>metadata</c> extension. Only enable this when every error
    /// producer in the application treats metadata as client-safe.
    /// </summary>
    All = 1,
}
