# WorkerService.Sample

A background worker demonstrating Koras.Results outside HTTP: exception boundaries with `Result.TryAsync`, **retry decisions driven by the error taxonomy** (`Unavailable` ⇒ retry next cycle, anything else ⇒ terminal), OpenTelemetry activity tagging via `TapActivityErrorAsync`, and cancellation that shuts the worker down cleanly instead of surfacing as a failure.

## Prerequisites

- .NET SDK 10 (see repository `global.json`)
- No configuration or secrets required. (A `UserSecretsId` is pre-wired in the csproj so you can add real connection settings with `dotnet user-secrets set` if you extend the sample — never put credentials in `appsettings.json`.)

## Run

```bash
dotnet run --project samples/WorkerService.Sample
```

Stop with `Ctrl+C` — note the clean shutdown (cancellation never becomes an error result).

## Expected output (pattern)

```
info: WorkerServiceSample.SyncWorker[0]  Synced 2 records
info: WorkerServiceSample.SyncWorker[0]  Synced 1 records
warn: WorkerServiceSample.SyncWorker[0]  Downstream unavailable (Downstream.Timeout); will retry next cycle
info: WorkerServiceSample.SyncWorker[0]  Synced 2 records
...
```

Every third downstream call throws a simulated `TimeoutException`; the mapper classifies it as `Unavailable` and the worker retries on the next cycle instead of crashing.

## What to look at

- `SyncWorker.FetchBatchAsync` — the exception-to-error mapping pattern for infrastructure boundaries.
- The `error.Type == ErrorType.Unavailable` branch — taxonomy-driven retry policy.
- `TapActivityErrorAsync()` — traces get `error.type` and `koras.error.code` tags with one call; add the OpenTelemetry SDK and an exporter to ship them.

## Switching to released packages

Replace the `<ProjectReference>` items with `<PackageReference>`s to `Koras.Results` and `Koras.Results.OpenTelemetry`.

## Related documentation

- [Worker Service guide](../../docs/guides/worker-service.md)
- [Exception conversion feature guide](../../docs/features/exception-conversion.md)
- [OpenTelemetry feature guide](../../docs/features/opentelemetry.md)
