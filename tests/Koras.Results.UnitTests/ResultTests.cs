using Koras.Results;

namespace Koras.Results.UnitTests;

public class ResultTests
{
    [Fact]
    public void Success_reports_success_and_hides_error()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        var exception = Assert.Throws<InvalidOperationException>(() => result.Error);
        Assert.Contains("IsFailure", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Failure_reports_failure_and_carries_error()
    {
        var error = Error.NotFound("User.NotFound", "Missing.");
        var result = Result.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Same(error, result.Error);
    }

    [Fact]
    public void Failure_rejects_null_error()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Failure(null!));
    }

    [Fact]
    public void Default_result_is_an_uninitialized_failure()
    {
        Result result = default;

        Assert.True(result.IsFailure);
        Assert.Same(Error.Uninitialized, result.Error);
    }

    [Fact]
    public void Implicit_conversion_from_error_produces_failure()
    {
        Result result = Error.Conflict("A.B", "m");

        Assert.True(result.IsFailure);
        Assert.Equal("A.B", result.Error.Code);
    }

    [Fact]
    public void FromError_matches_implicit_conversion()
    {
        var error = Error.Conflict("A.B", "m");
        Assert.Equal((Result)error, Result.FromError(error));
    }

    [Fact]
    public void Equality_follows_outcome_and_error_identity()
    {
        Assert.Equal(Result.Success(), Result.Success());
        Assert.Equal(
            Result.Failure(Error.NotFound("A", "x")),
            Result.Failure(Error.NotFound("A", "y")));
        Assert.NotEqual(Result.Success(), Result.Failure(Error.Failure("A", "m")));
        Assert.NotEqual(
            Result.Failure(Error.NotFound("A", "m")),
            Result.Failure(Error.NotFound("B", "m")));
        Assert.True(Result.Success() == Result.Success());
        Assert.True(Result.Success() != Result.Failure(Error.Failure("A", "m")));
    }

    [Fact]
    public void ToString_reflects_outcome()
    {
        Assert.Equal("Success", Result.Success().ToString());
        Assert.Contains("User.NotFound", Result.Failure(Error.NotFound("User.NotFound", "m")).ToString(), StringComparison.Ordinal);
    }
}
