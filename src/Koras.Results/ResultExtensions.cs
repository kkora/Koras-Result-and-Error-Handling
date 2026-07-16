namespace Koras.Results;

/// <summary>
/// Synchronous functional composition for <see cref="Result"/> and <see cref="Result{T}"/>:
/// <c>Map</c>, <c>Bind</c>, <c>Match</c>, <c>Switch</c>, <c>Ensure</c>, <c>Tap</c>,
/// <c>TapError</c>, and <c>MapError</c>.
/// </summary>
/// <remarks>
/// Failure short-circuits: on a failure result, transformation delegates are never invoked and
/// the original error propagates by identity. Delegates that throw are not caught — use
/// <see cref="Result.Try{T}(Func{T}, Func{Exception, Error}?)"/> at exception boundaries.
/// </remarks>
public static class ResultExtensions
{
    /// <summary>Transforms the value of a success; propagates failures unchanged.</summary>
    /// <typeparam name="TIn">The input value type.</typeparam>
    /// <typeparam name="TOut">The output value type.</typeparam>
    /// <param name="result">The result to transform.</param>
    /// <param name="map">The transformation; must not return null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is null.</exception>
    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return result.IsSuccess ? Result.Success(map(result.Value)) : Result.Failure<TOut>(result.Error);
    }

    /// <summary>Produces a value from a success; propagates failures unchanged.</summary>
    /// <typeparam name="TOut">The output value type.</typeparam>
    /// <param name="result">The result to transform.</param>
    /// <param name="map">The value factory; must not return null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is null.</exception>
    public static Result<TOut> Map<TOut>(this Result result, Func<TOut> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return result.IsSuccess ? Result.Success(map()) : Result.Failure<TOut>(result.Error);
    }

    /// <summary>Chains a result-returning operation onto a success; propagates failures unchanged.</summary>
    /// <typeparam name="TIn">The input value type.</typeparam>
    /// <typeparam name="TOut">The output value type.</typeparam>
    /// <param name="result">The result to chain from.</param>
    /// <param name="bind">The next operation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bind"/> is null.</exception>
    public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> result, Func<TIn, Result<TOut>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        return result.IsSuccess ? bind(result.Value) : Result.Failure<TOut>(result.Error);
    }

    /// <summary>Chains a void-result operation onto a success; propagates failures unchanged.</summary>
    /// <typeparam name="TIn">The input value type.</typeparam>
    /// <param name="result">The result to chain from.</param>
    /// <param name="bind">The next operation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bind"/> is null.</exception>
    public static Result Bind<TIn>(this Result<TIn> result, Func<TIn, Result> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        return result.IsSuccess ? bind(result.Value) : Result.Failure(result.Error);
    }

    /// <summary>Chains a value-producing operation onto a success; propagates failures unchanged.</summary>
    /// <typeparam name="TOut">The output value type.</typeparam>
    /// <param name="result">The result to chain from.</param>
    /// <param name="bind">The next operation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bind"/> is null.</exception>
    public static Result<TOut> Bind<TOut>(this Result result, Func<Result<TOut>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        return result.IsSuccess ? bind() : Result.Failure<TOut>(result.Error);
    }

    /// <summary>Chains a void-result operation onto a success; propagates failures unchanged.</summary>
    /// <param name="result">The result to chain from.</param>
    /// <param name="bind">The next operation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bind"/> is null.</exception>
    public static Result Bind(this Result result, Func<Result> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        return result.IsSuccess ? bind() : result;
    }

    /// <summary>Folds both branches into a single value.</summary>
    /// <typeparam name="TIn">The input value type.</typeparam>
    /// <typeparam name="TOut">The folded output type.</typeparam>
    /// <param name="result">The result to fold.</param>
    /// <param name="onSuccess">Invoked with the value on success.</param>
    /// <param name="onFailure">Invoked with the error on failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="onSuccess"/> or <paramref name="onFailure"/> is null.</exception>
    public static TOut Match<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return result.IsSuccess ? onSuccess(result.Value) : onFailure(result.Error);
    }

    /// <summary>Folds both branches into a single value.</summary>
    /// <typeparam name="TOut">The folded output type.</typeparam>
    /// <param name="result">The result to fold.</param>
    /// <param name="onSuccess">Invoked on success.</param>
    /// <param name="onFailure">Invoked with the error on failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="onSuccess"/> or <paramref name="onFailure"/> is null.</exception>
    public static TOut Match<TOut>(this Result result, Func<TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return result.IsSuccess ? onSuccess() : onFailure(result.Error);
    }

    /// <summary>Invokes the matching action for the branch.</summary>
    /// <typeparam name="TIn">The input value type.</typeparam>
    /// <param name="result">The result to inspect.</param>
    /// <param name="onSuccess">Invoked with the value on success.</param>
    /// <param name="onFailure">Invoked with the error on failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="onSuccess"/> or <paramref name="onFailure"/> is null.</exception>
    public static void Switch<TIn>(this Result<TIn> result, Action<TIn> onSuccess, Action<Error> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        if (result.IsSuccess)
        {
            onSuccess(result.Value);
        }
        else
        {
            onFailure(result.Error);
        }
    }

    /// <summary>Invokes the matching action for the branch.</summary>
    /// <param name="result">The result to inspect.</param>
    /// <param name="onSuccess">Invoked on success.</param>
    /// <param name="onFailure">Invoked with the error on failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="onSuccess"/> or <paramref name="onFailure"/> is null.</exception>
    public static void Switch(this Result result, Action onSuccess, Action<Error> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        if (result.IsSuccess)
        {
            onSuccess();
        }
        else
        {
            onFailure(result.Error);
        }
    }

    /// <summary>
    /// Asserts a post-condition on a success value: when <paramref name="predicate"/> returns
    /// false, the result becomes a failure carrying <paramref name="error"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result to check.</param>
    /// <param name="predicate">The condition the value must satisfy.</param>
    /// <param name="error">The error produced when the condition fails.</param>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> or <paramref name="error"/> is null.</exception>
    public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, Error error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(error);
        if (result.IsFailure)
        {
            return result;
        }

        return predicate(result.Value) ? result : Result.Failure<T>(error);
    }

    /// <summary>
    /// Asserts a post-condition on a success value: when <paramref name="predicate"/> returns
    /// false, the result becomes a failure carrying the error produced by
    /// <paramref name="errorFactory"/> (which receives the value).
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result to check.</param>
    /// <param name="predicate">The condition the value must satisfy.</param>
    /// <param name="errorFactory">Produces the error when the condition fails.</param>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> or <paramref name="errorFactory"/> is null.</exception>
    public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, Func<T, Error> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(errorFactory);
        if (result.IsFailure)
        {
            return result;
        }

        return predicate(result.Value) ? result : Result.Failure<T>(errorFactory(result.Value));
    }

    /// <summary>Runs a side effect on success; the result passes through unchanged.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result to observe.</param>
    /// <param name="action">The side effect, receiving the value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    public static Result<T> Tap<T>(this Result<T> result, Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (result.IsSuccess)
        {
            action(result.Value);
        }

        return result;
    }

    /// <summary>Runs a side effect on success; the result passes through unchanged.</summary>
    /// <param name="result">The result to observe.</param>
    /// <param name="action">The side effect.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    public static Result Tap(this Result result, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (result.IsSuccess)
        {
            action();
        }

        return result;
    }

    /// <summary>Runs a side effect on failure; the result passes through unchanged.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result to observe.</param>
    /// <param name="action">The side effect, receiving the error.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    public static Result<T> TapError<T>(this Result<T> result, Action<Error> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (result.IsFailure)
        {
            action(result.Error);
        }

        return result;
    }

    /// <summary>Runs a side effect on failure; the result passes through unchanged.</summary>
    /// <param name="result">The result to observe.</param>
    /// <param name="action">The side effect, receiving the error.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    public static Result TapError(this Result result, Action<Error> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (result.IsFailure)
        {
            action(result.Error);
        }

        return result;
    }

    /// <summary>Translates the error of a failure (e.g. at a layer boundary); successes pass through.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result to translate.</param>
    /// <param name="map">The error translation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is null.</exception>
    public static Result<T> MapError<T>(this Result<T> result, Func<Error, Error> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return result.IsFailure ? Result.Failure<T>(map(result.Error)) : result;
    }

    /// <summary>Translates the error of a failure (e.g. at a layer boundary); successes pass through.</summary>
    /// <param name="result">The result to translate.</param>
    /// <param name="map">The error translation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is null.</exception>
    public static Result MapError(this Result result, Func<Error, Error> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return result.IsFailure ? Result.Failure(map(result.Error)) : result;
    }
}
