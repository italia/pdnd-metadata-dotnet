# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.1.0]

Aligned with the token shapes in the current PDND Interoperabilità operating manual
(developer.pagopa.it) and with the AgID AUDIT_REST and INTEGRITY_REST patterns. Targets `net8.0`
and `net10.0`, as 2.0.0 did. No public API is removed or changed; the one behavioural change that
can affect consumers is the renamed counter dimension, described under Changed.

### Changed

- **Metrics:** the `pdnd.requests` counter dimension that identifies the consumer organization is
  now `pdnd.voucher.consumerId`, read from `consumerId` and falling back to `organizationId`. It
  was `pdnd.voucher.organizationId`, but current PDND vouchers do not carry that claim, so the
  dimension read `unknown` on real traffic. Dashboards and alerts on the old dimension name must
  be updated.
- `pdnd.voucher.organizationId`, `pdnd.voucher.clientId`, and `pdnd.voucher.dnonce` are documented
  as legacy. They are still produced when a token carries them.

### Fixed

- `metadata.truncated` was reported for snapshots that were complete. The `MaxHeaderCount` bound
  was checked before the allow, deny, and raw-capture filters, so reaching the bound exactly
  and then meeting a header that would have been skipped anyway (a denied credential, a header
  outside the allow list, a signed blob) emitted the flag although nothing had been dropped. The
  bound is now checked only for a header that would actually be captured.
- The voucher was not extracted from DPoP-bound requests. Only the `Bearer` scheme was
  recognised, while a DPoP-bound voucher arrives as `Authorization: DPoP <JWT>` (RFC 9449 §7.1):
  the DPoP proof was decoded but `purposeId`, `organizationId`, and every other voucher claim
  were absent. Both schemes are now accepted. `PdndAuthorization.TryGetVoucherToken` is added;
  `TryGetBearerToken` keeps its meaning.
- `net.forwarded_for` used only the first value of `Forwarded` and `X-Forwarded-For`. When
  several proxies each append their own header line, later hops were dropped. The chain now
  joins every value in arrival order. Token headers still use their first value only.
- `PathExclusions` matched raw string prefixes, so `/health` also skipped extraction for
  `/healthcare` or `/health-records`. Matching is now by whole path segment, like
  `PathString.StartsWithSegments`; a trailing slash on an exclusion is ignored.
- `MaxValueLength` did not apply to claims decoded from tokens, nor to `Digest` and
  `Content-Digest` values: they were bounded only by `MaxTokenLength`. They are now truncated
  like every other value.
- `Pdnd.Metadata.Diagnostics` dimensioned the client on `pdnd.voucher.clientId` only, while
  PDND vouchers carry `client_id`, so the dimension read `unknown` on real traffic. It now falls
  back to `pdnd.voucher.client_id`, which is also promoted to an activity tag.
- The documentation named the tracking evidence header `Agid-JWT-Tracking-Evidence`, which
  neither AgID nor PDND uses; the header is `Agid-JWT-TrackingEvidence`. Both have always been
  accepted, so no request was affected. The specification also attributed the tracking evidence
  to INTEGRITY_REST_02, which uses `Agid-JWT-Signature`; it belongs to AUDIT_REST_01/02.
- The documentation recommended `AddSource(PdndInstrumentation.ActivitySourceName)`, which has no
  effect: no `ActivitySource` is created and the tags go on the ASP.NET Core request activity.
- The `Pdnd.Metadata.Diagnostics` package shipped the core README instead of its own.
- Documentation diagrams brought in line with the code:
  - `architecture` placed the library before "Validation", contradicting the recommended
    registration after `UseAuthentication()`. It now runs after authentication and before the
    authorization decision. The arrow to Diagnostics was dashed, which the legend defines as out
    of scope; it is solid now.
  - `keyspace` stated that keys are lowercase; several are camelCase (`purposeId`,
    `consumerId`, `trackingEvidence`). Lookups are case-insensitive, which is what it now says.
  - `pipeline` used the non-standard tracking evidence header name and `organizationId`.
  - `conformance` still showed specification 1.1.0 and 30 vectors.

### Security

- The request counter in `Pdnd.Metadata.Diagnostics` used voucher claims as dimensions without
  any bound. The voucher is not verified at that point, so forged tokens with random
  identifiers could create an unbounded number of time series in the metrics backend. Each
  dimension now records at most `PdndDiagnosticsOptions.MaxDistinctValuesPerDimension` distinct
  values (default 100); later values are recorded as `other`.

### Added

- Voucher claims of the current PDND vouchers: `pdnd.voucher.consumerId`, `producerId`,
  `eserviceId`, `descriptorId`, and `cnf.jkt` (DPoP vouchers, RFC 9449 §6.1).
- Audit claims of the tracking evidence (AUDIT_REST_01/02): `pdnd.trackingEvidence.userID`,
  `userLocation`, `LoA`, `purposeId`, and `dnonce`.
