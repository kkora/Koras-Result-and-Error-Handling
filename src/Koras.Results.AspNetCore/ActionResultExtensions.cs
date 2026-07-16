using Microsoft.AspNetCore.Mvc;

namespace Koras.Results.AspNetCore;

/// <summary>
/// MVC adapters: convert results into <see cref="IActionResult"/> responses. Successes map to
/// 204/200 (or a custom factory); failures become RFC 9457 ProblemDetails responses using the
/// options registered via <see cref="KorasResultsServiceCollectionExtensions.AddKorasResults"/>
/// (or built-in defaults when the package was not registered).
/// </summary>
public static class ActionResultExtensions
{
    /// <summary>Maps success to 204 No Content and failure to ProblemDetails.</summary>
    /// <param name="result">The result to convert.</param>
    public static IActionResult ToActionResult(this Result result) =>
        result.IsSuccess ? new NoContentResult() : new KorasProblemActionResult(result.Error);

    /// <summary>Maps success to 200 OK with a JSON body and failure to ProblemDetails.</summary>
    /// <typeparam name="T">The result's value type.</typeparam>
    /// <param name="result">The result to convert.</param>
    public static IActionResult ToActionResult<T>(this Result<T> result) =>
        result.IsSuccess ? new OkObjectResult(result.Value) : new KorasProblemActionResult(result.Error);

    /// <summary>Maps success through <paramref name="onSuccess"/> and failure to ProblemDetails.</summary>
    /// <typeparam name="T">The result's value type.</typeparam>
    /// <param name="result">The result to convert.</param>
    /// <param name="onSuccess">Produces the success response (e.g. <c>v =&gt; new CreatedResult(...)</c>).</param>
    /// <exception cref="ArgumentNullException"><paramref name="onSuccess"/> is null.</exception>
    public static IActionResult ToActionResult<T>(this Result<T> result, Func<T, IActionResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        return result.IsSuccess ? onSuccess(result.Value) : new KorasProblemActionResult(result.Error);
    }

    /// <summary>
    /// Maps to <see cref="ActionResult{T}"/>: success carries the value (200 OK), failure becomes
    /// ProblemDetails.
    /// </summary>
    /// <typeparam name="T">The result's value type.</typeparam>
    /// <param name="result">The result to convert.</param>
    public static ActionResult<T> ToActionResultOf<T>(this Result<T> result) =>
        result.IsSuccess ? result.Value : new KorasProblemActionResult(result.Error);

    /// <summary>Awaits the result, then maps per <see cref="ToActionResult(Result)"/>.</summary>
    /// <param name="resultTask">The task producing the result.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resultTask"/> is null.</exception>
    public static Task<IActionResult> ToActionResultAsync(this Task<Result> resultTask)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        return Awaited(resultTask);

        static async Task<IActionResult> Awaited(Task<Result> resultTask) =>
            (await resultTask.ConfigureAwait(false)).ToActionResult();
    }

    /// <summary>Awaits the result, then maps per <see cref="ToActionResult{T}(Result{T})"/>.</summary>
    /// <typeparam name="T">The result's value type.</typeparam>
    /// <param name="resultTask">The task producing the result.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resultTask"/> is null.</exception>
    public static Task<IActionResult> ToActionResultAsync<T>(this Task<Result<T>> resultTask)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        return Awaited(resultTask);

        static async Task<IActionResult> Awaited(Task<Result<T>> resultTask) =>
            (await resultTask.ConfigureAwait(false)).ToActionResult();
    }
}
