# ADR-0002: Target net8.0;net9.0;net10.0 — no netstandard2.0

**Status:** Accepted · **Date:** 2026-07-16

## Context

The brief requires .NET 8, 9, and 10, with `netstandard2.0` "only when broad legacy compatibility is genuinely needed". Library authors sometimes want .NET Framework reach.

## Decision

All packages multi-target **`net8.0;net9.0;net10.0`**. No `netstandard2.0`.

## Rationale

- Target users (ASP.NET Core, Clean Architecture, microservices) run modern .NET; the AspNetCore satellite *cannot* target netstandard2.0 anyway (FrameworkReference requires net(core)).
- netstandard2.0 would cost: no `[MemberNotNullWhen]`/NRT attribute fidelity without polyfills, no built-in System.Text.Json (dependency!), C# feature constraints — directly against the zero-dependency promise.
- net8.0 (LTS, supported until Nov 2026) is the compatibility floor; net9.0/net10.0 targets let us adopt per-TFM improvements and give consumers exact-match binaries.

## Consequences

- .NET Framework / netstandard consumers are unsupported (documented). Revisit only with strong demand — would ship as a separate compatibility package rather than degrading the core.
- TFM changes happen only in major versions (see versioning policy).
