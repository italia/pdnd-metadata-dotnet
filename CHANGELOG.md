# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0]

### Breaking

- `PdndMetadataOptions.CaptureAllHeaders` now defaults to `false`. Header capture is opt-in.
  The previous default retained every inbound header except three, including bespoke credential
  headers the deny list did not know about. Consumers that relied on the permissive behaviour
  must set the flag explicitly.
- `PdndCallerMetadata.Items` is renamed to `PdndCallerMetadata.Snapshot()`. The property is
  retained as `[Obsolete]` and will be removed in 3.0.0. It allocates a full copy of the
  snapshot on every read, which a property name does not convey.

### Fixed

- `pdnd.signature.signed_headers` was never populated. The ModI `signed_headers` claim is an
  array carrying the signed headers with their values, and it was read with a string-only
  reader, so the key was silently absent on every conformant request. Array-of-objects,
  array-of-names, and comma-separated string encodings are now all accepted, and each signed
  header value is additionally exposed under `pdnd.signature.signed_header.<name>`.
- `JwtDecoder` accepted four-segment tokens, which are neither JWS nor JWE.
- `JwtDecoder` accepted standard-Base64 characters where Base64Url is required, because
  `Convert.FromBase64String` tolerates them after the character translation.
- `Pdnd.Metadata.Diagnostics` read the metadata snapshot under its own copy of the
  `HttpContext.Items` key, which did not match the one the ASP.NET Core middleware writes.
  Nothing failed: instrumentation was simply never recorded. The package now references the
  ASP.NET Core integration and uses the shared constant, and a test pins the contract between
  the two middleware components.
- `JwtDecoder` used the shared `Encoding.UTF8` instance, which substitutes U+FFFD rather than
  throwing, so invalid UTF-8 produced a mangled string that failed later during JSON parsing.
  Decoding now rejects it at the point where the problem is.

### Added

- `Pdnd.Metadata.Diagnostics`: projects the snapshot onto `Activity` tags and a request counter.
  It has no dependency on any OpenTelemetry package; the types used are part of the base class
  library and the OpenTelemetry SDK consumes them once the source names are subscribed.
- `PdndMetadataOptions.MaxHeaderCount` bounds the number of distinct headers captured. When the
  bound is reached, capture stops and `metadata.truncated` is emitted so that an incomplete
  snapshot is distinguishable from a sparse one.
- `PdndMetadataOptions.PathExclusions` skips extraction entirely for matching path prefixes,
  so health probes and metrics scrapes no longer pay the cost.
- Conformance vector corpus under `spec/vectors/`, executed by the test suite and validated
  structurally in CI. 22 vectors across 6 groups, including adversarial inputs.
- Project logo under `assets/`, with light and dark variants.

### Changed

- The header deny list now covers `Proxy-Authorization`, `Authentication`, `X-Api-Key`,
  `Api-Key`, `X-Auth-Token`, `X-Access-Token`, and `X-Csrf-Token` in addition to the previous
  three entries. A deny list is a backstop; the primary control is capture being opt-in.
- The PDND extraction path builds a single header lookup instead of performing one linear scan
  per well-known header.
- The sample API now models the recommended configuration rather than the permissive one.
- Two tests pinned the previous capture default and were re-anchored to the new posture. The
  first was named `Defaults_ShouldBeProductionFriendly` and asserted that capturing every
  inbound header was the production-friendly choice, which is a fair record of the original
  intent and of how the documentation and the code came to disagree. It is now split into three
  tests that state what the defaults are for: retain nothing that was not asked for, extract
  everything PDND sends, and bound every dimension a caller controls.
- The test suite migrated from `Items` to `Snapshot()`. The deprecated property remains covered
  by a dedicated test that suppresses the warning in file scope, so the shim stays exercised
  while any other use of it fails the build.
- `spec/PDND_METADATA_SCHEMA.md` is promoted to a versioned standalone specification under
  `spec/`, and its description of `signed_headers` is corrected: the claim is not a digest.
- Both READMEs stated that `Agid-JWT-Signature` belongs to INTEGRITY_REST_01. It is used in
  INTEGRITY_REST_02 as well, where it also carries the provider response signature.
- Both READMEs rewritten. The previous version carried 28 sections over 409 lines, a table of
  contents restating each section, fifteen source links attached inline to individual sentences,
  and a duplicate configuration block. The key reference is now a summary that points to the
  specification rather than restating it. Every code sample compiles against the shipped API.

### Infrastructure

- `TreatWarningsAsErrors` is enabled for every project in the repository. This is what surfaces
  uses of deprecated members, unhandled nullability, and unused results at build time rather than
  in review. It applies to this repository only and does not affect consumers.

- The build and test workflow lived at `src/pdnd-metadata-dotnet/.github/workflows/ci.yml`,
  where GitHub Actions never discovered it. No commit or pull request was gated by compilation
  or by the test suite. It now runs at the repository root on push and pull request.
- The publish workflow derives the package version from the release tag and refuses to publish
  when the tag disagrees with `VersionPrefix`.
- Repository layout flattened to `/src`, `/tests`, `/samples`, `/spec`, `/assets`, with the
  solution at the root. An orphan stub project and an empty `Directory.Build.props` that
  silently severed props inheritance for tests and samples were removed.
- Version and SourceLink are declared once, in `Directory.Build.props`.
- `publiccode.yml` no longer attributes the project to a government organisation, and its
  declared version is kept in step with the packages.

## [1.0.1]

### Fixed
- Improved best-effort JWE handling in `JwtDecoder` (5-segment compact tokens): decode protected header and normalize payload to `{}` without breaking extraction flow.
- Improved `Content-Digest` parsing (RFC 9530) to support structured field parameters (e.g. `;keyid="..."`) while preserving legacy compatibility.

### Tests
- Added JWE edge-case tests in `JwtDecoderTests`.
- Added `Content-Digest` parameter/skip-invalid-entry tests in `DigestParserTests`.

## [1.0.0]

### Added
- Core metadata extraction library (`Pdnd.Metadata`): framework-agnostic.
- ASP.NET Core integration (`Pdnd.Metadata.AspNetCore`): middleware, DI, model binding, Minimal API support.
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
