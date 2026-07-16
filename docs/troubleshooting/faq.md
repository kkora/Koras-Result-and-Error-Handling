# FAQ — Koras.Results

Real questions with honest answers. Design rationale links point at the ADRs in
`docs/architecture/decision-records/`.

### 1. Why is `default(Result)` a failure?

Because `Result` is a struct, and structs can exist without ever running your code
(`default`, array slots, unassigned fields). If `default` were a success, an uninitialized result
would masquerade as a valid outcome — and for `Result<T>` it would carry a null `Value` while
claiming `IsSuccess`. Instead, `default` is a failure carrying `Error.Uninitialized`
(code `"Result.Uninitialized"`, type `Unexpected`), so the bug surfaces as a classified error at
the first place you inspect it (ADR-0003).

### 2. Why can't a success carry null?

`Result<T>` exists to make "you have a value" a guarantee: `IsSuccess == true` means `Value` is
non-null, full stop (annotated for the compiler, enforced at runtime with
`ArgumentNullException`). A nullable success would reintroduce exactly the ambiguity the pattern
removes. "Nothing found" is a `NotFound` failure; "optional by design" belongs in the domain type
you wrap, not in the result envelope.

### 3. Why is there no `Result<T, TError>`?

A second generic parameter fragments the ecosystem: every layer must agree on `TError` or write
adapters, combinators double their generic arity, and error handling becomes structurally
different per team. Koras instead fixes one rich error model (`Error` with code, message, closed
`ErrorType`, metadata, plus `ValidationError`/`AggregateError`) that is expressive enough for the
observed cases and uniform enough for one HTTP mapping, one serialization contract, and one
dashboard vocabulary (ADR-0004, ADR-0005). Per-domain typing lives in error *codes* and static
error catalogs.

### 4. Why is `ErrorType` a closed enum I can't extend?

Because the enum's whole value is that its meaning is shared. A closed set of eight semantic
categories is what makes uniform status mapping, retry heuristics (`Unavailable` ⇒ retryable),
and cross-team contracts possible; open sets degenerate into stringly-typed chaos. Extensibility
exists on two other axes: unbounded `Code` values and `Metadata` (ADR-0004). If you disagree with
a default HTTP projection, override it (`MapErrorType`/`MapErrorCode`) rather than inventing a
category.

### 5. Why no netstandard2.0 / .NET Framework support?

netstandard2.0 would cost the zero-dependency promise (System.Text.Json becomes a package
dependency), nullable-annotation fidelity (`[MemberNotNullWhen]` etc.), and modern language
features — to serve a legacy audience the ASP.NET Core satellite couldn't serve anyway
(FrameworkReference requires .NET Core). Targets are net8.0/net9.0/net10.0 (ADR-0002). If demand
materializes, compatibility would ship as a separate package rather than degrading the core.

### 6. Why is MediatR capped below 13?

