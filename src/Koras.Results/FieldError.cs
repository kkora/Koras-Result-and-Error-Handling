namespace Koras.Results;

/// <summary>
/// A single field-level validation failure carried by a <see cref="ValidationError"/>.
/// </summary>
/// <param name="PropertyName">The name of the invalid property; empty string for model-level failures.</param>
/// <param name="Message">The human-readable validation message for this field.</param>
/// <param name="Code">An optional machine-readable code for the specific rule that failed.</param>
public sealed record FieldError(string PropertyName, string Message, string? Code = null);
