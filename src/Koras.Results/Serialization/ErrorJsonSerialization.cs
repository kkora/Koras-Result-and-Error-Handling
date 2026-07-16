using System.Text.Json;

namespace Koras.Results.Serialization;

/// <summary>
/// Shared read/write logic for the error wire shape. The shape is a public, versioned contract
/// (ADR-0007): camelCase fixed property names, structural discrimination (presence of
/// <c>fieldErrors</c> → <see cref="ValidationError"/>, <c>errors</c> → <see cref="AggregateError"/>),
/// and no polymorphic type names.
/// </summary>
internal static class ErrorJsonSerialization
{
    internal const string CodeProperty = "code";
    internal const string MessageProperty = "message";
    internal const string TypeProperty = "type";
    internal const string MetadataProperty = "metadata";
    internal const string FieldErrorsProperty = "fieldErrors";
    internal const string ErrorsProperty = "errors";
    internal const string PropertyNameProperty = "propertyName";
    internal const string FieldCodeProperty = "code";
    internal const string FieldMessageProperty = "message";

    internal static void WriteError(Utf8JsonWriter writer, Error error, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString(CodeProperty, error.Code);
        writer.WriteString(MessageProperty, error.Message);
        writer.WriteString(TypeProperty, TypeToString(error.Type));

        if (error.Metadata.Count > 0)
        {
            writer.WritePropertyName(MetadataProperty);
            writer.WriteStartObject();
            foreach (var pair in error.Metadata)
            {
                writer.WritePropertyName(pair.Key);
                JsonSerializer.Serialize(writer, pair.Value, pair.Value?.GetType() ?? typeof(object), options);
            }

            writer.WriteEndObject();
        }

        if (error is ValidationError validationError)
        {
            writer.WritePropertyName(FieldErrorsProperty);
            writer.WriteStartArray();
            foreach (var fieldError in validationError.FieldErrors)
            {
                writer.WriteStartObject();
                writer.WriteString(PropertyNameProperty, fieldError.PropertyName);
                writer.WriteString(FieldMessageProperty, fieldError.Message);
                if (fieldError.Code is not null)
                {
                    writer.WriteString(FieldCodeProperty, fieldError.Code);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }
        else if (error is AggregateError aggregateError)
        {
            writer.WritePropertyName(ErrorsProperty);
            writer.WriteStartArray();
            foreach (var child in aggregateError.Errors)
            {
                WriteError(writer, child, options);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    internal static Error ReadError(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected an error object, found {reader.TokenType}.");
        }

        string? code = null;
        string? message = null;
        string? typeString = null;
        Dictionary<string, object?>? metadata = null;
        List<FieldError>? fieldErrors = null;
        List<Error>? children = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var propertyName = reader.GetString();
            if (!reader.Read())
            {
                throw new JsonException("Truncated error object.");
            }

            switch (propertyName)
            {
                case CodeProperty:
                    code = reader.GetString();
                    break;
                case MessageProperty:
                    message = reader.GetString();
                    break;
                case TypeProperty:
                    typeString = reader.GetString();
                    break;
                case MetadataProperty:
                    metadata = ReadMetadata(ref reader);
                    break;
                case FieldErrorsProperty:
                    fieldErrors = ReadFieldErrors(ref reader);
                    break;
                case ErrorsProperty:
                    children = ReadChildren(ref reader, options);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        // Error.None round-trips structurally (empty code and message).
        if (string.IsNullOrEmpty(code) && string.IsNullOrEmpty(message) && fieldErrors is null && children is null)
        {
            return Error.None;
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(message) || typeString is null)
        {
            throw new JsonException("An error object requires non-empty 'code', 'message', and 'type' properties.");
        }

        var type = TypeFromString(typeString);

        if (fieldErrors is not null && children is not null)
        {
            throw new JsonException("An error object cannot carry both 'fieldErrors' and 'errors'.");
        }

        try
        {
            if (fieldErrors is not null)
            {
                if (type != ErrorType.Validation)
                {
                    throw new JsonException("An error with 'fieldErrors' must have type 'validation'.");
                }

                if (fieldErrors.Count == 0)
                {
                    throw new JsonException("A validation error requires at least one field error.");
                }

                return new ValidationError(code, message, fieldErrors.ToArray(), metadata);
            }

            if (children is not null)
            {
                var aggregate = new AggregateError(children);
                return metadata is null ? aggregate : aggregate.WithMetadata(metadata);
            }

            return new Error(code, message, type, metadata);
        }
        catch (ArgumentException exception)
        {
            throw new JsonException("The error payload is structurally invalid.", exception);
        }
    }

    internal static string TypeToString(ErrorType type) => type switch
    {
        ErrorType.Failure => "failure",
        ErrorType.Validation => "validation",
        ErrorType.NotFound => "notFound",
        ErrorType.Conflict => "conflict",
        ErrorType.Unauthorized => "unauthorized",
        ErrorType.Forbidden => "forbidden",
        ErrorType.Unavailable => "unavailable",
        ErrorType.Unexpected => "unexpected",
        _ => throw new JsonException($"Unknown error type '{type}'."),
    };

    internal static ErrorType TypeFromString(string value) => value switch
    {
        "failure" => ErrorType.Failure,
        "validation" => ErrorType.Validation,
        "notFound" => ErrorType.NotFound,
        "conflict" => ErrorType.Conflict,
        "unauthorized" => ErrorType.Unauthorized,
        "forbidden" => ErrorType.Forbidden,
        "unavailable" => ErrorType.Unavailable,
        "unexpected" => ErrorType.Unexpected,
        _ => throw new JsonException($"Unknown error type '{value}'."),
    };

    private static Dictionary<string, object?> ReadMetadata(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected a metadata object, found {reader.TokenType}.");
        }

        var metadata = new Dictionary<string, object?>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var key = reader.GetString()!;
            if (!reader.Read())
            {
                throw new JsonException("Truncated metadata object.");
            }

            metadata[key] = ReadMetadataValue(ref reader);
        }

        return metadata;
    }

    private static object? ReadMetadataValue(ref Utf8JsonReader reader) => reader.TokenType switch
    {
        JsonTokenType.Null => null,
        JsonTokenType.True => true,
        JsonTokenType.False => false,
        JsonTokenType.String => reader.GetString(),
        // The (object) cast is load-bearing: without it the ternary unifies long and double
        // to double, silently changing integral metadata to floating point on round-trip.
        JsonTokenType.Number => reader.TryGetInt64(out var integer) ? integer : (object)reader.GetDouble(),

        // Nested structures stay as JsonElement: no polymorphic type resolution, no gadget surface.
        JsonTokenType.StartObject or JsonTokenType.StartArray => JsonElement.ParseValue(ref reader),
        _ => throw new JsonException($"Unsupported metadata value token {reader.TokenType}."),
    };

    private static List<FieldError> ReadFieldErrors(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"Expected a fieldErrors array, found {reader.TokenType}.");
        }

        var fieldErrors = new List<FieldError>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"Expected a field error object, found {reader.TokenType}.");
            }

            string? propertyName = null;
            string? fieldMessage = null;
            string? fieldCode = null;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                var name = reader.GetString();
                if (!reader.Read())
                {
                    throw new JsonException("Truncated field error object.");
                }

                switch (name)
                {
                    case PropertyNameProperty:
                        propertyName = reader.GetString();
                        break;
                    case FieldMessageProperty:
                        fieldMessage = reader.GetString();
                        break;
                    case FieldCodeProperty:
                        fieldCode = reader.GetString();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            if (propertyName is null || fieldMessage is null)
            {
                throw new JsonException("A field error requires 'propertyName' and 'message' properties.");
            }

            fieldErrors.Add(new FieldError(propertyName, fieldMessage, fieldCode));
        }

        return fieldErrors;
    }

    private static List<Error> ReadChildren(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"Expected an errors array, found {reader.TokenType}.");
        }

        var children = new List<Error>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            children.Add(ReadError(ref reader, options));
        }

        return children;
    }
}
