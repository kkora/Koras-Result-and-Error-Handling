# Test Strategy — Koras.Results

This document describes the layered testing approach for the Koras.Results package family. The
guiding principle: **every test pins meaningful behavior**, not a coverage percentage. Coverage is
a tripwire, not a goal.

Current state (all green on net8.0, net9.0, and net10.0): **159 unit tests**, **46 integration
tests**, **13 architecture tests**.

## The layers

| Layer | Project | Targets | Verifies |
|---|---|---|---|
| Unit | `tests/Koras.Results.UnitTests` | net8.0;net9.0;net10.0 | Value semantics, guards, combinator laws, serialization contract |
| Integration | `tests/Koras.Results.IntegrationTests` | net8.0;net9.0;net10.0 | Real HTTP hosts (Minimal API + MVC), real MediatR DI pipeline |
| Architecture | `tests/Koras.Results.ArchitectureTests` | net10.0 only | Dependency direction, immutability, naming conventions |
| API surface | PublicApiAnalyzers (build-time) | all shipped projects | Every public-surface change is an explicit, reviewable diff |
| Package | `build/validate-packages.sh` + `build/consumption-test.sh` | packed `.nupkg` | Package contents, metadata, and real-world consumability |

### 1. Unit tests (`Koras.Results.UnitTests`)

The unit suite covers the core primitives and the two satellites that need no host
(FluentValidation, OpenTelemetry). Conventions in force:

- **Semantics**: success/failure state, `default(Result)` = uninitialized failure, equality by
  code+type, implicit conversions, `TryGetValue`/`GetValueOrDefault` (`ResultTests`,
  `ResultOfTTests`, `ErrorTests`, `ValidationErrorTests`, `AggregateErrorTests`).
- **Guards**: every null/invalid argument path is asserted with the exact exception type and,
  where the contract specifies it, the `ParamName` (e.g. `Error` constructor rejects
  null/empty/whitespace code and message with `ArgumentException`). Async guards are verified to
  throw *eagerly at call time*, not inside the returned task (`ResultTryTests.Try_guards_null_delegates`).
- **Short-circuit with invocation counters**: failure-path combinator tests count delegate
  invocations and assert zero, plus `Assert.Same` on the error to prove *identity* pass-through,
  not just an equal error (`ResultExtensionsTests.Map_short_circuits_on_failure_without_invoking_delegate`
  and the async equivalents in `ResultAsyncExtensionsTests`).
- **Serialization round-trip including exact-payload pinning**: `SerializationTests` asserts the
  *exact* JSON string for the documented wire shape (e.g.
  `{"isSuccess":false,"error":{"code":"A.B","message":"m","type":"failure"}}`). This is
  deliberate: the wire shape is a versioned public contract (ADR-0007), and changing these tests
  is a loud act that signals a breaking change. The suite also covers round-trips with metadata,
  all eight `ErrorType` wire strings, structural discrimination of `ValidationError`/`AggregateError`,
  malformed-payload rejection (`JsonException`), unknown-property tolerance (forward
  compatibility), and nested metadata preserved as `JsonElement`.
- **Cancellation**: `OperationCanceledException` always propagates — asserted for `Try`,
  `TryAsync`, and `ValidateToResultAsync`.

### 2. Integration tests (`Koras.Results.IntegrationTests`)

No mocks of ASP.NET Core or MediatR — the suite runs the real thing:

- **Two real HTTP pipelines on TestServer** (`AspNetCore/TestHost.cs` builds an in-memory host):
  `MinimalApiIntegrationTests` maps endpoints returning `ToHttpResult()`;
  `MvcIntegrationTests` uses a real `[ApiController]` (`MvcTestController`) registered via
  `AddApplicationPart`. Assertions run against actual HTTP responses: status codes for all eight
  error types, `application/problem+json` content type, `errors` dictionary shape, `errorCode` and
  `traceId` extensions, options overrides, localization, metadata exposure policy, and the
  no-`AddKorasResults` fallback path.
- **Log assertions**: `CapturingLoggerProvider` captures `(Category, Level, Message)` entries so
  tests can assert the Warning emitted when `Unexpected` details are suppressed.
- **Real MediatR DI pipeline**: `MediatR/ValidationBehaviorTests` builds a `ServiceProvider` with
  `AddMediatR` + `AddValidatorsFromAssemblyContaining` + `AddKorasResultsValidationBehavior` and
  sends requests through `IMediator` — covering short-circuiting, multi-validator aggregation,
  the fail-fast `InvalidOperationException` for non-Result responses, and cancellation.
- `KorasResultsOptionsTests` verifies the options contract (default status map, code-over-type
  precedence, secure defaults, DI idempotency).

Details in [integration-testing.md](integration-testing.md).

### 3. Architecture tests (`Koras.Results.ArchitectureTests`)

