using System.Text.Json;
using System.Text.Json.Serialization;

namespace Koras.Results.Serialization;

/// <summary>
/// System.Text.Json converter factory for <see cref="Result{T}"/>. Wire shape:
/// <c>{"isSuccess":true,"value":...}</c> or <c>{"isSuccess":false,"error":{...}}</c>.
/// </summary>
public sealed class ResultJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Result<>);

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var valueType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(ResultOfTJsonConverter<>).MakeGenericType(valueType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private sealed class ResultOfTJsonConverter<T> : JsonConverter<Result<T>>
    {
        public override Result<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"Expected a result object, found {reader.TokenType}.");
            }

            bool? isSuccess = null;
            Error? error = null;
            JsonElement? valueElement = null;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                var propertyName = reader.GetString();
                if (!reader.Read())
                {
                    throw new JsonException("Truncated result object.");
                }

                switch (propertyName)
                {
                    case ResultJsonConverter.IsSuccessProperty:
                        isSuccess = reader.GetBoolean();
                        break;
                    case ResultJsonConverter.ErrorProperty:
                        error = ErrorJsonSerialization.ReadError(ref reader, options);
                        break;
                    case ResultJsonConverter.ValueProperty:
                        // Buffered so the payload's property order does not matter.
                        valueElement = JsonElement.ParseValue(ref reader);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            switch (isSuccess)
            {
                case null:
                    throw new JsonException("A result object requires an 'isSuccess' property.");
                case false when error is not null:
                    return Result.Failure<T>(error);
                case false:
                    throw new JsonException("A failure result requires an 'error' property.");
            }

            if (valueElement is null || valueElement.Value.ValueKind == JsonValueKind.Null)
            {
                throw new JsonException("A success result requires a non-null 'value' property.");
            }

            var value = valueElement.Value.Deserialize<T>(options);
            return value is null
                ? throw new JsonException("A success result requires a non-null 'value' property.")
                : Result.Success(value);
        }

        public override void Write(Utf8JsonWriter writer, Result<T> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteBoolean(ResultJsonConverter.IsSuccessProperty, value.IsSuccess);
            if (value.IsSuccess)
            {
                writer.WritePropertyName(ResultJsonConverter.ValueProperty);
                JsonSerializer.Serialize(writer, value.Value, options);
            }
            else
            {
                writer.WritePropertyName(ResultJsonConverter.ErrorProperty);
                ErrorJsonSerialization.WriteError(writer, value.Error, options);
            }

            writer.WriteEndObject();
        }
    }
}
