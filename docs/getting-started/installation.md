# Installation

Koras.Results is distributed as five NuGet packages: one zero-dependency core and four opt-in integration satellites. Install only what you use — the core is safe to reference from any layer, including your domain project.

## Packages

```bash
# Core — Result, Result<T>, Error model, composition, JSON serialization (zero dependencies)
dotnet add package Koras.Results

# ASP.NET Core — ProblemDetails projection, Minimal API / MVC adapters, DI registration
dotnet add package Koras.Results.AspNetCore

# FluentValidation — ValidationResult -> Result conversion, ValidateToResultAsync
dotnet add package Koras.Results.FluentValidation

# MediatR (12.x) — validation pipeline behavior that fails with a Result instead of throwing
dotnet add package Koras.Results.MediatR

# OpenTelemetry — Activity tagging for failed results
dotnet add package Koras.Results.OpenTelemetry
```

Or in a `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Koras.Results" Version="*" />
  <PackageReference Include="Koras.Results.AspNetCore" Version="*" />
</ItemGroup>
```

The satellite packages reference the core transitively, so `dotnet add package Koras.Results.AspNetCore` alone gives you the full core API as well.

## Which package do I need?

| Scenario | Install |
|---|---|
| Domain / class library returning `Result<T>` | `Koras.Results` only |
| Minimal API or MVC application returning ProblemDetails | `Koras.Results.AspNetCore` |
| Validating requests with FluentValidation into results | `Koras.Results.FluentValidation` |
| MediatR (12.x) pipeline with automatic request validation | `Koras.Results.MediatR` (pulls in the FluentValidation package) |
| Tracing failures in OpenTelemetry spans | `Koras.Results.OpenTelemetry` |
| Console app or background worker | `Koras.Results` (+ OpenTelemetry package if you trace) |

A typical web API installs `Koras.Results.AspNetCore` in the host project and plain `Koras.Results` in the domain/application projects — the domain never needs to see HTTP types.

## Supported frameworks

All five packages multi-target:

| Target framework | Supported |
|---|---|
| `net8.0` | Yes (LTS) |
| `net9.0` | Yes |
| `net10.0` | Yes |
| `netstandard2.0` / .NET Framework | No (by design — see ADR-0002) |

Your application only needs to target one of these; NuGet selects the matching build automatically.

## SDK requirements

- **To consume the packages:** any .NET SDK capable of targeting .NET 8, 9, or 10. No special SDK version is required.
- **To build this repository from source** (including the samples): .NET SDK **10** — the repository's `global.json` pins `10.0.100` with `rollForward: latestFeature`. This is a build-time requirement only; the packages themselves run on .NET 8, 9, and 10.

```bash
dotnet --version   # building the repo? expect 10.0.1xx
```

## Version pins and licensing notes

- **MediatR:** `Koras.Results.MediatR` deliberately depends on MediatR **12.x**, the Apache-2.0 licensed release line. It does not reference MediatR 13+, which moved to a commercial license (ADR-0006).
- **OpenTelemetry:** `Koras.Results.OpenTelemetry` depends only on `System.Diagnostics.DiagnosticSource`, not on the OpenTelemetry SDK — it composes with any OTel setup (or none).
- **License:** every package in the family is MIT.

## Verifying the install

Create a file and compile — no registration or configuration is needed for the core:

```csharp
using Koras.Results;

Result<int> parsed = Result.Success(42);
Console.WriteLine(parsed.IsSuccess);        // True

Result<int> failed = Error.NotFound("Answer.NotFound", "No answer found.");
Console.WriteLine(failed.Error.Code);       // Answer.NotFound
```

## Next steps

- [Quick start](quick-start.md) — a working HTTP API with ProblemDetails in five minutes
- [Your first application](first-application.md) — the full todo-sample walkthrough
- [Dependency injection](dependency-injection.md) — registering the ASP.NET Core integration