Thirteen rules enforcing `docs/architecture/dependency-rules.md` and package boundaries, using
reflection plus NetArchTest. They target **net10.0 only** because the rules are TFM-invariant —
running them three times would triple runtime for zero additional signal. Enforced: the core
references nothing beyond the BCL; satellites never reference each other except the audited
MediatR→FluentValidation edge; every satellite references the core; public namespaces match
package identity; core public types are immutable; `Error` subclasses are sealed; extension
classes end in `Extensions`; Task-returning public methods carry the `Async` suffix.

### 4. API-surface tracking (PublicApiAnalyzers)

Every shipped project includes `Microsoft.CodeAnalysis.PublicApiAnalyzers` with
`PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt` as `AdditionalFiles` (ADR-0008). Any public
surface change fails the build until the text file is edited — turning accidental API changes
into deliberate, reviewable diffs. Pre-1.0, the entire surface lives in `Unshipped`; the 1.0
release moves it to `Shipped`. Binary-level enforcement (`EnablePackageValidation` +
`PackageValidationBaselineVersion`) activates after 1.0 (see
[compatibility-testing.md](compatibility-testing.md)).

### 5. Package validation and consumption smoke test

Run in CI on every push/PR (`.github/workflows/package.yml`) and on release:

- **`build/validate-packages.sh`** unzips every produced `.nupkg` and asserts: icon, README,
  nuspec; an assembly *and* XML documentation file for each of net8.0/net9.0/net10.0; nuspec
  metadata (MIT license expression, project URL, repository, readme, icon, tags); a matching
  `.snupkg`; and that the core package declares **zero** NuGet dependencies (ADR-0001).
- **`build/consumption-test.sh`** creates a brand-new net10.0 console project, installs the
  freshly packed `Koras.Results` from a local feed, compiles a program exercising
  `Try`/`Ensure`/`Map`/`Match` and the error taxonomy, runs it, and greps for the expected output.
  This catches packaging mistakes unit tests cannot (missing TFM assets, broken nuspec, bad
  dependency ranges).

## Coverage bar

- **Core (`Koras.Results`): ≥ 90 % branch coverage.**
- **Satellites: ≥ 80 % branch coverage.**

These thresholds come from `docs/planning/definition-of-done.md` and act as regression tripwires
(coverage must not drop below them), collected via `coverlet.collector`
(`--collect:"XPlat Code Coverage"` in `test.yml`, cobertura artifacts retained 14 days).
The bar is subordinate to the real rule: **test meaningful behavior, not lines.** A branch is
covered because a test asserts what that branch *means* (an exception type, an error identity, a
wire payload) — never by a test that merely executes it. Tests asserting nothing, or asserting
implementation details, are rejected in review even if they raise the percentage.

## Tooling choices and rationale

| Tool | Version | Why |
|---|---|---|
| xUnit | 2.9.2 | De-facto standard for .NET libraries; parallel by default; `Theory` data-driven tests fit the taxonomy/overload matrices well |
| Plain `Assert` (no FluentAssertions) | — | FluentAssertions moved to a paid license for commercial use from v8; a permissively-licensed library must not make contributors depend on it. xUnit's `Assert` (with `Assert.Same`, `Assert.Throws`, collection asserts) covers every need here |
| coverlet.collector | 6.0.2 | Cross-platform coverage via the VSTest collector; cobertura output uploads as a CI artifact |
| NetArchTest.Rules | 1.3.2 | Declarative architecture rules (sealing, naming) where its fluent API is clearer than raw reflection; raw reflection is used where precision matters (immutability, references) |
| Microsoft.CodeAnalysis.PublicApiAnalyzers | 3.3.4 | API-surface locking (ADR-0008) |
| BenchmarkDotNet | 0.14.0 | Performance verification — see [performance-testing.md](performance-testing.md) |

All test dependencies are build-time only and never ship in packages; versions are centralized in
`Directory.Packages.props` (Central Package Management), including per-TFM conditions for
`Microsoft.AspNetCore.Mvc.Testing`.

## What is *not* tested, and why

- **No mocking frameworks.** The library has almost no injectable seams by design (pure values);
  the few that exist (localizer, logger provider) are trivially hand-rolled in the tests.
- **No snapshot-file tooling.** Exact payloads are inline string literals — the diff *is* the
  review artifact.
- **No UI/E2E beyond TestServer.** The AspNetCore satellite's contract ends at the HTTP response;
  TestServer exercises the full middleware/serialization pipeline that produces it.

## Related documents

- [test-matrix.md](test-matrix.md) — feature-to-test mapping (KR-001..KR-016)
- [integration-testing.md](integration-testing.md) — TestServer harness details
- [compatibility-testing.md](compatibility-testing.md) — multi-TFM and package compatibility
- [performance-testing.md](performance-testing.md) — benchmark methodology
- `docs/planning/definition-of-done.md` — the test-related gates every change must pass
