using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Koras.Results.AspNetCore;

/// <summary>Dependency-injection registration for the Koras.Results ASP.NET Core integration.</summary>
public static class KorasResultsServiceCollectionExtensions
{
    /// <summary>
    /// Registers Koras.Results HTTP projection services: <see cref="KorasResultsOptions"/> (with
    /// optional configuration) and the default <see cref="IErrorMessageLocalizer"/>. Safe to call
    /// multiple times; existing registrations are preserved.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration of the mapping options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddKorasResults(
        this IServiceCollection services,
        Action<KorasResultsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<KorasResultsOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<IErrorMessageLocalizer, PassThroughErrorMessageLocalizer>();
        return services;
    }
}
