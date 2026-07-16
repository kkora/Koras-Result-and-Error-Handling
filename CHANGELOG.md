# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0-preview.1] - 2026-07-16

### Added

- `Koras.Results` core package: `Result` and `Result<T>` readonly structs (allocation-free success path, `default` = uninitialized failure), semantic error taxonomy (`ErrorType` with 8 categories), `Error` with stable codes and metadata, `ValidationError` with field-level errors, `AggregateError` with severity precedence.
- Functional composition: `Map`, `Bind`, `Match`, `Switch`, `Ensure`, `Tap`, `TapError`, `MapError` with complete async overload matrix (`ConfigureAwait(false)` throughout).
- `Result.Try` / `Result.TryAsync` exception boundaries with custom mappers; `OperationCanceledException` always propagates.
- `Result.Combine` aggregation (validation-error merging, severity-based `AggregateError`) including tuple-typed overloads.
- System.Text.Json serialization for all result and error types, attribute-wired with a stable camelCase wire shape.
- `Koras.Results.AspNetCore`: RFC 9457 ProblemDetails conversion with configurable `ErrorType`/error-code status mapping, Minimal API `ToHttpResult` adapters, MVC `ToActionResult` adapters, `errors` dictionary for validation failures, `errorCode`/`traceId` extensions, secure-by-default suppression of `Unexpected` error details, `IErrorMessageLocalizer` hook, `AddKorasResults` DI registration with startup options validation.
- `Koras.Results.FluentValidation`: `ToResult`/`ToValidationError` conversions and `ValidateToResult(Async)` helpers preserving property names, messages, and error codes.
- `Koras.Results.MediatR`: `ValidationBehavior<,>` short-circuiting Result-returning handlers with aggregated validation failures; `AddKorasResultsValidationBehavior` registration (MediatR 12.x — ADR-0006).
- `Koras.Results.OpenTelemetry`: `TagCurrentActivity`/`TagActivity`/`TapActivityErrorAsync` activity tagging (`error.type`, `koras.error.code`) following OpenTelemetry semantic conventions, zero-dependency.
- Repository foundation: multi-targeted builds (net8.0/net9.0/net10.0), central package management, PublicApiAnalyzers surface tracking, StyleCop, deterministic CI builds with Source Link, benchmarks, architecture tests, samples, and full documentation tree.

[Unreleased]: https://github.com/kkora/Koras-Result-and-Error-Handling/compare/v0.1.0-preview.1...HEAD
[0.1.0-preview.1]: https://github.com/kkora/Koras-Result-and-Error-Handling/releases/tag/v0.1.0-preview.1
