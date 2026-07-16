namespace Koras.Results;

/// <content>
/// Exception-boundary factories: <c>Try</c> and <c>TryAsync</c> convert thrown exceptions into
/// failure results at the edge between exception-based and result-based code.
/// </content>
public readonly partial struct Result
{
    /// <summary>
    /// Executes <paramref name="action"/> and converts any thrown exception into a failure result.
    /// <see cref="OperationCanceledException"/> is always rethrown: cancellation is not failure.
    /// </summary>
    /// <param name="action">The operation to execute.</param>
    /// <param name="mapError">
    /// Optional exception-to-error mapping. When omitted, the failure carries
    /// <c>"Unexpected.Exception"</c> with the exception type (but never its message) in metadata.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    public static Result Try(Action action, Func<Exception, Error>? mapError = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            action();
            return Success();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Failure(MapException(exception, mapError));
        }
    }

    /// <summary>
    /// Executes <paramref name="func"/> and converts any thrown exception into a failure result.
    /// <see cref="OperationCanceledException"/> is always rethrown: cancellation is not failure.
    /// </summary>
    /// <typeparam name="T">The type of the value produced on success.</typeparam>
    /// <param name="func">The operation to execute.</param>
    /// <param name="mapError">
    /// Optional exception-to-error mapping. When omitted, the failure carries
    /// <c>"Unexpected.Exception"</c> with the exception type (but never its message) in metadata.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is null.</exception>
    public static Result<T> Try<T>(Func<T> func, Func<Exception, Error>? mapError = null)
    {
        ArgumentNullException.ThrowIfNull(func);

        try
        {
            return Success(func());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Failure<T>(MapException(exception, mapError));
        }
    }

    /// <summary>
    /// Awaits <paramref name="action"/> and converts any thrown exception into a failure result.
    /// <see cref="OperationCanceledException"/> is always rethrown: cancellation is not failure.
    /// </summary>
    /// <param name="action">The asynchronous operation to execute.</param>
    /// <param name="mapError">
    /// Optional exception-to-error mapping. When omitted, the failure carries
    /// <c>"Unexpected.Exception"</c> with the exception type (but never its message) in metadata.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    public static Task<Result> TryAsync(Func<Task> action, Func<Exception, Error>? mapError = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return Awaited(action, mapError);

        static async Task<Result> Awaited(Func<Task> action, Func<Exception, Error>? mapError)
        {
            try
            {
                await action().ConfigureAwait(false);
                return Success();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return Failure(MapException(exception, mapError));
            }
        }
    }

    /// <summary>
    /// Awaits <paramref name="func"/> and converts any thrown exception into a failure result.
    /// <see cref="OperationCanceledException"/> is always rethrown: cancellation is not failure.
    /// </summary>
    /// <typeparam name="T">The type of the value produced on success.</typeparam>
    /// <param name="func">The asynchronous operation to execute.</param>
    /// <param name="mapError">
    /// Optional exception-to-error mapping. When omitted, the failure carries
    /// <c>"Unexpected.Exception"</c> with the exception type (but never its message) in metadata.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is null.</exception>
    public static Task<Result<T>> TryAsync<T>(Func<Task<T>> func, Func<Exception, Error>? mapError = null)
    {
        ArgumentNullException.ThrowIfNull(func);
        return Awaited(func, mapError);

        static async Task<Result<T>> Awaited(Func<Task<T>> func, Func<Exception, Error>? mapError)
        {
            try
            {
                return Success(await func().ConfigureAwait(false));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return Failure<T>(MapException(exception, mapError));
            }
        }
    }

    private static Error MapException(Exception exception, Func<Exception, Error>? mapError)
    {
        if (mapError is not null)
        {
            return mapError(exception);
        }

        // Deliberately excludes the exception message: it may contain sensitive details and the
        // default must be safe to project to clients. Map explicitly when the message is needed.
        return Error
            .Unexpected("Unexpected.Exception", "An unexpected error occurred.")
            .WithMetadata("exceptionType", exception.GetType().FullName);
    }
}
