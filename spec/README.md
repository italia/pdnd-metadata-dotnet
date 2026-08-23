# PDND Metadata Extraction Specification

**Version 1.0.0** · MIT · Not an official AgID or PagoPA document

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

This specification covers extraction only.

It does not describe how to verify a signature, how to check a digest against a body, how to
detect a replayed `jti`, or when to reject a request. Those are validation concerns, and an
implementation that follows this document has performed no security check whatsoever. The
division is deliberate and is stated again in every conformance vector that touches it: see
`adversarial-006`, which requires an implementation to faithfully report `alg: none` rather than
reject it, precisely because rejection belongs to a layer this document does not describe.

## Contents

| File | Purpose |
|------|---------|
| `PDND_METADATA_SCHEMA.md` | The canonical key vocabulary, with the source claim for each key |
| `vectors/vectors.json` | Conformance corpus: inbound headers paired with expected keys |
| `vectors/validate.py` | Structural validation of the corpus itself |

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
the file, feed the headers to the implementation, compare the produced keys. The .NET reference
runner is at `tests/Pdnd.Metadata.Tests/Conformance/ConformanceVectorTests.cs`.

Groups: `voucher`, `signature`, `digest`, `dpop`, `tracking-evidence`, `adversarial`.

The `adversarial` group is the part most worth running. It covers inputs that a naive
implementation accepts and should not: four-segment tokens, standard-Base64 characters where
Base64Url is required, invalid UTF-8 that a lenient decoder silently replaces with U+FFFD, and
`alg: none`.

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

**Credentials are never keys.** No canonical key ever carries a voucher, a proof, or a signed
blob. Keys carry claims *about* those artifacts. An implementation that stores the raw
`Authorization` header under any key does not conform.

## Versioning

The key vocabulary follows semantic versioning independently of any implementation. Removing or
renaming a canonical key is a major change. Adding one is a minor change. Corrections to
descriptions are patch changes.

## Contributing

Vectors are more valuable than prose. If you have a token shape from a real integration that this
corpus does not cover, open a pull request adding it, with the values redacted or replaced by
synthetic equivalents. Vectors that document a shape this implementation currently gets wrong are
especially welcome, and will be merged as failing tests before the fix.
