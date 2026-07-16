using System.Text.Json;
using System.Text.Json.Serialization;

namespace Koras.Results.Serialization;

/// <summary>
/// System.Text.Json converter for the non-generic <see cref="Result"/>. Wire shape:
/// <c>{"isSuccess":true}</c> or <c>{"isSuccess":false,"error":{...}}</c>.
/// </summary>
public sealed class ResultJsonConverter : JsonConverter<Result>
{
    internal const string IsSuccessProperty = "isSuccess";
    internal const string ErrorProperty = "error";
    internal const string ValueProperty = "value";

    /// <inheritdoc />
    public override Result Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected a result object, found {reader.TokenType}.");
        }

        bool? isSuccess = null;
        Error? error = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var propertyName = reader.GetString();
            if (!reader.Read())
            {
                throw new JsonException("Truncated result object.");
            }

            switch (propertyName)
            {
                case IsSuccessProperty:
                    isSuccess = reader.GetBoolean();
                    break;
                case ErrorProperty:
                    error = ErrorJsonSerialization.ReadError(ref reader, options);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        return isSuccess switch
        {
            null => throw new JsonException("A result object requires an 'isSuccess' property."),
            true => Result.Success(),
            false when error is not null => Result.Failure(error),
            false => throw new JsonException("A failure result requires an 'error' property."),
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Result value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteBoolean(IsSuccessProperty, value.IsSuccess);
        if (value.IsFailure)
        {
            writer.WritePropertyName(ErrorProperty);
            ErrorJsonSerialization.WriteError(writer, value.Error, options);
        }

        writer.WriteEndObject();
    }
}
