using System.Text.Json.Serialization;
using Koras.Results.Serialization;

namespace Koras.Results;

/// <summary>
/// An <see cref="Error"/> of type <see cref="ErrorType.Validation"/> that carries one or more
/// field-level failures. The ASP.NET Core integration projects these into the standard
/// ProblemDetails <c>errors</c> dictionary.
/// </summary>
[JsonConverter(typeof(ValidationErrorJsonConverter))]
public sealed class ValidationError : Error
{
    /// <summary>The default error code used when none is supplied.</summary>
    public const string DefaultCode = "Validation.Failed";

    /// <summary>The default message used when none is supplied.</summary>
    public const string DefaultMessage = "One or more validation errors occurred.";

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationError"/> class with the default
    /// code and message.
    /// </summary>
    /// <param name="fieldErrors">The field-level failures; at least one is required.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fieldErrors"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="fieldErrors"/> is empty or contains null entries.</exception>
    public ValidationError(params FieldError[] fieldErrors)
        : this(DefaultCode, DefaultMessage, fieldErrors)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationError"/> class with the default
    /// code and message.
    /// </summary>
    /// <param name="fieldErrors">The field-level failures; at least one is required.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fieldErrors"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="fieldErrors"/> is empty or contains null entries.</exception>
    public ValidationError(IEnumerable<FieldError> fieldErrors)
        : this(DefaultCode, DefaultMessage, fieldErrors)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationError"/> class with a custom code
    /// and message.
    /// </summary>
    /// <param name="code">A stable, machine-readable identifier.</param>
    /// <param name="message">A human-readable summary of the validation failure.</param>
    /// <param name="fieldErrors">The field-level failures; at least one is required.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fieldErrors"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="code"/> or <paramref name="message"/> is null or whitespace, or
    /// <paramref name="fieldErrors"/> is empty or contains null entries.
    /// </exception>
    public ValidationError(string code, string message, IEnumerable<FieldError> fieldErrors)
        : base(code, message, ErrorType.Validation)
    {
        ArgumentNullException.ThrowIfNull(fieldErrors);

        var copied = fieldErrors.ToArray();
        if (copied.Length == 0)
        {
            throw new ArgumentException("At least one field error is required.", nameof(fieldErrors));
        }

        if (Array.IndexOf(copied, null) >= 0)
        {
            throw new ArgumentException("Field errors must not contain null entries.", nameof(fieldErrors));
        }

        FieldErrors = copied;
    }

    // Trusted construction path for cloning and deserialization: inputs already validated.
    internal ValidationError(
        string code,
        string message,
        IReadOnlyList<FieldError> fieldErrors,
        IReadOnlyDictionary<string, object?>? metadata)
        : base(code, message, ErrorType.Validation, metadata)
    {
        FieldErrors = fieldErrors;
    }

    /// <summary>The field-level failures, in the order they were supplied. Never empty.</summary>
    public IReadOnlyList<FieldError> FieldErrors { get; }

    private protected override Error CloneWithMetadata(IReadOnlyDictionary<string, object?> metadata) =>
        new ValidationError(Code, Message, FieldErrors, metadata);
}
