# API Reference Overview — Koras.Results

How to explore the public API of the package family, and where the authoritative definitions
live.

## Three ways to explore the API

### 1. XML IntelliSense documentation (in your editor)

Every public member in every package carries XML documentation — enforced by the Definition of
Done and by `GenerateDocumentationFile` with warnings-as-errors, and the per-TFM XML files are
verified present in each `.nupkg` by `build/validate-packages.sh`. Hovering any Koras type or
method in an IDE shows the contract, including guard behavior (`<exception>` tags) and remarks
(e.g. the `default(Result)` semantics on the struct docs). With Source Link + symbol packages you
can also *step into* the implementation from a consuming project.

### 2. PublicAPI.Shipped.txt / PublicAPI.Unshipped.txt (machine-readable, authoritative)

Each shipped project carries a pair of PublicApiAnalyzers text files listing **every public
symbol** — the authoritative, diff-reviewed surface (ADR-0008):

| Package | Surface files |
|---|---|
| Koras.Results | `src/Koras.Results/PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` |
| Koras.Results.AspNetCore | `src/Koras.Results.AspNetCore/PublicAPI.*.txt` |
| Koras.Results.FluentValidation | `src/Koras.Results.FluentValidation/PublicAPI.*.txt` |
| Koras.Results.MediatR | `src/Koras.Results.MediatR/PublicAPI.*.txt` |
| Koras.Results.OpenTelemetry | `src/Koras.Results.OpenTelemetry/PublicAPI.*.txt` |

Pre-1.0, the entire surface lives in `Unshipped.txt` (it moves to `Shipped.txt` at 1.0.0 and
freezes). The build fails on any mismatch between code and files, so these lists are never
stale. When you need to know *exactly* what is public — every overload, property accessor, and
operator, with full nullability annotations — read these files; they are more precise than any
generated reference page.

### 3. Design documents and per-feature guides

- [`docs/api/public-api-design.md`](../api/public-api-design.md) — the normative API contract
  with rationale, semantics, and thread-safety/lifetime tables. Start here for the "why".
- `docs/features/` — per-feature guides mapped to the API (see
  `docs/features/feature-matrix.md` for the KR-001..KR-016 index).
- [`docs/api/backward-compatibility.md`](../api/backward-compatibility.md) — what you may rely
  on across versions.

## Per-package type index

The complete set of public types per package, as declared in the `PublicAPI.Unshipped.txt` files
today.

### Koras.Results (namespace `Koras.Results`)

| Type | Kind | Role |
|---|---|---|
| `Result` | readonly struct | Void-outcome result; factories (`Success`, `Failure`, `FromError`), `Try`/`TryAsync` exception boundaries, `Combine` |
| `Result<T>` | readonly struct | Value-carrying result; `Value`/`Error` access, `TryGetValue`/`TryGetError`, `GetValueOrDefault`, conversions |
| `ErrorType` | enum | The closed 8-value semantic taxonomy |
| `Error` | class | Code + message + type + metadata; static factories per taxonomy entry; `WithMetadata`; `None`/`Uninitialized` sentinels |
| `ValidationError` | sealed class : Error | Field-level validation failures (`FieldErrors`) |
| `AggregateError` | sealed class : Error | ≥2 combined errors, severity-precedence `Type`, flattened |
| `FieldError` | sealed record | `PropertyName`, `Message`, optional `Code` |
| `ResultExtensions` | static class | Sync combinators: `Map`, `Bind`, `Match`, `Switch`, `Ensure`, `Tap`, `TapError`, `MapError` |
| `ResultAsyncExtensions` | static class | The async overload matrix: `MapAsync`, `BindAsync`, `MatchAsync`, `EnsureAsync`, `TapAsync`, `TapErrorAsync` |

Namespace `Koras.Results.Serialization` (public so custom `JsonSerializerOptions` can register
them explicitly; normally attribute-wired and never touched):

| Type | Kind |
|---|---|
| `ErrorJsonConverter` | `JsonConverter<Error>` |
| `ValidationErrorJsonConverter` | `JsonConverter<ValidationError>` |
| `AggregateErrorJsonConverter` | `JsonConverter<AggregateError>` |
| `ResultJsonConverter` | `JsonConverter<Result>` |
| `ResultJsonConverterFactory` | `JsonConverterFactory` for `Result<T>` |

### Koras.Results.AspNetCore (namespace `Koras.Results.AspNetCore`)

| Type | Kind | Role |
|---|---|---|
| `KorasResultsOptions` | sealed class | Status mapping (`MapErrorType`/`MapErrorCode`/`GetStatusCode`), `IncludeUnexpectedErrorDetails`, `MetadataExposure`, `IncludeTraceId`, `ProblemTypeUriFactory` |
| `MetadataExposurePolicy` | enum | `None` (default) / `All` |
| `KorasResultsServiceCollectionExtensions` | static class | `AddKorasResults(...)` DI registration |
| `ProblemDetailsExtensions` | static class | `ToProblemDetails(...)` for `Error`/`Result`/`Result<T>` |
| `HttpResultExtensions` | static class | Minimal API: `ToHttpResult`, `ToCreatedHttpResult`, `ToHttpResultAsync` |
| `ActionResultExtensions` | static class | MVC: `ToActionResult`, `ToActionResultOf`, `ToActionResultAsync` |
| `IErrorMessageLocalizer` | interface | Localization hook for messages and field messages |
| `PassThroughErrorMessageLocalizer` | sealed class | Default localizer (identity) |

### Koras.Results.FluentValidation (namespace `Koras.Results.FluentValidation`)

| Type | Kind | Role |
|---|---|---|
| `ValidationResultExtensions` | static class | `ToResult()`, `ToResult<T>(value)`, `ToValidationError()` |
| `ValidatorExtensions` | static class | `ValidateToResult`, `ValidateToResultAsync` |

### Koras.Results.MediatR (namespace `Koras.Results.MediatR`)

| Type | Kind | Role |
|---|---|---|
| `ValidationBehavior<TRequest, TResponse>` | sealed class | `IPipelineBehavior<,>` running validators and short-circuiting Result responses |
| `KorasResultsMediatRServiceCollectionExtensions` | static class | `AddKorasResultsValidationBehavior()` |

### Koras.Results.OpenTelemetry (namespace `Koras.Results.OpenTelemetry`)

| Type | Kind | Role |
|---|---|---|
| `ActivityResultExtensions` | static class | `TagCurrentActivity`, `TagActivity`, `TapActivityErrorAsync` |
| `KorasResultsActivityTags` | static class | Tag-name constants: `ErrorType` (`error.type`), `ErrorCode` (`koras.error.code`), `AggregateCount` (`koras.error.aggregate_count`) |

Everything else in the source tree (e.g. `ProblemDetailsBuilder`, `KorasProblemHttpResult`,
`ResultHttpMapperLog`) is `internal` — implementation detail, not API, and absent from the
surface files by construction.
