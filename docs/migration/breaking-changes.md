# Breaking Changes Register — Koras.Results

The cumulative, authoritative list of every breaking change ever shipped, per major version.
Consumer-facing; each entry carries its migration path. See
[versioning-policy.md](versioning-policy.md) for what counts as breaking and
[upgrading.md](upgrading.md) for per-version upgrade walkthroughs.

## Current state

**There are no breaking changes to list.** No stable version has shipped yet (the project is at
`0.1.0-preview.N`), so no compatibility promise has been given and none has been broken. This
document exists now so the *process* is fixed before it is first needed.

Pre-1.0 surface changes are not recorded here — they are ordinary CHANGELOG entries under the
SemVer 0.x rules. This register starts counting at 1.0.0.

## The process a breaking change must follow

A breaking change is any item in the "What counts as breaking" list of
`docs/api/backward-compatibility.md` — binary/source surface, behavioral contracts, the JSON wire
shape, dependencies, or dropping a TFM. Every one of them must travel this path, in order:

1. **ADR first.** A decision record in `docs/architecture/decision-records/` documenting the
   motivation, alternatives considered (including "don't break"), and the migration story.
   No ADR, no break — this mirrors how every structural decision in this repo is made.
2. **Deprecation runway (where a replacement exists).** The old member ships
   `[Obsolete("Use X. Removed in vN+1.", error: false)]` in a **minor** release of the current
   major, together with CHANGELOG and migration-guide entries. **At least one full minor
   release** must ship with the warning before removal. The last minor of the major may escalate
   to `error: true`. Breaks with no possible in-place replacement (e.g. a wire-shape change)
   still get a documented runway: the new behavior ships opt-in in a minor where feasible.
3. **The break ships only in the next major.** Mechanically visible in the PR as: lines *removed*
   from `PublicAPI.Shipped.txt` (any removal from that file is definitionally major), an updated
   `PackageValidationBaselineVersion` story, and — for wire changes — updated exact-payload
   serialization tests.
4. **Documentation lands in the same PR**: an entry in the table below, a section in
   [upgrading.md](upgrading.md) for the new major, and CHANGELOG placement under the new
   version's `### Removed`/`### Changed`.
5. **Support window honored**: the previous major continues receiving fixes for 12 months after
   the new major ships (`SECURITY.md`), so no consumer is forced through the break on a patch
   timeline.

## Entry format for future breaking changes

Each major version gets one section containing this table:

```markdown
## 2.0.0 (YYYY-MM-DD)

| # | Package | Change | Category | Deprecated since | Migration | ADR |
|---|---------|--------|----------|------------------|-----------|-----|
| 2.0-1 | Koras.Results | `Result.Foo(...)` removed | Binary/source | 1.4.0 (`[Obsolete]`) | Call `Result.Bar(...)`; identical semantics, parameter order changed: `Bar(x, y)` | ADR-0012 |
| 2.0-2 | Koras.Results.AspNetCore | Default status for `Failure` changed 422 → 409 | Behavioral | n/a (documented in 1.6 release notes) | Restore old behavior: `options.MapErrorType(ErrorType.Failure, 422)` | ADR-0013 |
```

Column definitions:

- **#** — stable identifier (`<major>-<n>`) so upgrade notes and issues can reference an exact
  break.
- **Package** — which of the five packages (or "all").
- **Change** — one sentence, naming the exact member/behavior.
- **Category** — one of: Binary/source · Behavioral · Wire format · Dependency · TFM
  (the taxonomy from the backward-compatibility policy).
- **Deprecated since** — the minor that introduced the `[Obsolete]` warning, or `n/a` with the
  announcement vehicle for non-member breaks.
- **Migration** — the mechanical fix, inline where it fits, else a link into
  [upgrading.md](upgrading.md). "Recompile" is a valid migration for source-only breaks; every
  entry must have one.
- **ADR** — the decision record that authorized the break.

Ordering: newest major first; within a major, entries in table order by identifier.

## What does *not* belong here

- Additive changes (new members, new `ErrorType` values, new TFMs, new packages) — minors, by
  policy; the `ErrorType` switch-hazard is documented in
  [versioning-policy.md](versioning-policy.md).
- Deprecation *warnings* — those are announced in [upgrading.md](upgrading.md) sections and the
  CHANGELOG when introduced; they appear here only later, as the "Deprecated since" column of the
  removal that consummates them.
- Pre-1.0 preview churn.
