# ADR-0006: Pin MediatR dependency to [12.4, 13.0)

**Status:** Accepted · **Date:** 2026-07-16

## Context

MediatR 13+ moved to a commercial license (free tiers exist, but terms are use-dependent). MediatR 12.x remains Apache-2.0. `Koras.Results.MediatR` exists because Result-returning handlers + validation short-circuiting is a top community request.

## Decision

Depend on **`MediatR [12.4.0, 13.0.0)`** — an explicit upper bound (exceptional; our dependency rules otherwise forbid upper bounds).

## Rationale

- An MIT package silently pulling a commercially-licensed transitive dependency would be a trust violation for enterprise consumers.
- 12.x is stable, feature-complete for our needs (`IPipelineBehavior<,>`), and widely deployed.

## Consequences

- Users on MediatR 13+ get a NuGet version conflict rather than silent license exposure — this is the desired failure mode; the README/docs explain it.
- Alternatives tracked: if the ecosystem consolidates on a fork or on `Mediator`-style source-gen libraries, a new satellite can ship; the behavior class is ~60 lines and easily ported.
- Revisit when MediatR licensing or community consensus changes (issue template pinned).
