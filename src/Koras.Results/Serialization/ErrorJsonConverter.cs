using System.Text.Json;
using System.Text.Json.Serialization;

namespace Koras.Results.Serialization;

/// <summary>
/// System.Text.Json converter for <see cref="Error"/> and its subclasses. Wired automatically via
/// attributes — explicit registration is only needed for custom <see cref="JsonSerializerOptions"/>
/// pipelines that clear converters.
/// </summary>
public sealed class ErrorJsonConverter : JsonConverter<Error>
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) => typeof(Error).IsAssignableFrom(typeToConvert);

    /// <inheritdoc />
    public override Error Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        ErrorJsonSerialization.ReadError(ref reader, options);

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Error value, JsonSerializerOptions options) =>
        ErrorJsonSerialization.WriteError(writer, value, options);
}
