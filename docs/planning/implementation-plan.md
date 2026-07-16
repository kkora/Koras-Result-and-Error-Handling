# Implementation Plan — Koras.Results

## Approach

Strict inside-out order: error model → result primitives → composition → serialization → integrations → samples/docs → hardening → packaging. Every increment compiles, tests, and updates docs before the next begins (Phase-7 loop). The public API in `docs/api/public-api-design.md` is the implementation contract.

## Epics → features → tasks

Task detail lives in [backlog.md](backlog.md); milestone gating in [milestones.md](milestones.md); risks in [risks.md](risks.md); completion bar in [definition-of-done.md](definition-of-done.md).

| Epic | Features | Milestone |
|---|---|---|
| E1 Repository foundation | build system, CI, community files, CLAUDE.md | M0 |
| E2 Error model | KR-002, KR-003 | M1 |
| E3 Result primitives | KR-001 | M2 |
| E4 Composition | KR-004, KR-005, KR-006, KR-007 | M2 |
| E5 Serialization | KR-008 | M2 |
| E6 AspNetCore integration | KR-009, KR-010, KR-011, KR-015, KR-016 | M3, M5, M6 |
| E7 Satellites | KR-012, KR-013, KR-014 | M4 |
| E8 Quality infrastructure | architecture tests, benchmarks | M8 |
| E9 Samples & docs | 4 samples, docs tree, README | M7 |
| E10 Packaging & release | pack validation, consumption test, release workflow | M9 |

## Recommended MVP

All five packages with features KR-001…KR-016 (see `docs/features/mvp-scope.md`).

## Packages to create

`Koras.Results`, `Koras.Results.AspNetCore`, `Koras.Results.FluentValidation`, `Koras.Results.MediatR`, `Koras.Results.OpenTelemetry`.

## First five implementation tasks

1. **T-001** Repository foundation: solution, CPM, build props, editorconfig, global.json (M0).
2. **T-002** CI workflows + community files + CLAUDE.md (M0).
3. **T-101** `ErrorType` + `Error` with factories, metadata, equality + tests (M1).
4. **T-102** `FieldError`, `ValidationError`, `AggregateError` + tests (M1).
5. **T-201** `Result` + `Result<T>` structs with guards, conversions, `TryGetValue` + tests (M2).

## Highest risks (summary — full register in risks.md)

1. Public-API lock-in on structs → mitigated by analyzer-locked minimal surface.
2. Overload-matrix combinatorics in async extensions → mitigated by a written matrix + generated-style tests.
3. ProblemDetails correctness across MVC/Minimal API differences → mitigated by end-to-end integration tests on both.
4. MediatR licensing → version-bounded (ADR-0006).
5. Scope creep → out-of-scope list is normative.

## Deferred features

KR-101 (EF Core), KR-102 (LINQ syntax), KR-103 (IStringLocalizer adapter), KR-104 (gRPC), KR-105 (GraphQL), KR-106 (source-gen catalogs), KR-107 (analyzers). See `docs/features/future-roadmap.md`.
