# Versioning Policy for Consumers — Koras.Results

The consumer-facing view of the compatibility promise. The authoritative contract is
[`docs/api/backward-compatibility.md`](../api/backward-compatibility.md); the maintainer-facing
release rules are in [`docs/release/versioning.md`](../release/versioning.md). This page tells
you, as a package consumer, what you can rely on and how to upgrade safely.

## The promise

Within a major version: **no binary or source breaking changes, no behavioral contract changes,
no wire-format changes.** Updating from `1.0.0` to any `1.x.y` requires no recompiling concerns
and no behavior audits. Concretely, all of the following are frozen for the life of a major:

- every public signature (including parameter *names* — named arguments won't break);
- `ErrorType` numeric values;
- default status-code mappings, default option values (`IncludeUnexpectedErrorDetails = false`,
  `MetadataExposure = None`, `IncludeTraceId = true`), aggregation rules, equality semantics,
  and guard-exception types;
- the JSON wire shape (property names, casing, structural discrimination);
- dependency floors/bounds (notably MediatR's `[12.4, 13.0)` — see ADR-0006) and the target
  framework set (a TFM is only ever *dropped* in a major).

Enforcement is mechanical, not aspirational: PublicApiAnalyzers surface files, package validation
against the released baseline (post-1.0), exact-payload serialization tests, and an API review
checklist (details in the backward-compatibility document).

**Pre-1.0 caveat**: while versions are `0.x`, SemVer permits breaks in minors. We follow the
spirit of the promise and document every break in the CHANGELOG, but the formal guarantee starts
at 1.0.0.

## What a minor version may add

Minors are additive. Expect any of:

- new types, members, extension methods, overloads, and even new packages;
- new optional behavior behind unchanged defaults;
- a newly released .NET target framework;
- `[Obsolete]` warnings announcing future removals (never removals themselves);
- **new `ErrorType` members.**

### The `ErrorType` hazard: always write a `default` arm

Because new enum members may arrive in a minor, an exhaustive `switch` over `ErrorType` in your
code can silently stop being exhaustive. Always include a `default`:

```csharp
var retryable = error.Type switch
{
    ErrorType.Unavailable => true,
    ErrorType.Conflict => MaybeRetry(error),
    ErrorType.Failure or ErrorType.Validation or ErrorType.NotFound
        or ErrorType.Unauthorized or ErrorType.Forbidden or ErrorType.Unexpected => false,
    _ => false, // future ErrorType members land here — choose a safe default
};
```

Pick the *conservative* behavior for the `default` arm (treat unknown types like `Unexpected`:
don't retry, don't expose details). The HTTP projection in `Koras.Results.AspNetCore` already
handles any future member totally, so only your own switches need this care.

The same reasoning applies to the JSON contract: **unknown properties in library payloads are
ignored on read** (tested), so a newer producer can talk to an older consumer within a major.

## Safe upgrade practice

1. **Patch versions (`1.2.3` → `1.2.4`)**: take them freely; bug fixes only. Automate via
   Dependabot/Renovate.
2. **Minor versions (`1.2` → `1.3`)**:
   - read the CHANGELOG section for the version (new features, new deprecations);
   - rebuild with warnings visible — new `[Obsolete]` warnings are your early migration signal;
     address them at leisure, but before the next major;
   - if you switch over `ErrorType` anywhere, confirm the `default` arm exists (once — see
     above).
3. **Major versions (`1.x` → `2.0`)**: read `docs/migration/breaking-changes.md` and the
   per-version notes in `docs/migration/upgrading.md`; every break there includes its migration
   path. Budget real time only for items you actually use — the deprecation policy guarantees
   each removal spent at least one minor as a compiler warning first.
4. **Keep the package family on one version.** All five packages version together; mixing
   `Koras.Results 1.3.0` with `Koras.Results.AspNetCore 1.1.0` is unsupported (NuGet will usually
   resolve it, but the tested combination is same-version).
5. **Prereleases** (`-preview.N`) are for evaluation, not production: they may change without
   notice between builds. See `docs/migration/upgrading.md` for how they roll.

## Support window

The latest major is fully supported; the previous major receives bug and security fixes for
**12 months** after the next major ships (`SECURITY.md`). Plan major upgrades within that window.
