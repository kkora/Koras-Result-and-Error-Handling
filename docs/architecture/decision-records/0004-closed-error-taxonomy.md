# ADR-0004: Closed ErrorType taxonomy, domain-first; HTTP mapping lives only in AspNetCore

**Status:** Accepted · **Date:** 2026-07-16

## Context

Errors need classification for programmatic handling and HTTP projection. Ardalis.Result uses an HTTP-shaped status enum in the core (leaks transport into domain). FluentResults has no classification. An open/extensible taxonomy (user-defined categories) was considered.

## Decision

`ErrorType` is a **closed enum of eight semantic categories**: `Failure`, `Validation`, `NotFound`, `Conflict`, `Unauthorized`, `Forbidden`, `Unavailable`, `Unexpected`. It expresses business/technical meaning, never HTTP. The `ErrorType → status code` projection is defined exclusively in `Koras.Results.AspNetCore` and is overridable per type and per error code.

## Rationale

- A closed set is what makes uniform dashboards, retry heuristics (`Unavailable` ⇒ retryable), and org-wide HTTP contracts possible; an open set degenerates into stringly-typed chaos.
- Extensibility already exists on two other axes: unbounded `Code` values and `Metadata`.
- Keeping HTTP out of the core preserves the domain-purity promise (a domain project referencing Koras.Results sees no `400`s).

## Consequences

- New categories require a minor release and strong justification (none anticipated; the set was validated against the brief's required error kinds: infrastructure → `Unavailable`, domain → `Failure`).
- Apps disagreeing with a default projection override it in options rather than redefining semantics.
