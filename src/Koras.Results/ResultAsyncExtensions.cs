namespace Koras.Results;

/// <summary>
/// Asynchronous functional composition for <see cref="Result"/> and <see cref="Result{T}"/>.
/// Overloads cover three axes: synchronous receiver with asynchronous delegate, task receiver
/// with synchronous delegate, and task receiver with asynchronous delegate.
/// </summary>
/// <remarks>
/// <para>
/// All awaits use <c>ConfigureAwait(false)</c>. Failure short-circuits: delegates are never
/// invoked on failures and the original error propagates by identity. Delegate exceptions and
/// faulted tasks propagate unchanged — use
/// <see cref="Result.TryAsync{T}(Func{Task{T}}, Func{Exception, Error}?)"/> at exception boundaries.
/// </para>
/// <para>
/// Cancellation tokens belong to the I/O calls inside your delegates (capture them by closure);
/// the combinators are pure plumbing and deliberately take no token.
/// </para>
/// </remarks>
public static class ResultAsyncExtensions
{
    // ── Map ────────────────────────────────────────────────────────────────

    /// <summary>Transforms the value of a success with an asynchronous mapping; propagates failures unchanged.</summary>
    /// <typeparam name="TIn">The input value type.</typeparam>
    /// <typeparam name="TOut">The output value type.</typeparam>
    /// <param name="result">The result to transform.</param>
    /// <param name="map">The asynchronous transformation; must not return null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is null.</exception>
    public static Task<Result<TOut>> MapAsync<TIn, TOut>(this Result<TIn> result, Func<TIn, Task<TOut>> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return result.IsSuccess
            ? Awaited(result, map)
            : Task.FromResult(Result.Failure<TOut>(result.Error));

        static async Task<Result<TOut>> Awaited(Result<TIn> result, Func<TIn, Task<TOut>> map) =>
            Result.Success(await map(result.Value).ConfigureAwait(false));
    }

    /// <summary>Transforms the value of an awaited success; propagates failures unchanged.</summary>
    /// <typeparam name="TIn">The input value type.</typeparam>
    /// <typeparam name="TOut">The output value type.</typeparam>
    /// <param name="resultTask">The task producing the result to transform.</param>
    /// <param name="map">The transformation; must not return null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resultTask"/> or <paramref name="map"/> is null.</exception>
    public static Task<Result<TOut>> MapAsync<TIn, TOut>(this Task<Result<TIn>> resultTask, Func<TIn, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(map);
        return Awaited(resultTask, map);

        static async Task<Result<TOut>> Awaited(Task<Result<TIn>> resultTask, Func<TIn, TOut> map) =>
            (await resultTask.ConfigureAwait(false)).Map(map);
    }

    /// <summary>Transforms the value of an awaited success with an asynchronous mapping; propagates failures unchanged.</summary>
    /// <typeparam name="TIn">The input value type.</typeparam>
    /// <typeparam name="TOut">The output value type.</typeparam>
    /// <param name="resultTask">The task producing the result to transform.</param>
    /// <param name="map">The asynchronous transformation; must not return null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resultTask"/> or <paramref name="map"/> is null.</exception>
    public static Task<Result<TOut>> MapAsync<TIn, TOut>(this Task<Result<TIn>> resultTask, Func<TIn, Task<TOut>> map)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(map);
        return Awaited(resultTask, map);

