# Feature Catalog — Koras.Results

Every feature is specified with the same structure. Feature guides with full runnable examples live in `docs/features/<feature-name>.md` after implementation; this catalog is the planning source of truth.

Legend — Packages: **Core** = `Koras.Results`, **AspNet** = `Koras.Results.AspNetCore`, **FV** = `Koras.Results.FluentValidation`, **MedR** = `Koras.Results.MediatR`, **OTel** = `Koras.Results.OpenTelemetry`.

---

## KR-001 — Result types (`Result`, `Result<T>`)

- **User problem:** No standard way to express "this operation may fail for an expected reason" in a method signature.
- **User story:** As a developer, I return `Result<Order>` from domain services so callers must handle failure explicitly.
- **Business value:** Foundation of the entire package; eliminates null/exception control flow.
- **Functional requirements:** Non-generic `Result` for void operations and generic `Result<T>`; `IsSuccess`/`IsFailure`; `Error` access on failure; `Value` access on success; `TryGetValue(out T)`; implicit conversions `T → Result<T>`, `Error → Result`/`Result<T>`; static factories `Result.Success()`, `Result.Success<T>(value)`, `Result.Failure(error)`, `Result.Failure<T>(error)`; `default(Result)` and `default(Result<T>)` are *failure* (uninitialized guard), never success-with-null.
- **Nonfunctional:** `readonly struct`; zero allocations for success; thread-safe by immutability; NRT-annotated (`[MemberNotNullWhen]` on `IsSuccess`).
- **Public API proposal:** see `docs/api/public-api-design.md` §2.
- **Configuration:** none.
- **Dependencies:** none.
- **Error conditions:** accessing `Value` on failure or `Error` on success throws `InvalidOperationException` with a precise message; `Result.Success<T>(null!)` throws `ArgumentNullException`.
- **Security:** none (pure data).
- **Performance:** success path allocation-free; struct size kept ≤ 2 references + 1 byte-ish overhead.
- **Observability:** none intrinsic (see KR-014).
- **Unit tests:** construction, accessors, guard throws, implicit conversions, default-struct behavior, equality of `Result<T>`.
- **Integration tests:** n/a.
- **Docs:** concepts/core-abstractions.md + features/result-types.md.
- **Examples:** all samples.
- **Acceptance criteria:** all functional requirements covered by passing unit tests; API locked in PublicAPI.Unshipped.txt.
- **Status:** **MVP** (Core).

## KR-002 — Typed error model (`Error`, `ErrorType`, error codes, metadata)

- **User problem:** Ad-hoc strings/enums make errors unclassifiable and untestable across services.
- **User story:** As a platform engineer, I need every failure to carry a stable machine-readable `Code`, a human `Message`, and a semantic `ErrorType` so clients and dashboards can react programmatically.
- **Business value:** The error contract is the product; enables all HTTP/telemetry mapping.
- **Functional requirements:** Immutable `Error` with `Code`, `Message`, `Type`, optional `Metadata` (string-keyed, read-only); `ErrorType` enum: `Failure` (domain rule), `Validation`, `NotFound`, `Conflict`, `Unauthorized`, `Forbidden`, `Unavailable` (infrastructure/transient), `Unexpected` (bug/exception); static factories per type (`Error.NotFound(code, message)` …); `Error.None` sentinel; `WithMetadata` copy-with helpers; value-based equality on `Code` + `Type`.
- **Nonfunctional:** immutable class (shared freely across threads); metadata dictionary defensive-copied.
- **Error conditions:** null/empty code or message → `ArgumentException`.
- **Security:** docs mandate no secrets/PII in `Message`/`Metadata`; `Unexpected` messages are suppressed by default in HTTP responses (KR-009).
- **Performance:** errors allocate (failure path only — acceptable and documented); metadata dictionary allocated only when used.
- **Tests:** factory semantics, equality, metadata immutability, guards.
- **Acceptance criteria:** taxonomy covers all spec-required categories (validation, not-found, conflict, unauthorized, forbidden, infrastructure→`Unavailable`, domain→`Failure`).
- **Status:** **MVP** (Core).

## KR-003 — Validation errors (`ValidationError`, `FieldError`)

- **User problem:** Field-level validation failures need structure that survives to the HTTP `errors` dictionary.
- **User story:** As an API developer, I return one failure carrying all invalid fields so the client can render per-field messages.
- **Functional requirements:** `ValidationError : Error` with `Type == Validation` and `IReadOnlyList<FieldError>`; `FieldError(PropertyName, Message, Code?)`; construction from params/enumerable; default code `"Validation.Failed"`, custom code overload.
- **Error conditions:** empty field-error collection → `ArgumentException`.
- **Tests:** aggregation, ordering preserved, ProblemDetails projection (in AspNet tests).
- **Status:** **MVP** (Core).

