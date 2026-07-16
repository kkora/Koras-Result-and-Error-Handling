# ADR-0007: System.Text.Json only; converters attribute-wired in the core

**Status:** Accepted · **Date:** 2026-07-16

## Context

`Result<T>`/`Error` may cross process boundaries (queues, caches, inter-service calls). Newtonsoft.Json support was considered as a satellite.

## Decision

Serialization support is **System.Text.Json only**, implemented as converters in the core package and wired via `[JsonConverter]` attributes so round-tripping works with zero configuration. STJ ships in the BCL for all our TFMs — the zero-dependency promise holds.

Wire shape (camelCase, versioned by documentation):
```json
{ "isSuccess": false,
  "error": { "code": "User.NotFound", "type": "notFound", "message": "…", "metadata": { } } }
```
`ValidationError` adds `"fieldErrors": [...]`; `AggregateError` adds `"errors": [...]`. Discrimination on deserialization is structural (presence of `fieldErrors`/`errors`), not via a `$type` field — no polymorphic type-name handling, no deserialization gadget surface.

## Rationale

- Newtonsoft support would add a dependency axis and double the test matrix for a shrinking audience; teams needing it can serialize a DTO.
- Attribute-wiring beats `JsonSerializerOptions` registration for pit-of-success (works in ASP.NET Core body serialization, `System.Text.Json.JsonSerializer`, and Azure SDKs without setup).

## Consequences

- The wire shape is a public contract: covered by snapshot-style round-trip tests; changes require a major version.
- Docs prominently advise ProblemDetails (not serialized Results) at *public* API boundaries.
