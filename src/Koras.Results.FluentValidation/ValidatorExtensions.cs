using FluentValidation;

namespace Koras.Results.FluentValidation;

/// <summary>
/// Validates instances directly into <see cref="Result{T}"/> values: no exceptions, no separate
/// result inspection — invalid input becomes a failed result carrying a
/// <see cref="ValidationError"/>.
/// </summary>
public static class ValidatorExtensions
{
    /// <summary>
    /// Validates <paramref name="instance"/>: success carrying the instance when valid, otherwise
    /// a failure carrying a <see cref="ValidationError"/>.
    /// </summary>
    /// <typeparam name="T">The validated type.</typeparam>
    /// <param name="validator">The validator to run.</param>
    /// <param name="instance">The instance to validate; must not be null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="validator"/> or <paramref name="instance"/> is null.</exception>
    public static Result<T> ValidateToResult<T>(this IValidator<T> validator, T instance)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(instance);
        return validator.Validate(instance).ToResult(instance);
    }

    /// <summary>
    /// Asynchronously validates <paramref name="instance"/>: success carrying the instance when
    /// valid, otherwise a failure carrying a <see cref="ValidationError"/>.
    /// </summary>
    /// <typeparam name="T">The validated type.</typeparam>
    /// <param name="validator">The validator to run.</param>
    /// <param name="instance">The instance to validate; must not be null.</param>
    /// <param name="cancellationToken">Cancels asynchronous validation rules.</param>
    /// <exception cref="ArgumentNullException"><paramref name="validator"/> or <paramref name="instance"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Validation was cancelled.</exception>
    public static Task<Result<T>> ValidateToResultAsync<T>(
        this IValidator<T> validator,
        T instance,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(instance);
        return Awaited(validator, instance, cancellationToken);

        static async Task<Result<T>> Awaited(IValidator<T> validator, T instance, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(instance, cancellationToken).ConfigureAwait(false);
            return validationResult.ToResult(instance);
        }
    }
}
