using System.Globalization;

namespace Koras.Results.AspNetCore;

/// <summary>
/// The default <see cref="IErrorMessageLocalizer"/>: returns messages unchanged.
/// </summary>
public sealed class PassThroughErrorMessageLocalizer : IErrorMessageLocalizer
{
    /// <summary>A shared instance for non-DI scenarios.</summary>
    public static readonly PassThroughErrorMessageLocalizer Instance = new();

    /// <inheritdoc />
    public string Localize(Error error, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Message;
    }

    /// <inheritdoc />
    public string LocalizeField(FieldError fieldError, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(fieldError);
        return fieldError.Message;
    }
}
