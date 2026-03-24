# Pdnd.Metadata

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![issues - pdndmetadata](https://img.shields.io/github/issues/italia/pdnd-metadata-dotnet)](https://github.com/italia/pdnd-metadata-dotnet/issues)
[![stars - pdndmetadata](https://img.shields.io/github/stars/italia/pdnd-metadata-dotnet?style=social)](https://github.com/italia/pdnd-metadata-dotnet)
[![EN](https://img.shields.io/badge/lang-en-blue)](./README.EN.md)
[![IT](https://img.shields.io/badge/lang-it-green)](./README.md)

**Pdnd.Metadata** is a lightweight, multi-target .NET library (`net8.0` / `net10.0`) designed to extract **request metadata** from inbound HTTP calls in a consistent, HTTP-transport-agnostic format, with dedicated support for **PDND** scenarios (voucher, tracking evidence, digest, DPoP) and standard correlation/tracing signals.

The library targets a very practical need: when you expose an e-service as a **provider (erogatore)**, you often need to understand *who is calling*, *with what PDND context*, and *how the call can be correlated and audited*, without sprinkling ad-hoc header parsing across controllers, minimal APIs, and middleware.

## Contents

| Section | What you’ll find |
|---|---|
| [Why this library exists](#why-this-library-exists) | The problem it solves in real provider services |
| [PDND overview](#pdnd-overview) | What PDND is and why the inbound request carries tokens |
| [What Pdnd.Metadata does](#what-pdndmetadata-does) | Responsibilities, data model, and boundaries |
| [Extracted fields](#extracted-fields) | Canonical keys produced by the extractor (generic + PDND-specific) |
| [Safety model](#safety-model) | What is never stored, fail-soft behavior, recommended defaults |
| [Packages layout](#packages-layout) | Core vs ASP.NET Core integration |
| [Quick start (ASP.NET Core)](#quick-start-aspnet-core) | Registration, middleware, and consuming metadata |
| [Recommended production configuration](#recommended-production-configuration) | Conservative posture and governance notes |
| [Sample API](#sample-api) | Endpoints used to verify extraction locally |
| [What this library does not do](#what-this-library-does-not-do) | Explicit non-goals (validation, enforcement, PDND API calls) |
| [Canonical Keys Schema](#canonical-keys-schema) | Full reference of all extracted metadata keys |
| [Official PDND references](#official-pdnd-references) | Links to official documentation |
| [Author and maintainer](#author-and-maintainer) | Project ownership and maintenance |
| [Contributing](#contributing) | How to contribute to the project |
| [License](#license) | License information |
| [Contact](#contact) | Contact information |

## Why this library exists

In provider services, metadata extraction tends to grow organically:
- different teams parse different headers differently,
- correlation IDs get duplicated or overwritten,
- PDND tokens are treated as raw strings (with the risk of accidental logs),
- and the same logic is re-implemented in controllers, minimal APIs, or gateway filters.

**Pdnd.Metadata** standardizes this work:
- it collects metadata into a single, structured snapshot (`PdndCallerMetadata`),
- it extracts PDND-related information in a best-effort, non-blocking way,
- it makes “safe defaults” achievable (no raw `Authorization`, no signed blobs by default),
- and it keeps ASP.NET Core integration separate from the core extraction logic.

## PDND overview

**PDND** enables secure and traceable exchange of services/data between administrations. In the most common interaction pattern, the consumer calls the provider by sending a **voucher** (JWT) in:

- `Authorization: Bearer <voucher>`

Official reference: voucher usage. ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/riferimenti-tecnici/utilizzare-i-voucher))

For some e-services, additional information is carried through a **Tracking Evidence** token in a dedicated header. Official reference: “voucher bearer … con informazioni aggiuntive”. ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/tutorial/tutorial-per-il-fruitore/come-richiedere-un-voucher-bearer-per-le-api-di-un-erogatore-con-informazioni-aggiuntive))

The library also captures standard correlation/tracing signals commonly used in modern HTTP services (e.g., W3C Trace Context). PDND provides guidance on tracing/observability practices for interoperability monitoring. Official reference: tracing manual. ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-tracing))

Finally, the platform includes **DPoP** flows (proof-of-possession). Official reference: DPoP deep dive. ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/riferimenti-tecnici/utilizzare-i-voucher/approfondimento-su-dpop))

## What Pdnd.Metadata does

At runtime, the library builds a **metadata snapshot** representing what can be safely and usefully inferred from the inbound request:

1. **Generic request metadata**
   - method/scheme/host/path/query
   - remote/local IP and ports
   - normalized forwarded chain (best-effort)
   - correlation and tracing hints (W3C trace context + common request ids)
   - selected headers (configurable capture rules)

2. **PDND-aware extraction (best-effort)**
   - voucher: parse the JWT JOSE header (alg/kid/typ) and payload, extract standard and PDND-specific claims
   - tracking evidence: parse the token header/payload and extract selected fields
   - digest: parse the `Digest` header value into a normalized (alg, value) pair
   - content-digest: parse the `Content-Digest` header (RFC 9530) into a normalized (alg, value) pair
   - DPoP: parse the proof token header/payload and extract selected fields (incl. ath, nonce per RFC 9449)
   - signature: parse the `Agid-JWT-Signature` header for request integrity fields

3. **Fail-soft behavior**
   - missing headers are ignored
   - parsing errors are swallowed
   - oversized tokens are skipped (guard-rail through `MaxTokenLength`)

The output is designed to be stable and easy to integrate with logging, auditing, or internal tracing dashboards without leaking secrets.

## Extracted fields

The snapshot is a `PdndCallerMetadata` containing items indexed by canonical keys.

### Generic keys

- `http.method`, `http.scheme`, `http.host`, `http.path`, `http.query`
- `net.remote_ip`, `net.remote_port`, `net.local_ip`, `net.local_port`, `net.forwarded_for`
- `correlation.id`
- `trace.traceparent`, `trace.tracestate`, `trace.baggage`
- `http.header.<lowercase-name>` (only if header capture is enabled and the header is not denied)

> Note on forwarded headers: values like `Forwarded` / `X-Forwarded-For` are trustworthy only if they are set by a trusted reverse proxy / API gateway. In open networks they are user-controllable and must not be treated as authoritative identity signals.

### PDND keys

#### Voucher (from `Authorization: Bearer ...`)
- `pdnd.voucher.alg`, `pdnd.voucher.kid`, `pdnd.voucher.typ` (JOSE header)
- `pdnd.voucher.iss`
- `pdnd.voucher.sub`
- `pdnd.voucher.aud` (normalized string; can originate from a JWT array)
- `pdnd.voucher.jti`
- `pdnd.voucher.iat`, `pdnd.voucher.nbf`, `pdnd.voucher.exp` (stored as strings; typically epoch seconds)
- `pdnd.voucher.purposeId` (if present)
- `pdnd.voucher.clientId`, `pdnd.voucher.client_id` (if present)
- `pdnd.voucher.organizationId` (PDND fruitore organization, if present)
- `pdnd.voucher.dnonce` (anti-replay nonce, if present)

Reference: voucher usage and semantics. ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/riferimenti-tecnici/utilizzare-i-voucher))

#### Tracking Evidence (from `Agid-JWT-Tracking-Evidence` / `AgID-JWT-TrackingEvidence`)
- `pdnd.trackingEvidence.alg`, `pdnd.trackingEvidence.kid`, `pdnd.trackingEvidence.typ`
- `pdnd.trackingEvidence.iss`, `pdnd.trackingEvidence.sub`, `pdnd.trackingEvidence.jti` (when present)
- `pdnd.trackingEvidence.aud` (when present; may be comma-separated)
- `pdnd.trackingEvidence.iat`, `pdnd.trackingEvidence.nbf`, `pdnd.trackingEvidence.exp` (when present)

**Compatibility note:** in PDND documentation the header name appears in two variants (`Agid-JWT-Tracking-Evidence` and `AgID-JWT-TrackingEvidence`). The extractor supports both for interoperability.

Reference: additional-information flows. ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/tutorial/tutorial-per-il-fruitore/come-richiedere-un-voucher-bearer-per-le-api-di-un-erogatore-con-informazioni-aggiuntive))

#### Digest (from `Digest`)
- `pdnd.digest.alg`
- `pdnd.digest.value`

Reference: digest notes in voucher FAQ. ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/PDND-Interoperability-Operating-Manual/technical-references/utilizzare-i-voucher/faqs))

#### DPoP (from `DPoP`)
- `pdnd.dpop.alg`, `pdnd.dpop.kid`, `pdnd.dpop.typ`
- `pdnd.dpop.htm`, `pdnd.dpop.htu`, `pdnd.dpop.jti`, `pdnd.dpop.iat`, `pdnd.dpop.exp` (when present)
- `pdnd.dpop.ath` (access token hash, RFC 9449 u00a74.2)
- `pdnd.dpop.nonce` (server-provided nonce, RFC 9449 u00a74.3)

Reference: DPoP deep dive. ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/riferimenti-tecnici/utilizzare-i-voucher/approfondimento-su-dpop))

#### Content-Digest (from `Content-Digest`, RFC 9530)
- `pdnd.content_digest.alg`
- `pdnd.content_digest.value`

RFC 9530 replaces the legacy `Digest` header with `Content-Digest` using structured field dictionary format (`alg=:base64value:`). The library supports both.

#### Agid-JWT-Signature (from `Agid-JWT-Signature`)
- `pdnd.signature.alg`, `pdnd.signature.kid`, `pdnd.signature.typ` (JOSE header)
- `pdnd.signature.iss`, `pdnd.signature.sub`, `pdnd.signature.jti` (when present)
- `pdnd.signature.aud` (when present; may be comma-separated)
- `pdnd.signature.iat`, `pdnd.signature.exp` (when present)
- `pdnd.signature.signed_headers` (digest of signed headers for integrity)

Used in PDND pattern INTEGRITY_REST_01 for request signing.

## Safety model

### What is never stored (raw)
By default, the library never stores:
- `Authorization` (raw)
- `Cookie`, `Set-Cookie`

This prevents accidental persistence or logging of secrets.

### Signed blobs (raw)
By default, the library does **not** store raw values for:
- Tracking Evidence header
- DPoP header

Instead, it parses them best-effort and stores selected fields under canonical `pdnd.*` keys. This keeps output inspectable while reducing leakage risk.

### Fail-soft behavior
- Any parsing error is swallowed; request processing continues.
- Tokens longer than `MaxTokenLength` are skipped.
- Missing PDND headers do not produce errors.

### Operational note
Capturing headers can still collect sensitive data if your service (or gateways) inject domain-specific headers that contain personal information. For production use, a strict allow-list is recommended (see below).

## Packages layout

Both packages target **`net8.0`** (LTS) and **`net10.0`**, so they work on the .NET versions most commonly used in PA production environments.

- `Pdnd.Metadata`  
  Core abstractions and extraction pipeline, including PDND parsing utilities.

- `Pdnd.Metadata.AspNetCore`  
  ASP.NET Core integration: middleware, accessors, and minimal API binding types.

## Quick start (ASP.NET Core)

### 1) Register services (production-first)

Prefer an allow-list approach: capture only what you need (trace/correlation/forwarded + explicit PDND headers if you decide to persist them as raw headers).

```csharp
builder.Services.AddPdndMetadata(options =>
{
    options.CaptureAllHeaders = false;

    // Recommended: allow-list only non-sensitive headers used for correlation/tracing
    // (and any other header you explicitly govern).
    options.HeaderAllowList.Add("traceparent");
    options.HeaderAllowList.Add("tracestate");
    options.HeaderAllowList.Add("baggage");
    options.HeaderAllowList.Add("x-request-id");
    options.HeaderAllowList.Add("x-correlation-id");
    options.HeaderAllowList.Add("forwarded");
    options.HeaderAllowList.Add("x-forwarded-for");

    // PDND parsing (best-effort, no validation)
    // Parsing reads the relevant headers even if you are not capturing raw headers.
    options.ParsePdndVoucherFromAuthorizationBearer = true;
    options.ParsePdndTrackingEvidence = true;
    options.ParseDpopHeader = true;
    options.ParseDigestHeader = true;
    options.ParseContentDigestHeader = true;
    options.ParseAgidJwtSignature = true;

    // Do not store signed blobs
    options.CaptureRawTrackingEvidenceHeader = false;
    options.CaptureRawDpopHeader = false;
    options.CaptureRawSignatureHeader = false;

    // Guard-rail
    options.MaxTokenLength = 16_384;
});
```

### Demo mode (local only)

If you want to inspect headers during local development, you can temporarily enable full capture:

```csharp
builder.Services.AddPdndMetadata(options =>
{
    options.CaptureAllHeaders = true;

    options.ParsePdndVoucherFromAuthorizationBearer = true;
    options.ParsePdndTrackingEvidence = true;
    options.ParseDpopHeader = true;
    options.ParseDigestHeader = true;
    options.ParseContentDigestHeader = true;
    options.ParseAgidJwtSignature = true;

    options.CaptureRawTrackingEvidenceHeader = false;
    options.CaptureRawDpopHeader = false;
    options.CaptureRawSignatureHeader = false;

    options.MaxTokenLength = 16_384;
});
```

### 2) Add middleware

Place it before endpoint mapping so every request gets a snapshot.

```csharp
app.UsePdndMetadata();
```

### 3) Consume metadata (Controllers)

```csharp
[HttpGet("/controller/metadata")]
public IActionResult Get([FromServices] IPdndMetadataAccessor accessor)
{
    var md = accessor.Current;

    return Ok(new
    {
        correlationId = md?.GetFirstValue(PdndMetadataKeys.CorrelationId),
        voucherIss = md?.GetFirstValue(PdndMetadataKeys.PdndVoucherIss),
        dpopHtu = md?.GetFirstValue(PdndMetadataKeys.PdndDpopHtu)
    });
}
```

### 4) Consume metadata (Minimal APIs)

```csharp
app.MapGet("/minimal/pdnd", (PdndCallerMetadataParameter pdnd) =>
{
    var md = pdnd.Value;

    return Results.Ok(new
    {
        voucher = new
        {
            iss = md.GetFirstValue(PdndMetadataKeys.PdndVoucherIss),
            aud = md.GetFirstValue(PdndMetadataKeys.PdndVoucherAud),
            purposeId = md.GetFirstValue(PdndMetadataKeys.PdndVoucherPurposeId)
        },
        trackingEvidence = new
        {
            kid = md.GetFirstValue(PdndMetadataKeys.PdndTrackingKid),
            jti = md.GetFirstValue(PdndMetadataKeys.PdndTrackingJti)
        },
        dpop = new
        {
            htm = md.GetFirstValue(PdndMetadataKeys.PdndDpopHtm),
            htu = md.GetFirstValue(PdndMetadataKeys.PdndDpopHtu)
        },
        digest = new
        {
            alg = md.GetFirstValue(PdndMetadataKeys.PdndDigestAlg)
        }
    });
});
```

## Recommended production configuration

For production services, it’s usually better to explicitly decide *which headers you want to capture* rather than collecting everything and filtering later.

A conservative approach:
- `CaptureAllHeaders = false`
- keep a strict `HeaderAllowList` (trace + correlation + forwarded + only what you explicitly govern)
- keep `CaptureRawTrackingEvidenceHeader = false`, `CaptureRawDpopHeader = false`, and `CaptureRawSignatureHeader = false`
- consider disabling `CaptureRawDigestHeader` unless you actually need it
- in logging/auditing pipelines, avoid persisting the full `items` map unless you are confident about governance; prefer logging only canonical keys you whitelist

The library already enforces the most important rule by default: raw `Authorization` is never stored.

## Sample API

The sample project is meant to let you validate integration quickly, without logging raw tokens.

- `GET /minimal/pdnd`  
  Returns voucher / trackingEvidence / dpop / digest / contentDigest / signature sections.

- `GET /minimal/sanity`  
  Verifies that raw `Authorization`, raw `DPoP`, raw tracking evidence, and raw `Agid-JWT-Signature` headers are not captured.

Example request (fake tokens are sufficient for extraction checks):

```bash
curl \
  -H "Authorization: Bearer <jwt>" \
  -H "Agid-JWT-Tracking-Evidence: <jws>" \
  -H "DPoP: <dpop-jws>" \
  -H "Digest: SHA-256=<base64>" \
  -H "Content-Digest: sha-256=:<base64>:" \
  -H "Agid-JWT-Signature: <jws>" \
  http://localhost:5041/minimal/pdnd
```

## What this library does not do

This is intentionally an extraction layer, not a security enforcement layer.

- It does **not** validate JWT/JWS signatures.
- It does **not** enforce PDND authorization rules.
- It does **not** call PDND APIs (catalog/registry/auth services) to enrich metadata.
- It does **not** log tokens. If you add logging, keep it limited to canonical `pdnd.*` keys and avoid raw headers.

If you need validation/enforcement, place it in your auth layer (gateway/service middleware) and use Pdnd.Metadata strictly as an observability/diagnostics/audit-friendly snapshot.

## Canonical Keys Schema

For a complete, structured reference of all 55+ canonical metadata keys extracted by this library, see the **[PDND Metadata Schema](./src/PDND_METADATA_SCHEMA.md)** document.

The schema covers:
- All PDND interoperability patterns (ID_AUTH, INTEGRITY, AUDIT)
- JWT/JWS field mapping for each token type
- Configuration options reference
- Security considerations

This schema is intended as a **community reference** to standardize PDND metadata extraction across .NET implementations.

## Official PDND references

- PDND Interoperabilità – Guides hub ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides))
- Voucher (usage) ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/riferimenti-tecnici/utilizzare-i-voucher))
- Voucher with additional information (Tracking Evidence) ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/tutorial/tutorial-per-il-fruitore/come-richiedere-un-voucher-bearer-per-le-api-di-un-erogatore-con-informazioni-aggiuntive))
- Voucher FAQ / Digest field notes ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/PDND-Interoperability-Operating-Manual/technical-references/utilizzare-i-voucher/faqs))
- Tracing – Manuale Operativo ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-tracing))
- DPoP deep dive ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/riferimenti-tecnici/utilizzare-i-voucher/approfondimento-su-dpop))
- Release notes ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/release-note/2025))

## Author and maintainer
| [![Francesco Del Re](https://github.com/engineering87.png?size=100)](https://github.com/engineering87) |
| ------------------------------------------------------------------------------------------------------ |
| **Francesco Del Re** |
| Author & Maintainer |

## Contributing
Thank you for considering to help out with the source code!
If you'd like to contribute, please fork, fix, commit and send a pull request for the maintainers to review and merge into the main code base.

**Getting started with Git and GitHub**

 * [Setting up Git](https://docs.github.com/en/get-started/getting-started-with-git/set-up-git)
 * [Fork the repository](https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/working-with-forks/fork-a-repo)
 * [Open an issue](https://github.com/italia/pdnd-metadata-dotnet/issues) if you encounter a bug or have a suggestion for improvements/features

## License
Pdnd.Metadata source code is available under MIT License, see license in the source.

## Contact
Please contact at francesco.delre[at]protonmail.com for any details.