- `pdnd.voucher.consumerId`, `eserviceId`, and `descriptorId` are promoted to activity tags.
- `PdndDiagnosticsOptions`, registered with `services.AddPdndDiagnostics(...)`:
  `IncludeConsumerDimensions` (default `true`) and `MaxDistinctValuesPerDimension`
  (default `100`). `PdndInstrumentation.Record(metadata, options)` overload and
  `PdndInstrumentation.RequestCounterName` constant.
- Specification 1.2.0, with conformance vectors `voucher-009` (a DPoP-bound voucher in the current
  PDND shape) and `tracking-003` (audit claims). The corpus goes from 30 to 32 vectors.

### Infrastructure

- Test suite extended from 240 to 327 tests per target framework. New coverage includes every
  `Parse*` and `CaptureRaw*` switch when turned off or on, `PromoteTracingHeaders` and
  `NormalizeForwardedHeaders`, `MaxHeaderValuesPerName`, the middleware's path exclusion and
  fail-soft paths, Minimal API and MVC binding, the full `AddPdndMetadata()` /
  `UsePdndMetadata()` pipeline, concurrent writers, and the counter bound under contention.
  Line coverage is 98.9% and branch coverage 97.1%; the remaining lines are defensive branches
  that the input validation makes unreachable.

- `global.json` pins the build to the .NET 10 SDK (`10.0.100`, rolling forward to the latest
  feature band), so local builds use the same SDK as CI instead of whatever is newest on the
  machine.
- `dotnet test` is invoked with `--solution`. Under Microsoft.Testing.Platform the .NET 10 SDK
  rejects a positional solution argument, so the previous command failed on that SDK.
- `Microsoft.SourceLink.GitHub` is declared once, in `Directory.Build.props`, at 10.0.404. It was
  declared there at 10.0.301 and overridden at 10.0.403 in every project.
- GitHub Actions updated: `actions/checkout` v7, `actions/setup-dotnet` v6,
  `actions/setup-python` v7.
- CodeQL code scanning (`codeql.yml`) for C#, Python, and the GitHub Actions workflows, with the
  `security-and-quality` suite, on push and pull request to `main` and weekly. C# is analysed from
  a real build of both target frameworks rather than extracted without one.
- OpenSSF Scorecard (`scorecard.yml`), published to the Scorecard API and to code scanning, with
  its badge in both READMEs.
- Every GitHub Action in every workflow is pinned to a commit SHA, with the release in a trailing
  comment, so a moved tag cannot change what runs.
- Dependabot (`.github/dependabot.yml`) keeps the pinned actions and the NuGet packages current,
  weekly, grouped into one pull request per ecosystem. Major versions of the packages tied to the
  .NET release (`Microsoft.AspNetCore.OpenApi`, `Microsoft.SourceLink.GitHub`,
  `Swashbuckle.AspNetCore`) are excluded, because adopting them means moving the target framework.
  Commitlint ignores Dependabot commits, whose bodies quote release notes beyond the line limit.
- `SECURITY.md` lists 2.x as the supported line and describes the automated checks.
- Test dependencies updated: `Microsoft.NET.Test.Sdk` 18.12.0, `xunit.v3` 4.0.3. Sample updated:
  `Microsoft.AspNetCore.OpenApi` 10.0.15. The .NET 11 majors (`Microsoft.AspNetCore.OpenApi`
  11.x, `Microsoft.SourceLink.GitHub` 11.x) and `Swashbuckle.AspNetCore` 11.x, which requires
  `Microsoft.OpenApi` 3 and conflicts with ASP.NET Core 10, are deliberately not adopted.

## [2.0.0]

Released from 1.0.0. A 1.0.1 was prepared but never published to NuGet, so its changes are
listed here rather than under a version nobody can install.

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
- Best-effort JWE handling in `JwtDecoder`: five-segment compact tokens now decode their
  protected header and normalize the payload to an empty object rather than breaking extraction.
- `Content-Digest` parsing accepts RFC 9530 structured field parameters such as `;keyid="..."`
  while preserving compatibility with the legacy form.
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
  structurally in CI. 30 vectors across 6 groups, including adversarial inputs.
- Second reference implementation in Python under `spec/reference/python/`, written from the
  specification rather than ported from the C#, and gated in CI. It is not published and has no
  users: it exists to prove the specification is complete enough for an independent
  implementation. It found a genuine gap on its first run, recorded in `spec/CHANGELOG.md`.
- Specification changelog under `spec/CHANGELOG.md`, versioned independently of the packages.
- Four documentation diagrams under `assets/diagrams/`, each with a light and a dark variant:
  architectural placement, the extraction pipeline, the canonical key namespace, and the
  conformance model. All are produced by a single generator so that grid, palette, and
  typography cannot drift between figures, and CI fails if the committed output no longer
  matches it.
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

- The test project runs on Microsoft.Testing.Platform. The .NET 10 SDK no longer executes MTP
  test projects through the VSTest target, which broke `dotnet test` outright. The runner is
  now selected in `global.json`, the test project opts in explicitly, and the VSTest-only
  packages and command-line options are removed. Coverage collection is dropped for the moment:
  `--collect` has no equivalent under MTP, where each collector contributes its own option.
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
