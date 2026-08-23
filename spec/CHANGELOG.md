# Specification changelog

This file tracks the PDND metadata extraction specification and its conformance corpus. It is
versioned independently of any implementation: the `Pdnd.Metadata` packages have their own
version and their own changelog.

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
