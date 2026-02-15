# Pdnd.Metadata

**Pdnd.Metadata** is a lightweight .NET library designed to extract **request metadata** from inbound HTTP calls in a consistent, transport-agnostic format, with dedicated support for **PDND** scenarios (voucher, tracking evidence, digest, DPoP) and standard correlation/tracing signals.

The library targets a very practical need: when you expose an e-service as a **provider (erogatore)**, you often need to understand *who is calling*, *with what PDND context*, and *how the call can be correlated and audited*, without sprinkling ad-hoc header parsing across controllers, minimal APIs, and middleware.

## Contents

| Section | What you’ll find |
|---|---|
| [Why this library exists](#why-this-library-exists) | The problem it solves in real provider services |
| [PDND overview](#pdnd-interoperabilita-overview) | What PDND is and why the inbound request carries tokens |
| [What Pdnd.Metadata does](#what-pdndmetadata-does) | Responsibilities, data model, and boundaries |
| [Extracted fields](#extracted-fields) | Canonical keys produced by the extractor (generic + PDND-specific) |
| [Safety model](#safety-model) | What is never stored, fail-soft behavior, recommended defaults |
| [Packages layout](#packages-layout) | Core vs ASP.NET Core integration |
| [Quick start (ASP.NET Core)](#quick-start-aspnet-core) | Registration, middleware, and consuming metadata |
| [Recommended production configuration](#recommended-production-configuration) | Conservative posture and governance notes |
| [Sample API](#sample-api) | Endpoints used to verify extraction locally |
| [What this library does not do](#what-this-library-does-not-do) | Explicit non-goals (validation, enforcement, PDND API calls) |
| [Official PDND references](#official-pdnd-references) | Links to official documentation |

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

Official reference: voucher usage. ([developer.pagopa.it](https://www.developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/riferimenti-tecnici/utilizzare-i-voucher?utm_source=chatgpt.com))

For some e-services, additional information is carried through a **Tracking Evidence** token in a dedicated header. Official reference: “voucher bearer … con informazioni aggiuntive”. ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/tutorial/tutorial-per-il-fruitore/come-richiedere-un-voucher-bearer-per-le-api-di-un-erogatore-con-informazioni-aggiuntive?utm_source=chatgpt.com))

PDND guidance also includes **tracing** (W3C trace context) as part of observability and monitoring practices. Official reference: tracing technical reference. ([developer.pagopa.it](https://www.developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/riferimenti-tecnici/tracing?utm_source=chatgpt.com))

Finally, the platform evolves over time and includes **DPoP** flows (proof-of-possession) in its documentation and release notes. Official references: PDND guides hub and release notes. ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides?utm_source=chatgpt.com)) ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/release-note/2025?utm_source=chatgpt.com))

## What Pdnd.Metadata does

At runtime, the library builds a **metadata snapshot** representing what can be safely and usefully inferred from the inbound request:

1. **Generic request metadata**
   - method/scheme/host/path/query
   - remote/local IP and ports
   - normalized forwarded chain (best-effort)
   - correlation and tracing hints (W3C trace context + common request ids)
   - selected headers (configurable capture rules)

2. **PDND-aware extraction (best-effort)**
   - voucher: parse the JWT payload and extract selected claims
   - tracking evidence: parse the token header/payload and extract selected fields
   - digest: parse the header value into a normalized (alg, value) pair
   - DPoP: parse the proof token header/payload and extract selected fields

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

### PDND keys

#### Voucher (from `Authorization: Bearer ...`)
- `pdnd.voucher.iss`
- `pdnd.voucher.sub`
- `pdnd.voucher.aud` (normalized string; can originate from a JWT array)
- `pdnd.voucher.jti`
- `pdnd.voucher.iat`, `pdnd.voucher.nbf`, `pdnd.voucher.exp` (stored as strings; typically epoch seconds)
- `pdnd.voucher.purposeId` (if present)
- `pdnd.voucher.clientId` (if present)

Reference: voucher usage and semantics. ([developer.pagopa.it](https://www.developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/riferimenti-tecnici/utilizzare-i-voucher?utm_source=chatgpt.com))

#### Tracking Evidence (from `Agid-JWT-Tracking-Evidence` / `AgID-JWT-TrackingEvidence`)
- `pdnd.trackingEvidence.alg`, `pdnd.trackingEvidence.kid`, `pdnd.trackingEvidence.typ`
- `pdnd.trackingEvidence.iss`, `pdnd.trackingEvidence.sub`, `pdnd.trackingEvidence.jti` (when present)

Reference: additional-information flows. ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/tutorial/tutorial-per-il-fruitore/come-richiedere-un-voucher-bearer-per-le-api-di-un-erogatore-con-informazioni-aggiuntive?utm_source=chatgpt.com))

#### Digest (from `Digest`)
- `pdnd.digest.alg`
- `pdnd.digest.value`

Reference: digest notes in voucher FAQ. ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/PDND-Interoperability-Operating-Manual/technical-references/utilizzare-i-voucher/faqs?utm_source=chatgpt.com))

#### DPoP (from `DPoP`)
- `pdnd.dpop.alg`, `pdnd.dpop.kid`, `pdnd.dpop.typ`
- `pdnd.dpop.htm`, `pdnd.dpop.htu`, `pdnd.dpop.jti`, `pdnd.dpop.iat` (when present)

Reference: platform documentation hub and evolution via release notes. ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides?utm_source=chatgpt.com)) ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/release-note/2025?utm_source=chatgpt.com))

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

- `Pdnd.Metadata`  
  Core abstractions and extraction pipeline, including PDND parsing utilities.

- `Pdnd.Metadata.AspNetCore`  
  ASP.NET Core integration: middleware, accessors, and minimal API binding types.

## Quick start (ASP.NET Core)

### 1) Register services

<CODE>
builder.Services.AddPdndMetadata(options =>
{
    // Demo-friendly; in production prefer CaptureAllHeaders = false
    // and a strict allow-list (see "Recommended production configuration").
    options.CaptureAllHeaders = true;

    // PDND parsing (best-effort, no validation)
    options.ParsePdndVoucherFromAuthorizationBearer = true;
    options.ParsePdndTrackingEvidence = true;
    options.ParseDpopHeader = true;
    options.ParseDigestHeader = true;

    // Do not store signed blobs
    options.CaptureRawTrackingEvidenceHeader = false;
    options.CaptureRawDpopHeader = false;

    // Guard-rail
    options.MaxTokenLength = 16_384;
});
</CODE>

### 2) Add middleware

Place it before endpoint mapping so every request gets a snapshot.

<CODE>
app.UsePdndMetadata();
</CODE>

### 3) Consume metadata (Controllers)

<CODE>
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
</CODE>

### 4) Consume metadata (Minimal APIs)

<CODE>
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
</CODE>

## Recommended production configuration

For production services, it’s usually better to explicitly decide *which headers you want to capture* rather than collecting everything and filtering later.

A conservative approach:
- `CaptureAllHeaders = false`
- keep a strict `HeaderAllowList` (trace + correlation + forwarded + the PDND headers you explicitly require)
- keep `CaptureRawTrackingEvidenceHeader = false` and `CaptureRawDpopHeader = false`
- consider disabling `CaptureRawDigestHeader` unless you actually need it
- in logging/auditing pipelines, avoid persisting the full `items` map unless you are confident about governance; prefer logging only canonical keys you whitelist

The library already enforces the most important rule by default: raw `Authorization` is never stored.

## Sample API

The sample project is meant to let you validate integration quickly, without logging raw tokens.

- `GET /minimal/pdnd`  
  Returns voucher / trackingEvidence / dpop / digest sections.

- `GET /minimal/sanity`  
  Verifies that raw `Authorization`, raw `DPoP`, and raw tracking evidence headers are not captured.

Example request (fake tokens are sufficient for extraction checks):

<CODE>
curl \
  -H "Authorization: Bearer <jwt>" \
  -H "Agid-JWT-Tracking-Evidence: <jws>" \
  -H "DPoP: <dpop-jws>" \
  -H "Digest: SHA-256=<base64>" \
  http://localhost:5043/minimal/pdnd
</CODE>

## What this library does not do

This is intentionally an extraction layer, not a security enforcement layer.

- It does **not** validate JWT/JWS signatures.
- It does **not** enforce PDND authorization rules.
- It does **not** call PDND APIs (catalog/registry/auth services) to enrich metadata.
- It does **not** log tokens. If you add logging, keep it limited to canonical `pdnd.*` keys and avoid raw headers.

If you need validation/enforcement, place it in your auth layer (gateway/service middleware) and use Pdnd.Metadata strictly as an observability/diagnostics/audit-friendly snapshot.

---

## Official PDND references

- PDND Interoperabilità – Guides hub ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides?utm_source=chatgpt.com))
- Voucher (usage) ([developer.pagopa.it](https://www.developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/riferimenti-tecnici/utilizzare-i-voucher?utm_source=chatgpt.com))
- Voucher with additional information (Tracking Evidence) ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/tutorial/tutorial-per-il-fruitore/come-richiedere-un-voucher-bearer-per-le-api-di-un-erogatore-con-informazioni-aggiuntive?utm_source=chatgpt.com))
- Voucher FAQ / Digest field notes ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/PDND-Interoperability-Operating-Manual/technical-references/utilizzare-i-voucher/faqs?utm_source=chatgpt.com))
- Tracing ([developer.pagopa.it](https://www.developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/riferimenti-tecnici/tracing?utm_source=chatgpt.com))
- Release notes (platform evolution including DPoP) ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/release-note/2025?utm_source=chatgpt.com))