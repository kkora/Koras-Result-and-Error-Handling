using System.Text.Json.Serialization;
using Koras.Results.Serialization;

namespace Koras.Results;

/// <summary>
/// An <see cref="Error"/> that aggregates two or more heterogeneous errors, produced by
/// <see cref="Result.Combine(System.Collections.Generic.IEnumerable{Result})"/> when multiple
/// operations fail. Its <see cref="Error.Type"/> is the highest-severity type among its children,
/// so downstream projections never under-report severity.
/// </summary>
[JsonConverter(typeof(AggregateErrorJsonConverter))]
public sealed class AggregateError : Error
{
    /// <summary>The error code carried by every aggregate error.</summary>
    public const string DefaultCode = "Errors.Multiple";

    /// <summary>
    /// Initializes a new instance of the <see cref="AggregateError"/> class. Nested aggregate
    /// errors are flattened into their children.
    /// </summary>
    /// <param name="errors">The errors to aggregate; at least two (after flattening) are required.</param>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="errors"/> contains null entries or fewer than two errors after flattening.
    /// </exception>
    public AggregateError(IEnumerable<Error> errors)
        : this(Flatten(errors))
    {
    }

    private AggregateError(Error[] flattened)
        : base(DefaultCode, "Multiple errors occurred.", HighestSeverity(flattened))
    {
        Errors = flattened;
    }

    // Trusted construction path for cloning: children already flattened and validated.
    private AggregateError(IReadOnlyList<Error> flattened, ErrorType type, IReadOnlyDictionary<string, object?> metadata)
        : base(DefaultCode, "Multiple errors occurred.", type, metadata)
    {
        Errors = flattened;
    }

    /// <summary>The aggregated errors, flattened and in supplied order. Always at least two.</summary>
    public IReadOnlyList<Error> Errors { get; }

    private protected override Error CloneWithMetadata(IReadOnlyDictionary<string, object?> metadata) =>
        new AggregateError(Errors, Type, metadata);

    private static Error[] Flatten(IEnumerable<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var flattened = new List<Error>();
        foreach (var error in errors)
        {
            switch (error)
            {
                case null:
                    throw new ArgumentException("Errors must not contain null entries.", nameof(errors));
                case AggregateError aggregate:
                    flattened.AddRange(aggregate.Errors);
                    break;
                default:
                    flattened.Add(error);
                    break;
            }
        }

        if (flattened.Count < 2)
        {
            throw new ArgumentException("An aggregate error requires at least two errors.", nameof(errors));
        }

        return [.. flattened];
    }

    private static ErrorType HighestSeverity(Error[] errors)
    {
        var highest = ErrorType.Validation;
        var highestRank = -1;
        foreach (var error in errors)
        {
            var rank = SeverityRank(error.Type);
            if (rank > highestRank)
            {
                highestRank = rank;
                highest = error.Type;
            }
        }

        return highest;
    }

    // Severity precedence per docs/architecture/error-model.md:
    // Unexpected > Unavailable > Forbidden > Unauthorized > Conflict > NotFound > Failure > Validation.
    private static int SeverityRank(ErrorType type) => type switch
    {
        ErrorType.Unexpected => 7,
        ErrorType.Unavailable => 6,
        ErrorType.Forbidden => 5,
        ErrorType.Unauthorized => 4,
        ErrorType.Conflict => 3,
        ErrorType.NotFound => 2,
        ErrorType.Failure => 1,
        _ => 0,
    };
}
