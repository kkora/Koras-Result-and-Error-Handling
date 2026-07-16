using System.Text.Json;
using Koras.Results;

namespace Koras.Results.UnitTests;

/// <summary>
/// The JSON wire shape is a public, versioned contract (ADR-0007). Exact-payload assertions in
/// this class are deliberate: changing them is a breaking change and requires a major version.
/// </summary>
public class SerializationTests
{
    [Fact]
    public void Error_serializes_to_the_documented_shape()
    {
        var error = Error.NotFound("User.NotFound", "The user does not exist.");

        var json = JsonSerializer.Serialize(error);

        Assert.Equal(
            """{"code":"User.NotFound","message":"The user does not exist.","type":"notFound"}""",
            json);
    }

    [Fact]
    public void Error_round_trips_with_metadata()
    {
        var error = Error.Conflict("Order.Duplicate", "Duplicate order.")
            .WithMetadata("orderId", 42L)
            .WithMetadata("sku", "A-1")
            .WithMetadata("retryable", false)
            .WithMetadata("weight", 1.5)
            .WithMetadata("nothing", null);

        var roundTripped = JsonSerializer.Deserialize<Error>(JsonSerializer.Serialize(error))!;

        Assert.Equal(error.Code, roundTripped.Code);
        Assert.Equal(error.Message, roundTripped.Message);
        Assert.Equal(error.Type, roundTripped.Type);
        Assert.Equal(42L, roundTripped.Metadata["orderId"]);
        Assert.Equal("A-1", roundTripped.Metadata["sku"]);
        Assert.Equal(false, roundTripped.Metadata["retryable"]);
        Assert.Equal(1.5, roundTripped.Metadata["weight"]);
        Assert.Null(roundTripped.Metadata["nothing"]);
    }

    [Theory]
    [InlineData(ErrorType.Failure, "failure")]
    [InlineData(ErrorType.Validation, "validation")]
    [InlineData(ErrorType.NotFound, "notFound")]
    [InlineData(ErrorType.Conflict, "conflict")]
    [InlineData(ErrorType.Unauthorized, "unauthorized")]
    [InlineData(ErrorType.Forbidden, "forbidden")]
    [InlineData(ErrorType.Unavailable, "unavailable")]
    [InlineData(ErrorType.Unexpected, "unexpected")]
    public void Every_error_type_round_trips_with_a_stable_string(ErrorType type, string wire)
    {
        var json = JsonSerializer.Serialize(new Error("C.D", "m", type));
        Assert.Contains($"\"type\":\"{wire}\"", json, StringComparison.Ordinal);

        var back = JsonSerializer.Deserialize<Error>(json)!;
        Assert.Equal(type, back.Type);
    }

    [Fact]
    public void ValidationError_round_trips_as_its_own_type()
    {
        var error = new ValidationError(
            "Signup.Invalid",
            "Signup rejected.",
            [new FieldError("Email", "Required.", "NotEmpty"), new FieldError("Age", "Too low.")]);

        var json = JsonSerializer.Serialize<Error>(error);
        Assert.Contains("\"fieldErrors\":", json, StringComparison.Ordinal);

        var back = Assert.IsType<ValidationError>(JsonSerializer.Deserialize<Error>(json));
        Assert.Equal("Signup.Invalid", back.Code);
        Assert.Equal(2, back.FieldErrors.Count);
        Assert.Equal("NotEmpty", back.FieldErrors[0].Code);
        Assert.Null(back.FieldErrors[1].Code);

        // Also round-trips when declared as ValidationError.
        var declared = JsonSerializer.Deserialize<ValidationError>(JsonSerializer.Serialize(error));
        Assert.Equal(2, declared!.FieldErrors.Count);
    }

    [Fact]
    public void AggregateError_round_trips_with_children()
    {
        var aggregate = new AggregateError([
            Error.NotFound("A", "m"),
            new ValidationError(new FieldError("F", "bad")),
        ]);

        var json = JsonSerializer.Serialize<Error>(aggregate);
        var back = Assert.IsType<AggregateError>(JsonSerializer.Deserialize<Error>(json));

        Assert.Equal(2, back.Errors.Count);
        Assert.IsType<ValidationError>(back.Errors[1]);
        Assert.Equal(aggregate.Type, back.Type);
    }

    [Fact]
    public void Result_serializes_to_the_documented_shape()
    {
        Assert.Equal("""{"isSuccess":true}""", JsonSerializer.Serialize(Result.Success()));
        Assert.Equal(
            """{"isSuccess":false,"error":{"code":"A.B","message":"m","type":"failure"}}""",
            JsonSerializer.Serialize(Result.Failure(Error.Failure("A.B", "m"))));
    }

