using Koras.Results.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Koras.Results.IntegrationTests.AspNetCore;

/// <summary>
/// Builds an in-memory ASP.NET Core host (TestServer) with Koras.Results registered, letting each
/// test configure endpoints, controllers, options, and service overrides.
/// </summary>
internal static class TestHostFactory
{
    internal static async Task<IHost> StartAsync(
        Action<IEndpointRouteBuilder> mapEndpoints,
        Action<KorasResultsOptions>? configureOptions = null,
        Action<IServiceCollection>? configureServices = null,
        bool addControllers = false,
        ILoggerProvider? loggerProvider = null)
    {
        var builder = new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddKorasResults(configureOptions);
                    if (addControllers)
                    {
                        services.AddControllers()
                            .AddApplicationPart(typeof(TestHostFactory).Assembly);
                    }

                    configureServices?.Invoke(services);
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        mapEndpoints(endpoints);
                        if (addControllers)
                        {
                            endpoints.MapControllers();
                        }
                    });
                }));

        if (loggerProvider is not null)
        {
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.SetMinimumLevel(LogLevel.Debug);
                logging.AddProvider(loggerProvider);
            });
        }

        return await builder.StartAsync();
    }
}

/// <summary>An ILoggerProvider capturing log entries for assertions.</summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly List<(string Category, LogLevel Level, string Message)> _entries = [];

    public IReadOnlyList<(string Category, LogLevel Level, string Message)> Entries
    {
        get
        {
            lock (_entries)
            {
                return [.. _entries];
            }
        }
    }

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

    public void Dispose()
    {
    }

    private void Add(string category, LogLevel level, string message)
    {
        lock (_entries)
        {
            _entries.Add((category, level, message));
        }
    }

    private sealed class CapturingLogger(CapturingLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            provider.Add(category, logLevel, formatter(state, exception));
    }
}
