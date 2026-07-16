using Koras.Results;

namespace Koras.Results.UnitTests;

public class ResultExtensionsTests
{
    private static readonly Error TestError = Error.NotFound("Test.Error", "Test error.");

    // ── Map ────────────────────────────────────────────────────────────────

    [Fact]
    public void Map_transforms_success_value()
    {
        var result = Result.Success(2).Map(v => v * 10);
        Assert.Equal(20, result.Value);
    }

    [Fact]
    public void Map_short_circuits_on_failure_without_invoking_delegate()
    {
        var invocations = 0;
        var result = Result.Failure<int>(TestError).Map(v => { invocations++; return v; });

        Assert.True(result.IsFailure);
        Assert.Same(TestError, result.Error);
        Assert.Equal(0, invocations);
    }

    [Fact]
    public void Map_on_nongeneric_result_produces_value()
    {
        Assert.Equal(7, Result.Success().Map(() => 7).Value);
        Assert.Same(TestError, Result.Failure(TestError).Map(() => 7).Error);
    }

    // ── Bind ───────────────────────────────────────────────────────────────

    [Fact]
    public void Bind_chains_success()
    {
        var result = Result.Success(2).Bind(v => Result.Success(v + 1));
        Assert.Equal(3, result.Value);
    }

    [Fact]
    public void Bind_propagates_inner_failure()
    {
        var result = Result.Success(2).Bind(_ => Result.Failure<int>(TestError));
        Assert.Same(TestError, result.Error);
    }

    [Fact]
    public void Bind_short_circuits_on_failure_without_invoking_delegate()
    {
        var invocations = 0;
        var result = Result.Failure<int>(TestError).Bind(v => { invocations++; return Result.Success(v); });

        Assert.Same(TestError, result.Error);
        Assert.Equal(0, invocations);
    }

    [Fact]
    public void Bind_variants_cover_nongeneric_shapes()
    {
        Assert.True(Result.Success(1).Bind(_ => Result.Success()).IsSuccess);
        Assert.Equal(5, Result.Success().Bind(() => Result.Success(5)).Value);
        Assert.True(Result.Success().Bind(Result.Success).IsSuccess);
        Assert.Same(TestError, Result.Failure(TestError).Bind(() => Result.Success(5)).Error);
    }

    // ── Match / Switch ─────────────────────────────────────────────────────

    [Fact]
    public void Match_folds_both_branches()
    {
        Assert.Equal("v:9", Result.Success(9).Match(v => $"v:{v}", e => $"e:{e.Code}"));
        Assert.Equal("e:Test.Error", Result.Failure<int>(TestError).Match(v => $"v:{v}", e => $"e:{e.Code}"));
        Assert.Equal("ok", Result.Success().Match(() => "ok", e => e.Code));
        Assert.Equal("Test.Error", Result.Failure(TestError).Match(() => "ok", e => e.Code));
    }

    [Fact]
    public void Switch_invokes_only_matching_branch()
    {
        var log = new List<string>();
        Result.Success(1).Switch(v => log.Add($"s{v}"), _ => log.Add("f"));
        Result.Failure<int>(TestError).Switch(v => log.Add($"s{v}"), e => log.Add($"f{e.Code}"));
        Result.Success().Switch(() => log.Add("s"), _ => log.Add("f"));
        Result.Failure(TestError).Switch(() => log.Add("s"), e => log.Add("f2"));

        Assert.Equal(["s1", "fTest.Error", "s", "f2"], log);
    }

    // ── Ensure ─────────────────────────────────────────────────────────────

    [Fact]
    public void Ensure_passes_when_predicate_holds()
    {
        var result = Result.Success(10).Ensure(v => v > 5, TestError);
        Assert.Equal(10, result.Value);
    }

    [Fact]
    public void Ensure_fails_when_predicate_rejects()
    {
        var result = Result.Success(1).Ensure(v => v > 5, TestError);
        Assert.Same(TestError, result.Error);
    }

    [Fact]
    public void Ensure_with_factory_receives_the_value()
    {
        var result = Result.Success(1).Ensure(v => v > 5, v => Error.Validation("Too.Small", $"{v} is too small."));
        Assert.Equal("Too.Small", result.Error.Code);
        Assert.Contains("1 is too small", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ensure_short_circuits_on_failure_without_invoking_predicate()
    {
        var invocations = 0;
        var original = Error.Conflict("Original", "m");
        var result = Result.Failure<int>(original).Ensure(v => { invocations++; return true; }, TestError);

        Assert.Same(original, result.Error);
        Assert.Equal(0, invocations);
    }

    // ── Tap / TapError ─────────────────────────────────────────────────────

    [Fact]
    public void Tap_runs_only_on_success_and_passes_through()
    {
        var seen = new List<int>();
        var success = Result.Success(3).Tap(seen.Add);
        var failure = Result.Failure<int>(TestError).Tap(seen.Add);

        Assert.Equal([3], seen);
        Assert.Equal(3, success.Value);
        Assert.Same(TestError, failure.Error);

        var count = 0;
        Result.Success().Tap(() => count++);
        Result.Failure(TestError).Tap(() => count++);
        Assert.Equal(1, count);
    }

    [Fact]
    public void TapError_runs_only_on_failure_and_passes_through()
    {
        var seen = new List<string>();
        Result.Success(3).TapError(e => seen.Add(e.Code));
        Result.Failure<int>(TestError).TapError(e => seen.Add(e.Code));
        Result.Success().TapError(e => seen.Add("x"));
        Result.Failure(TestError).TapError(e => seen.Add(e.Code));

        Assert.Equal(["Test.Error", "Test.Error"], seen);
    }

    // ── MapError ───────────────────────────────────────────────────────────

    [Fact]
    public void MapError_translates_failures_and_passes_successes_through()
    {
        var translated = Result.Failure<int>(TestError).MapError(e => Error.Failure("Wrapped", e.Message));
        Assert.Equal("Wrapped", translated.Error.Code);

        var success = Result.Success(1).MapError(_ => TestError);
        Assert.Equal(1, success.Value);

        var nonGeneric = Result.Failure(TestError).MapError(e => Error.Failure("Wrapped", e.Message));
        Assert.Equal("Wrapped", nonGeneric.Error.Code);
        Assert.True(Result.Success().MapError(_ => TestError).IsSuccess);
    }

    // ── Guards ─────────────────────────────────────────────────────────────

    [Fact]
    public void Null_delegates_throw_eagerly()
    {
        var success = Result.Success(1);
        Assert.Throws<ArgumentNullException>(() => success.Map((Func<int, int>)null!));
        Assert.Throws<ArgumentNullException>(() => success.Bind((Func<int, Result<int>>)null!));
        Assert.Throws<ArgumentNullException>(() => success.Match(null!, e => 0));
        Assert.Throws<ArgumentNullException>(() => success.Match(v => v, null!));
        Assert.Throws<ArgumentNullException>(() => success.Ensure(null!, TestError));
        Assert.Throws<ArgumentNullException>(() => success.Ensure(v => true, (Error)null!));
        Assert.Throws<ArgumentNullException>(() => success.Tap(null!));
        Assert.Throws<ArgumentNullException>(() => success.TapError(null!));
        Assert.Throws<ArgumentNullException>(() => success.MapError(null!));
    }

    [Fact]
    public void Delegate_exceptions_are_not_caught()
    {
        var success = Result.Success(1);
        Assert.Throws<InvalidOperationException>(() => success.Map<int, int>(_ => throw new InvalidOperationException("boom")));
    }
}
