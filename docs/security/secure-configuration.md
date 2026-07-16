# Secure Configuration — Koras.Results

The library is **secure by default**: with a bare `services.AddKorasResults()` — or with no
registration at all (the extension methods fall back to built-in defaults) — no internal detail
leaves the process. Every relaxation is an explicit opt-in on `KorasResultsOptions`. This page
lists each default, what weakening it exposes, and when weakening is acceptable.

All defaults below are pinned by `KorasResultsOptionsTests.Secure_defaults_are_in_force` and the
Minimal API integration tests.

## The defaults and the cost of weakening them

| Option | Default | Weakened to | What weakening means |
|---|---|---|---|
| `IncludeUnexpectedErrorDetails` | `false` | `true` | Raw `Unexpected` error messages — which frequently wrap exception text, infrastructure names, connection details — are sent verbatim to every HTTP caller |
| `MetadataExposure` | `MetadataExposurePolicy.None` | `All` | The entire `Error.Metadata` dictionary of every failure is serialized into the ProblemDetails `metadata` extension for every caller |
| `IncludeTraceId` | `true` | `false` | Not a weakening — disabling *removes* the correlation id. Keep it on: a trace id is opaque, not sensitive, and it is how support joins a client report to server telemetry |
| `ProblemTypeUriFactory` | `null` (RFC 9110 `about:blank`-family/type links) | custom factory | Neutral. Ensure the factory itself does not embed sensitive data in URIs; it receives the full `Error` |
| Status mappings (`MapErrorType`/`MapErrorCode`) | semantic defaults (422/400/404/409/401/403/503/500) | custom | Neutral for confidentiality; avoid mapping `Unexpected` below 500, which would misclassify bugs as client errors in monitoring |

Additionally, the `Result.Try` **default** exception mapper excludes exception messages and
records only the exception *type name* in metadata. Supplying a custom `mapError` delegate
replaces that safety: whatever your delegate puts in `Error.Message` will follow the normal
projection rules (and will be exposed if the error is not `Unexpected`, or if details are
enabled). Write custom mappers to produce curated, client-safe messages — never
`ex.ToString()` or `ex.Message` for error types other than `Unexpected`.

## IncludeUnexpectedErrorDetails: Development only

The only recommended use of `IncludeUnexpectedErrorDetails = true` is local debugging
convenience. Gate it on the environment so it cannot reach production through configuration
drift:

```csharp
builder.Services.AddKorasResults(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        options.IncludeUnexpectedErrorDetails = true;
    }
});
```

Never bind this flag to raw configuration (`appsettings.json`) without an environment guard — a
copied config file becomes an information-disclosure incident. Note that even with the default
(`false`) you lose nothing diagnostically: the suppressed message is logged server-side at
Warning (event id 2) together with the error code, and the response still carries `errorCode` and
`traceId` for correlation. See `docs/troubleshooting/logging.md`.

## Metadata hygiene rules

`MetadataExposurePolicy` is deliberately all-or-nothing (`None`/`All`) in the current release —
there is no per-key filtering. Therefore, the contract for anything placed in `Error.Metadata`:

1. **Treat every metadata value as potentially client-visible.** Even if *you* never enable
   `All`, a future teammate might; and metadata also flows into serialized results crossing
   service boundaries (ADR-0007 wire shape includes `metadata`).
2. **Never put in metadata:** secrets, tokens, connection strings, internal hostnames/IPs, file
   paths, SQL/queries, raw exception dumps, or personal data (see
   [data-protection.md](data-protection.md)).
3. **Good metadata** is small, structured, and business-meaningful: an offending SKU, a limit
   value, a retry-after hint, a public correlation id.
4. Keys are camelCase; values must be JSON-primitive-representable (string, number, bool, null,
   arrays thereof) per `docs/architecture/error-model.md`.
5. If some consumers need rich metadata and others must not see it, keep `None` for HTTP and read
   the metadata in code (`result.Error.Metadata`) where you control the audience.

## Message hygiene rules

`Error.Message` for non-`Unexpected` types is sent to clients **by design** — those are your
domain messages ("Order quantity exceeds available stock"). Rules:

- Write messages for the *caller*, not the operator. Operational context (server names, retry
  internals) belongs in logs via `TapError`, or in metadata kept unexposed.
- Route genuinely unclassifiable/technical failures through `ErrorType.Unexpected` — that is the
  type the suppression default protects. Do not classify a raw exception wrap as `Failure` just
  to see its message in responses.
- `Error.Code` is always exposed (as the `errorCode` extension). Codes are stable machine
  identifiers (`"User.NotFound"`) — keep them free of dynamic data.

## Serialization configuration

No configuration is needed or offered for the JSON converters — the safe behavior (closed types,
structural discrimination, strict rejection of malformed payloads) is not optional. Prefer
ProblemDetails, not serialized `Result` objects, at **public** API boundaries; the serialized
form is intended for internal service-to-service and queue/cache use (ADR-0007).

## Checklist extract

For the per-release and deployment checklists that verify these settings, see
[security-checklist.md](security-checklist.md).
