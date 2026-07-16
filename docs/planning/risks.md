# Risk Register — Koras.Results

| # | Risk | Likelihood | Impact | Mitigation | Owner/Trigger |
|---|---|---|---|---|---|
| R1 | **Public-API lock-in**: struct-based results and a small surface must be right the first time; post-1.0 fixes are majors | Medium | High | API design doc is the contract; PublicApiAnalyzers make every change reviewable; 0.x preview cycle gathers feedback before freeze; review checklist | Any Unshipped.txt diff |
| R2 | **Async overload matrix defects**: dozens of similar overloads invite copy-paste bugs (wrong short-circuit, missing ConfigureAwait) | Medium | High | Normative matrix in design doc; systematic per-overload tests incl. delegate-invocation counting; code review focused on symmetry | test failures |
| R3 | **ProblemDetails divergence** between Minimal API and MVC pipelines (content negotiation, ProblemDetailsFactory, validation shape) | Medium | Medium | End-to-end WebApplicationFactory tests for both pipelines asserting exact payloads; match HttpValidationProblemDetails shape | integration tests |
| R4 | **MediatR licensing drift** (13+ commercial) | High (occurred) | Medium | ADR-0006: hard upper bound [12.4,13.0); documented exit; behavior is ~60 LOC and portable | dependabot PR to 13.x must be closed, not merged |
| R5 | **Crowded market / weak adoption** | High | Medium (product) | Differentiation via whole-path story + docs quality; migration guides from incumbents; not an engineering risk | download metrics post-release |
| R6 | **Scope creep toward FP framework** | Medium | Medium | Normative out-of-scope list in vision + roadmap; anti-persona documented | feature requests |
| R7 | **Serialization wire-format regret** (shape is forever) | Low | High | Shape reviewed in ADR-0007; snapshot tests pin exact JSON; explicit guidance to prefer ProblemDetails at public boundaries | snapshot test change |
| R8 | **default(Result) misuse**: consumers storing results in arrays/fields get Uninitialized failures they didn't expect | Low | Low | Deliberate design (fail-safe beats fail-open); documented in concepts + troubleshooting | support issues |
| R9 | **Multi-TFM matrix cost**: 3 TFMs × 5 packages × analyzers slow CI | Medium | Low | Central build props; test projects target all TFMs but heavy integration tests may pin latest TFM; CI caching | CI duration |
| R10 | **Trust/supply-chain**: unsigned/undocumented releases deter enterprises | Low | Medium | Deterministic builds, SourceLink, symbols, SBOM guidance, CodeQL, dependency review, SECURITY.md | release checklist |

Review cadence: every milestone exit re-scores R1–R10; new risks enter with an owner and trigger.
