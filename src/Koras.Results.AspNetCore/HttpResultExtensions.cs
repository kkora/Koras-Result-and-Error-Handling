using Microsoft.AspNetCore.Http;

// Inside this namespace the identifier "Results" binds to the Koras.Results namespace, so the
// ASP.NET Core static class needs an alias.
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Koras.Results.AspNetCore;

/// <summary>
/// Minimal API adapters: convert results into <see cref="IResult"/> responses. Successes map to
/// 204/200/201 (or a custom factory); failures become RFC 9457 ProblemDetails responses using the
/// options registered via <see cref="KorasResultsServiceCollectionExtensions.AddKorasResults"/>
/// (or built-in defaults when the package was not registered).
/// </summary>
public static class HttpResultExtensions
{
    /// <summary>Maps success to 204 No Content and failure to ProblemDetails.</summary>
    /// <param name="result">The result to convert.</param>
    public static IResult ToHttpResult(this Result result) =>
        result.IsSuccess ? HttpResults.NoContent() : new KorasProblemHttpResult(result.Error);

    /// <summary>Maps success to 200 OK with a JSON body and failure to ProblemDetails.</summary>
    /// <typeparam name="T">The result's value type.</typeparam>
    /// <param name="result">The result to convert.</param>
    public static IResult ToHttpResult<T>(this Result<T> result) =>
        result.IsSuccess ? HttpResults.Ok(result.Value) : new KorasProblemHttpResult(result.Error);

    /// <summary>Maps success through <paramref name="onSuccess"/> and failure to ProblemDetails.</summary>
    /// <typeparam name="T">The result's value type.</typeparam>
    /// <param name="result">The result to convert.</param>
    /// <param name="onSuccess">Produces the success response (e.g. <c>v =&gt; Results.Accepted()</c>).</param>
    /// <exception cref="ArgumentNullException"><paramref name="onSuccess"/> is null.</exception>
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        return result.IsSuccess ? onSuccess(result.Value) : new KorasProblemHttpResult(result.Error);
    }

    /// <summary>Maps success to 201 Created with a Location header and failure to ProblemDetails.</summary>
    /// <typeparam name="T">The result's value type.</typeparam>
    /// <param name="result">The result to convert.</param>
    /// <param name="locationFactory">Produces the Location URI from the created value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="locationFactory"/> is null.</exception>
    public static IResult ToCreatedHttpResult<T>(this Result<T> result, Func<T, string> locationFactory)
    {
        ArgumentNullException.ThrowIfNull(locationFactory);
        return result.IsSuccess
            ? HttpResults.Created(locationFactory(result.Value), result.Value)
            : new KorasProblemHttpResult(result.Error);
    }

    /// <summary>Awaits the result, then maps per <see cref="ToHttpResult(Result)"/>.</summary>
    /// <param name="resultTask">The task producing the result.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resultTask"/> is null.</exception>
    public static Task<IResult> ToHttpResultAsync(this Task<Result> resultTask)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        return Awaited(resultTask);

        static async Task<IResult> Awaited(Task<Result> resultTask) =>
            (await resultTask.ConfigureAwait(false)).ToHttpResult();
    }

    /// <summary>Awaits the result, then maps per <see cref="ToHttpResult{T}(Result{T})"/>.</summary>
    /// <typeparam name="T">The result's value type.</typeparam>
    /// <param name="resultTask">The task producing the result.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resultTask"/> is null.</exception>
    public static Task<IResult> ToHttpResultAsync<T>(this Task<Result<T>> resultTask)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        return Awaited(resultTask);

        static async Task<IResult> Awaited(Task<Result<T>> resultTask) =>
            (await resultTask.ConfigureAwait(false)).ToHttpResult();
    }

    /// <summary>Awaits the result, then maps per <see cref="ToHttpResult{T}(Result{T}, Func{T, IResult})"/>.</summary>
    /// <typeparam name="T">The result's value type.</typeparam>
    /// <param name="resultTask">The task producing the result.</param>
    /// <param name="onSuccess">Produces the success response.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resultTask"/> or <paramref name="onSuccess"/> is null.</exception>
    public static Task<IResult> ToHttpResultAsync<T>(this Task<Result<T>> resultTask, Func<T, IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(onSuccess);
        return Awaited(resultTask, onSuccess);

        static async Task<IResult> Awaited(Task<Result<T>> resultTask, Func<T, IResult> onSuccess) =>
            (await resultTask.ConfigureAwait(false)).ToHttpResult(onSuccess);
    }
}
