# Security Policy

## Supported Versions

| Version | Supported          |
|---------|--------------------|
| 1.0.x   | :white_check_mark: |

## Important Notice

**Pdnd.Metadata** is a metadata **extraction** library. It does **not** perform JWT signature validation, authorization enforcement, or any security-critical operation. Tokens are decoded best-effort for observability/audit purposes only.

If you are using this library, you **must** validate PDND vouchers and tokens through your authentication/authorization layer (e.g., ASP.NET Core authentication middleware, API gateway, or PDND-specific validation logic).

## Reporting a Vulnerability

If you discover a security vulnerability in this project, please report it responsibly:

- **Email**: francesco.delre[at]protonmail.com
- **Subject**: `[SECURITY] pdnd-metadata-dotnet — <brief description>`

Please **do not** open a public GitHub issue for security vulnerabilities.

You should expect an initial response within **72 hours**. We will work with you to understand the issue and coordinate a fix and disclosure timeline.