MediatR 13+ is commercially licensed; 12.x is Apache-2.0. An MIT package silently pulling a
commercial transitive dependency would be a trust violation, so the dependency range is
`[12.4, 13.0)` and NuGet fails loudly (NU1107) if you force MediatR 13 alongside it — the desired
failure mode (ADR-0006). Workarounds in
[common-errors.md](common-errors.md#nu1107--version-conflict-on-mediatr-13).

### 7. Can I serialize results with Newtonsoft.Json?

Not supported, and there is no Newtonsoft satellite planned (ADR-0007): it would add a dependency
axis and double the serialization test matrix for a shrinking audience. Options: serialize with
System.Text.Json (works with zero configuration — converters are attribute-wired), or map to your
own DTO and serialize that with Newtonsoft. At public API boundaries, prefer ProblemDetails over
serialized results regardless of serializer.

### 8. Does it work with AOT and trimming?

Honest answer: **the core logic is reflection-free, but full Native AOT compatibility is not yet
claimed or tested.** Specifics: results, errors, combinators, and `Try`/`Combine` use no
reflection. The `Result<T>` STJ converter is a `JsonConverterFactory` that calls
`MakeGenericType` + `Activator.CreateInstance` to build the closed converter — under trimming
this generally survives (the generic instantiation is rooted by your use of `Result<T>`), but it
is the kind of pattern AOT analysis warns about, and serializing `Result<T>` still relies on
reflection-based STJ for `T` unless you use source-generated contexts. The packages do not set
`IsAotCompatible`/`IsTrimmable`, so no guarantee is published. The MediatR behavior also uses a
one-time `MakeGenericMethod` per closed request type. If you run trimmed/AOT: test your specific
payload types, and prefer non-serialized use of results (which is plain code) where possible.

### 9. How do I migrate from FluentResults?

Mechanical mapping: `Result.Ok()` → `Result.Success()`; `Result.Ok(v)` → `Result.Success(v)`;
`Result.Fail("msg")` → `Result.Failure(Error.Failure("Some.Code", "msg"))` — you must now choose
a stable code and a semantic `ErrorType`, which is the point; `result.Errors` (list) has no
direct equivalent — multiple errors aggregate via `Result.Combine` into
`ValidationError`/`AggregateError`; `IError` custom classes become error codes + metadata
(subclassing is closed, ADR-0005). The composition operators (`Bind`, `Map`) translate almost
1:1. Biggest semantic difference: Koras failures carry exactly **one** `Error` (possibly an
aggregate), not an open error list.

### 10. How do I migrate from ErrorOr?

Closest relative (also struct-based). `ErrorOr<T>` → `Result<T>`; `Error.NotFound(code, desc)`
maps nearly 1:1 to `Error.NotFound(code, message)`; `ErrorType` values largely correspond
(ErrorOr's `Unexpected`/`Validation`/`Conflict`/`NotFound`/`Unauthorized`/`Forbidden` all exist
here; Koras adds `Unavailable` and uses `Failure` for domain rules). Differences: Koras results
carry one error, not a `List<Error>` (aggregate via `Combine`); `default(ErrorOr<T>)` is invalid
whereas `default(Result)` is a *well-defined* failure; Koras adds the serialization contract and
the HTTP/validation/telemetry satellites, so hand-rolled `Problem()` mapping code usually
disappears into `ToHttpResult()`.

### 11. What is the performance vs exceptions, really?

Measured (BenchmarkDotNet, .NET 10): signalling an expected failure as a result costs ~2.4 ns and
0 B; throwing/catching the equivalent exception costs ~2,506 ns and 320 B — roughly **1,000×** —
because exceptions capture stack traces and unwind. That is not "exceptions are bad": it is why
*expected* failures should be values and exceptions kept for genuine bugs. Success paths are
allocation-free. Full tables, environment, and caveats:
`docs/performance/benchmarks.md`.

### 12. Should my public API return serialized `Result<T>` JSON?

No — return ProblemDetails (RFC 9457) via the AspNetCore package. The serialized result shape is
a contract for *internal* boundaries (queues, caches, service-to-service); public clients get the
standard problem format with `errorCode`/`traceId` extensions (ADR-0007 consequence, stated in
the docs deliberately).

### 13. Why does accessing `Value` throw instead of returning default?

Returning `default` would convert a handled failure into a latent `NullReferenceException` (or a
silent zero) far from the cause. Throwing `InvalidOperationException` *with the error code in the
message* fails at the misuse site with diagnosable context. When you want non-throwing access,
that is what `TryGetValue`, `GetValueOrDefault(fallback)`, and `Match` are for.

### 14. Why doesn't `Map`/`Bind` catch exceptions my delegate throws?

Because that would silently convert bugs into domain failures. The combinators are pure plumbing:
delegate exceptions propagate (pinned by test). Exception→result conversion is explicit and
boundary-shaped — `Result.Try`/`TryAsync`, ideally with a custom mapper that assigns a real code
and type — never an ambient behavior of the pipeline. (Note `OperationCanceledException` is
special everywhere: cancellation is not failure and always propagates.)
