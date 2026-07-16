using System.Globalization;

namespace Koras.Results.AspNetCore;

/// <summary>
/// Localizes error messages before they are sent to HTTP clients. Register a custom
/// implementation as a singleton to translate by <see cref="Error.Code"/> and
/// <see cref="FieldError.Code"/>; the default implementation passes messages through unchanged.
/// </summary>
/// <remarks>Implementations must be thread-safe; they are resolved as singletons.</remarks>
public interface IErrorMessageLocalizer
{
    /// <summary>Returns the client-facing message for <paramref name="error"/> in <paramref name="culture"/>.</summary>
    /// <param name="error">The error whose message is being localized.</param>
    /// <param name="culture">The target culture (typically the request culture).</param>
    string Localize(Error error, CultureInfo culture);

    /// <summary>Returns the client-facing message for <paramref name="fieldError"/> in <paramref name="culture"/>.</summary>
    /// <param name="fieldError">The field error whose message is being localized.</param>
    /// <param name="culture">The target culture (typically the request culture).</param>
    string LocalizeField(FieldError fieldError, CultureInfo culture);
}
