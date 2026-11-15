# Security Policy

## Supported Versions

| Version | Supported          |
|---------|--------------------|
| 2.x     | :white_check_mark: |
| 1.0.x   | :x:                |

Fixes are released on the latest 2.x minor version.

## Important Notice

**Pdnd.Metadata** is a metadata **extraction** library. It does **not** perform JWT signature validation, authorization enforcement, or any security-critical operation. Tokens are decoded best-effort for observability/audit purposes only.

If you are using this library, you **must** validate PDND vouchers and tokens through your authentication/authorization layer (e.g., ASP.NET Core authentication middleware, API gateway, or PDND-specific validation logic).

## Automated Checks

- **CodeQL** (`.github/workflows/codeql.yml`) analyses the shipped C# libraries under `src/`, the
  Python specification tooling, and the GitHub Actions workflows with the `security-and-quality`
  query suite, on every push and pull request to `main` and weekly. Findings appear under
  Security > Code scanning. Three quality rules that conflict with documented design decisions
  (generic `catch` for fail-soft behaviour, explicit loops instead of LINQ on per-request paths)
  are excluded in `.github/codeql/codeql-config.yml`, with the reasons; no security query is.
- **Reproducible restore**: every project has a `packages.lock.json` with content hashes, and CI
  restores in locked mode, so a dependency cannot change without a reviewed lock file update.
- **OpenSSF Scorecard** (`.github/workflows/scorecard.yml`) assesses the repository's security
  practices weekly and on changes to `main` or to branch protection. Results are published to the
  [Scorecard viewer](https://scorecard.dev/viewer/?uri=github.com/italia/pdnd-metadata-dotnet)
  and to code scanning.

## Reporting a Vulnerability

If you discover a security vulnerability in this project, please report it responsibly:

- **Email**: francesco.delre[at]protonmail.com
- **Subject**: `[SECURITY] pdnd-metadata-dotnet — <brief description>`

Please **do not** open a public GitHub issue for security vulnerabilities.

You should expect an initial response within **72 hours**. We will work with you to understand the issue and coordinate a fix and disclosure timeline.
