# Backward Compatibility Policy — Koras.Results

## The promise

Within a major version: **no binary or source breaking changes, no behavioral contract changes, no wire-format changes.** A consumer can update from `1.0.0` to any `1.x.y` without recompiling concerns or behavior audits.

## What counts as breaking (all require a major version)

**Binary/source:**
- Removing/renaming any public type or member; changing signatures, return types, parameter names (named-argument breakage counts), generic constraints, or member accessibility.
- Changing a struct to a class or vice versa; adding interface implementations to structs is allowed only after verifying no ambiguity breakage; removing them never.
- Changing `ErrorType` numeric values or removing members (adding members is minor — see below).
- New *required* parameters. (Adding overloads is minor but must be checked for overload-resolution breaks.)

**Behavioral:**
- Changing default status-code mappings, default option values, aggregation rules, equality semantics, or guard-exception types.
- Changing the JSON wire shape (property names, casing, discrimination rules).

**Dependency:**
- Raising the minimum version of FluentValidation/MediatR beyond a patch bump; changing the MediatR upper bound; adding any new dependency to an existing package.

## What is allowed in minors

New types, new members on classes, new extension methods, new overloads (resolution-checked), new `ErrorType` members (documented hazard: exhaustive `switch` users should include `default` — stated in docs), new optional parameters ONLY via new overloads (never by editing an existing signature), new packages.

## Enforcement mechanisms

1. **PublicApiAnalyzers** (`PublicAPI.Shipped.txt`/`Unshipped.txt`) — every surface change is an explicit reviewed diff (ADR-0008).
2. **Package validation** — `EnablePackageValidation` + `PackageValidationBaselineVersion` (activated at 1.0) verifies binary compat against the last released package on every pack.
3. **Serialization snapshot tests** — the wire shape is pinned by tests; changing them is a loud act.
4. **PR checklist** — `docs/api/public-api-review-checklist.md` gates every API-touching PR.

## Deprecation process

1. Mark `[Obsolete("Use X. Removed in vN+1.", error: false)]` in a minor release; document in CHANGELOG + migration guide.
2. At least one full minor release must ship with the warning before removal.
3. Removal only in the next major; `error: true` may precede removal in the last minor of the current major.

## TFM policy

Target frameworks are part of the compatibility contract: dropping a TFM is major; adding one is minor.
