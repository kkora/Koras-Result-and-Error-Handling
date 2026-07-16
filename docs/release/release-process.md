# Release Process — Koras.Results

Releases are **tag-driven**: pushing a `vX.Y.Z` tag runs `.github/workflows/release.yml`, which
builds, tests, packs, validates, publishes to nuget.org, and creates the GitHub release. No
release step runs on a developer machine.

## Prerequisites (once per repository)

- The `nuget-release` GitHub environment exists, is protected, and holds the `NUGET_API_KEY`
  secret — see [nuget-publishing.md](nuget-publishing.md).
- You have completed the release-level Definition of Done
  (`docs/planning/definition-of-done.md`) and the checklists in
  [release-checklist.md](release-checklist.md) and `docs/security/security-checklist.md`.

## Step by step

### 1. Finalize the CHANGELOG

In `CHANGELOG.md` (Keep a Changelog format), move the contents of `[Unreleased]` under a new
version heading with the release date, leave a fresh empty `[Unreleased]` section above it, and
update the comparison links at the bottom:

```markdown
## [Unreleased]

## [0.2.0] - 2026-08-01
### Added
- ...

[Unreleased]: https://github.com/korastechnologies/koras-results/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/korastechnologies/koras-results/releases/tag/v0.2.0
```

### 2. Bump the version

Edit `<VersionPrefix>` in the root `Directory.Build.props` to the new base version (e.g.
`0.2.0`). This is the single version source for all five packages. Choose the number per
[versioning.md](versioning.md).

### 3. Merge and tag

Land the CHANGELOG + version bump PR on `main` (all CI workflows — build, test, package, CodeQL —
must be green), then tag **that exact commit**:

```bash
git tag v0.2.0            # or v0.2.0-preview.1 for a prerelease
git push origin v0.2.0
```

Tag format is `v*.*.*` (the workflow trigger pattern); prerelease tags carry the SemVer suffix
after a hyphen.

### 4. The release.yml pipeline (what runs on the tag)

In order, in the protected `nuget-release` environment (which may require manual approval before
the job starts):

1. **Checkout** with `fetch-depth: 0` (full history for deterministic version metadata) and
   .NET SDK setup (8.0.x / 9.0.x / 10.0.x per `global.json`).
2. **Derive version from tag**: strips the `v` prefix and splits on the first `-` —
   `v0.2.0-preview.1` yields `VersionPrefix=0.2.0`, `VersionSuffix=preview.1`; a stable tag
   yields an empty suffix. The tag, not the props file, is authoritative for the built version
   (they must match — the checklist verifies it).
3. **Restore → Build** (`Release`, warnings as errors, all TFMs) with the derived version
   properties. `CI=true` enables `ContinuousIntegrationBuild` (deterministic) and Source Link.
4. **Test**: the full suite (`dotnet test -c Release --no-build`) on net8.0/net9.0/net10.0 —
   a release cannot ship from a commit whose tests fail *in this pipeline*, even if they passed
   earlier.
5. **Pack** all five packages (plus `.snupkg` symbol packages) into `artifacts/` with the same
   version properties.
6. **Validate**: `bash build/validate-packages.sh artifacts` — per-TFM assemblies and XML docs,
   README/icon/nuspec metadata, symbols present, core has zero dependencies. A validation failure
   stops the release before anything is published.
7. **Push to NuGet.org**: `dotnet nuget push "artifacts/*.nupkg" --api-key $NUGET_API_KEY
   --source https://api.nuget.org/v3/index.json --skip-duplicate`. `--skip-duplicate` makes the
   step idempotent: rerunning the workflow (e.g. after a transient failure later in the job) does
   not fail on already-published packages. Symbol packages upload automatically alongside their
   `.nupkg`.
8. **GitHub release**: `softprops/action-gh-release` creates the release for the tag with
   **generated release notes** (from merged PRs since the previous tag) and attaches all
   `.nupkg`/`.snupkg` files as release artifacts.

### 5. Post-release verification

- Wait for nuget.org indexing (typically minutes), then run the smoke test from the checklist:
  install the exact new version from **nuget.org** (not a local feed) into a fresh project and
  run a small program — the same shape as `build/consumption-test.sh`.
- Verify on nuget.org: all five packages at the new version, README rendering, license, icon,
  "Source repository" link, and symbol availability (step into Koras code from a consumer with
  Source Link enabled).
- Check the GitHub release notes for accuracy; edit the generated notes if a highlight deserves
  better wording.
- Complete the remaining items in [release-checklist.md](release-checklist.md) (announce, etc.).

## First stable release (1.0.0) — additional steps

Per ADR-0008, the 1.0.0 release additionally:

- moves the contents of every `PublicAPI.Unshipped.txt` into `PublicAPI.Shipped.txt`;
- uncomments `<PackageValidationBaselineVersion>1.0.0</PackageValidationBaselineVersion>` in
  `src/Directory.Build.props` **in the first post-1.0 PR**, so every subsequent pack verifies
  binary compatibility against the released 1.0.0.

## If something goes wrong

- **Pipeline failed before the push step**: fix, delete and re-push the tag (nothing was
  published).
- **Pipeline failed after a partial push**: rerun the workflow — `--skip-duplicate` skips the
  packages that made it; the rest publish.
- **A bad version reached nuget.org**: packages cannot be deleted, only **unlisted**. Unlist the
  affected version(s) on nuget.org, fix, and release a new patch version. Never reuse a version
  number.
