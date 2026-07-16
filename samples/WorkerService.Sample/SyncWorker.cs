using System.Diagnostics;
using Koras.Results;
using Koras.Results.OpenTelemetry;

namespace WorkerServiceSample;

/// <summary>
/// A background worker that polls a flaky downstream system. It demonstrates:
/// exception boundaries (<see cref="Result.TryAsync{T}(Func{Task{T}}, Func{Exception, Error}?)"/>),
/// retry decisions driven by the error taxonomy (<see cref="ErrorType.Unavailable"/> is
/// retryable, everything else is terminal), activity tagging for traces, and clean
/// cancellation (cancellation is never converted into a failure).
/// </summary>
public sealed class SyncWorker(FlakyDownstream downstream, ILogger<SyncWorker> logger) : BackgroundService
{
    private static readonly ActivitySource Activities = new("WorkerServiceSample.Sync");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var activity = Activities.StartActivity("sync-batch");

            var result = await FetchBatchAsync(stoppingToken)
                .TapActivityErrorAsync(); // failure -> error.type / koras.error.code tags on the trace

            result.Switch(
                onSuccess: batch => logger.LogInformation("Synced {Count} records", batch.Count),
                onFailure: error =>
                {
                    if (error.Type == ErrorType.Unavailable)
                    {
                        logger.LogWarning("Downstream unavailable ({ErrorCode}); will retry next cycle", error.Code);
                    }
                    else
                    {
                        logger.LogError("Terminal sync failure {ErrorCode}: {ErrorMessage}", error.Code, error.Message);
                    }
                });

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break; // graceful shutdown
            }
        }
    }

    private Task<Result<IReadOnlyList<string>>> FetchBatchAsync(CancellationToken cancellationToken) =>
        Result.TryAsync(
            () => downstream.FetchAsync(cancellationToken),
            ex => ex switch
            {
                TimeoutException => Error.Unavailable("Downstream.Timeout", "The downstream system timed out."),
                HttpRequestException => Error.Unavailable("Downstream.Unreachable", "The downstream system is unreachable."),
                _ => Error.Unexpected("Downstream.Unknown", "An unexpected sync error occurred.")
                        .WithMetadata("exceptionType", ex.GetType().Name),
            });
}

/// <summary>Simulates a downstream dependency that fails intermittently.</summary>
public sealed class FlakyDownstream
{
    private int _calls;

    public async Task<IReadOnlyList<string>> FetchAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken);
        var call = Interlocked.Increment(ref _calls);
        return (call % 3) switch
        {
            0 => throw new TimeoutException("simulated timeout"),
            1 => ["record-a", "record-b"],
            _ => ["record-c"],
        };
    }
}
