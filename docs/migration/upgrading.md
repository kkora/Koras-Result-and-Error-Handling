# Upgrading — Koras.Results

Per-version upgrade notes for the package family. Read alongside
[versioning-policy.md](versioning-policy.md) (what version numbers promise) and
[breaking-changes.md](breaking-changes.md) (the cumulative break register).

## Current state: pre-1.0, nothing to migrate

Koras.Results has **not yet shipped a stable release**. The current line is `0.1.0-preview.N`,
and there are no previous stable versions to upgrade *from* — so this document is, today,
deliberately almost empty. There are no renamed APIs, no changed defaults, and no migration
steps.

What pre-1.0 consumers should know:

- **Previews may change without notice.** While the API is locked by tooling *within* the repo
  (PublicApiAnalyzers), the project reserves the SemVer 0.x right to change surface between
  preview builds. Every change lands in `CHANGELOG.md` under `[Unreleased]`.
- **Upgrade all five packages together** to the same version — the family versions as one.
- At **1.0.0** the API freezes formally: the surface moves to `PublicAPI.Shipped.txt`, package
  validation runs against the released baseline, and the backward-compatibility promise takes
  effect (`docs/api/backward-compatibility.md`).

## How prereleases roll

Understanding the suffixes (produced by `.github/workflows/package.yml` and the release
pipeline):

| Version shape | Source | Meaning |
|---|---|---|
| `0.1.0-dev` | local build | Developer machine; never distributed |
| `0.1.0-pr.<pr>.<run>` | PR build | CI artifact for a specific pull request; for review only |
| `0.1.0-preview.<run>` | main build | CI artifact from `main`; the freshest integrated state |
| `0.1.0-preview.N` (tagged `v0.1.0-preview.N`) | release pipeline | A *published* preview on nuget.org |
| `0.1.0` (tagged `v0.1.0`) | release pipeline | A stable release |

SemVer orders these correctly: `dev` < `pr.*` < `preview.*` < stable for the same base version,
so `dotnet add package Koras.Results --prerelease` always resolves to the newest published
preview, and dropping `--prerelease` after 1.0 moves you to stable automatically.

To try a preview: `dotnet add package Koras.Results --version 0.1.0-preview.N` (pin exactly —
floating on previews invites surprise rebuild diffs).

---

## Template for future per-version notes

Each release from 1.0.0 onward appends a section here, newest first, in this structure:

```markdown
## Upgrading to X.Y.Z (from X.(Y-1).* or earlier)

**Risk level**: none / low (warnings only) / high (major — breaking changes)

### Before you upgrade
- Prerequisites (minimum TFM, companion package versions, tooling).

### Breaking changes (majors only)
- Link each entry in breaking-changes.md that applies, with the local fix inline:
  | Removed/changed | Replacement | Mechanical fix |
  |---|---|---|

### New deprecations
- Members now marked [Obsolete], their replacements, and the major in which removal lands.

### Behavioral notes
- Anything observable that changed within the compatibility rules (new ErrorType members —
  check your `switch` default arms; new optional features worth adopting).

### Recommended adoption
- New APIs worth moving to even though the old ones still work.

### Verify
- Build with warnings-as-errors once; run your serialization round-trip tests if you persist
  result JSON; smoke-test one failing endpoint for the ProblemDetails shape.
```

Rules for maintainers writing these sections (enforced via the release checklist):

1. A section is added for **every** stable release, even when it says "no action required" — the
   absence of notes must be a statement, not an omission.
2. Every `[Obsolete]` introduced must appear under "New deprecations" in the same release's
   section, with its replacement.
3. Major-version sections must be *complete*: a consumer following the section top-to-bottom,
   without reading source diffs, ends with a compiling, behaviorally equivalent application.
4. Wire-format implications (there should be none within a major) get their own callout whenever
   serialization code was touched at all.