        static async Task<Result<TOut>> Awaited(Task<Result<TIn>> resultTask, Func<TIn, Task<TOut>> map)
        {
            var result = await resultTask.ConfigureAwait(false);
            return result.IsSuccess
                ? Result.Success(await map(result.Value).ConfigureAwait(false))
                : Result.Failure<TOut>(result.Error);
        }
    }

    /// <summary>Produces a value from an awaited success; propagates failures unchanged.</summary>
    /// <typeparam name="TOut">The output value type.</typeparam>
    /// <param name="resultTask">The task producing the result to transform.</param>
    /// <param name="map">The value factory; must not return null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resultTask"/> or <paramref name="map"/> is null.</exception>
    public static Task<Result<TOut>> MapAsync<TOut>(this Task<Result> resultTask, Func<TOut> map)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(map);
        return Awaited(resultTask, map);

        static async Task<Result<TOut>> Awaited(Task<Result> resultTask, Func<TOut> map) =>
            (await resultTask.ConfigureAwait(false)).Map(map);
    }

    // ── Bind ───────────────────────────────────────────────────────────────

    /// <summary>Chains an asynchronous result-returning operation onto a success; propagates failures unchanged.</summary>
    /// <typeparam name="TIn">The input value type.</typeparam>
    /// <typeparam name="TOut">The output value type.</typeparam>
    /// <param name="result">The result to chain from.</param>
    /// <param name="bind">The next asynchronous operation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bind"/> is null.</exception>
    public static Task<Result<TOut>> BindAsync<TIn, TOut>(this Result<TIn> result, Func<TIn, Task<Result<TOut>>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        return result.IsSuccess
            ? bind(result.Value)
            : Task.FromResult(Result.Failure<TOut>(result.Error));
    }

    /// <summary>Chains a result-returning operation onto an awaited success; propagates failures unchanged.</summary>
    /// <typeparam name="TIn">The input value type.</typeparam>
    /// <typeparam name="TOut">The output value type.</typeparam>
    /// <param name="resultTask">The task producing the result to chain from.</param>
    /// <param name="bind">The next operation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resultTask"/> or <paramref name="bind"/> is null.</exception>
    public static Task<Result<TOut>> BindAsync<TIn, TOut>(this Task<Result<TIn>> resultTask, Func<TIn, Result<TOut>> bind)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(bind);
        return Awaited(resultTask, bind);

        static async Task<Result<TOut>> Awaited(Task<Result<TIn>> resultTask, Func<TIn, Result<TOut>> bind) =>
            (await resultTask.ConfigureAwait(false)).Bind(bind);
    }

    /// <summary>Chains an asynchronous result-returning operation onto an awaited success; propagates failures unchanged.</summary>
    /// <typeparam name="TIn">The input value type.</typeparam>
    /// <typeparam name="TOut">The output value type.</typeparam>
    /// <param name="resultTask">The task producing the result to chain from.</param>
    /// <param name="bind">The next asynchronous operation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resultTask"/> or <paramref name="bind"/> is null.</exception>
    public static Task<Result<TOut>> BindAsync<TIn, TOut>(this Task<Result<TIn>> resultTask, Func<TIn, Task<Result<TOut>>> bind)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(bind);
        return Awaited(resultTask, bind);

        static async Task<Result<TOut>> Awaited(Task<Result<TIn>> resultTask, Func<TIn, Task<Result<TOut>>> bind)
        {
            var result = await resultTask.ConfigureAwait(false);
            return result.IsSuccess
                ? await bind(result.Value).ConfigureAwait(false)
                : Result.Failure<TOut>(result.Error);
        }
    }

    /// <summary>Chains an asynchronous void-result operation onto an awaited success; propagates failures unchanged.</summary>
    /// <typeparam name="TIn">The input value type.</typeparam>
    /// <param name="resultTask">The task producing the result to chain from.</param>
    /// <param name="bind">The next asynchronous operation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resultTask"/> or <paramref name="bind"/> is null.</exception>
    public static Task<Result> BindAsync<TIn>(this Task<Result<TIn>> resultTask, Func<TIn, Task<Result>> bind)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(bind);
        return Awaited(resultTask, bind);

        static async Task<Result> Awaited(Task<Result<TIn>> resultTask, Func<TIn, Task<Result>> bind)
        {
            var result = await resultTask.ConfigureAwait(false);
            return result.IsSuccess
                ? await bind(result.Value).ConfigureAwait(false)
                : Result.Failure(result.Error);
        }
    }

    /// <summary>Chains an asynchronous void-result operation onto an awaited success; propagates failures unchanged.</summary>
    /// <param name="resultTask">The task producing the result to chain from.</param>
    /// <param name="bind">The next asynchronous operation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resultTask"/> or <paramref name="bind"/> is null.</exception>
    public static Task<Result> BindAsync(this Task<Result> resultTask, Func<Task<Result>> bind)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(bind);
        return Awaited(resultTask, bind);

        static async Task<Result> Awaited(Task<Result> resultTask, Func<Task<Result>> bind)
        {
            var result = await resultTask.ConfigureAwait(false);
            return result.IsSuccess ? await bind().ConfigureAwait(false) : result;
        }
    }

    // ── Match ──────────────────────────────────────────────────────────────

    /// <summary>Folds both branches of an awaited result into a single value.</summary>
    /// <typeparam name="TIn">The input value type.</typeparam>
    /// <typeparam name="TOut">The folded output type.</typeparam>
    /// <param name="resultTask">The task producing the result to fold.</param>
    /// <param name="onSuccess">Invoked with the value on success.</param>
    /// <param name="onFailure">Invoked with the error on failure.</param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public static Task<TOut> MatchAsync<TIn, TOut>(
        this Task<Result<TIn>> resultTask, Func<TIn, TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return Awaited(resultTask, onSuccess, onFailure);

        static async Task<TOut> Awaited(Task<Result<TIn>> resultTask, Func<TIn, TOut> onSuccess, Func<Error, TOut> onFailure) =>
            (await resultTask.ConfigureAwait(false)).Match(onSuccess, onFailure);
    }

    /// <summary>Folds both branches of an awaited result into a single value using asynchronous folds.</summary>
    /// <typeparam name="TIn">The input value type.</typeparam>
    /// <typeparam name="TOut">The folded output type.</typeparam>
    /// <param name="resultTask">The task producing the result to fold.</param>
    /// <param name="onSuccess">Invoked with the value on success.</param>
    /// <param name="onFailure">Invoked with the error on failure.</param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public static Task<TOut> MatchAsync<TIn, TOut>(
        this Task<Result<TIn>> resultTask, Func<TIn, Task<TOut>> onSuccess, Func<Error, Task<TOut>> onFailure)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return Awaited(resultTask, onSuccess, onFailure);

        static async Task<TOut> Awaited(Task<Result<TIn>> resultTask, Func<TIn, Task<TOut>> onSuccess, Func<Error, Task<TOut>> onFailure)
        {
            var result = await resultTask.ConfigureAwait(false);
            return result.IsSuccess
                ? await onSuccess(result.Value).ConfigureAwait(false)
                : await onFailure(result.Error).ConfigureAwait(false);
        }
    }

    /// <summary>Folds both branches of an awaited void result into a single value.</summary>
    /// <typeparam name="TOut">The folded output type.</typeparam>
    /// <param name="resultTask">The task producing the result to fold.</param>
    /// <param name="onSuccess">Invoked on success.</param>
    /// <param name="onFailure">Invoked with the error on failure.</param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public static Task<TOut> MatchAsync<TOut>(
        this Task<Result> resultTask, Func<TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return Awaited(resultTask, onSuccess, onFailure);

        static async Task<TOut> Awaited(Task<Result> resultTask, Func<TOut> onSuccess, Func<Error, TOut> onFailure) =>
            (await resultTask.ConfigureAwait(false)).Match(onSuccess, onFailure);
    }

    // ── Ensure ─────────────────────────────────────────────────────────────

    /// <summary>Asserts a post-condition on an awaited success value.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="resultTask">The task producing the result to check.</param>
    /// <param name="predicate">The condition the value must satisfy.</param>
    /// <param name="error">The error produced when the condition fails.</param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public static Task<Result<T>> EnsureAsync<T>(this Task<Result<T>> resultTask, Func<T, bool> predicate, Error error)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(error);
        return Awaited(resultTask, predicate, error);

        static async Task<Result<T>> Awaited(Task<Result<T>> resultTask, Func<T, bool> predicate, Error error) =>
            (await resultTask.ConfigureAwait(false)).Ensure(predicate, error);
    }

    /// <summary>Asserts an asynchronous post-condition on a success value.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result to check.</param>
    /// <param name="predicate">The asynchronous condition the value must satisfy.</param>
    /// <param name="error">The error produced when the condition fails.</param>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> or <paramref name="error"/> is null.</exception>
    public static Task<Result<T>> EnsureAsync<T>(this Result<T> result, Func<T, Task<bool>> predicate, Error error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(error);
        return result.IsFailure ? Task.FromResult(result) : Awaited(result, predicate, error);

        static async Task<Result<T>> Awaited(Result<T> result, Func<T, Task<bool>> predicate, Error error) =>
            await predicate(result.Value).ConfigureAwait(false) ? result : Result.Failure<T>(error);
    }

    /// <summary>Asserts an asynchronous post-condition on an awaited success value.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="resultTask">The task producing the result to check.</param>
    /// <param name="predicate">The asynchronous condition the value must satisfy.</param>
    /// <param name="error">The error produced when the condition fails.</param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public static Task<Result<T>> EnsureAsync<T>(this Task<Result<T>> resultTask, Func<T, Task<bool>> predicate, Error error)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(error);
        return Awaited(resultTask, predicate, error);

        static async Task<Result<T>> Awaited(Task<Result<T>> resultTask, Func<T, Task<bool>> predicate, Error error)
        {
            var result = await resultTask.ConfigureAwait(false);
            if (result.IsFailure)
            {
                return result;
            }

            return await predicate(result.Value).ConfigureAwait(false) ? result : Result.Failure<T>(error);
        }
    }

    // ── Tap / TapError ─────────────────────────────────────────────────────

    /// <summary>Runs an asynchronous side effect on success; the result passes through unchanged.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result to observe.</param>
    /// <param name="action">The asynchronous side effect, receiving the value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    public static Task<Result<T>> TapAsync<T>(this Result<T> result, Func<T, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return result.IsFailure ? Task.FromResult(result) : Awaited(result, action);

        static async Task<Result<T>> Awaited(Result<T> result, Func<T, Task> action)
        {
            await action(result.Value).ConfigureAwait(false);
            return result;
        }
    }

    /// <summary>Runs a side effect on an awaited success; the result passes through unchanged.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="resultTask">The task producing the result to observe.</param>
    /// <param name="action">The side effect, receiving the value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resultTask"/> or <paramref name="action"/> is null.</exception>
    public static Task<Result<T>> TapAsync<T>(this Task<Result<T>> resultTask, Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(action);
        return Awaited(resultTask, action);

        static async Task<Result<T>> Awaited(Task<Result<T>> resultTask, Action<T> action) =>
            (await resultTask.ConfigureAwait(false)).Tap(action);
    }

    /// <summary>Runs an asynchronous side effect on an awaited success; the result passes through unchanged.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="resultTask">The task producing the result to observe.</param>
    /// <param name="action">The asynchronous side effect, receiving the value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resultTask"/> or <paramref name="action"/> is null.</exception>
    public static Task<Result<T>> TapAsync<T>(this Task<Result<T>> resultTask, Func<T, Task> action)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(action);
        return Awaited(resultTask, action);

        static async Task<Result<T>> Awaited(Task<Result<T>> resultTask, Func<T, Task> action)
        {
            var result = await resultTask.ConfigureAwait(false);
            if (result.IsSuccess)
            {
                await action(result.Value).ConfigureAwait(false);
            }

            return result;
        }
    }

    /// <summary>Runs a side effect on an awaited success; the result passes through unchanged.</summary>
    /// <param name="resultTask">The task producing the result to observe.</param>
    /// <param name="action">The side effect.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resultTask"/> or <paramref name="action"/> is null.</exception>
    public static Task<Result> TapAsync(this Task<Result> resultTask, Action action)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(action);
        return Awaited(resultTask, action);

        static async Task<Result> Awaited(Task<Result> resultTask, Action action) =>
            (await resultTask.ConfigureAwait(false)).Tap(action);
    }

    /// <summary>Runs a side effect on an awaited failure; the result passes through unchanged.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="resultTask">The task producing the result to observe.</param>
    /// <param name="action">The side effect, receiving the error.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resultTask"/> or <paramref name="action"/> is null.</exception>
    public static Task<Result<T>> TapErrorAsync<T>(this Task<Result<T>> resultTask, Action<Error> action)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(action);
        return Awaited(resultTask, action);

        static async Task<Result<T>> Awaited(Task<Result<T>> resultTask, Action<Error> action) =>
            (await resultTask.ConfigureAwait(false)).TapError(action);
    }

    /// <summary>Runs an asynchronous side effect on an awaited failure; the result passes through unchanged.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="resultTask">The task producing the result to observe.</param>
    /// <param name="action">The asynchronous side effect, receiving the error.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resultTask"/> or <paramref name="action"/> is null.</exception>
    public static Task<Result<T>> TapErrorAsync<T>(this Task<Result<T>> resultTask, Func<Error, Task> action)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(action);
        return Awaited(resultTask, action);

        static async Task<Result<T>> Awaited(Task<Result<T>> resultTask, Func<Error, Task> action)
        {
            var result = await resultTask.ConfigureAwait(false);
            if (result.IsFailure)
            {
                await action(result.Error).ConfigureAwait(false);
            }

            return result;
        }
    }
}
