using System.Diagnostics;

namespace Koras.Results.OpenTelemetry;

/// <summary>
/// Tags <see cref="Activity"/> instances with failure information following OpenTelemetry
/// semantic conventions. Successes, absent activities, and non-recording activities are
/// allocation-free no-ops. This package never creates activities — it only annotates the
/// caller's.
/// </summary>
public static class ActivityResultExtensions
{
    /// <summary>Tags <see cref="Activity.Current"/> when the result is a failure; returns the result unchanged.</summary>
    /// <param name="result">The result to observe.</param>
    public static Result TagCurrentActivity(this Result result) => result.TagActivity(Activity.Current);

    /// <summary>Tags <see cref="Activity.Current"/> when the result is a failure; returns the result unchanged.</summary>
    /// <typeparam name="T">The result's value type.</typeparam>
    /// <param name="result">The result to observe.</param>
    public static Result<T> TagCurrentActivity<T>(this Result<T> result) => result.TagActivity(Activity.Current);

    /// <summary>Tags <paramref name="activity"/> when the result is a failure; returns the result unchanged.</summary>
    /// <param name="result">The result to observe.</param>
    /// <param name="activity">The activity to tag; null is a no-op.</param>
    public static Result TagActivity(this Result result, Activity? activity)
    {
        if (result.IsFailure)
        {
            Tag(activity, result.Error);
        }

        return result;
    }

    /// <summary>Tags <paramref name="activity"/> when the result is a failure; returns the result unchanged.</summary>
    /// <typeparam name="T">The result's value type.</typeparam>
    /// <param name="result">The result to observe.</param>
    /// <param name="activity">The activity to tag; null is a no-op.</param>
    public static Result<T> TagActivity<T>(this Result<T> result, Activity? activity)
    {
        if (result.IsFailure)
        {
            Tag(activity, result.Error);
        }

        return result;
    }

    /// <summary>
    /// Awaits the result and tags <see cref="Activity.Current"/> on failure — the combinator form
    /// for use inside async pipelines.
    /// </summary>
    /// <param name="resultTask">The task producing the result to observe.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resultTask"/> is null.</exception>
    public static Task<Result> TapActivityErrorAsync(this Task<Result> resultTask)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        return Awaited(resultTask);

        static async Task<Result> Awaited(Task<Result> resultTask) =>
            (await resultTask.ConfigureAwait(false)).TagCurrentActivity();
    }

    /// <summary>
    /// Awaits the result and tags <see cref="Activity.Current"/> on failure — the combinator form
    /// for use inside async pipelines.
    /// </summary>
    /// <typeparam name="T">The result's value type.</typeparam>
    /// <param name="resultTask">The task producing the result to observe.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resultTask"/> is null.</exception>
    public static Task<Result<T>> TapActivityErrorAsync<T>(this Task<Result<T>> resultTask)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        return Awaited(resultTask);

        static async Task<Result<T>> Awaited(Task<Result<T>> resultTask) =>
            (await resultTask.ConfigureAwait(false)).TagCurrentActivity();
    }

    private static void Tag(Activity? activity, Error error)
    {
        if (activity is null || !activity.IsAllDataRequested)
        {
            return;
        }

        activity.SetStatus(ActivityStatusCode.Error, error.Code);
        activity.SetTag(KorasResultsActivityTags.ErrorType, ToSnakeCase(error.Type));
        activity.SetTag(KorasResultsActivityTags.ErrorCode, error.Code);

        if (error is AggregateError aggregate)
        {
            activity.SetTag(KorasResultsActivityTags.AggregateCount, aggregate.Errors.Count);
        }
    }

    private static string ToSnakeCase(ErrorType type) => type switch
    {
        Results.ErrorType.Failure => "failure",
        Results.ErrorType.Validation => "validation",
        Results.ErrorType.NotFound => "not_found",
        Results.ErrorType.Conflict => "conflict",
        Results.ErrorType.Unauthorized => "unauthorized",
        Results.ErrorType.Forbidden => "forbidden",
        Results.ErrorType.Unavailable => "unavailable",
        _ => "unexpected",
    };
}
