using Koras.Results;

namespace Koras.Results.UnitTests;

public class ResultAsyncExtensionsTests
{
    private static readonly Error TestError = Error.NotFound("Test.Error", "Test error.");

    private static Task<Result<int>> SuccessTask(int value = 1) => Task.FromResult(Result.Success(value));

    private static Task<Result<int>> FailureTask() => Task.FromResult(Result.Failure<int>(TestError));

    // ── MapAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task MapAsync_sync_receiver_async_delegate()
    {
        var result = await Result.Success(2).MapAsync(v => Task.FromResult(v * 10));
        Assert.Equal(20, result.Value);

        var invocations = 0;
        var failure = await Result.Failure<int>(TestError).MapAsync(v => { invocations++; return Task.FromResult(v); });
        Assert.Same(TestError, failure.Error);
        Assert.Equal(0, invocations);
    }

    [Fact]
    public async Task MapAsync_task_receiver_sync_delegate()
    {
        var result = await SuccessTask(2).MapAsync(v => v + 1);
        Assert.Equal(3, result.Value);

        var failure = await FailureTask().MapAsync(v => v + 1);
        Assert.Same(TestError, failure.Error);
    }

    [Fact]
    public async Task MapAsync_task_receiver_async_delegate()
    {
        var result = await SuccessTask(2).MapAsync(v => Task.FromResult(v + 1));
        Assert.Equal(3, result.Value);

        var invocations = 0;
        var failure = await FailureTask().MapAsync(v => { invocations++; return Task.FromResult(v); });
        Assert.Same(TestError, failure.Error);
        Assert.Equal(0, invocations);
    }

    [Fact]
    public async Task MapAsync_nongeneric_task_receiver()
    {
        var result = await Task.FromResult(Result.Success()).MapAsync(() => 5);
        Assert.Equal(5, result.Value);

        var failure = await Task.FromResult(Result.Failure(TestError)).MapAsync(() => 5);
        Assert.Same(TestError, failure.Error);
    }

    // ── BindAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task BindAsync_sync_receiver_async_delegate()
    {
        var result = await Result.Success(2).BindAsync(v => Task.FromResult(Result.Success(v + 1)));
        Assert.Equal(3, result.Value);

        var invocations = 0;
        var failure = await Result.Failure<int>(TestError)
            .BindAsync(v => { invocations++; return Task.FromResult(Result.Success(v)); });
        Assert.Same(TestError, failure.Error);
        Assert.Equal(0, invocations);
    }

    [Fact]
    public async Task BindAsync_task_receiver_sync_delegate()
    {
        var result = await SuccessTask(2).BindAsync(v => Result.Success(v * 2));
        Assert.Equal(4, result.Value);

        var inner = await SuccessTask(2).BindAsync(_ => Result.Failure<int>(TestError));
        Assert.Same(TestError, inner.Error);
    }

    [Fact]
    public async Task BindAsync_task_receiver_async_delegate()
    {
        var result = await SuccessTask(2).BindAsync(v => Task.FromResult(Result.Success(v * 2)));
        Assert.Equal(4, result.Value);

        var invocations = 0;
        var failure = await FailureTask().BindAsync(v => { invocations++; return Task.FromResult(Result.Success(v)); });
        Assert.Same(TestError, failure.Error);
        Assert.Equal(0, invocations);
    }

    [Fact]
    public async Task BindAsync_to_nongeneric_result()
    {
        var result = await SuccessTask(2).BindAsync(_ => Task.FromResult(Result.Success()));
        Assert.True(result.IsSuccess);

        var failure = await FailureTask().BindAsync(_ => Task.FromResult(Result.Success()));
        Assert.Same(TestError, failure.Error);

        var chained = await Task.FromResult(Result.Success()).BindAsync(() => Task.FromResult(Result.Failure(TestError)));
        Assert.Same(TestError, chained.Error);
    }

    // ── MatchAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task MatchAsync_folds_both_branches()
    {
        Assert.Equal("v:1", await SuccessTask().MatchAsync(v => $"v:{v}", e => $"e:{e.Code}"));
        Assert.Equal("e:Test.Error", await FailureTask().MatchAsync(v => $"v:{v}", e => $"e:{e.Code}"));

        Assert.Equal("v:1", await SuccessTask().MatchAsync(
            v => Task.FromResult($"v:{v}"),
            e => Task.FromResult($"e:{e.Code}")));
        Assert.Equal("e:Test.Error", await FailureTask().MatchAsync(
            v => Task.FromResult($"v:{v}"),
            e => Task.FromResult($"e:{e.Code}")));

        Assert.Equal("ok", await Task.FromResult(Result.Success()).MatchAsync(() => "ok", e => e.Code));
        Assert.Equal("Test.Error", await Task.FromResult(Result.Failure(TestError)).MatchAsync(() => "ok", e => e.Code));
    }

    // ── EnsureAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task EnsureAsync_variants_enforce_predicates()
    {
        Assert.Equal(1, (await SuccessTask().EnsureAsync(v => v > 0, TestError)).Value);
        Assert.Same(TestError, (await SuccessTask().EnsureAsync(v => v > 5, TestError)).Error);

        Assert.Equal(1, (await Result.Success(1).EnsureAsync(v => Task.FromResult(v > 0), TestError)).Value);
        Assert.Same(TestError, (await Result.Success(1).EnsureAsync(v => Task.FromResult(v > 5), TestError)).Error);

        Assert.Equal(1, (await SuccessTask().EnsureAsync(v => Task.FromResult(v > 0), TestError)).Value);
        Assert.Same(TestError, (await SuccessTask().EnsureAsync(v => Task.FromResult(v > 5), TestError)).Error);
    }

    [Fact]
    public async Task EnsureAsync_short_circuits_on_failure_without_invoking_predicate()
    {
        var invocations = 0;
        var original = Error.Conflict("Original", "m");
        var result = await Task.FromResult(Result.Failure<int>(original))
            .EnsureAsync(v => { invocations++; return Task.FromResult(true); }, TestError);

        Assert.Same(original, result.Error);
        Assert.Equal(0, invocations);
    }

    // ── TapAsync / TapErrorAsync ───────────────────────────────────────────

    [Fact]
    public async Task TapAsync_variants_run_only_on_success()
    {
        var seen = new List<int>();

        await Result.Success(1).TapAsync(v => { seen.Add(v); return Task.CompletedTask; });
        await Result.Failure<int>(TestError).TapAsync(v => { seen.Add(v); return Task.CompletedTask; });
        await SuccessTask(2).TapAsync(seen.Add);
        await FailureTask().TapAsync(seen.Add);
        await SuccessTask(3).TapAsync(v => { seen.Add(v); return Task.CompletedTask; });
        await FailureTask().TapAsync(v => { seen.Add(v); return Task.CompletedTask; });

        var count = 0;
        await Task.FromResult(Result.Success()).TapAsync(() => count++);
        await Task.FromResult(Result.Failure(TestError)).TapAsync(() => count++);

        Assert.Equal([1, 2, 3], seen);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task TapErrorAsync_variants_run_only_on_failure()
    {
        var seen = new List<string>();

        await SuccessTask().TapErrorAsync(e => seen.Add(e.Code));
        await FailureTask().TapErrorAsync(e => seen.Add(e.Code));
        await SuccessTask().TapErrorAsync(e => { seen.Add(e.Code); return Task.CompletedTask; });
        await FailureTask().TapErrorAsync(e => { seen.Add(e.Code); return Task.CompletedTask; });

        Assert.Equal(["Test.Error", "Test.Error"], seen);
    }

    // ── Pipelines, faults, guards ──────────────────────────────────────────

    [Fact]
    public async Task Full_pipeline_composes_and_short_circuits()
    {
        var result = await SuccessTask(10)
            .MapAsync(v => v * 2)
            .EnsureAsync(v => v > 5, TestError)
            .BindAsync(v => Task.FromResult(Result.Success($"value-{v}")))
            .MatchAsync(v => v, e => $"failed-{e.Code}");

        Assert.Equal("value-20", result);

        var shortCircuited = await SuccessTask(1)
            .EnsureAsync(v => v > 5, TestError)
            .MapAsync(v => v * 1000)
            .MatchAsync(v => $"v{v}", e => e.Code);

        Assert.Equal("Test.Error", shortCircuited);
    }

    [Fact]
    public async Task Faulted_tasks_propagate_their_exceptions()
    {
        var faulted = Task.FromException<Result<int>>(new InvalidOperationException("boom"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => faulted.MapAsync(v => v + 1));
    }

    [Fact]
    public async Task Async_delegate_exceptions_are_not_caught()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SuccessTask().MapAsync((Func<int, int>)(_ => throw new InvalidOperationException("boom"))));
    }

    [Fact]
    public void Null_arguments_throw_eagerly_not_inside_the_task()
    {
        // These asserts verify the EAGER guard contract: argument validation must throw at call
        // time (synchronously), never deferred into the returned task. Assert.Throws with a
        // discarded task is therefore exactly what is being tested.
#pragma warning disable xUnit2014
        Assert.Throws<ArgumentNullException>(() => { _ = ((Task<Result<int>>)null!).MapAsync(v => v); });
        Assert.Throws<ArgumentNullException>(() => { _ = SuccessTask().MapAsync((Func<int, int>)null!); });
        Assert.Throws<ArgumentNullException>(() => { _ = SuccessTask().BindAsync((Func<int, Result<int>>)null!); });
        Assert.Throws<ArgumentNullException>(() => { _ = SuccessTask().MatchAsync(null!, e => 0); });
        Assert.Throws<ArgumentNullException>(() => { _ = SuccessTask().EnsureAsync(v => true, null!); });
        Assert.Throws<ArgumentNullException>(() => { _ = SuccessTask().TapAsync((Action<int>)null!); });
        Assert.Throws<ArgumentNullException>(() => { _ = SuccessTask().TapErrorAsync((Action<Error>)null!); });
#pragma warning restore xUnit2014
    }

    [Fact]
    public async Task Cancellation_propagates_through_pipelines()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        static async Task<int> CancelledOperation(CancellationToken ct)
        {
            await Task.Delay(1, ct);
            return 1;
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Result.Success(1).MapAsync(_ => CancelledOperation(cts.Token)));
    }
}
