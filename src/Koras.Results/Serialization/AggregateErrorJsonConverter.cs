using System.Text.Json;
using System.Text.Json.Serialization;

namespace Koras.Results.Serialization;

/// <summary>
/// System.Text.Json converter for <see cref="AggregateError"/>-declared members. Shares the
/// wire shape of <see cref="ErrorJsonConverter"/>.
/// </summary>
public sealed class AggregateErrorJsonConverter : JsonConverter<AggregateError>
{
    /// <inheritdoc />
    public override AggregateError Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        ErrorJsonSerialization.ReadError(ref reader, options) as AggregateError
        ?? throw new JsonException("The payload is not an aggregate error (missing 'errors').");

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, AggregateError value, JsonSerializerOptions options) =>
        ErrorJsonSerialization.WriteError(writer, value, options);
}
