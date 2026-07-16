using Koras.Results;

namespace Koras.Results.UnitTests;

public class ErrorTests
{
    [Fact]
    public void Constructor_sets_all_properties()
    {
        var metadata = new Dictionary<string, object?> { ["sku"] = "A-1" };
        var error = new Error("Order.Rejected", "The order was rejected.", ErrorType.Conflict, metadata);

        Assert.Equal("Order.Rejected", error.Code);
        Assert.Equal("The order was rejected.", error.Message);
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal("A-1", error.Metadata["sku"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_invalid_code(string? code)
    {
        var exception = Assert.Throws<ArgumentException>(() => new Error(code!, "message", ErrorType.Failure));
        Assert.Equal("code", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_invalid_message(string? message)
    {
        var exception = Assert.Throws<ArgumentException>(() => new Error("Code", message!, ErrorType.Failure));
        Assert.Equal("message", exception.ParamName);
    }

    [Fact]
    public void Metadata_is_never_null_and_empty_by_default()
    {
        var error = Error.Failure("A.B", "m");
        Assert.NotNull(error.Metadata);
        Assert.Empty(error.Metadata);
    }

    [Fact]
    public void Metadata_is_defensively_copied()
    {
        var source = new Dictionary<string, object?> { ["k"] = 1 };
        var error = new Error("A.B", "m", ErrorType.Failure, source);

        source["k"] = 2;
        source["extra"] = 3;

        Assert.Equal(1L, Convert.ToInt64(error.Metadata["k"], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Single(error.Metadata);
    }

    [Theory]
    [InlineData(ErrorType.Failure)]
    [InlineData(ErrorType.Validation)]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Conflict)]
    [InlineData(ErrorType.Unauthorized)]
    [InlineData(ErrorType.Forbidden)]
    [InlineData(ErrorType.Unavailable)]
    [InlineData(ErrorType.Unexpected)]
    public void Factory_methods_set_the_matching_type(ErrorType type)
    {
        var error = type switch
        {
            ErrorType.Failure => Error.Failure("C", "m"),
            ErrorType.Validation => Error.Validation("C", "m"),
            ErrorType.NotFound => Error.NotFound("C", "m"),
            ErrorType.Conflict => Error.Conflict("C", "m"),
            ErrorType.Unauthorized => Error.Unauthorized("C", "m"),
            ErrorType.Forbidden => Error.Forbidden("C", "m"),
            ErrorType.Unavailable => Error.Unavailable("C", "m"),
            ErrorType.Unexpected => Error.Unexpected("C", "m"),
            _ => throw new InvalidOperationException(),
        };

        Assert.Equal(type, error.Type);
    }

    [Fact]
    public void Equality_is_code_and_type_only()
    {
        var a = Error.NotFound("User.NotFound", "message one");
        var b = Error.NotFound("User.NotFound", "different message").WithMetadata("id", 5);
        var differentCode = Error.NotFound("Account.NotFound", "message one");
        var differentType = Error.Conflict("User.NotFound", "message one");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, differentCode);
        Assert.NotEqual(a, differentType);
        Assert.False(a.Equals(null));
    }

    [Fact]
    public void WithMetadata_returns_new_instance_and_preserves_existing_entries()
    {
        var original = Error.Failure("A.B", "m").WithMetadata("first", 1);
        var enriched = original.WithMetadata("second", 2);

        Assert.Single(original.Metadata);
        Assert.Equal(2, enriched.Metadata.Count);
        Assert.NotSame(original, enriched);
        Assert.Equal(original, enriched); // equality unaffected by metadata
    }

    [Fact]
    public void WithMetadata_dictionary_merges_and_overwrites()
    {
        var error = Error.Failure("A.B", "m").WithMetadata("k", "old");
        var merged = error.WithMetadata(new Dictionary<string, object?> { ["k"] = "new", ["extra"] = true });

        Assert.Equal("new", merged.Metadata["k"]);
        Assert.Equal(true, merged.Metadata["extra"]);
    }

    [Fact]
    public void WithMetadata_rejects_invalid_key_and_null_dictionary()
    {
        var error = Error.Failure("A.B", "m");
        Assert.Throws<ArgumentException>(() => error.WithMetadata(" ", 1));
        Assert.Throws<ArgumentNullException>(() => error.WithMetadata(null!));
    }

    [Fact]
    public void ToString_contains_type_code_and_message()
    {
        var text = Error.NotFound("User.NotFound", "The user does not exist.").ToString();
        Assert.Contains("NotFound", text, StringComparison.Ordinal);
        Assert.Contains("User.NotFound", text, StringComparison.Ordinal);
        Assert.Contains("The user does not exist.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Sentinels_are_singletons_with_expected_shape()
    {
        Assert.Same(Error.None, Error.None);
        Assert.Equal(string.Empty, Error.None.Code);
        Assert.Equal("Result.Uninitialized", Error.Uninitialized.Code);
        Assert.Equal(ErrorType.Unexpected, Error.Uninitialized.Type);
    }
}
