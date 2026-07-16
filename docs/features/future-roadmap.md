# Future Roadmap — Koras.Results

Post-MVP feature plan. See [feature-catalog.md](feature-catalog.md) for KR-1xx specifications and [../product/product-roadmap.md](../product/product-roadmap.md) for release themes.

## 1.1 — Ecosystem depth

- **KR-101 `Koras.Results.EntityFrameworkCore`.** `SaveChangesResultAsync()` extension mapping `DbUpdateConcurrencyException` → `Conflict`, transient provider failures → `Unavailable`, unique-constraint violations → `Conflict` with constraint metadata. Guarded behind provider-agnostic detection with per-provider extension points.
- **KR-102 LINQ query syntax.** `Select`/`SelectMany`/`Where` instance methods on `Result<T>` enabling query expressions. Additive, zero-risk to existing API.
- **KR-103 Localization adapter.** `IStringLocalizer`-backed `IErrorMessageLocalizer` with resource-key convention `Errors:{Code}`.

## 1.2 — Protocol edges

- **KR-104 `Koras.Results.Grpc`.** `ErrorType` → `StatusCode` mapping (NotFound→NotFound, Validation→InvalidArgument, Conflict→Aborted, Unauthorized→Unauthenticated, Forbidden→PermissionDenied, Unavailable→Unavailable, else Internal); `ToRpcException()` with `google.rpc.Status` details carrying error code + field errors.
- **KR-105 `Koras.Results.GraphQL`.** HotChocolate `IError` conversion preserving codes and field paths.

## 2.0 — Source generation & AOT

- **KR-106 Source-generated error catalogs.** `[ErrorCatalog]` on partial classes generates factories from declarative definitions; compile-time duplicate-code detection; optional docs (markdown) emission for client teams.
- **Native AOT audit.** Core is already reflection-free; 2.0 certifies the AspNetCore path (trim annotations, STJ source-gen context support).
- **TFM policy.** Drop EOL TFMs; adopt current LTS/STS lineup.

## Experimental track

- **KR-107 `Koras.Results.Analyzers`.** Diagnostics: KRS001 unobserved `Result` return value; KRS002 `Value` access not dominated by `IsSuccess` check; KRS003 error code literal not matching `Namespace.Code` convention. Ships with code fixes.

## Decision gates

Each roadmap item requires: an issue with design sketch → ADR if it changes architecture → API proposal reviewed against `docs/api/public-api-review-checklist.md` → implementation with the standard quality gates.
