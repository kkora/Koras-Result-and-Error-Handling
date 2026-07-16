# User Personas — Koras.Results

## Persona 1: Priya — Senior ASP.NET Core API developer

- **Context:** Builds public REST APIs for a SaaS product; team of six; Minimal APIs on .NET 10.
- **Pain:** Every endpoint hand-maps service failures to `Results.NotFound()` / `Results.Problem()`; responses drift out of RFC 9457 shape; new endpoints copy the wrong pattern.
- **Goal:** One `return result.ToHttpResult();` per endpoint; consistent `application/problem+json` everywhere.
- **Success moment:** Deletes 400 lines of mapping code in the first week; API review passes without error-shape comments.
- **Cares about:** Correct status codes, validation-error shape matching `ValidationProblemDetails`, no perf regression.

## Persona 2: Marcus — Clean Architecture / DDD tech lead

- **Context:** Enterprise line-of-business system; strict layering; the domain project must reference nothing but the BCL.
- **Pain:** Domain exceptions leak infrastructure semantics; the home-grown `OperationResult` has no tests and a `Value` property that returns `default` on failure (a production bug happened).
- **Goal:** A dependency-free Result type he can allow in the domain layer, with an error taxonomy expressing business semantics.
- **Success moment:** Architecture test `Domain should only reference Koras.Results` passes; error taxonomy replaces 14 custom exception types.
- **Cares about:** Zero dependencies, immutability, allocation behavior, semantic versioning discipline.

## Persona 3: Elena — Microservices platform engineer

- **Context:** 40+ services owned by 9 teams; platform team maintains shared building blocks.
- **Pain:** Each team's error contract differs; cross-service debugging requires learning each team's failure vocabulary; support tooling can't classify errors.
- **Goal:** Standardize a Result/error contract in the platform template; get uniform `error.code` values into traces.
- **Success moment:** Grafana dashboard slices failure rates by `error.code` across all services.
- **Cares about:** OpenTelemetry integration, stable wire format, upgrade safety across many services, docs she can link instead of writing.

## Persona 4: Tomás — Library author

- **Context:** Maintains internal NuGet packages consumed by other teams.
- **Pain:** Throwing exceptions from library APIs forces consumers into try/catch; returning nulls loses failure reasons.
- **Goal:** Public APIs returning `Result<T>` so failure modes appear in signatures.
- **Success moment:** Consumers stop filing "why did this throw?" issues.
- **Cares about:** Small transitive footprint, long-term API stability, multi-TFM support.

## Anti-persona: Fatima — F#/LanguageExt power user

Wants full monadic abstractions, applicatives, and higher-kinded emulation. Koras.Results deliberately does not serve this need; the docs point her to LanguageExt. Designing for her would create the complexity that alienates the other four personas.
