# Compatibility Testing — Koras.Results

Compatibility has three axes: **target frameworks** (does each TFM build behave identically?),
**package consumption** (does the packed artifact work in a fresh project?), and **API surface
over time** (did we break anyone?). Each axis has its own enforcement.

## 1. Multi-TFM matrix (net8.0 / net9.0 / net10.0)

All shipped packages and both multi-target test projects declare
`<TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>` (`src/Directory.Build.props`,
`tests/Directory.Build.props`). `dotnet test` therefore runs the **entire unit and integration
suite three times**, once per runtime — the same test assertions must hold on every TFM. CI
(`.github/workflows/test.yml`) installs all three SDK channels (8.0.x, 9.0.x, 10.0.x, with
`global.json` pinning the 10.0.100 SDK band) and runs `dotnet test -c Release` over the whole
solution.

The architecture test project is the deliberate exception: it targets **net10.0 only**, because
its rules (assembly references, sealing, naming) are TFM-invariant.

Per ADR-0002 there is no `netstandard2.0` target, so no down-level compatibility testing exists
or is needed.

### Per-TFM test dependencies via CPM conditions

The ASP.NET Core test host must match the framework under test — a net8.0 test run must not load
a net10.0 TestServer. `Directory.Packages.props` handles this with conditional Central Package
Management entries:

```xml
<ItemGroup>
  <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.11" />
  <PackageVersion Update="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.0"  Condition="'$(TargetFramework)' == 'net9.0'" />
  <PackageVersion Update="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" Condition="'$(TargetFramework)' == 'net10.0'" />
</ItemGroup>
```

The base entry (8.0.11) applies to net8.0; the `Update` conditions raise it per compilation. The
shipped `Koras.Results.AspNetCore` package needs no equivalent because it uses
`<FrameworkReference Include="Microsoft.AspNetCore.App" />` — the framework reference resolves to
whatever runtime the consumer targets.

## 2. Package-consumption test against the packed .nupkg

Unit tests reference projects; consumers reference *packages*. The gap between the two (missing
lib folders, wrong dependency ranges, broken nuspec) is covered by two scripts run in
`.github/workflows/package.yml` on every push and PR, and again in the release pipeline:

- `build/validate-packages.sh` — structural validation of every `.nupkg`: assemblies **and** XML
  documentation for each of net8.0/net9.0/net10.0, icon, README, nuspec metadata, `.snupkg`
  presence, and zero dependencies in the core package.
- `build/consumption-test.sh` — a true end-to-end consumption check: it writes a `NuGet.Config`
  with the artifacts directory as a local feed, scaffolds a **fresh net10.0 console project**,
  runs `dotnet add package Koras.Results --version <packed-version>`, compiles a program using
  `Result.Try`, `Ensure`, `Map`, `Match`, and `ErrorType`, executes it, and asserts the expected
  output (`CONSUMPTION-OK 84`, `TAXONOMY-OK`). This proves the published artifact is actually
  usable — restore, compile, and run — not merely well-formed.

Packed versions carry deterministic prerelease suffixes so artifacts are traceable:
`preview.<run>` on main, `pr.<n>.<run>` on pull requests (see `docs/release/versioning.md`).

## 3. API-surface compatibility over time

Two mechanisms, layered per ADR-0008:

### PublicApiAnalyzers (active now)

Every shipped project includes `Microsoft.CodeAnalysis.PublicApiAnalyzers` with
`PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` as `AdditionalFiles`
(`src/Directory.Build.props`). Adding, removing, or changing any public member fails the build
until the corresponding text file is edited, so:

- no public-surface change can land unreviewed — the PR diff shows exactly what changed;
- removals from `Shipped` (a breaking change) are impossible to miss in review.

Pre-1.0, the entire surface lives in `Unshipped.txt`. At 1.0 the surface moves to `Shipped.txt`
and becomes the frozen baseline for the major.

### PackageValidationBaselineVersion (planned, post-1.0)

`src/Directory.Build.props` already sets `<EnablePackageValidation>true</EnablePackageValidation>`
and carries the prepared, commented-out property:

```xml
<!-- Set after 1.0.0 ships to enforce binary backward compatibility (ADR-0008):
     <PackageValidationBaselineVersion>1.0.0</PackageValidationBaselineVersion> -->
```

Once 1.0.0 is on nuget.org, enabling this makes every `dotnet pack` run the .NET SDK's ApiCompat
against the last released package — catching *binary* breaks (which source-level analyzers can
miss) automatically. This is **not yet active** because there is no released baseline to compare
against; activating it is a step on the 1.0 release checklist.

### Behavioral and wire compatibility

Binary compatibility is not the whole story. Two further pins:

- **Serialization snapshot tests** (`SerializationTests`) assert exact JSON payloads; the wire
  shape is a versioned contract and changing those literals requires a major version (ADR-0007).
- **Behavioral contract tests** (default status-code map, secure option defaults, aggregation
  rules, guard exception types) live in the unit/integration suites; changing their expectations
  is treated as a breaking change per `docs/api/backward-compatibility.md`.

## Adding a new TFM

Adding a target (e.g. net11.0 when it ships) is a **minor** version change; dropping one is
**major** (`docs/api/backward-compatibility.md`). The mechanical steps: extend
`TargetFrameworks` in `src/` and `tests/` `Directory.Build.props`, add the conditional
`Microsoft.AspNetCore.Mvc.Testing` entry, add the SDK channel to all workflow `dotnet-version`
lists, and extend `EXPECTED_TFMS` in `build/validate-packages.sh`.
