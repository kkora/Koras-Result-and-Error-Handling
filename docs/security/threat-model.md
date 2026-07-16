# Threat Model — Koras.Results

Koras.Results is a **value library**: it performs no I/O, opens no network connections, reads no
configuration, handles no credentials, and stores no data. That removes entire threat classes
(there is no server, no storage, no authentication logic to attack), but three genuine surfaces
remain and are analyzed here. See also `SECURITY.md` for the disclosure process.

## Assets

| Asset | Description |
|---|---|
| A1 | Internal failure details — exception messages, `Error.Message` text, `Error.Metadata` contents that may describe infrastructure, code paths, or data |
| A2 | Integrity of deserialized `Result`/`Error` values crossing process boundaries (queues, caches, service-to-service calls) |
| A3 | The build and dependency chain of the published packages |

## Trust boundaries

1. **Application → HTTP client** (the AspNetCore projection): everything placed in a
   ProblemDetails response leaves the trust boundary and reaches arbitrary callers.
2. **External JSON → library types** (deserialization): payloads from queues, caches, or other
   services are untrusted input parsed by the core's converters.
3. **NuGet / CI → consumer** (supply chain): what we depend on and how we build becomes part of
   every consumer's trust base.

## Surface 1: Information disclosure via error → HTTP projection

A `Result` failure created deep in the domain may carry text intended for logs, not clients
("Connection string for server X leaked", stack-adjacent detail, tenant identifiers in
metadata). The projection to RFC 9457 ProblemDetails is where that text could escape.

**Mitigations (all defaults, all verified by tests):**

- `KorasResultsOptions.IncludeUnexpectedErrorDetails` defaults to **false**: for
  `ErrorType.Unexpected` errors the response `detail` is replaced with the fixed string
  `"An unexpected error occurred."` (`ProblemDetailsBuilder.SuppressedUnexpectedDetail`), and the
  original message is emitted **only** to the server-side log as a Warning (event id 2). The
  substitution happens before any localization — the localizer is never invoked for a suppressed
  detail, so no hook can reintroduce the message
  (`src/Koras.Results.AspNetCore/ProblemDetailsBuilder.cs`).
- `KorasResultsOptions.MetadataExposure` defaults to **`MetadataExposurePolicy.None`**: error
  metadata never appears in responses unless the application opts into `All`.
- The **`Result.Try` default exception mapper never copies the exception message**. It produces
  `Error.Unexpected("Unexpected.Exception", "An unexpected error occurred.")` with only
  `metadata["exceptionType"]` = the exception's full type name — no message, no stack trace, no
  inner-exception data (`src/Koras.Results/Result.Try.cs`; pinned by
  `ResultTryTests.Try_converts_exceptions_using_the_safe_default`).
- Non-`Unexpected` error messages *are* sent to clients by design — they are the developer's own
  domain messages. The hygiene rules for writing them live in
  [secure-configuration.md](secure-configuration.md) and [data-protection.md](data-protection.md).

## Surface 2: Deserialization

The core registers System.Text.Json converters via attributes, so any process deserializing
`Result<T>`/`Error` parses untrusted bytes with this code.

**Mitigations (ADR-0007; pinned by `SerializationTests`):**

- **Closed, sealed type set.** Only `Error`, `ValidationError`, and `AggregateError` can be
  produced; both subclasses are sealed and no user subclasses exist. There is no type table to
  poison.
- **Structural discrimination, no polymorphic `$type`.** The subclass is chosen by the presence
  of `fieldErrors` / `errors` properties — never by a type name from the payload. The classic
  .NET deserialization-gadget vector (attacker-controlled type names) does not exist here.
  Ambiguous payloads (both markers present) are rejected.
- **Nested metadata values stay inert.** Non-primitive metadata deserializes as `JsonElement` —
  parsed JSON data, never materialized into arbitrary CLR types.
- **Malformed input throws `JsonException`** — missing discriminators, null success values,
  failure without error, unknown `type` strings, empty `fieldErrors`, single-child aggregates all
  fail parsing rather than producing half-valid objects. Unknown *extra* properties are skipped
  (forward compatibility) without being interpreted.
- Payload size/depth limits are the host serializer's responsibility
  (`JsonSerializerOptions.MaxDepth`, request size limits) — the converters add no unbounded
  recursion of their own beyond nesting already present in the payload.

## Surface 3: Supply chain

**Mitigations:**

- **Zero-dependency core** — `Koras.Results` references nothing outside the BCL, enforced by an
  architecture test and by `build/validate-packages.sh` (fails the build if the core nuspec lists
  any dependency).
- **Central Package Management** — every version in one reviewed file
  (`Directory.Packages.props`).
- **Upper-bounded MediatR** `[12.4.1, 13.0.0)` so a commercially licensed major can never be
  pulled in silently (ADR-0006), with a matching Dependabot ignore rule for MediatR majors.
- **Single package source with source mapping** — the committed `NuGet.Config` clears all
  sources, registers only nuget.org, and maps `*` to it, preventing dependency-confusion via
  extra feeds.
- **Dependabot** (weekly, NuGet + GitHub Actions), **CodeQL** (`security-and-quality` queries on
  push/PR/weekly), **dependency-review** on every PR (fails on moderate+ advisories, license
  allowlist), and a `dotnet list package --vulnerable --include-transitive` gate in `build.yml`.
- **Deterministic CI builds with Source Link and symbol packages** so published binaries are
  auditable back to source.

Details in [dependency-security.md](dependency-security.md).

## STRIDE summary

| Threat | Applies? | Vector | Mitigation |
|---|---|---|---|
| **S**poofing | Not directly | The library performs no authentication | n/a — `Unauthorized`/`Forbidden` are classifications supplied by the app, not decisions made here |
| **T**ampering | Yes | Forged/malformed JSON crossing boundary 2 | Closed sealed types, structural discrimination, strict `JsonException` on malformed input; integrity/authenticity of the transport is the application's duty |
| **R**epudiation | Marginal | Lost failure evidence | Suppressed `Unexpected` details are always logged server-side (Warning, event id 2) with code and original message; `traceId` in responses joins client reports to traces |
| **I**nformation disclosure | **Yes — primary risk** | Error → ProblemDetails projection (boundary 1) | Suppression default, metadata `None` default, leak-safe `Try` mapper; opt-in required for anything more |
| **D**enial of service | Low | Pathological JSON payloads | Parsing is linear; depth/size bounded by host serializer limits; no unbounded allocation amplification in converters |
| **E**levation of privilege | No | No code execution paths from data | No polymorphic deserialization, no reflection over payload-supplied names, no dynamic code |

## Explicit non-goals

The library does not and will not: encrypt or sign payloads, authenticate callers, rate-limit,
sanitize application-authored messages (it cannot know what is sensitive), or manage secrets.
Those belong to the hosting application.
