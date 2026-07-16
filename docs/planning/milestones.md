# Milestones — Koras.Results

| # | Milestone | Deliverable | Exit criteria |
|---|---|---|---|
| **M0** | Repository foundation | Solution, Directory.Build.props / Directory.Packages.props (CPM), global.json, .editorconfig, CLAUDE.md, community files, CI workflows, all projects created and compiling empty | `dotnet build -c Release` green with TreatWarningsAsErrors; workflows valid |
| **M1** | Abstractions & core models | `ErrorType`, `Error`, `FieldError`, `ValidationError`, `AggregateError` + unit tests | Tests green; API in Unshipped.txt |
| **M2** | Core implementation | `Result`, `Result<T>`, sync + async combinators, `Try`, `Combine`, JSON converters + full unit/serialization tests | ≥90 % branch coverage on core; round-trip suite green |
| **M3** | DI & configuration | `KorasResultsOptions`, `AddKorasResults`, options validation | Options tests green |
| **M4** | Integration packages | FluentValidation, MediatR, OpenTelemetry satellites + tests | Package tests green; dependency rules hold |
| **M5** | ASP.NET Core integration | ProblemDetails, Minimal API, MVC adapters, localization hook + `WebApplicationFactory` integration tests | End-to-end status/payload assertions green |
| **M6** | Observability & diagnostics | Mapper logging, traceId extension, OTel tag verification tests | Trace/log assertions green |
| **M7** | Samples & documentation | 4 samples w/ READMEs; full docs tree; root README | Samples run; docs complete per checklist |
| **M8** | Performance & security hardening | BenchmarkDotNet project + baseline run; threat model; security checklist pass; vulnerability audit | Benchmarks executed; `dotnet list package --vulnerable` clean |
| **M9** | NuGet packaging & release readiness | pack validation, consumption smoke test, release workflow, versioning docs, CHANGELOG | `.nupkg`s validated; consumer project builds against local feed |

Order of execution: M0 → M1 → M2 → M3 → M4/M5 (M4 satellites can interleave with M5) → M6 → M7 → M8 → M9. Each milestone ends with: build + tests + analyzers green, docs updated, CHANGELOG updated.
