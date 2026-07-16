using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Koras.Results.Serialization;

namespace Koras.Results;

/// <summary>
/// The outcome of an operation that produces a value: either a success carrying a non-null
/// <typeparamref name="T"/>, or a failure carrying an <see cref="Error"/>.
/// </summary>
/// <typeparam name="T">The type of the value produced on success.</typeparam>
/// <remarks>
/// <para>
/// This is an allocation-free <see langword="readonly"/> struct: success results allocate nothing.
/// A success never carries null — model optionality in the domain type, not in the result.
/// </para>
/// <para>
/// <c>default(Result&lt;T&gt;)</c> is a <em>failure</em> carrying
/// <see cref="Results.Error.Uninitialized"/>, never a success (ADR-0003).
/// </para>
/// </remarks>
[JsonConverter(typeof(ResultJsonConverterFactory))]
public readonly struct Result<T> : IEquatable<Result<T>>
{
    private readonly bool _isSuccess;
    private readonly T? _value;
    private readonly Error? _error;

    private Result(bool isSuccess, T? value, Error? error)
    {
        _isSuccess = isSuccess;
        _value = value;
        _error = error;
    }

    /// <summary>Whether the operation succeeded. When true, <see cref="Value"/> is non-null.</summary>
    [MemberNotNullWhen(true, nameof(_value))]
    public bool IsSuccess => _isSuccess;

    /// <summary>Whether the operation failed. Always the inverse of <see cref="IsSuccess"/>.</summary>
    public bool IsFailure => !_isSuccess;

    /// <summary>
    /// The value produced by the operation.
    /// </summary>
    /// <exception cref="InvalidOperationException">The result is a failure.</exception>
    public T Value => _isSuccess
        ? _value!
        : throw new InvalidOperationException(
            $"Cannot access Value on a failure result (error '{(_error ?? Error.Uninitialized).Code}'). " +
            "Check IsSuccess or use TryGetValue before accessing Value.");

    /// <summary>
    /// The error describing the failure.
    /// </summary>
    /// <exception cref="InvalidOperationException">The result is a success.</exception>
    public Error Error => _isSuccess
        ? throw new InvalidOperationException(
            "Cannot access Error on a success result. Check IsFailure before accessing Error.")
        : _error ?? Error.Uninitialized;

    /// <summary>Attempts to get the value; returns false for failures.</summary>
    /// <param name="value">The value when this is a success; default otherwise.</param>
    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        value = _value;
        return _isSuccess;
    }

    /// <summary>Attempts to get the error; returns false for successes.</summary>
    /// <param name="error">The error when this is a failure; null otherwise.</param>
    public bool TryGetError([MaybeNullWhen(false)] out Error error)
    {
        error = _isSuccess ? null : _error ?? Error.Uninitialized;
        return !_isSuccess;
    }

    /// <summary>Returns the value on success, or <see langword="default"/> on failure.</summary>
    public T? GetValueOrDefault() => _value;

    /// <summary>Returns the value on success, or <paramref name="fallback"/> on failure.</summary>
    /// <param name="fallback">The value to return when this result is a failure.</param>
    public T GetValueOrDefault(T fallback) => _isSuccess ? _value! : fallback;

    /// <summary>Drops the value, preserving the success/failure outcome and error.</summary>
    public Result ToResult() => _isSuccess ? Result.Success() : Result.Failure(_error ?? Error.Uninitialized);

    internal static Result<T> CreateSuccess(T value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(
                nameof(value),
                "A success result cannot carry null. Model optional values in the domain type instead.");
        }

        return new Result<T>(true, value, null);
    }

    internal static Result<T> CreateFailure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(false, default, error);
    }

    /// <summary>Converts a value into a success result.</summary>
    /// <param name="value">The value; must not be null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator Result<T>(T value) => CreateSuccess(value);

    /// <summary>Converts an <see cref="Results.Error"/> into a failure result.</summary>
    /// <param name="error">The error describing the failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is null.</exception>
    public static implicit operator Result<T>(Error error) => CreateFailure(error);

    /// <summary>Drops the value, preserving the success/failure outcome and error.</summary>
    /// <param name="result">The result to convert.</param>
    public static implicit operator Result(Result<T> result) => result.ToResult();

    /// <summary>
    /// Determines equality: two successes are equal when their values are equal
    /// (via <see cref="EqualityComparer{T}.Default"/>); two failures are equal when their errors
    /// are equal (by code and type).
    /// </summary>
    /// <param name="other">The result to compare with.</param>
    public bool Equals(Result<T> other)
    {
        if (_isSuccess != other._isSuccess)
        {
            return false;
        }

        return _isSuccess
            ? EqualityComparer<T>.Default.Equals(_value, other._value)
            : (_error ?? Error.Uninitialized).Equals(other._error ?? Error.Uninitialized);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _isSuccess
        ? HashCode.Combine(true, _value)
        : HashCode.Combine(false, _error ?? Error.Uninitialized);

    /// <summary>Equality operator; see <see cref="Equals(Result{T})"/>.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);

    /// <summary>Inequality operator; see <see cref="Equals(Result{T})"/>.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => _isSuccess ? $"Success({_value})" : $"Failure({Error})";
}
