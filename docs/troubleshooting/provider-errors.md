# Integration Package Troubleshooting — Koras.Results

Issues specific to the satellite packages: MediatR, FluentValidation, and OpenTelemetry. Core and
AspNetCore issues live in [common-errors.md](common-errors.md).

## MediatR (Koras.Results.MediatR)

### InvalidOperationException: "…response type is not Result or Result\<T>…"

```
System.InvalidOperationException: Request 'LegacyCreate' failed validation, but its response type
'String' is not Result or Result<T>, so the failure cannot be returned as a value. Change the
handler to return a Result, or validate before sending.
```

**This is by design**, not a bug (documented in the API contract and pinned by
`ValidationBehaviorTests.Non_result_responses_throw_on_validation_failure_instead_of_swallowing`).
`ValidationBehavior<,>` can only short-circuit with a *failed result* when the response type can
carry one. For any other response type it has two options on validation failure: silently swallow
the failure (unacceptable) or fail fast — it fails fast, with guidance in the message.

Behavior summary for non-Result responses: **valid** requests pass through to the handler
normally; **invalid** requests throw. **Fix**: change the handler to return
`Result`/`Result<TValue>` (`IRequest<Result<UserDto>>` instead of `IRequest<UserDto>`). If a
request type genuinely cannot adopt Result, validate it before `Send` (e.g. call
`validator.ValidateToResult(request)` yourself) so the behavior finds no failures.

Related: requests with **no registered validators** always pass straight through — the behavior
only engages when validators exist for `TRequest`.

### MediatR restore conflict (NU1107)

Covered in [common-errors.md](common-errors.md#nu1107--version-conflict-on-mediatr-13) — the
`[12.4, 13.0)` bound is a deliberate licensing guard (ADR-0006).

### Behavior not running at all

`AddKorasResultsValidationBehavior()` must be called on the same `IServiceCollection` as
`AddMediatR`, and validators must be registered (e.g.
`AddValidatorsFromAssemblyContaining<T>()`). The integration tests show the canonical
registration triple — `AddMediatR` + `AddValidatorsFromAssembly…` + `AddKorasResultsValidationBehavior`.

## FluentValidation (Koras.Results.FluentValidation / the MediatR behavior)

### Duplicate validation failures when sharing a ValidationContext manually

Symptom: the same field error appears twice (or N times) in a `ValidationError` when you run
several validators yourself by passing **one shared `ValidationContext<T>`** to each —
FluentValidation accumulates failures on the shared context, so validator #2's result includes
validator #1's failures, and collecting both results duplicates them.

**The shipped behavior already avoids this**: `ValidationBehavior<,>` creates **a fresh
`ValidationContext<TRequest>` per validator** (see the comment in
`src/Koras.Results.MediatR/ValidationBehavior.cs`) and aggregates the independent results —
`ValidationBehaviorTests.Multiple_validators_aggregate_their_failures` pins exactly 2 failures
from 2 single-rule validators, not 3.

If you orchestrate validators manually, do the same:

```csharp
// WRONG — shared context accumulates:
var ctx = new ValidationContext<Signup>(model);
var all = validators.SelectMany(v => v.Validate(ctx).Errors);   // duplicates!

// RIGHT — fresh context per validator:
var all = validators.SelectMany(v => v.Validate(new ValidationContext<Signup>(model)).Errors);
```

Or skip manual orchestration: `validator.ValidateToResult(instance)` /
`ValidateToResultAsync(instance, ct)` handle a single validator, and `Result.Combine` merges
multiple `ValidationError`s into one (field order preserved) if you need multi-validator
aggregation outside MediatR.

### Empty PropertyName in field errors

Model-level failures (rules on the whole object) arrive with `PropertyName == ""`. This is
intentional pass-through, not data loss — the HTTP projection groups them under the empty key,
matching ASP.NET Core's own model-level validation shape.

## OpenTelemetry (Koras.Results.OpenTelemetry)

### Tags missing from spans

`TagCurrentActivity()` / `TagActivity()` / `TapActivityErrorAsync()` are deliberately silent
no-ops in several situations. Check in order:

1. **No listener** — `Activity.Current` is `null` because nothing is listening.
   `ActivitySource.StartActivity` returns null when no `ActivityListener`/OTel SDK subscribes to
   the source; there is then no activity to tag. Fix: configure your tracing SDK to listen to the
   relevant `ActivitySource` (`AddSource(...)` in OpenTelemetry .NET), or verify you are inside a
   started activity at all.
2. **Non-recording sampler** — the activity exists but was sampled out
   (`IsAllDataRequested == false`, e.g. `ActivitySamplingResult.PropagationData`). The package
   intentionally skips tagging non-recording activities (no work for data nobody records) —
   pinned by `OpenTelemetryTests.Non_recording_activities_are_not_tagged`. Fix: check your
   sampler; with parent-based/ratio samplers only sampled traces carry tags, which is correct
   behavior.
3. **Activity is null at the call site** — `TagActivity(null)` is a safe no-op by contract.
4. **The result was a success** — successes never modify the activity (no tag spam; pinned by
   `Success_leaves_the_activity_untouched`). Only failures set status `Error`, `error.type`,
   `koras.error.code`, and (for aggregates) `koras.error.aggregate_count`.
5. **Tagging never called** — the package only annotates; it never creates activities and is
   never invoked implicitly. Ensure the pipeline actually flows through a
   `TagCurrentActivity()`/`TapActivityErrorAsync()` call.

Quick self-test: temporarily add an `ActivityListener` with
`Sample = AllDataAndRecorded` for your source (exactly what `OpenTelemetryTests` does) and assert
`activity.GetTagItem("koras.error.code")` after tagging a known failure.

### Expecting metrics or logs from this package

There are none by design — no meters ship in the MVP and the package never logs
(`docs/architecture/observability.md`). Derive metrics from traces or add counters in your own
`TapError`.
