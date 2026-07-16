using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using Koras.Results.Serialization;

namespace Koras.Results;

/// <summary>
/// An immutable, serializable description of why an operation failed: a stable machine-readable
/// <see cref="Code"/>, a human-readable <see cref="Message"/>, a semantic <see cref="Type"/>,
/// and optional <see cref="Metadata"/>.
/// </summary>
/// <remarks>
/// <para>
/// Equality is defined by <see cref="Code"/> and <see cref="Type"/> only: errors are identities,
/// messages and metadata are presentation. Instances are immutable and safe to share across threads.
/// </para>
/// <para>
/// This class is not designed for user inheritance; <see cref="ValidationError"/> and
/// <see cref="AggregateError"/> are the only supported subclasses (ADR-0005).
/// </para>
/// </remarks>
[JsonConverter(typeof(ErrorJsonConverter))]
public class Error : IEquatable<Error>
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyMetadata =
        ReadOnlyDictionary<string, object?>.Empty;

    /// <summary>
    /// A sentinel used internally to represent "no error". It is never exposed by a failed result;
    /// accessing <see cref="Result.Error"/> on a success throws instead of returning it.
    /// </summary>
    public static readonly Error None = new();

    /// <summary>
    /// The error carried by an uninitialized result, i.e. <c>default(Result)</c> or
    /// <c>default(Result&lt;T&gt;)</c>. An uninitialized result is always a failure (ADR-0003).
    /// </summary>
    public static readonly Error Uninitialized =
        new("Result.Uninitialized", "The result was constructed as a default value and carries no outcome.", ErrorType.Unexpected);

    /// <summary>
    /// Initializes a new instance of the <see cref="Error"/> class.
    /// </summary>
    /// <param name="code">A stable, machine-readable identifier such as <c>"User.NotFound"</c>. Must be non-empty.</param>
    /// <param name="message">A human-readable description of the failure. Must be non-empty.</param>
    /// <param name="type">The semantic classification of the failure.</param>
    /// <param name="metadata">Optional additional data; keys should be camelCase and values JSON-primitive-representable. The dictionary is copied.</param>
    /// <exception cref="ArgumentException"><paramref name="code"/> or <paramref name="message"/> is null, empty, or whitespace.</exception>
    public Error(string code, string message, ErrorType type, IReadOnlyDictionary<string, object?>? metadata = null)
    {
        ThrowIfNullOrWhiteSpace(code, nameof(code));
        ThrowIfNullOrWhiteSpace(message, nameof(message));

        Code = code;
        Message = message;
        Type = type;
        Metadata = metadata is null or { Count: 0 }
            ? EmptyMetadata
            : new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(metadata));
    }

    private Error()
    {
        Code = string.Empty;
        Message = string.Empty;
        Type = ErrorType.Failure;
        Metadata = EmptyMetadata;
    }

    /// <summary>The stable, machine-readable error identifier, e.g. <c>"Order.InsufficientStock"</c>.</summary>
    public string Code { get; }

    /// <summary>The human-readable description of the failure. Never expose sensitive data here.</summary>
    public string Message { get; }

    /// <summary>The semantic classification of the failure.</summary>
    public ErrorType Type { get; }

    /// <summary>Additional structured data about the failure. Never null; empty when unused.</summary>
    public IReadOnlyDictionary<string, object?> Metadata { get; }

    /// <summary>Creates an error representing a domain or business rule violation (<see cref="ErrorType.Failure"/>).</summary>
    /// <param name="code">A stable, machine-readable identifier.</param>
    /// <param name="message">A human-readable description.</param>
    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);

    /// <summary>Creates an error representing invalid input (<see cref="ErrorType.Validation"/>).</summary>
    /// <param name="code">A stable, machine-readable identifier.</param>
    /// <param name="message">A human-readable description.</param>
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    /// <summary>Creates an error representing a missing resource (<see cref="ErrorType.NotFound"/>).</summary>
    /// <param name="code">A stable, machine-readable identifier.</param>
    /// <param name="message">A human-readable description.</param>
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    /// <summary>Creates an error representing a state conflict (<see cref="ErrorType.Conflict"/>).</summary>
    /// <param name="code">A stable, machine-readable identifier.</param>
    /// <param name="message">A human-readable description.</param>
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    /// <summary>Creates an error representing missing or invalid caller identity (<see cref="ErrorType.Unauthorized"/>).</summary>
    /// <param name="code">A stable, machine-readable identifier.</param>
    /// <param name="message">A human-readable description.</param>
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    /// <summary>Creates an error representing an authenticated but unpermitted caller (<see cref="ErrorType.Forbidden"/>).</summary>
    /// <param name="code">A stable, machine-readable identifier.</param>
    /// <param name="message">A human-readable description.</param>
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    /// <summary>Creates an error representing an unavailable or throttled dependency (<see cref="ErrorType.Unavailable"/>).</summary>
    /// <param name="code">A stable, machine-readable identifier.</param>
    /// <param name="message">A human-readable description.</param>
    public static Error Unavailable(string code, string message) => new(code, message, ErrorType.Unavailable);

    /// <summary>Creates an error representing an unexpected condition or bug (<see cref="ErrorType.Unexpected"/>).</summary>
    /// <param name="code">A stable, machine-readable identifier.</param>
    /// <param name="message">A human-readable description.</param>
    public static Error Unexpected(string code, string message) => new(code, message, ErrorType.Unexpected);

    /// <summary>
    /// Returns a copy of this error with <paramref name="key"/> set to <paramref name="value"/> in its metadata.
    /// </summary>
    /// <param name="key">The metadata key (camelCase by convention). Must be non-empty.</param>
    /// <param name="value">The metadata value; should be JSON-primitive-representable.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty, or whitespace.</exception>
    public Error WithMetadata(string key, object? value)
    {
        ThrowIfNullOrWhiteSpace(key, nameof(key));

        var merged = new Dictionary<string, object?>(Metadata.Count + 1);
        foreach (var pair in Metadata)
        {
            merged[pair.Key] = pair.Value;
        }

        merged[key] = value;
        return CloneWithMetadata(merged);
    }

    /// <summary>
    /// Returns a copy of this error with all entries of <paramref name="metadata"/> merged into its
    /// metadata (existing keys are overwritten).
    /// </summary>
    /// <param name="metadata">The entries to merge.</param>
    /// <exception cref="ArgumentNullException"><paramref name="metadata"/> is null.</exception>
    public Error WithMetadata(IReadOnlyDictionary<string, object?> metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var merged = new Dictionary<string, object?>(Metadata.Count + metadata.Count);
        foreach (var pair in Metadata)
        {
            merged[pair.Key] = pair.Value;
        }

        foreach (var pair in metadata)
        {
            merged[pair.Key] = pair.Value;
        }

        return CloneWithMetadata(merged);
    }

    // private protected: subclasses inside this assembly preserve their shape (field errors,
    // aggregated children) across WithMetadata; the hierarchy stays closed to external code.
    private protected virtual Error CloneWithMetadata(IReadOnlyDictionary<string, object?> metadata) =>
        new(Code, Message, Type, metadata);

    /// <summary>
    /// Determines equality by <see cref="Code"/> and <see cref="Type"/>. Message and metadata are
    /// presentation and do not participate in equality.
    /// </summary>
    /// <param name="other">The error to compare with.</param>
    public bool Equals(Error? other) =>
        other is not null && Code == other.Code && Type == other.Type;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Error other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Code, Type);

    /// <inheritdoc />
    public override string ToString() => $"{Type}: {Code} — {Message}";

    private static void ThrowIfNullOrWhiteSpace(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"'{paramName}' must be a non-empty string.", paramName);
        }
    }
}
