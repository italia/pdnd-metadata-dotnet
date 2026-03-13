# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2025-07-XX

### Added
- Core metadata extraction library (`Pdnd.Metadata`) — framework-agnostic.
- ASP.NET Core integration (`Pdnd.Metadata.AspNetCore`) — middleware, DI, model binding, Minimal API support.
- PDND voucher JWT decoding (best-effort, no signature validation) with all standard and PDND-specific claims (`purposeId`, `clientId`, `organizationId`, `dnonce`).
- `Agid-JWT-TrackingEvidence` header extraction (both naming variants).
- `Agid-JWT-Signature` header extraction (INTEGRITY_REST pattern support).
- DPoP proof token extraction (RFC 9449) including `ath` and `nonce`.
- Legacy `Digest` header parsing.
- `Content-Digest` header parsing (RFC 9530 structured field dictionary format).
- Forwarded headers normalization (`Forwarded` RFC 7239 / `X-Forwarded-For`).
- W3C Trace Context promotion (`traceparent`, `tracestate`, `baggage`).
- Correlation ID and Request ID extraction.
- Configurable header capture (allow-list / deny-list), with security-first defaults.
- Guard-rails: `MaxTokenLength`, `MaxValueLength`, `MaxHeaderValuesPerName`.
- 62 unit tests covering all extractors and edge cases.
- Sample API project with Controller and Minimal API endpoints.
- PDND Metadata Schema reference document.
