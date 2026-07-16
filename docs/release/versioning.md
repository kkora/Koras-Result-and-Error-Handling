# Versioning Policy — Koras.Results

All packages follow **SemVer 2.0** and version together (one `VersionPrefix` in
`Directory.Build.props` covers the whole family). The compatibility definitions below mirror
`docs/api/backward-compatibility.md`, which is the authoritative contract; this page is the
release-facing summary.

## Patch / minor / major

### Patch (x.y.Z)

Bug fixes only. No public-surface change (the `PublicAPI.*.txt` files do not change), no
behavioral-contract change, no wire-format change, no dependency-range change beyond a patch
bump of an existing dependency. Consumers update without reading release notes.

### Minor (x.Y.0)

Additive and backward-compatible:

- New types, new members on classes, new extension methods, new overloads (checked for
  overload-resolution breaks), new packages.
- New optional capability behind existing defaults.
- **New `ErrorType` members** are allowed in minors, with a documented hazard: consumers writing
  exhaustive `switch` statements over `ErrorType` must include a `default` arm (stated in the
  migration docs — see `docs/migration/versioning-policy.md`).
- New optional parameters only via new overloads — never by editing an existing signature (named
  arguments and binary compatibility both break otherwise).
- **Adding a target framework** is minor.
- Deprecations (`[Obsolete]` warnings) are introduced in minors — see below.

### Major (X.0.0)

Anything that breaks binary, source, behavioral, wire, or dependency compatibility:

- Removing/renaming public members; signature, return-type, parameter-name, constraint, or
  accessibility changes; struct↔class changes.
- Changing `ErrorType` numeric values or removing members.
- Changing default status-code mappings, default option values, aggregation rules, equality
  semantics, or guard-exception types.
- Changing the JSON wire shape (property names, casing, discrimination rules) — the exact-payload
  serialization tests make this loud.
- **Dropping a target framework.** TFM changes in the removal direction happen *only* in majors
  (ADR-0002; TFMs are part of the compatibility contract).
- Raising a dependency minimum beyond a patch, changing the MediatR upper bound, or adding any
  new dependency to an existing package.

## Prerelease scheme

- The current pre-1.0 line is **`0.1.0-preview.N`**. `VersionPrefix` (`0.1.0`) lives in
  `Directory.Build.props`; CI appends the suffix.
- Per `.github/workflows/package.yml`:
  - **main builds** pack as `0.1.0-preview.<run_number>` — a monotonically increasing preview
    stream;
  - **PR builds** pack as `0.1.0-pr.<pr-number>.<run_number>` — traceable to the PR, clearly
    non-releasable, and ordered below `preview.*` builds of the same base version by SemVer
    prerelease rules.
- **Local builds** (outside CI) get the suffix `dev` (`Directory.Build.props`), so a developer
  machine can never accidentally produce something that looks like a CI artifact.
- **Release builds** derive both prefix and suffix from the git tag in
  `.github/workflows/release.yml` (`v0.1.0-preview.7` → prefix `0.1.0`, suffix `preview.7`;
  `v1.0.0` → no suffix). Stable versions come only from tags without a prerelease part.

Pre-1.0 caveat (SemVer §4): while the major version is 0, minors may contain breaking changes.
We still follow the spirit of the rules above and document every break in the CHANGELOG, but the
formal promise starts at 1.0.0, when the API surface moves to `PublicAPI.Shipped.txt` and
`PackageValidationBaselineVersion` activates (ADR-0008).

## Deprecation policy

Per `docs/api/backward-compatibility.md`:

1. Mark the member `[Obsolete("Use X. Removed in vN+1.", error: false)]` in a **minor** release;
   document in the CHANGELOG and migration guide.
2. **At least one full minor release** must ship with the warning before any removal — consumers
   always get a compiling upgrade step that surfaces the warning.
3. Removal happens only in the **next major**. Optionally, the last minor of the current major
   may escalate to `error: true` to force attention before the break lands.

## LTS / support statement

- The **latest major** always receives fixes.
- The **previous major** receives bug and security fixes for **12 months** after the next major
  ships (mirrored in `SECURITY.md`'s supported-versions table).
- Older majors are unsupported.

Target frameworks follow the same rhythm: each major's TFM set is fixed at release (currently
net8.0/net9.0/net10.0); newly released .NET versions may be added in minors, and end-of-life
TFMs are only dropped at the next major.

## Related

- `docs/api/backward-compatibility.md` — the full compatibility contract and enforcement stack
- [release-process.md](release-process.md) — how a version actually ships
- `docs/migration/versioning-policy.md` — the consumer-facing view of this policy
