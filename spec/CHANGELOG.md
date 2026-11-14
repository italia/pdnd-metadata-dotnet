# Specification changelog

This file tracks the PDND metadata extraction specification and its conformance corpus. It is
versioned independently of any implementation: the `Pdnd.Metadata` packages have their own
version and their own changelog.

## [1.2.0]

### Added

- **DPoP-bound vouchers.** The voucher is read from `Authorization: DPoP <JWT>` as well as from
  `Authorization: Bearer <JWT>` (RFC 9449 §7.1). Until now a DPoP-bound request produced the
  `pdnd.dpop.*` keys but no `pdnd.voucher.*` key at all, so purpose and organization were missing
  in exactly the flow with the strongest binding.
- **Claims of the vouchers PDND currently issues.** `pdnd.voucher.consumerId`, `producerId`,
  `eserviceId`, `descriptorId`, and `cnf.jkt`, as they appear in the voucher examples of the PDND
  operating manual. Until now the identity of the fruitore was read only from `organizationId`,
  which those vouchers do not carry.
- `pdnd.voucher.digest.alg` and `pdnd.voucher.digest.value`, the AUDIT_REST_02 hash of the
  tracking evidence, are now part of the specification. Implementations already produced them.
- **Audit claims of the tracking evidence.** `pdnd.trackingEvidence.userID`, `userLocation`, and
  `LoA`, the claims the AgID AUDIT_REST patterns suggest, plus `purposeId` and `dnonce`.
- Two vectors, taking the corpus from 30 to 32:
  - `voucher-009`, a DPoP-bound voucher in the current PDND shape, under the `DPoP` scheme
  - `tracking-003`, a tracking evidence carrying the audit claims

### Changed

- `pdnd.voucher.organizationId`, `pdnd.voucher.clientId`, and `pdnd.voucher.dnonce` are moved to a
  legacy section. They are still produced when present.
- The tracking evidence header is named `Agid-JWT-TrackingEvidence`, as in the AgID guidelines
  and the PDND manual. The hyphenated `Agid-JWT-Tracking-Evidence`, previously presented as the
  primary name, is not used by either and remains accepted for compatibility only.
- The pattern table attributed the tracking evidence to INTEGRITY_REST_02. The tracking evidence
  belongs to AUDIT_REST_01/02; INTEGRITY_REST_02 uses `Agid-JWT-Signature`.
- The configuration reference had not followed the 2.0.0 defaults: `CaptureAllHeaders` is
  `false`, the deny list has ten entries, and `MaxHeaderCount` and `PathExclusions` were missing.

- `net.forwarded_for` is built from every value of `Forwarded` (or, failing that,
  `X-Forwarded-For`), across repeated header lines, in arrival order. Proxies that append their
  own header line rather than extend the existing one were silently dropped from the chain.
- Claim values decoded from tokens are bounded by `MaxValueLength`, like every other value.

## [1.1.0]

### Added

- **Reference implementation in Python** under `reference/python/`. It is not a published
  package and has no users. It exists to prove the specification is complete enough for an
  independent implementation, and it earned its place immediately: see the fixed entry below.
- **Conformance declaration.** How an implementation states which version it targets and what
  the corpus run produced, and why there are no optional vector groups.
- **Deprecation policy.** What triggers each kind of version bump, how a key is retired, and
  what happens when the platform itself changes.
- Eight conformance vectors, taking the corpus from 22 to 30:
  - `voucher-005`, a repeated header uses its first value
  - `voucher-006`, the `Bearer` scheme is matched case-insensitively
  - `voucher-007`, a five-segment JWE voucher yields JOSE header claims only
  - `voucher-008`, non-ASCII claim values are preserved exactly
  - `signature-006`, signed header names keep claim order rather than being sorted
  - `signature-007`, a repeated signature header uses its first value
  - `adversarial-007`, a token beyond the length bound yields nothing
  - `adversarial-008`, a token whose payload does not decode yields nothing at all

### Fixed

- **A token is now specified to decode as a unit.** Version 1.0.0 did not say what happens when
  a JOSE header decodes but the payload does not. The two implementations disagreed: one emitted
  the header claims, the other emitted nothing. This was found by writing the second
  implementation and running the corpus, which is the entire reason the second implementation
  exists. The specified behaviour is all-or-nothing, because emitting header claims from an
  unreadable token tells a consumer that a well-formed token arrived when it did not.

### Changed

- **Order is now normative.** Signed header names follow the order of the claim, and normalized
  audiences follow the order of the array. Neither was stated, and alphabetical ordering was a
  reasonable independent reading.
- **The multi-value separator is now normative**: a comma and a single space.
- **Repeated headers are now specified**: the first occurrence wins.

None of the three changes above alters the behaviour of either implementation. They record
decisions that were previously implicit in one implementation's source, which is the definition
of a specification gap rather than a specification.

## [1.0.0]

Initial publication. Canonical key vocabulary covering voucher claims, tracking evidence, DPoP
proofs, message digests, and the AgID ModI integrity signature header, together with 22
conformance vectors across six groups.
