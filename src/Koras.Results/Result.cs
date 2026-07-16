using System.Text.Json.Serialization;
using Koras.Results.Serialization;

namespace Koras.Results;

/// <summary>
/// The outcome of an operation that produces no value: either a success, or a failure carrying
/// an <see cref="Error"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is an allocation-free <see langword="readonly"/> struct: success results allocate nothing.
/// </para>
/// <para>
/// <c>default(Result)</c> is a <em>failure</em> carrying <see cref="Results.Error.Uninitialized"/>,
/// never a success — an uninitialized result cannot masquerade as a valid outcome (ADR-0003).
/// </para>
/// </remarks>
[JsonConverter(typeof(ResultJsonConverter))]
public readonly partial struct Result : IEquatable<Result>
{
    private readonly bool _isSuccess;
    private readonly Error? _error;

    private Result(bool isSuccess, Error? error)
    {
        _isSuccess = isSuccess;
        _error = error;
    }

    /// <summary>Whether the operation succeeded.</summary>
    public bool IsSuccess => _isSuccess;

    /// <summary>Whether the operation failed. Always the inverse of <see cref="IsSuccess"/>.</summary>
    public bool IsFailure => !_isSuccess;

    /// <summary>
    /// The error describing the failure.
    /// </summary>
    /// <exception cref="InvalidOperationException">The result is a success.</exception>
    public Error Error => _isSuccess
        ? throw new InvalidOperationException(
            "Cannot access Error on a success result. Check IsFailure before accessing Error.")
        : _error ?? Error.Uninitialized;

    /// <summary>Creates a success result.</summary>
    public static Result Success() => new(true, null);

    /// <summary>Creates a failure result carrying <paramref name="error"/>.</summary>
    /// <param name="error">The error describing the failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is null.</exception>
    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(false, error);
    }

    /// <summary>Creates a success result carrying <paramref name="value"/>.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value; must not be null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static Result<T> Success<T>(T value) => Result<T>.CreateSuccess(value);

    /// <summary>Creates a failure result of <typeparamref name="T"/> carrying <paramref name="error"/>.</summary>
    /// <typeparam name="T">The type of the absent value.</typeparam>
    /// <param name="error">The error describing the failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is null.</exception>
    public static Result<T> Failure<T>(Error error) => Result<T>.CreateFailure(error);

    /// <summary>Converts an <see cref="Results.Error"/> into a failure result.</summary>
    /// <param name="error">The error describing the failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is null.</exception>
    public static implicit operator Result(Error error) => Failure(error);

    /// <summary>Named alternative to the implicit conversion from <see cref="Results.Error"/>.</summary>
    /// <param name="error">The error describing the failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is null.</exception>
    public static Result FromError(Error error) => Failure(error);

    /// <summary>
    /// Determines equality: two successes are equal; two failures are equal when their errors are
    /// equal (by code and type).
    /// </summary>
    /// <param name="other">The result to compare with.</param>
    public bool Equals(Result other) =>
        _isSuccess == other._isSuccess
        && (_isSuccess || (_error ?? Error.Uninitialized).Equals(other._error ?? Error.Uninitialized));

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        _isSuccess ? 1 : HashCode.Combine(false, _error ?? Error.Uninitialized);

    /// <summary>Equality operator; see <see cref="Equals(Result)"/>.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    public static bool operator ==(Result left, Result right) => left.Equals(right);

    /// <summary>Inequality operator; see <see cref="Equals(Result)"/>.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    public static bool operator !=(Result left, Result right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => _isSuccess ? "Success" : $"Failure({Error})";
}
