# ADR-0003: Result types are readonly structs; default(Result) is failure

**Status:** Accepted · **Date:** 2026-07-16

## Context

Result types can be classes (FluentResults, CSFE, Ardalis) or structs (ErrorOr). They appear on every hot path of an application, so allocation behavior matters. Structs bring the `default(T)` pitfall: an uninitialized struct must not masquerade as a valid result.

## Decision

`Result` and `Result<T>` are **`readonly struct`s**. `default(Result)`/`default(Result<T>)` is a **failure** carrying `Error.Uninitialized` (`"Result.Uninitialized"`, `ErrorType.Unexpected`). `Result<T>` implements `IEquatable<Result<T>>`. No interfaces are implemented that would encourage boxing in user code (no `IResult`-style abstraction).

Internal representation: success flag encoded via the `_error` field (`null` = success), so a success `Result<T>` stores only the value + one null reference — zero allocation.

Public async surface uses `Task`, not `ValueTask` (simpler for consumers, negligible difference at library level; combinators are `ConfigureAwait(false)` throughout).

## Rationale

- Success results allocate nothing — measurable benefit on hot paths (benchmarked vs class-based libraries).
- `default` = failure eliminates the classic struct hazard: an uninitialized result cannot pass `IsSuccess` and cannot yield a null `Value` without throwing.
- Immutability (`readonly`) guarantees thread-safety for free.

## Consequences

- Structs limit later evolution (no subclassing, layout is ABI-ish) — mitigated by keeping the surface minimal and locked (ADR-0008).
- Copy semantics: results are small (≤ ~2 words + T); documented that they're cheap to copy.
- `Error` remains a class (failure path allocates — acceptable; failures are exceptional-ish and carry rich data).