## KR-004 — Functional composition (Map, Bind, Match, Ensure, Tap, TapError)

- **User problem:** Chaining fallible operations with `if (r.IsFailure) return r.Error;` boilerplate.
- **User story:** As a developer, I compose a pipeline of fallible steps that short-circuits on first failure.
- **Functional requirements:** `Map` (transform value), `Bind` (chain returning Result), `Match`/`Switch` (exhaustive fold), `Ensure` (predicate + error), `Tap`/`TapError` (side effects), `MapError` (translate errors); defined for both `Result` and `Result<T>` where meaningful.
- **Nonfunctional:** delegates invoked at most once; failure short-circuits without invoking delegates; no hidden allocations beyond user delegates.
- **Error conditions:** null delegate → `ArgumentNullException`. Delegates that throw are NOT caught (documented; use `Try` for exception boundaries).
- **Tests:** short-circuit semantics, delegate-invocation counts, null guards, error propagation identity.
- **Status:** **MVP** (Core).

## KR-005 — Async composition (MapAsync, BindAsync, MatchAsync, EnsureAsync, TapAsync …)

- **User problem:** Real pipelines are async; sync-only combinators force awkward `await` nesting.
- **Functional requirements:** async variants for all KR-004 combinators; overloads for `Task<Result<T>>` receivers (chaining without intermediate awaits), async delegates on sync receivers, and sync delegates on task receivers; `ConfigureAwait(false)` throughout.
- **Nonfunctional:** no sync-over-async anywhere; `CancellationToken` flows through user delegates (delegates accept the token where the API takes one).
- **Tests:** all overload matrices, cancellation propagation, short-circuit (async delegate not invoked on failure).
- **Status:** **MVP** (Core).

## KR-006 — Exception conversion (`Result.Try`, `Result.TryAsync`)

- **User problem:** Third-party/BCL code throws; boundaries need a safe bridge into the Result world.
- **Functional requirements:** `Result.Try(Func<T>)`, `Result.Try(Action)`, `Result.TryAsync(Func<Task<T>>)`, `Result.TryAsync(Func<Task>)`; optional `Func<Exception, Error>` mapper (default: `Error.Unexpected("Unexpected.Exception", …)` with exception type in metadata, message NOT exposed by default HTTP mapping); `OperationCanceledException` is **rethrown**, never converted (cancellation is not failure).
- **Security:** default error omits exception message/stack from anything client-facing.
- **Tests:** capture, mapping, cancellation rethrow, async faults, mapper receiving original exception.
- **Status:** **MVP** (Core).

## KR-007 — Result combination (`Result.Combine`)

- **User problem:** Independent checks (e.g., multiple validations) should aggregate all failures, not stop at the first.
- **Functional requirements:** `Result.Combine(params Result[])` / `IEnumerable<Result>`; generic `Combine` returning tuple values for 2–4 results; aggregation rule: single failure → that error; multiple → `ValidationError` merge when all are validation errors, else `AggregateError` (an `Error` carrying child errors in a typed list).
- **Tests:** all-success, single-failure, multi-failure aggregation, validation-merge rule, tuple variants.
- **Status:** **MVP** (Core).

## KR-008 — JSON serialization support

- **User problem:** Results/errors crossing internal process boundaries (queues, caches) need a stable wire shape.
- **Functional requirements:** System.Text.Json converters for `Error`, `ValidationError`, `Result`, `Result<T>`; stable camelCase shape; round-trip fidelity including `ErrorType` and metadata (primitive JSON values); converters auto-discovered via `[JsonConverter]` attributes — no setup needed.
- **Security:** deserialization constructs only our sealed types; metadata values deserialize to `JsonElement`-backed primitives, never polymorphic types.
- **Docs caveat:** public API boundaries should use ProblemDetails, not serialized Results.
- **Tests:** round-trip suite, malformed-payload rejection, metadata type fidelity, NRT correctness.
- **Status:** **MVP** (Core).

## KR-009 — ProblemDetails conversion (RFC 9457)

- **User problem:** Hand-mapping errors to ProblemDetails per endpoint drifts and leaks internals.
- **Functional requirements:** `error.ToProblemDetails(...)` / `result.ToProblemDetails(...)`; `ErrorType` → status mapping (Validation→400, Unauthorized→401, Forbidden→403, NotFound→404, Conflict→409, Failure→422, Unavailable→503, Unexpected→500) — fully overridable per app; `ValidationError` → `errors` dictionary (matching `HttpValidationProblemDetails` shape); `type` URI templating per error code; extensions: `errorCode` always included, metadata optionally; `Unexpected` detail replaced by generic text unless `IncludeUnexpectedErrorDetails` (default **false** — secure by default).
- **Configuration:** `KorasResultsOptions` (status overrides per `ErrorType` and per error code, type-URI factory, message localization hook, metadata exposure policy).
- **Tests:** every taxonomy mapping, override precedence, validation shape, suppression default, localization hook invocation.
- **Status:** **MVP** (AspNet).

