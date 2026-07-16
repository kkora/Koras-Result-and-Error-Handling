# Release Checklist — Koras.Results

Printable, per-release checklist. Complete top to bottom; every unchecked box blocks the release.
Companion documents: [release-process.md](release-process.md) (the how),
`docs/planning/definition-of-done.md` (the DoD this list embeds),
`docs/security/security-checklist.md` Part A (run it in parallel).

Release version: `________`  Tag: `v________`  Date: `________`  Driver: `________`

## 1. Code readiness (Definition of Done)

- [ ] Public API matches `docs/api/public-api-design.md` (or the doc was updated with the change,
      through the API review checklist)
- [ ] `dotnet build -c Release` green on all TFMs — TreatWarningsAsErrors
- [ ] No suppressed warnings without inline justification; no `!` without justification
- [ ] XML documentation on every public member
- [ ] `PublicAPI.Unshipped.txt` reflects every surface change since the last release
- [ ] No placeholder code (`NotImplementedException`, `TODO`, fake success values)
- [ ] `dotnet format --verify-no-changes` clean; analyzers silent
- [ ] All tests green on all TFMs: `dotnet test -c Release` (unit, integration, architecture)
- [ ] Coverage at or above target: core ≥ 90 % branch, satellites ≥ 80 %
- [ ] Async APIs shipped this release have cancellation and fault-propagation tests
- [ ] Benchmarks rerun if core primitives changed; results recorded in the PR and
      `docs/performance/benchmarks.md` updated if numbers moved

## 2. Documentation

- [ ] Feature guides / concepts pages updated for user-visible changes
- [ ] Samples updated if user-visible behavior changed
- [ ] Test matrix (`docs/testing/test-matrix.md`) rows added for new features
- [ ] Migration docs updated: `docs/migration/upgrading.md` entry for this version;
      `docs/migration/breaking-changes.md` if (major only) anything broke

## 3. CHANGELOG & version

- [ ] `CHANGELOG.md`: `[Unreleased]` moved under `## [X.Y.Z] - YYYY-MM-DD`; fresh empty
      `[Unreleased]` section restored; comparison links updated
- [ ] `<VersionPrefix>` in `Directory.Build.props` bumped to `X.Y.Z`
- [ ] Version number choice justified against [versioning.md](versioning.md)
      (patch / minor / major; pre-1.0 caveat noted in notes if applicable)
- [ ] Deprecations added this release carry `[Obsolete]` with replacement guidance and are listed
      in the CHANGELOG

## 4. Security & dependencies

- [ ] `dotnet list package --vulnerable --include-transitive` reports nothing
- [ ] `docs/security/security-checklist.md` Part A completed and attached to the release PR
- [ ] Dependency changes since last release each have an ADR or match existing policy

## 5. Pre-tag gate

- [ ] Release PR (CHANGELOG + version bump) merged to `main`
- [ ] All CI workflows green on the merge commit (build, test, package, CodeQL)
- [ ] `package.yml` artifacts from that commit passed `validate-packages.sh` **and**
      `consumption-test.sh`
- [ ] nuget.org Trusted Publishing policy active (not expired-pending) for this repository,
      `release.yml`, and the `nuget-release` environment

## 6. Tag & pipeline

- [ ] Tag created on the exact merge commit: `git tag vX.Y.Z && git push origin vX.Y.Z`
      (tag format `vX.Y.Z`, prerelease as `vX.Y.Z-suffix.N`)
- [ ] `release.yml` environment approval granted (if required reviewers configured)
- [ ] Pipeline green end-to-end: version derivation → build → test → pack → validate → push
      (`--skip-duplicate`) → GitHub release
- [ ] GitHub release exists with generated notes and `.nupkg`/`.snupkg` artifacts attached;
      notes reviewed and edited if needed

## 7. Post-publish smoke test (from nuget.org, not local artifacts)

- [ ] All five packages visible on nuget.org at `X.Y.Z` (allow indexing time); README, license,
      icon, repository link render correctly
- [ ] Fresh-project install test:

      ```bash
      dotnet new console -n SmokeTest && cd SmokeTest
      dotnet add package Koras.Results --version X.Y.Z
      # minimal Program.cs using Result.Try / Ensure / Map / Match — expect it to build and run
      dotnet run
      ```

- [ ] Web smoke: `dotnet add package Koras.Results.AspNetCore --version X.Y.Z` into a minimal API
      project; a failing endpoint returns `application/problem+json` with `errorCode`/`traceId`
- [ ] Source Link check: from the smoke project, step into a Koras method (symbols load from the
      nuget.org symbol server)

## 8. Wrap-up

- [ ] 1.0.0 only: `PublicAPI.Unshipped.txt` contents moved to `Shipped`;
      `PackageValidationBaselineVersion` PR opened (ADR-0008)
- [ ] Announce: release notes link shared in the project's channels (discussions/README badge
      refresh as applicable)
- [ ] `SECURITY.md` supported-versions table still correct
- [ ] Milestone closed; next milestone opened; deferred items re-triaged into
      `docs/planning/backlog.md`
- [ ] Retro note (anything that made this release harder than it should be) filed as an issue
