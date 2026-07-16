using FluentValidation.Results;

namespace Koras.Results.FluentValidation;

/// <summary>
/// Converts FluentValidation results into Koras.Results values. Property names, messages, and
/// FluentValidation error codes are preserved on each <see cref="FieldError"/>.
/// </summary>
public static class ValidationResultExtensions
{
    /// <summary>
    /// Converts to a <see cref="Result"/>: success when valid, otherwise a failure carrying a
    /// <see cref="ValidationError"/>.
    /// </summary>
    /// <param name="validationResult">The FluentValidation result.</param>
    /// <exception cref="ArgumentNullException"><paramref name="validationResult"/> is null.</exception>
    public static Result ToResult(this ValidationResult validationResult)
    {
        ArgumentNullException.ThrowIfNull(validationResult);
        return validationResult.IsValid
            ? Result.Success()
            : Result.Failure(validationResult.ToValidationError());
    }

    /// <summary>
    /// Converts to a <see cref="Result{T}"/>: success carrying <paramref name="value"/> when
    /// valid, otherwise a failure carrying a <see cref="ValidationError"/>.
    /// </summary>
    /// <typeparam name="T">The validated value's type.</typeparam>
    /// <param name="validationResult">The FluentValidation result.</param>
    /// <param name="value">The validated value; must not be null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="validationResult"/> is null, or the result is valid and <paramref name="value"/> is null.</exception>
    public static Result<T> ToResult<T>(this ValidationResult validationResult, T value)
    {
        ArgumentNullException.ThrowIfNull(validationResult);
        return validationResult.IsValid
            ? Result.Success(value)
            : Result.Failure<T>(validationResult.ToValidationError());
    }

    /// <summary>Converts the failures of an invalid result into a <see cref="ValidationError"/>.</summary>
    /// <param name="validationResult">The FluentValidation result; must be invalid.</param>
    /// <exception cref="ArgumentNullException"><paramref name="validationResult"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="validationResult"/> is valid.</exception>
    public static ValidationError ToValidationError(this ValidationResult validationResult)
    {
        ArgumentNullException.ThrowIfNull(validationResult);
        if (validationResult.IsValid)
        {
            throw new InvalidOperationException(
                "Cannot convert a valid ValidationResult into a ValidationError. Check IsValid first.");
        }

        var fieldErrors = validationResult.Errors
            .Select(failure => new FieldError(
                failure.PropertyName ?? string.Empty,
                failure.ErrorMessage,
                string.IsNullOrEmpty(failure.ErrorCode) ? null : failure.ErrorCode))
            .ToArray();

        return new ValidationError(fieldErrors);
    }
}
