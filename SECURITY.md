# Security Policy

## Supported versions

| Version | Supported |
|---|---|
| latest major (currently 0.x preview) | ✅ |
| previous major | ✅ fixes for 12 months after the next major ships |
| older | ❌ |

## Reporting a vulnerability

**Please do not report security vulnerabilities through public GitHub issues, discussions, or pull requests.**

Instead, use **GitHub private vulnerability reporting** on this repository (Security → Report a vulnerability). If that is unavailable to you, email **security@korastechnologies.example** with:

- A description of the issue and its impact
- Steps to reproduce or a proof of concept
- Affected package(s) and version(s)

You will receive an acknowledgment within **3 business days** and a remediation plan or resolution target within **14 days**. We follow coordinated disclosure: we ask that you give us the opportunity to release a fix before public disclosure, and we will credit reporters in release notes unless anonymity is requested.

## Scope notes

Koras.Results performs no I/O, handles no credentials, and stores no data; the primary security surfaces are (1) information disclosure through error-to-HTTP projection and (2) JSON deserialization of error payloads. Hardening details and secure defaults are documented in [`docs/security/threat-model.md`](docs/security/threat-model.md).

## Supply chain

Packages are built deterministically in GitHub Actions with Source Link, published with symbols, and the dependency graph is intentionally minimal (the core package has zero dependencies). CodeQL, dependency review, and Dependabot run on this repository.
