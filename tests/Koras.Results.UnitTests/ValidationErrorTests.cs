using Koras.Results;

namespace Koras.Results.UnitTests;

public class ValidationErrorTests
{
    [Fact]
    public void Default_constructor_uses_default_code_and_message()
    {
        var error = new ValidationError(new FieldError("Email", "Required."));

        Assert.Equal(ValidationError.DefaultCode, error.Code);
        Assert.Equal(ValidationError.DefaultMessage, error.Message);
        Assert.Equal(ErrorType.Validation, error.Type);
    }

    [Fact]
    public void Field_error_order_is_preserved()
    {
        var error = new ValidationError(
            new FieldError("A", "first"),
            new FieldError("B", "second"),
            new FieldError("A", "third"));

        Assert.Equal(["first", "second", "third"], error.FieldErrors.Select(f => f.Message));
    }

    [Fact]
    public void Custom_code_and_message_are_used()
    {
        var error = new ValidationError("Order.Invalid", "Order is invalid.", [new FieldError("Qty", "Too low.")]);

        Assert.Equal("Order.Invalid", error.Code);
        Assert.Equal("Order is invalid.", error.Message);
    }

    [Fact]
    public void Empty_or_null_field_errors_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => new ValidationError());
        Assert.Throws<ArgumentException>(() => new ValidationError(Enumerable.Empty<FieldError>()));
        Assert.Throws<ArgumentNullException>(() => new ValidationError((IEnumerable<FieldError>)null!));
        Assert.Throws<ArgumentException>(() => new ValidationError(new FieldError("A", "m"), null!));
    }

    [Fact]
    public void Supplied_collection_is_defensively_copied()
    {
        var list = new List<FieldError> { new("A", "m") };
        var error = new ValidationError(list);

        list.Add(new FieldError("B", "n"));

        Assert.Single(error.FieldErrors);
    }

    [Fact]
    public void WithMetadata_preserves_validation_error_shape()
    {
        var error = new ValidationError(new FieldError("Email", "Required."));
        var enriched = error.WithMetadata("requestId", "r-1");

        var validation = Assert.IsType<ValidationError>(enriched);
        Assert.Single(validation.FieldErrors);
        Assert.Equal("r-1", validation.Metadata["requestId"]);
    }
}

public class AggregateErrorTests
{
    [Fact]
    public void Requires_at_least_two_errors()
    {
        Assert.Throws<ArgumentException>(() => new AggregateError([Error.Failure("A", "m")]));
        Assert.Throws<ArgumentNullException>(() => new AggregateError(null!));
        Assert.Throws<ArgumentException>(() => new AggregateError([Error.Failure("A", "m"), null!]));
    }

    [Fact]
    public void Type_is_highest_severity_of_children()
    {
        var aggregate = new AggregateError([
            new ValidationError(new FieldError("A", "m")),
            Error.NotFound("B", "m"),
            Error.Unavailable("C", "m"),
        ]);

        Assert.Equal(ErrorType.Unavailable, aggregate.Type);
        Assert.Equal(AggregateError.DefaultCode, aggregate.Code);
    }

    [Fact]
    public void Nested_aggregates_are_flattened()
    {
        var inner = new AggregateError([Error.Failure("A", "m"), Error.Conflict("B", "m")]);
        var outer = new AggregateError([inner, Error.NotFound("C", "m")]);

        Assert.Equal(3, outer.Errors.Count);
        Assert.DoesNotContain(outer.Errors, e => e is AggregateError);
    }

    [Fact]
    public void WithMetadata_preserves_aggregate_shape()
    {
        var aggregate = new AggregateError([Error.Failure("A", "m"), Error.Unexpected("B", "m")]);
        var enriched = aggregate.WithMetadata("traceId", "t-1");

        var typed = Assert.IsType<AggregateError>(enriched);
        Assert.Equal(2, typed.Errors.Count);
        Assert.Equal(ErrorType.Unexpected, typed.Type);
    }
}
