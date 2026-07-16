# ADR-0005: Error is an immutable class with a closed hierarchy

**Status:** Accepted · **Date:** 2026-07-16

## Context

The error object must carry code, message, type, metadata, and (for validation) field-level details; it must serialize predictably and project into ProblemDetails. Options: struct, open class hierarchy (user subclasses), closed hierarchy, or a single class with a "kind" discriminator only.

## Decision

`Error` is an **immutable class**; `ValidationError` and `AggregateError` are the only subclasses, both `sealed`. `Error`'s constructor is `public` (custom codes/types are normal usage) but the hierarchy is closed: subclass constructors are internal-ish (`ValidationError`/`AggregateError` are constructible, but *new* subclasses aren't supported or tested and the base class is not designed for inheritance — non-virtual members, documented).

Equality: `Code` + `Type` value equality; message/metadata excluded (errors are identities; messages are presentation).

## Rationale

- Failure paths already allocate (messages, metadata); a class costs nothing extra and keeps `Result<T>`'s struct layout to one reference.
- A *closed* shape set keeps JSON converters and ProblemDetails projection total — every shape is known, tested, and versioned. User subclasses would break round-tripping and projection silently.
- Rich per-domain errors are expressed through codes + metadata + static factory catalogs (see extension-model.md), which cover every observed need without inheritance.

## Consequences

- Users cannot attach behavior to errors via subclassing — by design; behavior belongs in handlers, not error objects.
- If a genuinely new structured shape emerges (like ValidationError did), it ships *in the core* as a new sealed subclass in a minor release, with converter + projection support in lockstep.
