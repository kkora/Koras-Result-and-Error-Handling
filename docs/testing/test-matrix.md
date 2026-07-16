# Test Matrix — Koras.Results

Maps every MVP feature (KR-001..KR-016, from `docs/features/feature-matrix.md`) to the test
classes that verify it. All paths are relative to `tests/`.

Projects:

- **Unit** = `Koras.Results.UnitTests` (runs on net8.0/net9.0/net10.0)
- **Integration** = `Koras.Results.IntegrationTests` (runs on net8.0/net9.0/net10.0)
- **Architecture** = `Koras.Results.ArchitectureTests` (net10.0 only, TFM-invariant rules)

| Feature | Test classes (file) | Category | What is pinned |
|---|---|---|---|
| KR-001 Result / Result\<T> | `ResultTests`, `ResultOfTTests` (`Koras.Results.UnitTests/ResultTests.cs`, `ResultOfTTests.cs`) | Unit | Success/failure state, `Value`/`Error` access guards (`InvalidOperationException`), `default` = `Error.Uninitialized` failure, null-success rejection, implicit conversions, `TryGetValue`/`TryGetError`, `GetValueOrDefault`, equality and operators, `ToString` |
| KR-002 Typed error model | `ErrorTests` (`Koras.Results.UnitTests/ErrorTests.cs`) | Unit | Constructor guards with `ParamName`, all 8 factory methods, code+type equality (message/metadata excluded), defensive metadata copy, `WithMetadata` copy-with semantics, `None`/`Uninitialized` sentinels |
| KR-003 Validation errors | `ValidationErrorTests`, `AggregateErrorTests` (`Koras.Results.UnitTests/ValidationErrorTests.cs`) | Unit | Default code/message, field-error order, empty-collection rejection, defensive copy, `WithMetadata` shape preservation; aggregate ≥2 rule, severity precedence, nested-aggregate flattening |
| | `MinimalApiIntegrationTests.Validation_error_produces_errors_dictionary_matching_aspnetcore_shape`, `MvcIntegrationTests.Validation_error_returns_400_with_errors_dictionary` | Integration | `errors` dictionary grouped by property over real HTTP |
| KR-004 Functional composition | `ResultExtensionsTests` (`Koras.Results.UnitTests/ResultExtensionsTests.cs`) | Unit | Map/Bind/Match/Switch/Ensure/Tap/TapError/MapError semantics, short-circuit with invocation counters and error-identity (`Assert.Same`), eager null-delegate guards, delegate exceptions not caught |
| KR-005 Async composition | `ResultAsyncExtensionsTests` (`Koras.Results.UnitTests/ResultAsyncExtensionsTests.cs`) | Unit | Full receiver/delegate overload matrix (sync receiver + async delegate, task receiver + sync delegate, task receiver + async delegate), non-generic bridges, async short-circuit with invocation counters |
| KR-006 Exception conversion (Try) | `ResultTryTests` (`Koras.Results.UnitTests/ResultTryTests.cs`) | Unit | Leak-safe default mapper (`Unexpected.Exception`, `exceptionType` metadata, message never leaks), custom mapper receives original exception, `OperationCanceledException` rethrown (sync and async), eager guard behavior for `TryAsync` |
| KR-007 Result combination | `ResultCombineTests` (`Koras.Results.UnitTests/ResultCombineTests.cs`) | Unit | 0/1/≥2-failure aggregation rules, single-error identity pass-through, `ValidationError` merging, heterogeneous `AggregateError` with severity precedence, tuple overloads (2/3/4), null guard |
| KR-008 JSON serialization | `SerializationTests` (`Koras.Results.UnitTests/SerializationTests.cs`) | Unit | Exact-payload pinning of the wire shape, round-trips (metadata, all `ErrorType` strings, `ValidationError`, `AggregateError`, complex values), property-order independence, serializer-options respect, malformed-payload rejection (`JsonException`), unknown-property tolerance, nested metadata as `JsonElement` |
| KR-009 ProblemDetails conversion | `KorasResultsOptionsTests` (`Koras.Results.IntegrationTests/AspNetCore/KorasResultsOptionsTests.cs`) | Integration | Default status map for all 8 types, code-over-type precedence, invalid status/code guards, secure defaults, `ToProblemDetails` eager building, `ValidationProblemDetails` projection, throws on success |
| KR-010 Minimal API integration | `MinimalApiIntegrationTests` (`Koras.Results.IntegrationTests/AspNetCore/MinimalApiIntegrationTests.cs`) | Integration | 200/204/201 success mapping, all 8 error-type default statuses with `application/problem+json`, `errorCode`/`traceId` extensions, `Unexpected` detail suppression + Warning log, opt-in details, mapping overrides, metadata policy, custom type URI, localizer, defaults-without-DI, `ToHttpResultAsync` sugar |
| KR-011 MVC integration | `MvcIntegrationTests` + `MvcTestController` (`Koras.Results.IntegrationTests/AspNetCore/MvcIntegrationTests.cs`) | Integration | `ToActionResult` 200/204, 404 ProblemDetails, validation dictionary, `ToActionResultOf`, custom success factory (`CreatedResult`), `ToActionResultAsync` sugar — through a real `[ApiController]` |
| KR-012 FluentValidation integration | `FluentValidationTests` (`Koras.Results.UnitTests/FluentValidationTests.cs`) | Unit | `ToResult`/`ToResult<T>`/`ToValidationError` mapping (property, message, error code), model-level empty property name, `ValidateToResult(Async)` both outcomes, cancellation propagation, null guards |
| KR-013 MediatR integration | `ValidationBehaviorTests` (`Koras.Results.IntegrationTests/MediatR/ValidationBehaviorTests.cs`) | Integration | Real DI pipeline: valid requests reach handler, invalid short-circuit without invoking handler, multi-validator aggregation, `Result` (void) responses, no-validator pass-through, `InvalidOperationException` for non-Result responses, cancellation propagation |
| KR-014 OpenTelemetry tagging | `OpenTelemetryTests` (`Koras.Results.UnitTests/OpenTelemetryTests.cs`) | Unit | `error.type` (snake_case for all 8 types) and `koras.error.code` tags, `ActivityStatusCode.Error` + status description, success no-op, null/absent-activity no-op, aggregate child count, non-recording activities untouched, `TapActivityErrorAsync` in pipelines |
| KR-015 Localization hooks | `MinimalApiIntegrationTests.Custom_localizer_translates_messages_and_field_messages`; `KorasResultsOptionsTests.AddKorasResults_preserves_custom_localizer_registration` | Integration | `IErrorMessageLocalizer` applied to detail and field messages; custom registration wins over the default `PassThroughErrorMessageLocalizer` |
| KR-016 DI & options | `KorasResultsOptionsTests` (registration idempotency, options binding); `MinimalApiIntegrationTests.Works_without_AddKorasResults_registration_using_defaults`, `Status_mapping_overrides_apply_with_code_over_type_precedence` | Integration | `AddKorasResults` registers options + localizer idempotently; configured options flow into request handling; sensible defaults without registration |

## Cross-cutting suites (not feature-specific)

| Concern | Test class (file) | Category |
|---|---|---|
| Package boundaries, zero-dep core, namespace identity, immutability, sealing, naming, Async suffix | `ArchitectureTests` (`Koras.Results.ArchitectureTests/ArchitectureTests.cs`) — 13 tests | Architecture |
| Public API surface | `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` per shipped project (build-time analyzer, not a test class) | API surface |
| Package contents & metadata | `build/validate-packages.sh` (CI: `package.yml`) | Package |
| Consumption from a fresh project | `build/consumption-test.sh` (CI: `package.yml`) | Package |
| Performance baselines | `benchmarks/Koras.Results.Benchmarks/ResultBenchmarks.cs` (`ResultBenchmarks`, `FailurePathBenchmarks`, `SerializationBenchmarks`) | Benchmark |

## Notes

- Test counts at the time of writing: 159 unit, 46 integration, 13 architecture — all green on
  every targeted framework.
- Post-MVP features (KR-101, KR-102, KR-104..KR-107) have no tests yet because they have no
  implementation; the matrix will be extended as they land.
- Every new feature PR must add its row here (see `docs/planning/definition-of-done.md`).