    [Fact]
    public void Result_round_trips()
    {
        var success = JsonSerializer.Deserialize<Result>(JsonSerializer.Serialize(Result.Success()));
        Assert.True(success.IsSuccess);

        var failure = JsonSerializer.Deserialize<Result>(
            JsonSerializer.Serialize(Result.Failure(Error.Unavailable("Db.Down", "down"))));
        Assert.True(failure.IsFailure);
        Assert.Equal("Db.Down", failure.Error.Code);
        Assert.Equal(ErrorType.Unavailable, failure.Error.Type);
    }

    [Fact]
    public void ResultOfT_serializes_to_the_documented_shape()
    {
        Assert.Equal("""{"isSuccess":true,"value":42}""", JsonSerializer.Serialize(Result.Success(42)));
        Assert.Equal(
            """{"isSuccess":false,"error":{"code":"A.B","message":"m","type":"notFound"}}""",
            JsonSerializer.Serialize(Result.Failure<int>(Error.NotFound("A.B", "m"))));
    }

    [Fact]
    public void ResultOfT_round_trips_complex_values()
    {
        var result = Result.Success(new OrderDto("A-1", 3));
        var back = JsonSerializer.Deserialize<Result<OrderDto>>(JsonSerializer.Serialize(result));

        Assert.True(back.IsSuccess);
        Assert.Equal("A-1", back.Value.Sku);
        Assert.Equal(3, back.Value.Quantity);
    }

    [Fact]
    public void ResultOfT_read_is_property_order_independent()
    {
        var result = JsonSerializer.Deserialize<Result<int>>("""{"value":5,"isSuccess":true}""");
        Assert.Equal(5, result.Value);
    }

    [Fact]
    public void ResultOfT_respects_serializer_options_for_the_value()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(Result.Success(new OrderDto("A-1", 3)), options);

        Assert.Contains("\"sku\":\"A-1\"", json, StringComparison.Ordinal);
        var back = JsonSerializer.Deserialize<Result<OrderDto>>(json, options);
        Assert.Equal("A-1", back.Value.Sku);
    }

    [Theory]
    [InlineData("""{"isSuccess":false}""")]                          // failure without error
    [InlineData("""{"isSuccess":true}""")]                           // success without value (generic)
    [InlineData("""{"isSuccess":true,"value":null}""")]              // success with null value
    [InlineData("""{"value":1}""")]                                  // missing discriminator
    [InlineData("""[1,2]""")]                                        // wrong token
    public void ResultOfT_rejects_malformed_payloads(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Result<int>>(json));
    }

    [Theory]
    [InlineData("""{"code":"A","message":"m","type":"nope"}""")]     // unknown type
    [InlineData("""{"code":"A","type":"failure"}""")]                // missing message
    [InlineData("""{"code":"A","message":"m","type":"failure","fieldErrors":[],"errors":[]}""")] // ambiguous
    [InlineData("""{"code":"A","message":"m","type":"failure","fieldErrors":[]}""")]             // wrong type for fieldErrors + empty
    [InlineData("""{"code":"A","message":"m","type":"validation","fieldErrors":[]}""")]          // empty fieldErrors
    [InlineData("""{"code":"A","message":"m","type":"failure","errors":[{"code":"B","message":"m","type":"failure"}]}""")] // single child aggregate
    public void Error_rejects_malformed_payloads(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Error>(json));
    }

    [Fact]
    public void Unknown_properties_are_ignored_for_forward_compatibility()
    {
        var error = JsonSerializer.Deserialize<Error>(
            """{"code":"A.B","message":"m","type":"failure","futureProperty":{"x":1}}""")!;
        Assert.Equal("A.B", error.Code);

        var result = JsonSerializer.Deserialize<Result<int>>(
            """{"isSuccess":true,"value":1,"futureProperty":[1,2,3]}""");
        Assert.Equal(1, result.Value);
    }

    [Fact]
    public void Nested_metadata_structures_are_preserved_as_json_elements()
    {
        var json = """{"code":"A.B","message":"m","type":"failure","metadata":{"nested":{"a":[1,2]}}}""";
        var error = JsonSerializer.Deserialize<Error>(json)!;

        var element = Assert.IsType<JsonElement>(error.Metadata["nested"]);
        Assert.Equal(JsonValueKind.Object, element.ValueKind);

        // And it re-serializes faithfully.
        var reserialized = JsonSerializer.Serialize(error);
        Assert.Contains("\"nested\":{\"a\":[1,2]}", reserialized, StringComparison.Ordinal);
    }

    private sealed record OrderDto(string Sku, int Quantity);
}
