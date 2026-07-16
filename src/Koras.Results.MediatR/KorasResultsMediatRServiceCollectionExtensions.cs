using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Koras.Results.MediatR;

/// <summary>Dependency-injection registration for the Koras.Results MediatR integration.</summary>
public static class KorasResultsMediatRServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ValidationBehavior{TRequest, TResponse}"/> as an open-generic MediatR
    /// pipeline behavior. Requires MediatR and FluentValidation validators to be registered
    /// separately. Safe to call multiple times.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddKorasResultsValidationBehavior(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(
            ServiceDescriptor.Transient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>)));
        return services;
    }
}
