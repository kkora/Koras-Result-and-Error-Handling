namespace Koras.Results;

/// <summary>
/// Classifies an <see cref="Error"/> by its semantic (business or technical) meaning.
/// The taxonomy is deliberately closed; extensibility lives in <see cref="Error.Code"/>
/// and <see cref="Error.Metadata"/>. See ADR-0004.
/// </summary>
/// <remarks>
/// Numeric values are part of the serialization contract and must never be reordered.
/// </remarks>
public enum ErrorType
{
    /// <summary>A domain or business rule rejected the operation.</summary>
    Failure = 0,

    /// <summary>The input was syntactically or semantically invalid.</summary>
    Validation = 1,

    /// <summary>A referenced resource does not exist.</summary>
    NotFound = 2,

    /// <summary>The operation conflicts with current state (duplicate, concurrency, versioning).</summary>
    Conflict = 3,

    /// <summary>The caller's identity is missing or invalid.</summary>
    Unauthorized = 4,

    /// <summary>The caller is authenticated but not permitted to perform the operation.</summary>
    Forbidden = 5,

    /// <summary>A dependency is unavailable, throttling, or timing out; typically retryable.</summary>
    Unavailable = 6,

    /// <summary>An unclassified or unexpected condition — usually a bug or converted exception.</summary>
    Unexpected = 7,
}
