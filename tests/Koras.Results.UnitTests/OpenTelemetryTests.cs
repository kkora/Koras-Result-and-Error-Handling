using System.Diagnostics;
using Koras.Results.OpenTelemetry;

namespace Koras.Results.UnitTests;

public class OpenTelemetryTests : IDisposable
{
    private readonly ActivitySource _source = new("Koras.Results.Tests");
    private readonly ActivityListener _listener;

    public OpenTelemetryTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Koras.Results.Tests",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
        _source.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Failure_tags_the_activity_with_type_and_code()
    {
        using var activity = _source.StartActivity("op")!;

        var result = Result.Failure<int>(Error.NotFound("User.NotFound", "m")).TagActivity(activity);

        Assert.True(result.IsFailure);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("User.NotFound", activity.StatusDescription);
        Assert.Equal("not_found", activity.GetTagItem(KorasResultsActivityTags.ErrorType));
        Assert.Equal("User.NotFound", activity.GetTagItem(KorasResultsActivityTags.ErrorCode));
    }

    [Fact]
    public void Success_leaves_the_activity_untouched()
    {
        using var activity = _source.StartActivity("op")!;

        Result.Success(1).TagActivity(activity);
        Result.Success().TagActivity(activity);

        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        Assert.Null(activity.GetTagItem(KorasResultsActivityTags.ErrorCode));
    }

    [Fact]
    public void Null_and_absent_activities_are_safe_no_ops()
    {
        var result = Result.Failure(Error.Failure("A", "m"));

        Assert.Equal(result, result.TagActivity(null));

        Assert.Null(Activity.Current);
        Assert.Equal(result, result.TagCurrentActivity());
    }

    [Fact]
    public void Aggregate_errors_record_the_child_count()
    {
        using var activity = _source.StartActivity("op")!;
        var aggregate = new AggregateError([Error.Failure("A", "m"), Error.NotFound("B", "m")]);

        Result.Failure(aggregate).TagActivity(activity);

        Assert.Equal(2, activity.GetTagItem(KorasResultsActivityTags.AggregateCount));
    }

    [Theory]
    [InlineData(ErrorType.Failure, "failure")]
    [InlineData(ErrorType.Validation, "validation")]
    [InlineData(ErrorType.NotFound, "not_found")]
    [InlineData(ErrorType.Conflict, "conflict")]
    [InlineData(ErrorType.Unauthorized, "unauthorized")]
    [InlineData(ErrorType.Forbidden, "forbidden")]
    [InlineData(ErrorType.Unavailable, "unavailable")]
    [InlineData(ErrorType.Unexpected, "unexpected")]
    public void Error_type_tag_uses_snake_case(ErrorType type, string expected)
    {
        using var activity = _source.StartActivity("op")!;

        Result.Failure(new Error("C.D", "m", type)).TagActivity(activity);

        Assert.Equal(expected, activity.GetTagItem(KorasResultsActivityTags.ErrorType));
    }

    [Fact]
    public async Task TapActivityErrorAsync_tags_current_activity_in_pipelines()
    {
        using var activity = _source.StartActivity("op")!;

        var result = await Task.FromResult(Result.Failure<int>(Error.Unavailable("Db.Down", "down")))
            .TapActivityErrorAsync();

        Assert.True(result.IsFailure);
        Assert.Equal("Db.Down", activity.GetTagItem(KorasResultsActivityTags.ErrorCode));

        var voidResult = await Task.FromResult(Result.Failure(Error.Failure("X", "m"))).TapActivityErrorAsync();
        Assert.True(voidResult.IsFailure);
    }

    [Fact]
    public void Non_recording_activities_are_not_tagged()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Koras.Results.Tests.PropagationOnly",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.PropagationData,
        };
        ActivitySource.AddActivityListener(listener);
        using var source = new ActivitySource("Koras.Results.Tests.PropagationOnly");
        using var activity = source.StartActivity("op")!;

        Assert.False(activity.IsAllDataRequested);
        Result.Failure(Error.Failure("A", "m")).TagActivity(activity);

        Assert.Null(activity.GetTagItem(KorasResultsActivityTags.ErrorCode));
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
    }
}
