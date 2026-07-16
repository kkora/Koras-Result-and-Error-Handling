# Dependency Security — Koras.Results

Policy source: `docs/architecture/dependency-rules.md` (hard rules) and ADR-0001/ADR-0006. This
page is the security-focused register and the audit tooling reference.

## Dependency register (shipped packages)

| Package | Runtime dependencies | Version policy | License | Notes |
|---|---|---|---|---|
| `Koras.Results` | **None** | — | — | Zero NuGet dependencies; System.Text.Json comes from the BCL on net8.0+. Enforced by an architecture test (`Core_references_no_packages_beyond_the_base_class_library`) and by `build/validate-packages.sh`, which fails if the core nuspec lists any `<dependency>` |
| `Koras.Results.AspNetCore` | `Microsoft.AspNetCore.App` via **`<FrameworkReference>`** only | matches consumer TFM | MIT | A framework reference, not a package reference — zero transitive NuGet weight; servicing arrives with the consumer's runtime updates |
| `Koras.Results.FluentValidation` | FluentValidation | CPM pins **11.11.0** (policy floor ≥ 11.9) | Apache-2.0 | Leaks `ValidationResult` types into signatures by design — that is the product |
| `Koras.Results.MediatR` | MediatR; Microsoft.Extensions.DependencyInjection.Abstractions | **`[12.4.1, 13.0.0)`**; DI abstractions ≥ 8.0 | Apache-2.0; MIT | The MediatR **upper bound is deliberate** (ADR-0006): MediatR 13+ is commercially licensed, and an MIT package silently pulling a commercial transitive dependency would be a trust violation. Users on MediatR 13 get an explicit NuGet conflict (the desired failure mode) instead of silent license exposure. Also references `Koras.Results.FluentValidation` (the single audited satellite-to-satellite edge) |
| `Koras.Results.OpenTelemetry` | **None** | — | — | `System.Diagnostics.Activity` (DiagnosticSource) is in-box on net8.0+; the csproj carries no package reference |

Build/test-only dependencies (xUnit, coverlet, NetArchTest, Microsoft.AspNetCore.Mvc.Testing,
BenchmarkDotNet, PublicApiAnalyzers, StyleCop.Analyzers) never ship in packages. All versions —
shipped and test — live in `Directory.Packages.props` (Central Package Management), so a
dependency change is always a one-file, reviewable diff.

Source integrity: the committed `NuGet.Config` clears all package sources, registers **only
nuget.org**, and maps the `*` pattern to it — no secondary feeds, no dependency-confusion window.

## Audit tooling

### In CI, on every push and pull request

- **Vulnerability audit** (`.github/workflows/build.yml`):
  `dotnet list package --vulnerable --include-transitive` runs after every restore; if any
  vulnerable package is reported, the build fails. This covers transitive dependencies of test
  and benchmark projects too.
- **Dependency review** (`.github/workflows/dependency-review.yml`): the
  `actions/dependency-review-action` gate on every PR fails on advisories of **moderate**
  severity or higher in newly introduced dependencies, and enforces a license allowlist —
  `MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause, MS-PL, 0BSD, ISC` — blocking copyleft and
  commercial licenses at the door (the same policy that produced ADR-0006).
- **CodeQL** (`.github/workflows/codeql.yml`): `security-and-quality` query suite for C# on every
  push/PR plus a weekly scheduled run.

### Continuous

- **Dependabot** (`.github/dependabot.yml`): weekly (Mondays) update PRs for both the NuGet
  ecosystem and GitHub Actions. Test dependencies are grouped (`xunit*`,
  `Microsoft.NET.Test.Sdk`, `coverlet*`) to reduce PR noise. Crucially, it carries an explicit
  ignore rule:

  ```yaml
  ignore:
    # ADR-0006: MediatR must stay on the Apache-2.0 licensed 12.x line.
    - dependency-name: "MediatR"
      update-types: ["version-update:semver-major"]
  ```

  so Dependabot will propose MediatR 12.x patches/minors but never a 13.x major.

### Per release

The release-level Definition of Done requires a clean
`dotnet list package --vulnerable --include-transitive` run and completion of
`docs/security/security-checklist.md` before any push to nuget.org.

## Accepting a new dependency

Any new runtime dependency for an existing package is a **breaking-change-class event** (it is
listed under "Dependency" breaks in `docs/api/backward-compatibility.md`) and requires an ADR
covering necessity, alternatives, license, maintenance status, security posture, size impact, and
public-API leakage — see the checklist in `docs/architecture/dependency-rules.md`. The default
answer is no; prefer `FrameworkReference` or in-box BCL types, as the AspNetCore and
OpenTelemetry packages demonstrate.

## SBOM guidance

Consumers or auditors who need a Software Bill of Materials for these packages have two
first-party routes:

1. **dotnet CLI**: recent .NET SDKs can produce an SPDX SBOM at build/publish time (e.g.
   `dotnet publish /p:GenerateSBOM=true` with the Microsoft.Sbom.Targets package, or the
   `sbom-tool` CLI against the build output). Given the dependency graph above, the interesting
   content is small: the core SBOM contains only the Koras assembly itself.
2. **GitHub dependency graph export**: this repository's dependency graph (populated from the
   lockfile-less restore plus dependency-review) can be exported as SPDX from the repository's
   *Insights → Dependency graph → Export SBOM* — useful for auditing the full build-time chain
   including test tooling and Actions.

Because the shipped dependency surface is tiny and fully enumerated in this document, a manual
review of `Directory.Packages.props` plus each package's nuspec (validated by
`build/validate-packages.sh`) is also a practical audit path.