## KR-010 — Minimal API integration

- **Functional requirements:** `result.ToHttpResult()` returning `Microsoft.AspNetCore.Http.IResult`; success mapping: `Result` → 204, `Result<T>` → 200 with JSON body; overloads for 201/Created-at-route and custom success factories; failures via KR-009; typed `Results<Ok<T>, ProblemHttpResult>`-friendly variants where practical.
- **Tests:** end-to-end via `WebApplicationFactory` asserting status + `application/problem+json` payloads.
- **Status:** **MVP** (AspNet).

## KR-011 — MVC integration

- **Functional requirements:** `result.ToActionResult(controller)` / `ToActionResult()` returning `IActionResult`/`ActionResult<T>`; success 200/204/201 overloads mirroring KR-010; failures produce `ObjectResult` with ProblemDetails and correct content type.
- **Tests:** end-to-end controller tests via `WebApplicationFactory`.
- **Status:** **MVP** (AspNet).

## KR-012 — FluentValidation integration

- **Functional requirements:** `ValidationResult.ToResult()` / `ToResult<T>(value)`; `IValidator<T>.ValidateToResultAsync(instance, ct)` returning `Result<T>` (success carries the validated instance); error-code passthrough from FluentValidation `ErrorCode` to `FieldError.Code`.
- **Dependencies:** FluentValidation ≥ 11 (Apache-2.0).
- **Tests:** mapping fidelity (property names, messages, codes), success passthrough, cancellation.
- **Status:** **MVP** (FV).

## KR-013 — MediatR integration

- **Functional requirements:** `ValidationBehavior<TRequest, TResponse>` where `TResponse` is `Result`/`Result<T>`: runs all registered `IValidator<TRequest>`, short-circuits with failed Result (never throws `ValidationException`); `AddKorasResultsValidationBehavior()` registration helper.
- **Dependencies:** MediatR 12.x (Apache-2.0 — pinned; see ADR-0006), FluentValidation via KR-012.
- **Tests:** short-circuit, passthrough on valid, multiple validators aggregated, non-Result responses ignored.
- **Status:** **MVP** (MedR).

## KR-014 — OpenTelemetry error tagging

- **Functional requirements:** `result.TagCurrentActivity()` / `TagActivity(activity)` setting `otel.status_code=ERROR`, `error.type` (taxonomy, stable lowercase), `koras.error.code`; `TapActivityError()` combinator for pipelines; no-ops safely when no listener/activity; depends only on `System.Diagnostics.DiagnosticSource`.
- **Tests:** tag values via `ActivityListener`, no-op safety, success leaves activity untouched.
- **Status:** **MVP** (OTel).

## KR-015 — Localization hooks

- **Functional requirements:** `IErrorMessageLocalizer` (error → localized message) consulted by ProblemDetails conversion; default pass-through implementation; DI-friendly registration; culture from `CultureInfo.CurrentUICulture`/request culture.
- **Tests:** custom localizer replaces messages in ProblemDetails output; field errors localized individually.
- **Status:** **MVP** (AspNet).

## KR-016 — DI & options registration

- **Functional requirements:** `services.AddKorasResults(Action<KorasResultsOptions>?)`; options validated on start (`ValidateOnStart`), bindable from configuration; idempotent registration.
- **Tests:** options resolution, validation failures at startup, override behavior.
- **Status:** **MVP** (AspNet).

---

## Post-MVP features (specified at planning level)

| ID | Feature | Target | Notes |
|---|---|---|---|
| KR-101 | EF Core exception mapping | 1.1 | `DbUpdateException`/concurrency → `Conflict`/`Unavailable` |
| KR-102 | LINQ query syntax (`Select`/`SelectMany`) | 1.1 | enables `from x in result` composition |
| KR-103 | IStringLocalizer adapter package | 1.1 | builds on KR-015 |
| KR-104 | gRPC status conversion | 1.2 | `ErrorType` → `Grpc.Core.StatusCode` |
| KR-105 | GraphQL (HotChocolate) error conversion | 1.2 | `Error` → `IError` |
| KR-106 | Source-generated error catalogs | 2.0 | compile-time unique codes, docs extraction |
| KR-107 | Roslyn analyzers (unobserved results, unchecked `Value`) | Experimental | separate analyzer package |

## Explicitly out of scope

Option/Maybe/Either types; validation rule engines; mediator implementations; HTTP client concerns; retry policies (belongs to Polly/resilience — we only classify errors as `Unavailable` so callers can retry).
