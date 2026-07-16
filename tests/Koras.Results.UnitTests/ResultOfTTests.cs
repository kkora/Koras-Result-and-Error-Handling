using Koras.Results;

namespace Koras.Results.UnitTests;

public class ResultOfTTests
{
    [Fact]
    public void Success_carries_value_and_hides_error()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void Failure_carries_error_and_hides_value()
    {
        var error = Error.NotFound("User.NotFound", "Missing.");
        var result = Result.Failure<string>(error);

        Assert.True(result.IsFailure);
        Assert.Same(error, result.Error);
        var exception = Assert.Throws<InvalidOperationException>(() => result.Value);
        Assert.Contains("User.NotFound", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Success_rejects_null_value()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Success<string>(null!));
        Assert.Throws<ArgumentNullException>(() =>
        {
            Result<string> _ = (string)null!;
        });
    }

    [Fact]
    public void Failure_rejects_null_error()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Failure<int>(null!));
    }

    [Fact]
    public void Default_result_is_an_uninitialized_failure()
    {
        Result<int> result = default;

        Assert.True(result.IsFailure);
        Assert.Same(Error.Uninitialized, result.Error);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Implicit_conversions_work_in_both_directions()
    {
        Result<int> success = 5;
        Result<int> failure = Error.Conflict("A", "m");

        Assert.True(success.IsSuccess);
        Assert.Equal(5, success.Value);
        Assert.True(failure.IsFailure);

        Result dropped = success;
        Assert.True(dropped.IsSuccess);

        Result droppedFailure = failure;
        Assert.True(droppedFailure.IsFailure);
        Assert.Equal("A", droppedFailure.Error.Code);
    }

    [Fact]
    public void TryGetValue_and_TryGetError_reflect_outcome()
    {
        var success = Result.Success("hello");
        var failure = Result.Failure<string>(Error.Failure("A", "m"));

        Assert.True(success.TryGetValue(out var value));
        Assert.Equal("hello", value);
        Assert.False(success.TryGetError(out _));

        Assert.False(failure.TryGetValue(out _));
        Assert.True(failure.TryGetError(out var error));
        Assert.Equal("A", error.Code);
    }

    [Fact]
    public void GetValueOrDefault_returns_value_or_fallback()
    {
        var success = Result.Success(10);
        var failure = Result.Failure<int>(Error.Failure("A", "m"));

        Assert.Equal(10, success.GetValueOrDefault());
        Assert.Equal(0, failure.GetValueOrDefault());
        Assert.Equal(99, failure.GetValueOrDefault(99));
        Assert.Equal(10, success.GetValueOrDefault(99));
    }

    [Fact]
    public void ToResult_preserves_outcome()
    {
        Assert.True(Result.Success(1).ToResult().IsSuccess);

        var converted = Result.Failure<int>(Error.NotFound("A", "m")).ToResult();
        Assert.True(converted.IsFailure);
        Assert.Equal("A", converted.Error.Code);
    }

    [Fact]
    public void Equality_compares_values_or_errors()
    {
        Assert.Equal(Result.Success(3), Result.Success(3));
        Assert.NotEqual(Result.Success(3), Result.Success(4));
        Assert.Equal(
            Result.Failure<int>(Error.NotFound("A", "x")),
            Result.Failure<int>(Error.NotFound("A", "y")));
        Assert.NotEqual(Result.Success(3), Result.Failure<int>(Error.Failure("A", "m")));
        Assert.True(Result.Success(3) == Result.Success(3));
        Assert.True(Result.Success(3) != Result.Success(4));
    }

    [Fact]
    public void Value_types_and_reference_types_are_both_supported()
    {
        Assert.Equal(3.14, Result.Success(3.14).Value);
        var list = new List<int> { 1 };
        Assert.Same(list, Result.Success(list).Value);
    }
}
