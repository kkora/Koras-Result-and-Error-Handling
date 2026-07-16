using Koras.Results;

namespace Koras.Results.UnitTests;

public class ResultCombineTests
{
    [Fact]
    public void Combine_of_all_successes_is_success()
    {
        Assert.True(Result.Combine(Result.Success(), Result.Success()).IsSuccess);
        Assert.True(Result.Combine(Array.Empty<Result>()).IsSuccess);
    }

    [Fact]
    public void Combine_with_single_failure_passes_the_error_through_by_identity()
    {
        var error = Error.NotFound("A", "m");
        var combined = Result.Combine(Result.Success(), Result.Failure(error), Result.Success());

        Assert.Same(error, combined.Error);
    }

    [Fact]
    public void Combine_merges_multiple_validation_errors_into_one()
    {
        var first = new ValidationError(new FieldError("Email", "Required."));
        var second = new ValidationError(new FieldError("Age", "Too low."), new FieldError("Age", "Not a number."));

        var combined = Result.Combine(Result.Failure(first), Result.Failure(second));

        var validation = Assert.IsType<ValidationError>(combined.Error);
        Assert.Equal(3, validation.FieldErrors.Count);
        Assert.Equal(["Email", "Age", "Age"], validation.FieldErrors.Select(f => f.PropertyName));
    }

    [Fact]
    public void Combine_aggregates_heterogeneous_errors_with_severity_precedence()
    {
        var combined = Result.Combine(
            Result.Failure(new ValidationError(new FieldError("A", "m"))),
            Result.Failure(Error.Unavailable("Db.Down", "down")),
            Result.Failure(Error.NotFound("X", "m")));

        var aggregate = Assert.IsType<AggregateError>(combined.Error);
        Assert.Equal(3, aggregate.Errors.Count);
        Assert.Equal(ErrorType.Unavailable, aggregate.Type);
    }

    [Fact]
    public void Combine_rejects_null_input()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Combine((IEnumerable<Result>)null!));
    }

    [Fact]
    public void Tuple_combine_returns_all_values_on_success()
    {
        var two = Result.Combine(Result.Success(1), Result.Success("a"));
        Assert.Equal((1, "a"), two.Value);

        var three = Result.Combine(Result.Success(1), Result.Success("a"), Result.Success(true));
        Assert.Equal((1, "a", true), three.Value);

        var four = Result.Combine(Result.Success(1), Result.Success("a"), Result.Success(true), Result.Success(2.5));
        Assert.Equal((1, "a", true, 2.5), four.Value);
        Assert.Equal(1, four.Value.First);
        Assert.Equal(2.5, four.Value.Fourth);
    }

    [Fact]
    public void Tuple_combine_aggregates_failures()
    {
        var error1 = Error.NotFound("A", "m");
        var error2 = Error.Conflict("B", "m");

        var single = Result.Combine(Result.Failure<int>(error1), Result.Success("a"));
        Assert.Same(error1, single.Error);

        var multiple = Result.Combine(Result.Failure<int>(error1), Result.Failure<string>(error2));
        var aggregate = Assert.IsType<AggregateError>(multiple.Error);
        Assert.Equal(2, aggregate.Errors.Count);
    }
}
