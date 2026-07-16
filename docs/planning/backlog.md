# Implementation Backlog — Koras.Results

Task fields: ID · description · files · dependencies · expected public API · tests · docs · risks · completion criteria. Completion criteria implicitly include the [definition of done](definition-of-done.md).

## Epic E1 — Repository foundation (M0)

### T-001 Build system & solution
- **Files:** `Koras.Results.sln`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `.editorconfig`, `.gitattributes`, `NuGet.Config`, `src/*/**.csproj`, `tests/*/**.csproj`
- **Dependencies:** none
- **Public API:** none
- **Tests:** solution builds Release, warnings-as-errors
- **Docs:** none
- **Risks:** SDK/TFM availability in CI → pin via global.json rollForward
- **Done:** `dotnet build -c Release` green with all projects empty

### T-002 CI, community files, CLAUDE.md
- **Files:** `.github/workflows/{build,release,codeql,dependency-review}.yml`, issue templates, PR template, `dependabot.yml`, `CLAUDE.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md`, `SUPPORT.md`, `CHANGELOG.md`, `ROADMAP.md`
- **Dependencies:** T-001
- **Done:** workflows lint-valid; files complete per Phase-5 checklist

## Epic E2 — Error model (M1)

### T-101 ErrorType + Error
- **Files:** `src/Koras.Results/ErrorType.cs`, `Error.cs`; tests `ErrorTests.cs`
- **Dependencies:** T-001
- **API:** `ErrorType`, `Error` per design §1.1–1.2
- **Tests:** factories set correct type; guards (null/whitespace code/message); equality Code+Type; metadata immutability + copy-with; ToString format; None/Uninitialized sentinels
- **Docs:** XML docs complete
- **Risks:** equality contract subtleties → property-based-ish test cases
- **Done:** tests green; Unshipped.txt updated

### T-102 FieldError, ValidationError, AggregateError
- **Files:** `FieldError.cs`, `ValidationError.cs`, `AggregateError.cs`; tests
- **Dependencies:** T-101
- **API:** design §1.3–1.5
- **Tests:** ordering preserved; empty-collection guards; severity precedence for aggregate type; flattening of nested aggregates
- **Done:** tests green

## Epic E3 — Result primitives (M2)

### T-201 Result & Result<T>
- **Files:** `Result.cs`, `ResultOfT.cs`; tests `ResultTests.cs`, `ResultOfTTests.cs`
- **Dependencies:** T-101
- **API:** design §1.6–1.7 (factories, guards, implicit conversions, TryGetValue/TryGetError, GetValueOrDefault, equality, default=failure)
- **Tests:** default-struct is failure w/ Uninitialized; Value/Error access guards; null-value rejection; conversions; equality; `[MemberNotNullWhen]` honored (compile-time usage test)
- **Risks:** struct default semantics — covered explicitly
- **Done:** tests green; zero-allocation success path (verified later by benchmarks)

## Epic E4 — Composition (M2)

### T-202 Sync combinators
- **Files:** `ResultExtensions.cs`; tests `ResultExtensionsTests.cs`
- **API:** design §1.8
- **Tests:** per combinator: success path, failure short-circuit (delegate NOT invoked — counter asserts), null-delegate guards, error identity preserved
- **Done:** full matrix green

### T-203 Async combinators
- **Files:** `ResultAsyncExtensions.cs`; tests `ResultAsyncExtensionsTests.cs`
- **Dependencies:** T-202
- **API:** design §1.9
- **Tests:** overload matrix; short-circuit without invoking async delegates; ConfigureAwait verified by convention test; faulted-task propagation (exceptions flow, not converted)
- **Risks:** combinatorial surface → tests generated from a shared helper pattern
- **Done:** matrix green

### T-204 Try / TryAsync
- **Files:** `Result.Try` members in `Result.cs` (partial `Result.Try.cs`); tests `ResultTryTests.cs`
- **API:** design §1.6
- **Tests:** capture; custom mapper receives original exception; OperationCanceledException rethrown (sync + async, incl. TaskCanceledException); default error metadata contains exceptionType; async fault capture
- **Done:** tests green

### T-205 Combine
- **Files:** `Result.Combine.cs`; tests `ResultCombineTests.cs`
- **Dependencies:** T-102, T-201
- **Tests:** empty input → success; single failure identity; multi-failure aggregation rules (all-validation merge vs AggregateError); tuple variants values/first-error semantics
- **Done:** tests green

## Epic E5 — Serialization (M2)

