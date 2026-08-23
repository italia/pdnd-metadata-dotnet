# PDND Metadata Extraction Specification

**Version 1.1.0** · MIT · Not an official AgID or PagoPA document

## What this is

A naming convention and a conformance corpus for extracting metadata from inbound PDND requests.

Every organisation that exposes an e-service on PDND Interoperabilità writes the same code: read
`Authorization`, decode the voucher, pull out `purposeId` and `organizationId`, read the tracking
evidence, read the DPoP proof, read `Digest` or `Content-Digest`, read `Agid-JWT-Signature`. Each
one invents its own names for the results, so two services in the same organisation log the same
fact under two different keys, and no dashboard, no SIEM rule, and no audit query works across
both.

The AgID guidelines specify what goes on the wire. They do not specify what an implementation
should call these things once extracted, because that is outside their scope. This document fills
that gap, and nothing more.

## What this is not

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="../assets/diagrams/pipeline-dark.svg">
    <img src="../assets/diagrams/pipeline.svg" alt="Extraction pipeline">
  </picture>
</p>

This specification covers extraction only.

It does not describe how to verify a signature, how to check a digest against a body, how to
detect a replayed `jti`, or when to reject a request. Those are validation concerns, and an
implementation that follows this document has performed no security check whatsoever. The
division is deliberate and is stated again in every conformance vector that touches it: see
`adversarial-006`, which requires an implementation to faithfully report `alg: none` rather than
reject it, precisely because rejection belongs to a layer this document does not describe.

## Contents

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="../assets/diagrams/conformance-dark.svg">
    <img src="../assets/diagrams/conformance.svg" alt="Conformance model">
  </picture>
</p>

| File | Purpose |
|------|---------|
| `PDND_METADATA_SCHEMA.md` | The canonical key vocabulary, with the source claim for each key |
| `CHANGELOG.md` | Specification history, versioned independently of any implementation |
| `vectors/vectors.json` | Conformance corpus: inbound headers paired with expected keys |
| `vectors/validate.py` | Structural validation of the corpus itself |
| `reference/python/` | Second implementation, written from this document rather than ported |

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="../assets/diagrams/keyspace-dark.svg">
    <img src="../assets/diagrams/keyspace.svg" alt="Canonical key namespace">
  </picture>
</p>

## Using the corpus

Each vector pairs a set of inbound HTTP headers with the canonical keys an implementation is
expected to produce. Keys listed under `__absent__` must not be produced.

```json
{
  "id": "signature-001",
  "group": "signature",
  "description": "ModI signed_headers as an array of single-property objects.",
  "request": { "headers": { "Agid-JWT-Signature": "eyJ..." } },
  "expect": {
    "pdnd.signature.signed_headers": "digest, content-type",
    "pdnd.signature.signed_header.digest": "SHA-256=RBNvo1WzZ4oRRq0W9+hknpT7T8If536DEMBg9hyq/4o="
  }
}
```

The corpus is language neutral. A conformance runner is roughly forty lines in any language: read
the file, feed the headers to the implementation, compare the produced keys. Two runners exist: `reference/python/conformance.py`, which is the shortest complete example, and
`tests/Pdnd.Metadata.Tests/Conformance/ConformanceVectorTests.cs`, which wires the corpus into a
test suite.

```
$ cd reference/python && python3 conformance.py
30/30 vectors passed
```

A header value is either a string or, when the vector exercises repetition, an array of strings.

Groups: `voucher`, `signature`, `digest`, `dpop`, `tracking-evidence`, `adversarial`.

The `adversarial` group is the part most worth running. It covers inputs that a naive
implementation accepts and should not: four-segment tokens, standard-Base64 characters where
Base64Url is required, invalid UTF-8 that a lenient decoder silently replaces with U+FFFD, and
`alg: none`.

## Why a second implementation exists

`reference/python/` is not a product. It is a few hundred lines written from this document,
deliberately without reading the .NET source, for one reason: a specification with a single
implementation is that implementation's documentation, and the gaps are invisible because the
code silently answers every question the prose forgot to.

