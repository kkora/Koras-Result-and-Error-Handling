# Security Checklist — Koras.Results

Two audiences, two lists. Maintainers complete Part A for every release; consumers use Part B
when deploying an application that uses these packages.

## Part A — Maintainers, per release

Complete alongside `docs/release/release-checklist.md`.

### Dependencies & supply chain

- [ ] `dotnet list package --vulnerable --include-transitive` reports nothing (also enforced in
      `build.yml`, but rerun locally against the release commit)
- [ ] `Directory.Packages.props` diff since the last release reviewed; every shipped-dependency
      change has an ADR or matches existing policy
- [ ] MediatR reference still within `[12.4.1, 13.0.0)`; Dependabot ignore rule for MediatR
      majors still present in `.github/dependabot.yml` (ADR-0006)
- [ ] `NuGet.Config` still declares nuget.org as the only source with `*` source mapping
- [ ] Core package has zero dependencies — `build/validate-packages.sh` passed on the release
      artifacts (checks the nuspec)
- [ ] Dependabot, CodeQL, and dependency-review workflows are enabled and green on `main`
- [ ] No open unresolved security advisories or private vulnerability reports for the release
      scope (check GitHub Security tab)

### Secure defaults (regression check)

- [ ] `KorasResultsOptionsTests.Secure_defaults_are_in_force` green:
      `IncludeUnexpectedErrorDetails == false`, `MetadataExposure == None`,
      `IncludeTraceId == true`
- [ ] `Unexpected_error_details_are_suppressed_by_default_and_logged` green (suppression +
      Warning log, no leak in response body)
- [ ] `ResultTryTests.Try_converts_exceptions_using_the_safe_default` green (exception message
      never enters the default error; only `exceptionType` metadata)
- [ ] Serialization malformed-payload tests green (`JsonException` on every malformed shape; no
      polymorphic type handling introduced)

### Repository hygiene

- [ ] No secrets, credentials, tokens, or personal data in code, tests, samples, docs, or CI
      logs (DoD item; spot-check new files this release)
- [ ] NuGet publishing is keyless (Trusted Publishing): no long-lived nuget.org API key exists
      as a secret anywhere; the policy pins repository, workflow, and the protected
      `nuget-release` environment (see `docs/release/nuget-publishing.md`)
- [ ] Release built by the `release.yml` pipeline from a tag — never from a developer machine
- [ ] Symbols (`.snupkg`) published and Source Link resolvable for the release build
- [ ] `SECURITY.md` supported-versions table updated if this release changes the supported set

### Documentation

- [ ] Threat model still accurate — any new configuration option, serialization behavior, or
      dependency added this release is reflected in `docs/security/threat-model.md` and
      `docs/security/secure-configuration.md`
- [ ] CHANGELOG notes any security-relevant behavior change explicitly

## Part B — Consumers, deploying an application

### Configuration

- [ ] `IncludeUnexpectedErrorDetails` is `false` in production (the default). If enabled anywhere,
      it is guarded by `IsDevelopment()` in code — not by raw configuration alone
- [ ] `MetadataExposure` is `None` (the default) unless you have audited every `WithMetadata`
      call site in your codebase for client-safe content
- [ ] `IncludeTraceId` left `true` so support can correlate client-reported problems to traces
- [ ] Custom `Result.Try` exception mappers (if any) do not copy `ex.Message`/`ex.ToString()`
      into errors that project to non-500 responses

### Error content hygiene

- [ ] No secrets, connection strings, hostnames, file paths, or PII in `Error.Code`,
      `Error.Message`, `Error.Metadata`, or validation messages (see
      `docs/security/data-protection.md`)
- [ ] Genuinely technical/unclassified failures use `ErrorType.Unexpected` (so the suppression
      default protects them), not `Failure`
- [ ] Validation messages do not echo the rejected input value back

### Logging & telemetry

- [ ] Log pipeline access-controls cover the `Koras.Results.AspNetCore.ResultHttpMapper`
      category — its Warning event (id 2) contains original `Unexpected` messages
- [ ] Application `TapError` logging follows the rule: codes/types freely, messages/metadata
      treated as sensitive at Information+ levels

### Serialization boundaries

- [ ] Serialized `Result`/`Error` JSON is exchanged only over trusted internal transports
      (queues, caches, internal services) — public APIs return ProblemDetails instead
- [ ] Host serializer limits (request size, `MaxDepth`) are in place where untrusted parties can
      submit result/error JSON

### Supply chain (your side)

- [ ] Package references pinned via your own lockfile/CPM; `Koras.Results*` restored from
      nuget.org (check the `Koras` reserved prefix and package signatures)
- [ ] Your own `dotnet list package --vulnerable --include-transitive` gate covers the Koras
      packages like any other dependency
- [ ] If you use MediatR: you are on 12.x; do not attempt to force-upgrade past the
      `[12.4, 13.0)` bound — the resulting NU1107 conflict is a licensing guard, not a bug
      (`docs/troubleshooting/common-errors.md`)
