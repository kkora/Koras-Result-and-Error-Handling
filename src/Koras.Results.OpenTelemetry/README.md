# Koras.Results.OpenTelemetry

Observability for [Koras.Results](https://www.nuget.org/packages/Koras.Results): tag traces with failure information, following OpenTelemetry semantic conventions.

```csharp
var result = await _orders.PlaceAsync(cmd, ct)
    .TapActivityErrorAsync();   // on failure: otel.status=ERROR, error.type, koras.error.code

// or explicitly:
result.TagCurrentActivity();
result.TagActivity(activity);
```

- `error.type` — the error taxonomy value (`not_found`, `validation`, …)
- `koras.error.code` — the stable error code (`User.NotFound`)
- Success results and absent/non-recording activities are allocation-free no-ops
- Depends only on `System.Diagnostics.DiagnosticSource`; works with any OpenTelemetry SDK setup and never creates activities itself

Documentation: https://github.com/korastechnologies/koras-results/tree/main/docs