It earned its place on the first run. Version 1.0.0 did not say what happens when a token's JOSE
header decodes but its payload does not. The two implementations disagreed, one emitting the
header claims and the other emitting nothing. That is now specified, and `adversarial-008` pins
it.

## Design decisions worth knowing

**Header names are lowercased.** HTTP header names are case-insensitive, so preserving the casing
a caller happened to send would make keys unstable across callers.

**`signed_headers` is an array, not a scalar.** In the AgID integrity patterns the claim carries
the signed headers together with their values, so that the provider can compare them against what
arrived on the wire. The canonical encoding is an array of single-property objects. Two
non-conformant shapes are also accepted and normalized, because they have been observed and
dropping them silently is worse than normalizing them. This is documented as a specification
requirement because it is the single most common place an implementation goes wrong: reading the
claim with a string-only reader produces no key at all, and no error, on every conformant request.

**Absence and emptiness are indistinguishable, except for truncation.** If a claim is missing, no
key is produced. If a claim is present but empty, still no key. The one case where the difference
matters is header capture stopping at a configured bound, which emits `metadata.truncated` so
that a consumer can tell an incomplete snapshot from a sparse one.

**A token is decoded as a unit.** If any segment required for extraction fails to decode, the
token yields no keys at all. A three-segment token needs both its JOSE header and its payload;
a five-segment JWE needs only its header, because the payload is encrypted. Emitting header
claims from a token whose payload is unreadable would tell a consumer that a well-formed token
arrived when it did not. The length bound is checked before any decoding, so an oversized token
also yields nothing rather than partial claims.

**Order is preserved, never sorted.** The names in `pdnd.signature.signed_headers` follow the
order of the claim, and the values in a normalized `aud` follow the order of the array. Sorting
would be a reasonable independent choice, which is exactly why the specification has to say
which one it is.

**Multi-valued keys are joined with a comma and a single space.** When a claim carries several
values, the canonical form is `first, second`. Not `first,second`.

**A repeated header uses its first value.** HTTP permits a header to appear more than once. The
first occurrence wins and the rest are ignored.

**Credentials are never keys.** No canonical key ever carries a voucher, a proof, or a signed
blob. Keys carry claims *about* those artifacts. An implementation that stores the raw
`Authorization` header under any key does not conform.

## Declaring conformance

An implementation declares conformance by naming the specification version it targets and the
result of running the corpus:

> Conforms to PDND Metadata Extraction 1.1.0. Passes 30 of 30 conformance vectors.

There is no certification, no authority, and no badge to apply for. The claim is verifiable by
anyone who runs the corpus, which is the only enforcement mechanism a specification of this size
should have.

Two rules make the claim meaningful:

- **All vectors, or none.** There are no optional groups. An implementation that skips the
  `adversarial` group has skipped the part that matters.
- **The corpus is not the test suite.** Passing 30 of 30 says the implementation agrees with
  this specification on 30 inputs. It says nothing about the inputs nobody has written a vector
  for yet, which is why the contribution guidance below asks for vectors rather than prose.

## Versioning

The key vocabulary follows semantic versioning independently of any implementation.

| Change | Bump |
| --- | --- |
| Removing or renaming a canonical key | Major |
| Changing the value produced for an existing key | Major |
| Adding a canonical key | Minor |
| Adding a conformance vector that no conforming implementation would fail | Minor |
| Adding a vector that pins previously unstated behaviour | Minor, and noted in the changelog |
| Correcting a description without changing behaviour | Patch |

**Deprecation.** A key scheduled for removal is marked deprecated in the schema for at least one
minor version before the major release that removes it, and the entry names its replacement. A
key is never silently repurposed: if the meaning changes, the old name is retired and a new one
is introduced.

**Relationship to the platform.** This specification names things; it does not define them. If
PagoPA or AgID change the wire format, the vocabulary follows, and that is a minor version when
a claim is added and a major one when a claim disappears. Where this specification and the
official documentation disagree, the official documentation is right and this is a defect.

## Contributing

Vectors are more valuable than prose. If you have a token shape from a real integration that this
corpus does not cover, open a pull request adding it, with the values redacted or replaced by
synthetic equivalents. Vectors that document a shape this implementation currently gets wrong are
especially welcome, and will be merged as failing tests before the fix.