### T-301 JSON converters
- **Files:** `Serialization/ErrorJsonConverter.cs`, `ResultJsonConverter.cs`, `ResultJsonConverterFactory.cs`; tests `SerializationTests.cs`
- **Dependencies:** T-102, T-201
- **API:** design §1.10; wire shape per ADR-0007
- **Tests:** round-trip all shapes (Error, ValidationError, AggregateError, Result, Result<T> success/failure, nested metadata primitives); camelCase; structural discrimination; malformed payloads → JsonException; no polymorphic gadget acceptance
- **Risks:** wire shape is forever → snapshot-style assertions on exact JSON
- **Done:** round-trip suite green

## Epic E6 — AspNetCore (M3, M5, M6)

### T-401 Options + DI (M3)
- **Files:** `src/Koras.Results.AspNetCore/KorasResultsOptions.cs`, `MetadataExposurePolicy.cs`, `KorasResultsServiceCollectionExtensions.cs`; tests
- **API:** design §2
- **Tests:** default mappings; precedence code>type>default; invalid status (<100 or >599) rejected at configure-time validation; idempotent AddKorasResults; options bindable
- **Done:** tests green

### T-402 ProblemDetails conversion (M5)
- **Files:** `ProblemDetailsExtensions.cs`, `IErrorMessageLocalizer.cs`, `PassThroughErrorMessageLocalizer.cs`, internal `ProblemDetailsBuilder.cs`
- **Dependencies:** T-401
- **Tests:** every ErrorType default status; ValidationError → errors dictionary (grouped by property); errorCode extension always present; traceId present when enabled; Unexpected detail suppressed by default and included when opted-in; metadata exposure policy; localizer invoked per error and per field
- **Risks:** matching HttpValidationProblemDetails shape exactly → integration assert against ASP.NET Core's own output shape
- **Done:** unit + integration green

### T-403 Minimal API adapters (M5)
- **Files:** `HttpResultExtensions.cs`; integration tests (WebApplicationFactory, Minimal API app)
- **Tests:** 204/200/201/custom success; each failure type end-to-end content-type `application/problem+json`; Task sugar overloads
- **Done:** end-to-end green

### T-404 MVC adapters (M5)
- **Files:** `ActionResultExtensions.cs`; integration tests (controllers)
- **Tests:** mirror T-403 for MVC; ActionResult<T> variant
- **Done:** end-to-end green

### T-405 Mapper logging + observability (M6)
- **Files:** internal logging in ProblemDetailsBuilder via ILogger resolved from HttpContext
- **Tests:** Debug log on mapping; Warning on suppression (FakeLogger assertions)
- **Done:** log assertions green

## Epic E7 — Satellites (M4)

### T-501 FluentValidation package
- **Files:** `src/Koras.Results.FluentValidation/*`; tests
- **API:** design §3
- **Tests:** mapping fidelity (property/message/code); model-level failures (empty PropertyName); valid → success carrying instance; cancellation propagation
- **Done:** tests green

### T-502 MediatR package
- **Files:** `src/Koras.Results.MediatR/*`; tests (real MediatR pipeline via DI)
- **Dependencies:** T-501
- **API:** design §4
- **Tests:** invalid request short-circuits with Result failure (handler not invoked); valid passes through; multiple validators aggregate; non-Result TResponse throws InvalidOperationException on failure; no validators → passthrough
- **Done:** tests green

### T-503 OpenTelemetry package
- **Files:** `src/Koras.Results.OpenTelemetry/*`; tests (ActivityListener)
- **API:** design §5
- **Tests:** tags set on failure (error.type value format, koras.error.code); success untouched; null Activity.Current no-op; non-recording activity no-op; aggregate count tag
- **Done:** tests green

## Epic E8 — Quality infrastructure (M8)

### T-601 Architecture tests
- **Files:** `tests/Koras.Results.ArchitectureTests/*`
- **Tests:** core has no references beyond BCL; satellite dependency rules; all public core types immutable/sealed conventions; namespace = package rule
### T-602 Benchmarks
- **Files:** `benchmarks/Koras.Results.Benchmarks/*`
- **Content:** success/failure result creation, Map/Bind chains, Combine, serialization, vs raw exception throw/catch baseline
- **Done:** benchmarks compile and run locally (short job); methodology documented

## Epic E9 — Samples & docs (M7)
### T-701 Console sample · T-702 Minimal API sample · T-703 WebApi (MVC) sample · T-704 Worker sample
- Each: project + README (setup, run commands, expected output, error scenarios, doc links); reference local projects; instructions to switch to NuGet packages
### T-705 Docs tree + root README (per Phase 11/13 requirements)

## Epic E10 — Packaging & release (M9)
### T-801 Pack metadata validation (icon, README, symbols, SourceLink) — inspect .nupkg contents in CI script
### T-802 Consumption smoke test — script creates console app against local feed, builds, runs
### T-803 Release workflow + versioning docs + CHANGELOG finalization
