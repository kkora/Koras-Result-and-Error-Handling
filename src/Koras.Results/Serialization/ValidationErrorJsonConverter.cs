using System.Text.Json;
using System.Text.Json.Serialization;

namespace Koras.Results.Serialization;

/// <summary>
/// System.Text.Json converter for <see cref="ValidationError"/>-declared members. Shares the
/// wire shape of <see cref="ErrorJsonConverter"/>.
/// </summary>
public sealed class ValidationErrorJsonConverter : JsonConverter<ValidationError>
{
    /// <inheritdoc />
    public override ValidationError Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        ErrorJsonSerialization.ReadError(ref reader, options) as ValidationError
        ?? throw new JsonException("The payload is not a validation error (missing 'fieldErrors').");

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ValidationError value, JsonSerializerOptions options) =>
        ErrorJsonSerialization.WriteError(writer, value, options);
}
