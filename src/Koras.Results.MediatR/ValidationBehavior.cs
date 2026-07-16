using FluentValidation;
using FluentValidation.Results;
using Koras.Results.FluentValidation;
using MediatR;

namespace Koras.Results.MediatR;

/// <summary>
/// A MediatR pipeline behavior that runs all registered <see cref="IValidator{T}"/> instances for
/// the request and, when validation fails, short-circuits with a <em>failed result</em> instead
/// of throwing <see cref="ValidationException"/>.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">
/// The response type; must be <see cref="Result"/> or <see cref="Result{T}"/> for failures to
/// short-circuit. For any other response type the behavior passes valid requests through and
/// throws <see cref="InvalidOperationException"/> on invalid ones (fail-fast beats silently
/// swallowing validation).
/// </typeparam>
/// <remarks>
/// All validators run; their failures aggregate into a single <see cref="ValidationError"/>.
/// Register via
/// <see cref="KorasResultsMediatRServiceCollectionExtensions.AddKorasResultsValidationBehavior"/>.
/// </remarks>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Resolved once per closed generic type: null when TResponse is not Result/Result<T>.
    private static readonly Func<ValidationError, TResponse>? FailureFactory = CreateFailureFactory();

    private readonly IValidator<TRequest>[] _validators;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationBehavior{TRequest, TResponse}"/>
    /// class.
    /// </summary>
    /// <param name="validators">The validators registered for <typeparamref name="TRequest"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="validators"/> is null.</exception>
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        ArgumentNullException.ThrowIfNull(validators);
        _validators = validators.ToArray();
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Validation failed and <typeparamref name="TResponse"/> is not <see cref="Result"/> or
    /// <see cref="Result{T}"/>.
    /// </exception>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        if (_validators.Length == 0)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var failures = new List<ValidationFailure>();
        foreach (var validator in _validators)
        {
            // A fresh context per validator: FluentValidation accumulates failures on a shared
            // context, which would duplicate earlier validators' failures in later results.
            var context = new ValidationContext<TRequest>(request);
            var validationResult = await validator.ValidateAsync(context, cancellationToken).ConfigureAwait(false);
            failures.AddRange(validationResult.Errors);
        }

        if (failures.Count == 0)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var error = new ValidationResult(failures).ToValidationError();
        if (FailureFactory is null)
        {
            throw new InvalidOperationException(
                $"Request '{typeof(TRequest).Name}' failed validation, but its response type " +
                $"'{typeof(TResponse).Name}' is not Result or Result<T>, so the failure cannot be " +
                "returned as a value. Change the handler to return a Result, or validate before sending.");
        }

        return FailureFactory(error);
    }

    private static Func<ValidationError, TResponse>? CreateFailureFactory()
    {
        if (typeof(TResponse) == typeof(Result))
        {
            return error => (TResponse)(object)Result.Failure(error);
        }

        if (typeof(TResponse).IsGenericType && typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
        {
            // Bind Result.Failure<T>(Error) once; per-failure calls are then delegate invocations.
            var valueType = typeof(TResponse).GetGenericArguments()[0];
            var method = typeof(Result)
                .GetMethod(nameof(Result.Failure), 1, [typeof(Error)])!
                .MakeGenericMethod(valueType);
            return error => (TResponse)method.Invoke(null, [error])!;
        }

        return null;
    }
}
