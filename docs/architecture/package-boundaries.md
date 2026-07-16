# Package Boundaries — Koras.Results

## Boundary contract per package

### Koras.Results (core)
- **Owns:** result primitives, error model, composition, JSON converters.
- **May reference:** nothing beyond the BCL.
- **Public namespace:** `Koras.Results` (single namespace — the whole core fits comfortably; no artificial sub-namespaces).
- **Never contains:** HTTP concepts, DI registration, logging, configuration, validation rules.

### Koras.Results.AspNetCore
- **Owns:** `ErrorType`→HTTP status projection, ProblemDetails construction, Minimal API `IResult` adapters, MVC `IActionResult` adapters, `KorasResultsOptions`, `IErrorMessageLocalizer`, DI registration.
- **May reference:** `Koras.Results`, the ASP.NET Core shared framework (`FrameworkReference`, no NuGet dependency weight).
- **Rule:** all HTTP semantics live here and only here.

### Koras.Results.FluentValidation
- **Owns:** `ValidationResult`/`ValidationFailure` → `ValidationError` conversion; `ValidateToResultAsync`.
- **May reference:** `Koras.Results`, `FluentValidation`.
- **Rule:** no ASP.NET Core reference — usable in console/worker apps.

### Koras.Results.MediatR
- **Owns:** `ValidationBehavior<,>`, registration helpers.
- **May reference:** `Koras.Results`, `Koras.Results.FluentValidation`, `MediatR` 12.x, `Microsoft.Extensions.DependencyInjection.Abstractions`.

### Koras.Results.OpenTelemetry
- **Owns:** Activity tagging extensions and tag-name constants.
- **May reference:** `Koras.Results`, `System.Diagnostics.DiagnosticSource`.
- **Rule:** never references the OpenTelemetry SDK — tags flow through `Activity`, which any OTel setup exports.

## Dependency direction (enforced by architecture tests)

```
FluentValidation ─┐
AspNetCore ───────┼──▶ Koras.Results (core) ──▶ (nothing)
OpenTelemetry ────┘
MediatR ──▶ Koras.Results.FluentValidation ──▶ core
```

- Satellites may depend on the core; the core depends on nothing.
- Satellites may not depend on each other, with one audited exception: `MediatR → FluentValidation` (the validation behavior is meaningless without validator conversion).
- Tests enforce these rules via NetArchTest (`Koras.Results.ArchitectureTests`).

## What would violate a boundary (examples)

| Violation | Correct home |
|---|---|
| `Error.ToStatusCode()` in core | AspNetCore `ErrorTypeStatusMap` |
| `Result<T>.ToActionResult()` in core | AspNetCore extensions |
| Logging inside `Result.Try` | Caller's concern (Tap/TapError) |
| FluentValidation types in MediatR public API | FluentValidation package adapters |
