namespace Koras.Results;

/// <content>
/// Combination factories: aggregate independent results so that all failures are reported,
/// not just the first.
/// </content>
public readonly partial struct Result
{
    /// <summary>
    /// Combines independent results: success when all succeed; a single failure passes through
    /// unchanged; multiple failures aggregate (all-validation failures merge into one
    /// <see cref="ValidationError"/>, otherwise an <see cref="AggregateError"/> is produced).
    /// </summary>
    /// <param name="results">The results to combine.</param>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is null.</exception>
    public static Result Combine(params Result[] results) => Combine((IEnumerable<Result>)results);

    /// <summary>
    /// Combines independent results: success when all succeed; a single failure passes through
    /// unchanged; multiple failures aggregate (all-validation failures merge into one
    /// <see cref="ValidationError"/>, otherwise an <see cref="AggregateError"/> is produced).
    /// </summary>
    /// <param name="results">The results to combine.</param>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is null.</exception>
    public static Result Combine(IEnumerable<Result> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        List<Error>? errors = null;
        foreach (var result in results)
        {
            if (result.IsFailure)
            {
                (errors ??= []).Add(result.Error);
            }
        }

        return errors switch
        {
            null => Success(),
            [var single] => Failure(single),
            _ => Failure(Merge(errors)),
        };
    }

    /// <summary>
    /// Combines two results into a tuple-valued result, aggregating failures per
    /// <see cref="Combine(System.Collections.Generic.IEnumerable{Result})"/>.
    /// </summary>
    /// <typeparam name="T1">The first value type.</typeparam>
    /// <typeparam name="T2">The second value type.</typeparam>
    /// <param name="result1">The first result.</param>
    /// <param name="result2">The second result.</param>
    public static Result<(T1 First, T2 Second)> Combine<T1, T2>(Result<T1> result1, Result<T2> result2)
    {
        var combined = Combine(result1.ToResult(), result2.ToResult());
        return combined.IsFailure
            ? Failure<(T1, T2)>(combined.Error)
            : Success((result1.Value, result2.Value));
    }

    /// <summary>
    /// Combines three results into a tuple-valued result, aggregating failures per
    /// <see cref="Combine(System.Collections.Generic.IEnumerable{Result})"/>.
    /// </summary>
    /// <typeparam name="T1">The first value type.</typeparam>
    /// <typeparam name="T2">The second value type.</typeparam>
    /// <typeparam name="T3">The third value type.</typeparam>
    /// <param name="result1">The first result.</param>
    /// <param name="result2">The second result.</param>
    /// <param name="result3">The third result.</param>
    public static Result<(T1 First, T2 Second, T3 Third)> Combine<T1, T2, T3>(
        Result<T1> result1, Result<T2> result2, Result<T3> result3)
    {
        var combined = Combine(result1.ToResult(), result2.ToResult(), result3.ToResult());
        return combined.IsFailure
            ? Failure<(T1, T2, T3)>(combined.Error)
            : Success((result1.Value, result2.Value, result3.Value));
    }

    /// <summary>
    /// Combines four results into a tuple-valued result, aggregating failures per
    /// <see cref="Combine(System.Collections.Generic.IEnumerable{Result})"/>.
    /// </summary>
    /// <typeparam name="T1">The first value type.</typeparam>
    /// <typeparam name="T2">The second value type.</typeparam>
    /// <typeparam name="T3">The third value type.</typeparam>
    /// <typeparam name="T4">The fourth value type.</typeparam>
    /// <param name="result1">The first result.</param>
    /// <param name="result2">The second result.</param>
    /// <param name="result3">The third result.</param>
    /// <param name="result4">The fourth result.</param>
    public static Result<(T1 First, T2 Second, T3 Third, T4 Fourth)> Combine<T1, T2, T3, T4>(
        Result<T1> result1, Result<T2> result2, Result<T3> result3, Result<T4> result4)
    {
        var combined = Combine(result1.ToResult(), result2.ToResult(), result3.ToResult(), result4.ToResult());
        return combined.IsFailure
            ? Failure<(T1, T2, T3, T4)>(combined.Error)
            : Success((result1.Value, result2.Value, result3.Value, result4.Value));
    }

    private static Error Merge(List<Error> errors)
    {
        var allValidation = true;
        foreach (var error in errors)
        {
            if (error is not ValidationError)
            {
                allValidation = false;
                break;
            }
        }

        if (allValidation)
        {
            var fieldErrors = new List<FieldError>();
            foreach (var error in errors)
            {
                fieldErrors.AddRange(((ValidationError)error).FieldErrors);
            }

            return new ValidationError(fieldErrors);
        }

        return new AggregateError(errors);
    }
}
