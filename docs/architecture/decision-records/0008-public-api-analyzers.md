# ADR-0008: Public API surface locked with Microsoft.CodeAnalysis.PublicApiAnalyzers

**Status:** Accepted · **Date:** 2026-07-16

## Context

Accidental breaking changes are the fastest way to lose enterprise trust. Options: PublicApiAnalyzers (source-level tracking), Microsoft.DotNet.ApiCompat (binary-level, `EnablePackageValidation`), or both.

## Decision

**Both, in layers:**
1. `Microsoft.CodeAnalysis.PublicApiAnalyzers` in every shipped project: `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` per TFM-neutral surface. Any public-surface change fails the build until the developer *explicitly* edits the text file — making API changes visible in diffs and reviewable in PRs.
2. `EnablePackageValidation` on pack, with `PackageValidationBaselineVersion` set after 1.0 ships — binary-level backward-compat verification against the last released package.

Pre-1.0, everything lives in `PublicAPI.Unshipped.txt`; the 1.0 release moves the surface to `Shipped` and becomes the frozen baseline.

## Consequences

- Every PR that touches public API shows a text-file diff — deliberate friction.
- CI needs no extra tooling for (1); (2) activates post-1.0 with the baseline version property.
- Contributors need a one-paragraph guide (in CONTRIBUTING.md) on updating the files (`dotnet format analyzers` or IDE code-fix).
