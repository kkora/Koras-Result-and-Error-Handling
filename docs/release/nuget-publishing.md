# NuGet Publishing Setup — Koras.Results

One-time repository and nuget.org configuration required for the release pipeline
([release-process.md](release-process.md)) to work, plus the standing publishing hygiene rules.

## GitHub repository setup

### The protected `nuget-release` environment

`.github/workflows/release.yml` runs its single job with `environment: nuget-release`. Configure
it under *Settings → Environments → nuget-release*:

- **Secret `NUGET_API_KEY`** — the nuget.org API key, stored **only** in this environment (not as
  a repository-level secret). Environment scoping means the key is exposed exclusively to
  workflow runs that target this environment — i.e. tag-triggered release runs — never to PR or
  push builds.
- **Protection rules (recommended)**: required reviewers (a human approves each release run
  before the job starts) and a deployment-branch/tag policy restricting the environment to
  `v*.*.*` tags. With required reviewers on, pushing a tag *queues* the release until approved —
  a useful last gate.

### API key scoping guidance

Create the key on nuget.org (*Account → API Keys*) with least privilege:

- **Scope: Push only** — specifically *Push new packages and package versions*. The CI key must
  not be able to unlist packages or manage key/owner settings.
- **Package glob: `Koras.Results*`** — the key can push only this family, so a leaked key cannot
  publish arbitrary packages under the account.
- **Expiry**: nuget.org keys live at most 365 days. Track the expiry (calendar entry) and rotate
  by generating a new key and replacing the environment secret; the old key can be revoked
  immediately after a successful release with the new one.
- Regenerate immediately if a release run ever prints the key or a fork PR somehow gains
  environment access (it should not — environment secrets are not available to fork PRs).

## nuget.org setup

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
