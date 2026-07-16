# ADR-0001: Core package plus integration satellites

**Status:** Accepted · **Date:** 2026-07-16

## Context

Koras.Results needs ASP.NET Core, FluentValidation, MediatR, and OpenTelemetry integrations. Options: (a) one package with everything, (b) core + satellites, (c) metapackage + micro-packages.

## Decision

**Core + satellites (b).** `Koras.Results` ships with zero dependencies; each integration is its own package depending on the core and its integration target only.

## Rationale

- Clean Architecture teams must be able to reference the Result type from a domain project with zero transitive baggage — that is a headline feature.
- Each integration has an independent dependency lifecycle (FluentValidation majors, MediatR licensing, ASP.NET Core TFM coupling); separate packages isolate those risks.
- A monolith would force ASP.NET Core's shared framework onto console/worker consumers.
- A metapackage (c) is unnecessary at 5 packages and would create a second versioning surface.

## Consequences

- Five nuspecs/CI artifacts to maintain; central build props keep the cost low.
- Users must install 2 packages for the common web scenario (documented in the quick start; acceptable).
- Satellite-to-satellite references are forbidden except MediatR→FluentValidation (audited in architecture tests).
