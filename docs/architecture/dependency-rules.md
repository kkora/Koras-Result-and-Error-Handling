# Dependency Rules — Koras.Results

## Hard rules

1. **`Koras.Results` (core) has zero NuGet dependencies.** Not even `Microsoft.Extensions.*` abstractions. This is the package's core promise and is enforced by an architecture test and by CI package validation.
2. **Framework references over package references.** `Koras.Results.AspNetCore` uses `<FrameworkReference Include="Microsoft.AspNetCore.App" />` — no transitive NuGet weight.
3. **Every third-party dependency requires an ADR** documenting: necessity, alternatives considered, license, maintenance status, security posture, size impact, and whether it leaks into our public API.
4. **No dependency may leak concrete third-party types into a Koras public API** except in the package whose entire purpose is that integration (e.g., `FluentValidation.Results.ValidationResult` appears in `Koras.Results.FluentValidation` extension signatures — that is the product).
5. **Version ranges:** satellites reference the *minimum* supported version of their integration target (FluentValidation `[11.0,)` effective via lowest-tested 11.x; MediatR `[12.0.0, 13.0.0)` — upper-bounded for licensing, ADR-0006). We never pin upper bounds otherwise.
6. **Central package management.** All versions live in `Directory.Packages.props`; project files contain no `Version` attributes.

## Approved dependency register

| Package | Dependency | Version policy | License | Public-API leak? |
|---|---|---|---|---|
| Koras.Results | — | — | — | — |
| Koras.Results.AspNetCore | Microsoft.AspNetCore.App (framework ref) | matches TFM | MIT | yes — by design (IResult/IActionResult/ProblemDetails) |
| Koras.Results.FluentValidation | FluentValidation | ≥ 11.9 | Apache-2.0 | yes — by design |
| Koras.Results.MediatR | MediatR | [12.4, 13.0) | Apache-2.0 | yes — by design (IPipelineBehavior) |
| Koras.Results.MediatR | Microsoft.Extensions.DependencyInjection.Abstractions | ≥ 8.0 | MIT | yes (IServiceCollection) |
| Koras.Results.OpenTelemetry | System.Diagnostics.DiagnosticSource | ≥ 8.0 | MIT | yes (Activity) |

Test/build-only dependencies (xUnit, coverlet, NetArchTest, Microsoft.AspNetCore.Mvc.Testing, BenchmarkDotNet, PublicApiAnalyzers, SourceLink) never ship in packages.

## Adding a dependency — checklist

- [ ] Is it genuinely required (can't be a `FrameworkReference`, can't be avoided)?
- [ ] License compatible with MIT distribution? (Apache-2.0, MIT, BSD OK; GPL/commercial NO)
- [ ] Actively maintained (release within 12 months, responsive issues)?
- [ ] Trimming/AOT compatible where our TFMs claim it?
- [ ] ADR written and linked here?
- [ ] `Directory.Packages.props` entry + Dependabot will track it?
