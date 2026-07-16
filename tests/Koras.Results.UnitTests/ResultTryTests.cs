using Koras.Results;

namespace Koras.Results.UnitTests;

public class ResultTryTests
{
    [Fact]
    public void Try_returns_success_when_nothing_throws()
    {
        Assert.True(Result.Try(() => { }).IsSuccess);
        Assert.Equal(42, Result.Try(() => 42).Value);
    }

    [Fact]
    public void Try_converts_exceptions_using_the_safe_default()
    {
        var result = Result.Try<int>(() => throw new FormatException("secret detail"));

        Assert.True(result.IsFailure);
        Assert.Equal("Unexpected.Exception", result.Error.Code);
        Assert.Equal(ErrorType.Unexpected, result.Error.Type);
        Assert.Equal(typeof(FormatException).FullName, result.Error.Metadata["exceptionType"]);
        // The exception message must never leak into the default error.
        Assert.DoesNotContain("secret detail", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Try_uses_the_custom_mapper_with_the_original_exception()
    {
        Exception? observed = null;
        var result = Result.Try<int>(
            () => throw new TimeoutException("timed out"),
            ex => { observed = ex; return Error.Unavailable("Db.Timeout", "The database timed out."); });

        Assert.Equal("Db.Timeout", result.Error.Code);
        Assert.IsType<TimeoutException>(observed);
    }

    [Fact]
    public void Try_rethrows_cancellation()
    {
        Assert.Throws<OperationCanceledException>(
            () => Result.Try(() => throw new OperationCanceledException()));
        Assert.Throws<TaskCanceledException>(
            () => Result.Try<int>(() => throw new TaskCanceledException()));
    }

    [Fact]
    public async Task TryAsync_returns_success_when_nothing_throws()
    {
        Assert.True((await Result.TryAsync(() => Task.CompletedTask)).IsSuccess);
        Assert.Equal(7, (await Result.TryAsync(() => Task.FromResult(7))).Value);
    }

    [Fact]
    public async Task TryAsync_converts_async_faults()
    {
        var result = await Result.TryAsync<int>(async () =>
        {
            await Task.Yield();
            throw new InvalidOperationException("boom");
        });

        Assert.Equal("Unexpected.Exception", result.Error.Code);
        Assert.Equal(typeof(InvalidOperationException).FullName, result.Error.Metadata["exceptionType"]);
    }

    [Fact]
    public async Task TryAsync_uses_the_custom_mapper()
    {
        var result = await Result.TryAsync(
            () => Task.FromException(new TimeoutException()),
            _ => Error.Unavailable("Op.Timeout", "Timed out."));

        Assert.Equal("Op.Timeout", result.Error.Code);
        Assert.Equal(ErrorType.Unavailable, result.Error.Type);
    }

    [Fact]
    public async Task TryAsync_rethrows_cancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Result.TryAsync(() => Task.Delay(5000, cts.Token)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Result.TryAsync<int>(async () =>
            {
                await Task.Delay(5000, cts.Token);
                return 1;
            }));
    }

    [Fact]
    public void Try_guards_null_delegates()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Try((Action)null!));
        Assert.Throws<ArgumentNullException>(() => Result.Try((Func<int>)null!));

        // TryAsync guards must throw eagerly at call time, not inside the returned task.
#pragma warning disable xUnit2014
        Assert.Throws<ArgumentNullException>(() => { _ = Result.TryAsync((Func<Task>)null!); });
        Assert.Throws<ArgumentNullException>(() => { _ = Result.TryAsync((Func<Task<int>>)null!); });
#pragma warning restore xUnit2014
    }

    [Fact]
    public void Try_nongeneric_converts_exceptions()
    {
        var result = Result.Try(() => throw new InvalidOperationException("x"));
        Assert.True(result.IsFailure);
        Assert.Equal("Unexpected.Exception", result.Error.Code);
    }
}
