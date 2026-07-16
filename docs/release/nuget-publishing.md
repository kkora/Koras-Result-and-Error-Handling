# NuGet Publishing Setup — Koras.Results

One-time repository and nuget.org configuration required for the release pipeline
([release-process.md](release-process.md)) to work, plus the standing publishing hygiene rules.

## GitHub repository setup

### The protected `nuget-release` environment

`.github/workflows/release.yml` runs its single job with `environment: nuget-release`. Configure
it under *Settings → Environments → nuget-release*:

- **No secrets required** — publishing uses [Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
  (OIDC), not a stored API key. The environment still matters: the nuget.org policy pins it, so
  only workflow runs targeting `nuget-release` can obtain a publish credential.
- **Protection rules (recommended)**: required reviewers (a human approves each release run
  before the job starts) and a deployment-branch/tag policy restricting the environment to
  `v*.*.*` tags. With required reviewers on, pushing a tag *queues* the release until approved —
  a useful last gate.

## nuget.org setup

### Trusted Publishing policy

Publishing is keyless: the release job requests a GitHub OIDC token, `NuGet/login@v1` exchanges
it with nuget.org for a **1-hour, single-use API key**, and `dotnet nuget push` uses that key.
Nothing long-lived is stored anywhere, so there is no rotation and nothing to leak.

Create the policy on nuget.org (*username menu → Trusted Publishing*), owned by the
`kora.kanchan` user account:

- **Repository Owner:** `kkora`
- **Repository:** the GitHub repository name (must match exactly; update the policy if the
  repository is ever renamed)
- **Workflow File:** `release.yml` (file name only, no `.github/workflows/` path)
- **Environment:** `nuget-release` — binds the policy to the protected environment above

The workflow's `NuGet/login` step authenticates as `user: kora.kanchan`; the policy applies to
all packages owned by that account.

Note: for private repositories a new policy starts **temporarily active for 7 days** and must
see one successful publish in that window to become permanent (nuget.org needs the repo ID from
a real token to lock the policy against repo-resurrection attacks). If the window lapses, reset
it from the Trusted Publishing page.

### Prefix reservation for `Koras`

Reserve the **`Koras.*` package ID prefix** on nuget.org (*Account → Package ID prefix
reservation* request). Reservation gives every `Koras.Results*` package the verified checkmark,
prevents third parties from publishing look-alike IDs under the prefix, and is the consumer's
cue that the package comes from the actual owner. Do this **before** the first public publish —
reservation is much simpler when no conflicting packages exist.

### Package ownership

After the first publish, add a second owner account (organization or trusted maintainer) so a
single lost account cannot orphan the packages.

## What the build already provides

These are configured in `src/Directory.Build.props` and verified by `build/validate-packages.sh`;
listed here so the publishing picture is complete:

- **Symbols**: `IncludeSymbols=true` + `SymbolPackageFormat=snupkg`. Every pack produces a
  `.snupkg`; `dotnet nuget push` uploads it alongside the `.nupkg` automatically, and nuget.org
  serves it via the standard symbol server (`https://symbols.nuget.org/download/symbols`).
  The validation script fails if any package is missing its symbols companion.
- **Deterministic builds + Source Link, gated on CI**: `Deterministic=true` always;
  `ContinuousIntegrationBuild` turns on when the `CI` environment variable is `true` (all
  workflows set it). Source Link ships in the .NET SDK and is explicitly disabled outside CI —
  `<EnableSourceLink Condition="'$(CI)' != 'true'">false</EnableSourceLink>` — so only CI builds,
  where the GitHub origin is resolvable, embed source-server metadata. Combined with
  `PublishRepositoryUrl` and `EmbedUntrackedSources`, consumers can step from a released binary
  directly into the exact tagged source.
- **Package metadata**: MIT license expression, README, icon, tags, repository URL — asserted per
  package by the validation script.
- **Package validation**: `EnablePackageValidation=true` on every pack;
  `PackageValidationBaselineVersion` activates after 1.0 (ADR-0008) to add binary
  backward-compatibility checks against the last released version.

## The validation script in the publish path

`build/validate-packages.sh artifacts` runs in the release workflow **after pack and before
push** — it is the final structural gate. It verifies, for every `.nupkg`: assemblies and XML
documentation for net8.0/net9.0/net10.0, icon, README, nuspec fields (license expression,
project URL, repository, readme, icon, tags), the `.snupkg` companion, and that the **core
package declares zero dependencies**. Any failure aborts the release with nothing published.

The same script runs on every push/PR via `package.yml`, so a release should never be the first
time it executes against a given change.

## Publishing rules of thumb

- Only the pipeline publishes. If `dotnet nuget push` is ever run by hand in an emergency, use a
  freshly created, immediately-revoked key — and treat the event as a process failure to fix.
- `--skip-duplicate` is part of the push command by design; reruns are safe.
- Published versions are immutable: fixing a bad release means unlisting + shipping a new patch,
  never re-pushing (see the failure modes in [release-process.md](release-process.md)).
- Prerelease packages (`0.1.0-preview.N` from main) are CI artifacts by default — they are
  uploaded to the workflow run, **not** pushed to nuget.org. Only tagged versions reach the
  public feed.
