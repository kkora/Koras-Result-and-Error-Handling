# Architecture Decision Records

| ADR | Title | Status |
|---|---|---|
| [0001](0001-core-plus-satellite-packages.md) | Core package plus integration satellites | Accepted |
| [0002](0002-target-frameworks.md) | Target net8.0/net9.0/net10.0; no netstandard2.0 | Accepted |
| [0003](0003-result-as-readonly-struct.md) | Result types are readonly structs; default = failure | Accepted |
| [0004](0004-closed-error-taxonomy.md) | Closed ErrorType taxonomy, domain-first; HTTP mapping only in AspNetCore | Accepted |
| [0005](0005-error-class-hierarchy.md) | Error is an immutable class with a closed hierarchy | Accepted |
| [0006](0006-mediatr-version-pin.md) | Pin MediatR to 12.x (licensing) | Accepted |
| [0007](0007-system-text-json-only.md) | System.Text.Json only for serialization | Accepted |
| [0008](0008-public-api-analyzers.md) | Public API locked via PublicApiAnalyzers | Accepted |

Format: lightweight [MADR](https://adr.github.io/madr/)-style. New ADRs are proposed by PR; superseding an ADR requires a new ADR referencing the old one.
