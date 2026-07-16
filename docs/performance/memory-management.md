# Memory Management — Koras.Results

How the types are laid out, what allocates when, and how the pieces behave under GC. Companion to
[performance-guide.md](performance-guide.md).

## Struct layout

Both result types are `readonly struct`s (ADR-0003) with private fields
(`src/Koras.Results/Result.cs`, `ResultOfT.cs`):

```
Result        { bool _isSuccess; Error? _error; }
Result<T>     { bool _isSuccess; T? _value; Error? _error; }
```

- **Success** stores `_isSuccess = true`, the value inline (or the reference, for reference-type
  `T`), and a **null** `_error` — no allocation, no sentinel object needed on this path.
- **Failure** stores `_isSuccess = false`, `default` value, and the `Error` reference.
- **`default(Result<T>)`** is all-zero: `_isSuccess = false`, `_error = null`. Every member that
  observes the error normalizes `_error ?? Error.Uninitialized`, so the uninitialized state is a
  well-defined failure without any construction cost.

Size: roughly `sizeof(bool)` + one reference for `Result`; plus `sizeof(T)` (or a reference) for
`Result<T>`, subject to alignment/padding. For typical `T` this is 2–3 machine words — well within
"cheap to copy" territory.

## Copy semantics

Structs copy by value: every assignment, parameter pass, combinator return, and `Task<Result<T>>`
unwrap copies the fields. Consequences:

- Copies are shallow and cheap; the `Error` field copies as a reference (the error object itself
  is never duplicated — combinator short-circuits pass the *same* instance through, verified by
  `Assert.Same` in the tests).
- Immutability (`readonly struct`) means copies can never diverge — there is no defensive-copy
  hazard and no torn-mutation risk. All result and error types are safe to share across threads.
- Avoid a huge struct `T` inside `Result<T>` on very hot paths (the copy cost is `sizeof(T)`);
  for reference-type and small-value `T` the copy cost is negligible.
- No interface implementations exist that would encourage boxing in normal use
  (`IEquatable<Result<T>>` is implemented, but equality is invoked on the struct directly);
  boxing occurs only if *you* cast a result to `object`.

## No caches, no mutable statics

The library holds no state. The only statics are immutable sentinels and pre-built delegates:

- `Error.None` and `Error.Uninitialized` (core) — two objects for the process lifetime.
- Frozen lookup tables in the AspNetCore package (`ProblemDetailsBuilder.Defaults`, the RFC 9110
  title/type map) — allocated once, read-only.
- `ValidationBehavior<TRequest,TResponse>.FailureFactory` — one cached delegate per closed
  generic pair, built once in the static initializer (this is where the single
  `MakeGenericMethod` reflection cost is paid, never per request).

Nothing grows over time: there are no pools, no memoization, no per-error caches to leak.

## GC behavior of errors and metadata

- **`Error` objects** are small, short-lived class instances on typical failure paths — created,
  projected/logged, and collected in gen 0. Errors promoted to `static readonly` catalogs live
  forever by design and cost nothing after startup.
- **Metadata is allocated only when used.** An `Error` constructed without metadata references a
  shared empty dictionary — `Error.Failure("A", "m")` allocates one object, not two, and
  `Metadata` is still never null.
- **`WithMetadata` copies.** Both overloads return a *new* `Error` with a *new* dictionary
  containing the merged entries; the original error and its dictionary are untouched (pinned by
  `ErrorTests.WithMetadata_returns_new_instance_and_preserves_existing_entries`). Chaining
  `.WithMetadata(...)` N times therefore allocates N errors and N dictionaries — build a single
  dictionary and pass it to the constructor (which also defensively copies, once) when attaching
  many entries on a hot path.
- **Constructor metadata is defensively copied** (`ErrorTests.Metadata_is_defensively_copied`),
  so callers cannot mutate an error after construction; the copy is the price of immutability and
  happens once per error.
- `ValidationError`/`AggregateError` defensively copy their collections into read-only lists —
  again once, at construction. `AggregateError` flattening allocates one list for the flattened
  view.

## Serialization buffer behavior

- **Serialization** streams directly through the caller-provided `Utf8JsonWriter` — the
  converters write properties in order and buffer nothing themselves. Measured garbage for a
  small `Result<T>` (~128 B for the benchmark DTO) is STJ's own output handling plus the returned
  string when using the `JsonSerializer.Serialize` string API; writing to a stream or
  `IBufferWriter` avoids the string.
- **Deserialization of `Result<T>` buffers the `value` property once**: the converter reads
  `value` via `JsonElement.ParseValue` so that payload **property order does not matter**
  (`{"value":…,"isSuccess":…}` parses identically — pinned by
  `ResultOfT_read_is_property_order_independent`). That `JsonElement` holds a copy of the value's
  JSON until the element is deserialized into `T`; it is ordinary gen-0 garbage sized to the
  value payload. Error payloads are read directly without extra buffering.
- **Nested metadata values** deserialize as `JsonElement` and are *retained* on the error for
  faithful re-serialization — a metadata-heavy error keeps its parsed JSON alive as long as the
  error itself.
- No pooled buffers, statics, or caches are involved in serialization; converter instances are
  stateless (the `Result<T>` converter factory creates one converter per closed generic type,
  cached by STJ inside `JsonSerializerOptions` per standard STJ behavior).

## Practical guidance

1. Let success results live on the stack — don't wrap them in `object`/collections of `object`.
2. Use static error catalogs for recurring failures; construct ad-hoc errors only when the
   message/metadata genuinely varies.
3. Batch metadata into one dictionary instead of chained `WithMetadata` calls on hot paths.
4. Reuse `JsonSerializerOptions` instances (standard STJ advice) so converter instances and
   metadata caches are built once.
