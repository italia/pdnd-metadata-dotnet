# PDND Metadata Schema – Canonical Keys Reference

> **Version**: 1.2.0  
> **Library**: `Pdnd.Metadata` / `Pdnd.Metadata.AspNetCore`  
> **Target**: .NET 8+ / .NET 10+ (multi-target)  
> **Approach**: Best-effort, fail-soft, no signature validation

This document defines the **canonical metadata keys** extracted by the library from PDND interoperability requests.  
It is intended as a **community reference** to standardize how erogatori (API providers) capture and normalize PDND-specific metadata for audit, observability, and compliance purposes.

Token shapes follow the examples of the [PDND Interoperabilità operating manual](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita)
and the AgID ModI security patterns. Neither PagoPA nor AgID publishes or endorses this document.

---

## Design Principles

| Principle | Description |
|-----------|-------------|
| **Best-effort** | Extraction never fails the request; malformed tokens are silently skipped |
| **No validation** | JWT/JWS tokens are decoded but **never** cryptographically verified — this is the responsibility of the authentication/authorization layer |
| **Fail-soft** | Every extractor catches all exceptions internally |
| **Transport-agnostic** | Core extraction works on `PdndRequestContext`, not on `HttpContext` directly |
| **Bounded** | Every value, including claims decoded from tokens, is truncated to `MaxValueLength` |
| **Security by default** | Header capture is opt-in (`CaptureAllHeaders` is `false`); credential-bearing header names are additionally deny-listed; raw signed blobs are not captured unless explicitly enabled |

---

## 1. HTTP / Connection Metadata

| Canonical Key | Type | Source | Description |
|---------------|------|--------|-------------|
| `http.method` | string | Derived | HTTP method (GET, POST, …) |
| `http.scheme` | string | Connection | Request scheme (http/https) |
| `http.host` | string | Connection | Request host |
| `http.path` | string | Derived | Request path |
| `http.query` | string | Derived | Raw query string (truncated) |
| `http.header.<name>` | string | Header | Raw header value (lowercase name) |
| `net.remote_ip` | string | Connection | Client remote IP address |
| `net.remote_port` | string | Connection | Client remote port |
| `net.local_ip` | string | Connection | Server local IP address |
| `net.local_port` | string | Connection | Server local port |
| `net.forwarded_for` | string | Derived | Normalized forwarded chain (from `Forwarded`, falling back to `X-Forwarded-For`). Every value of the header is used, across repeated header lines, in arrival order |
| `metadata.truncated` | string | Derived | Present when a header that would have been captured was dropped because `MaxHeaderCount` was reached |

## 2. Security / TLS Metadata

| Canonical Key | Type | Source | Description |
|---------------|------|--------|-------------|
| `security.https` | string | Tls | `"true"` if HTTPS |
| `security.mtls.client_certificate_present` | string | Tls | `"true"` if mTLS client cert detected |
| `security.mtls.client_certificate_thumbprint` | string | Tls | Thumbprint of the client certificate (no subject or issuer) |
| `security.tls.protocol` | string | Tls | Negotiated TLS protocol (e.g., `Tls13`), when the host exposes it |
| `security.http.protocol` | string | Tls | HTTP protocol version (e.g., HTTP/2) |

## 3. Tracing / Correlation Metadata

| Canonical Key | Type | Source | Description | Reference |
|---------------|------|--------|-------------|-----------|
| `trace.traceparent` | string | Tracing | W3C Trace Context traceparent | [W3C Trace Context](https://www.w3.org/TR/trace-context/) |
| `trace.tracestate` | string | Tracing | W3C Trace Context tracestate | [W3C Trace Context](https://www.w3.org/TR/trace-context/) |
| `trace.baggage` | string | Tracing | W3C Baggage | [W3C Baggage](https://www.w3.org/TR/baggage/) |
| `correlation.id` | string | Tracing | X-Correlation-Id header value | — |
| `request.id` | string | Tracing | X-Request-Id header value | — |

## 4. Claims Metadata

| Canonical Key | Type | Source | Description |
|---------------|------|--------|-------------|
| `claim.<type>` | string | Claims | Authenticated user claim (from `ClaimsPrincipal`) |

---

## 5. PDND Voucher (Authorization Bearer or DPoP JWT)

Extracted from the `Authorization: Bearer <JWT>` header, or from `Authorization: DPoP <JWT>` when
the voucher is DPoP-bound (RFC 9449 §7.1). The scheme is matched case-insensitively.  
Like every other value, claim values are truncated to `MaxValueLength`.  
Reference: [PDND – Utilizzare i voucher](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/riferimenti-tecnici/utilizzare-i-voucher)

### JOSE Header

| Canonical Key | JWT Field | Source | Description |
|---------------|-----------|--------|-------------|
| `pdnd.voucher.alg` | `alg` | Header | Signing algorithm (e.g., RS256) |
| `pdnd.voucher.kid` | `kid` | Header | Key identifier (PDND key published in the JWKS) |
| `pdnd.voucher.typ` | `typ` | Header | Token type (`at+jwt`) |

### Payload (Standard JWT Claims)

| Canonical Key | JWT Claim | Source | Description |
|---------------|-----------|--------|-------------|
| `pdnd.voucher.iss` | `iss` | Claims | Token issuer (PDND authorization server, e.g. `interop.pagopa.it`) |
| `pdnd.voucher.sub` | `sub` | Claims | Subject (fruitore client ID) |
| `pdnd.voucher.aud` | `aud` | Claims | Audience (erogatore; may be comma-separated if array) |
| `pdnd.voucher.jti` | `jti` | Claims | Unique token identifier |
| `pdnd.voucher.iat` | `iat` | Claims | Issued at (epoch seconds) |
| `pdnd.voucher.nbf` | `nbf` | Claims | Not before (epoch seconds) |
| `pdnd.voucher.exp` | `exp` | Claims | Expiration (epoch seconds) |

### Payload (PDND-Specific Claims)

| Canonical Key | JWT Claim | Source | Description |
|---------------|-----------|--------|-------------|
| `pdnd.voucher.client_id` | `client_id` | Claims | Client identifier, as issued by PDND |
| `pdnd.voucher.purposeId` | `purposeId` | Claims | Purpose (finalità) identifier |
| `pdnd.voucher.consumerId` | `consumerId` | Claims | Fruitore organization identifier |
| `pdnd.voucher.producerId` | `producerId` | Claims | Erogatore organization identifier |
| `pdnd.voucher.eserviceId` | `eserviceId` | Claims | E-service identifier |
| `pdnd.voucher.descriptorId` | `descriptorId` | Claims | E-service version (descriptor) identifier |
| `pdnd.voucher.cnf.jkt` | `cnf.jkt` | Claims | JWK thumbprint of the key bound to a DPoP voucher (RFC 9449 §6.1). DPoP vouchers only |
| `pdnd.voucher.digest.alg` | `digest.alg` | Claims | Hash algorithm of the tracking evidence (`SHA256`). AUDIT_REST_02 only |
| `pdnd.voucher.digest.value` | `digest.value` | Claims | Hash of the `Agid-JWT-TrackingEvidence` JWS. AUDIT_REST_02 only |

### Payload (Legacy or Non-Standard Claims)

Not present in the vouchers PDND currently issues. Kept so that tokens that still carry them are
not silently dropped.

| Canonical Key | JWT Claim | Source | Description |
|---------------|-----------|--------|-------------|
| `pdnd.voucher.clientId` | `clientId` | Claims | Client identifier, camelCase form |
| `pdnd.voucher.organizationId` | `organizationId` | Claims | Organization identifier. Superseded by `consumerId` |
| `pdnd.voucher.dnonce` | `dnonce` | Claims | Nonce. In AUDIT_REST_02 it belongs to the tracking evidence (`pdnd.trackingEvidence.dnonce`) |

---

## 6. PDND Tracking Evidence (`Agid-JWT-TrackingEvidence`)

Extracted from the `Agid-JWT-TrackingEvidence` header (AgID patterns AUDIT_REST_01 and
AUDIT_REST_02). PDND documentation also writes it `AgID-JWT-TrackingEvidence`: header names are
case-insensitive, so both are the same header. The hyphenated spelling
`Agid-JWT-Tracking-Evidence` is accepted for compatibility, although neither AgID nor PDND uses it.
When both are present, `Agid-JWT-TrackingEvidence` is read first; the hyphenated header is used only when the official one is absent or blank.  
In AUDIT_REST_02 the SHA-256 hash of this JWS travels in the voucher, under `digest.value`.  
Reference: [PDND – Voucher con informazioni aggiuntive](https://developer.pagopa.it/it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/v1.0/tutorial/tutorial-per-il-fruitore/come-richiedere-un-voucher-bearer-per-le-api-di-un-erogatore-con-informazioni-aggiuntive)

### JOSE Header

| Canonical Key | JWT Field | Source | Description |
|---------------|-----------|--------|-------------|
| `pdnd.trackingEvidence.alg` | `alg` | Header | Signing algorithm |
| `pdnd.trackingEvidence.kid` | `kid` | Header | Key identifier (fruitore key registered on PDND) |
| `pdnd.trackingEvidence.typ` | `typ` | Header | Token type |

### Payload

| Canonical Key | JWT Claim | Source | Description |
|---------------|-----------|--------|-------------|
| `pdnd.trackingEvidence.iss` | `iss` | Claims | Issuer |
| `pdnd.trackingEvidence.sub` | `sub` | Claims | Subject |
| `pdnd.trackingEvidence.jti` | `jti` | Claims | Unique identifier |
| `pdnd.trackingEvidence.aud` | `aud` | Claims | Audience (may be comma-separated) |
| `pdnd.trackingEvidence.iat` | `iat` | Claims | Issued at (epoch seconds) |
| `pdnd.trackingEvidence.nbf` | `nbf` | Claims | Not before (epoch seconds) |
| `pdnd.trackingEvidence.exp` | `exp` | Claims | Expiration (epoch seconds) |
| `pdnd.trackingEvidence.userID` | `userID` | Claims | User identifier within the fruitore domain |
| `pdnd.trackingEvidence.userLocation` | `userLocation` | Claims | Workstation identifier within the fruitore domain |
| `pdnd.trackingEvidence.LoA` | `LoA` | Claims | Level of assurance of the user authentication |
| `pdnd.trackingEvidence.purposeId` | `purposeId` | Claims | Purpose identifier |
| `pdnd.trackingEvidence.dnonce` | `dnonce` | Claims | Nonce |

The audit claim set is agreed between fruitore and erogatore. `userID`, `userLocation`, and `LoA`
are the claims the AgID guidelines suggest; `purposeId` and `dnonce` appear in published examples.
An absent claim produces no key.

---

## 7. DPoP Proof (`DPoP` Header)

Extracted from the `DPoP` header.  
Reference: [RFC 9449 – OAuth 2.0 Demonstrating Proof of Possession](https://www.rfc-editor.org/rfc/rfc9449),
[PDND – Approfondimento su DPoP](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/riferimenti-tecnici/utilizzare-i-voucher/approfondimento-su-dpop)

### JOSE Header

| Canonical Key | JWT Field | Source | Description |
|---------------|-----------|--------|-------------|
| `pdnd.dpop.alg` | `alg` | Header | Signing algorithm |
| `pdnd.dpop.kid` | `kid` | Header | Key identifier, if present (PDND proofs carry the key in `jwk` instead) |
| `pdnd.dpop.typ` | `typ` | Header | Token type (should be `dpop+jwt`) |

The `jwk` header field is not extracted, and its thumbprint is not computed: correlating the
proof with `pdnd.voucher.cnf.jkt` is a verification step.

### Payload

| Canonical Key | JWT Claim | Source | Description | RFC Section |
|---------------|-----------|--------|-------------|-------------|
| `pdnd.dpop.htm` | `htm` | Claims | HTTP method the proof is bound to | §4.2 |
| `pdnd.dpop.htu` | `htu` | Claims | HTTP target URI | §4.2 |
| `pdnd.dpop.jti` | `jti` | Claims | Unique proof identifier | §4.2 |
| `pdnd.dpop.iat` | `iat` | Claims | Issued at (epoch seconds) | §4.2 |
| `pdnd.dpop.exp` | `exp` | Claims | Expiration (epoch seconds) | — |
| `pdnd.dpop.ath` | `ath` | Claims | Access token hash (SHA-256, base64url) | §4.2 |
| `pdnd.dpop.nonce` | `nonce` | Claims | Server-provided nonce. Not used by PDND today | §4.2 |

---

## 8. Digest / Content-Digest

### Legacy Digest Header

Extracted from the `Digest` header. This is the header the AgID INTEGRITY_REST patterns sign
through `signed_headers`.  
Reference: [RFC 3230 – Instance Digests in HTTP](https://www.rfc-editor.org/rfc/rfc3230) *(obsoleted by RFC 9530)*

| Canonical Key | Source | Description |
|---------------|--------|-------------|
| `pdnd.digest.alg` | Header | Digest algorithm (e.g., SHA-256) |
| `pdnd.digest.value` | Header | Digest value (base64) |

### Content-Digest Header (RFC 9530)

Extracted from the `Content-Digest` header.  
Reference: [RFC 9530 – Digest Fields](https://www.rfc-editor.org/rfc/rfc9530) *(replaces RFC 3230)*

| Canonical Key | Source | Description |
|---------------|--------|-------------|
| `pdnd.content_digest.alg` | Header | Content-Digest algorithm (e.g., sha-256) |
| `pdnd.content_digest.value` | Header | Content-Digest value (base64, from `:value:` format) |

---

## 9. Agid-JWT-Signature (`Agid-JWT-Signature` Header)

Extracted from the `Agid-JWT-Signature` header.  
Used by the AgID patterns INTEGRITY_REST_01 and INTEGRITY_REST_02 for message integrity. In
INTEGRITY_REST_02 the signing key is referenced through `kid`, the identifier of the key loaded on
PDND, and the same header also carries the erogatore response signature.  
Reference: [AgID – Linee Guida sull'interoperabilità tecnica](https://www.agid.gov.it/it/infrastrutture/sistema-pubblico-connettivita/il-nuovo-modello-interoperabilita)

### JOSE Header

| Canonical Key | JWT Field | Source | Description |
|---------------|-----------|--------|-------------|
| `pdnd.signature.alg` | `alg` | Header | Signing algorithm |
| `pdnd.signature.kid` | `kid` | Header | Key identifier |
| `pdnd.signature.typ` | `typ` | Header | Token type |

### Payload

| Canonical Key | JWT Claim | Source | Description |
|---------------|-----------|--------|-------------|
| `pdnd.signature.iss` | `iss` | Claims | Issuer |
| `pdnd.signature.sub` | `sub` | Claims | Subject |
| `pdnd.signature.jti` | `jti` | Claims | Unique identifier |
| `pdnd.signature.aud` | `aud` | Claims | Audience (may be comma-separated) |
| `pdnd.signature.iat` | `iat` | Claims | Issued at (epoch seconds) |
| `pdnd.signature.exp` | `exp` | Claims | Expiration (epoch seconds) |
| `pdnd.signature.signed_headers` | `signed_headers` | Claims | Comma-separated list of signed header names, lowercased. Not a digest: body integrity is carried separately by `Digest` or `Content-Digest` and verified as a distinct step. |
| `pdnd.signature.signed_header.<name>` | `signed_headers[]` | Claims | Value of each individual signed header, when the claim carries values. Header names are lowercased. Never produced for `authorization`, `proxy-authorization`, `cookie`, or `set-cookie`, whose signed value is the credential itself, nor for names longer than 256 characters; implementations with a configurable deny list also withhold the names it covers. The name still appears in `pdnd.signature.signed_headers`. |

---

## Configuration Reference (`PdndMetadataOptions`)

| Option | Default | Description |
|--------|---------|-------------|
| `CaptureAllHeaders` | `false` | Capture all request headers as `http.header.*`, subject to the deny list |
| `HeaderAllowList` | minimal set | Headers captured when `CaptureAllHeaders` is `false` |
| `HeaderDenyList` | `Authorization`, `Proxy-Authorization`, `Authentication`, `Cookie`, `Set-Cookie`, `X-Api-Key`, `Api-Key`, `X-Auth-Token`, `X-Access-Token`, `X-Csrf-Token` | Headers that are never captured |
| `MaxHeaderCount` | `64` | Max distinct header names captured; beyond it `metadata.truncated` is emitted |
| `MaxHeaderValuesPerName` | `10` | Max values per multi-value header |
| `MaxValueLength` | `2048` | Max length before truncation, including claims decoded from tokens |
| `MaxTokenLength` | `16384` | Max JWT/JWS length to attempt decoding |
| `PathExclusions` | empty | Path prefixes skipped entirely, matched by whole segment (`/health` does not exclude `/healthcare`) |
| `PromoteTracingHeaders` | `true` | Promote W3C/correlation headers to canonical keys |
| `NormalizeForwardedHeaders` | `true` | Parse and normalize `Forwarded` / `X-Forwarded-For` |
| `ParsePdndVoucherFromAuthorizationBearer` | `true` | Decode the voucher from `Authorization` (`Bearer` or `DPoP` scheme) |
| `ParsePdndTrackingEvidence` | `true` | Decode `Agid-JWT-TrackingEvidence` |
| `ParseDpopHeader` | `true` | Decode `DPoP` proof header |
| `ParseDigestHeader` | `true` | Parse legacy `Digest` header |
| `ParseContentDigestHeader` | `true` | Parse `Content-Digest` header (RFC 9530) |
| `ParseAgidJwtSignature` | `true` | Decode `Agid-JWT-Signature` header |
| `CaptureRawTrackingEvidenceHeader` | `false` | Store raw tracking evidence blob |
| `CaptureRawDpopHeader` | `false` | Store raw DPoP proof blob |
| `CaptureRawDigestHeader` | `true` | Store raw `Digest` / `Content-Digest` header (not a credential) |
| `CaptureRawSignatureHeader` | `false` | Store raw Agid-JWT-Signature blob |

---

## PDND Interoperability Patterns Coverage

Coverage means the keys are extracted. Nothing in this table is verified.

| Pattern | Header/Token | Status |
|---------|-------------|--------|
| **PDND voucher, Bearer** | `Authorization: Bearer <JWT>` | ✅ Full |
| **PDND voucher, DPoP** (RFC 9449) | `Authorization: DPoP <JWT>` + `DPoP` | ✅ Full (the proof `jwk` is not extracted) |
| **ID_AUTH_REST_01/02** (ModI token in `Authorization`) | `Authorization: Bearer <JWT>` | ⚠️ Partial: registered claims only, under `pdnd.voucher.*` |
| **INTEGRITY_REST_01** | `Agid-JWT-Signature` + `Digest` | ✅ Full |
| **INTEGRITY_REST_02** | `Agid-JWT-Signature` (+ `Digest`) | ✅ Full |
| **AUDIT_REST_01** | `Agid-JWT-TrackingEvidence` | ✅ Full |
| **AUDIT_REST_02** | `Agid-JWT-TrackingEvidence` + voucher `digest` | ✅ Full |
| **Body integrity** (RFC 9530) | `Content-Digest` | ✅ Full |
| **Forwarded identity** | `Forwarded` / `X-Forwarded-For` | ✅ Full |
| **Distributed tracing** | `traceparent` / `tracestate` / `baggage` | ✅ Full |
| **mTLS detection** | TLS features | ✅ Hint-level |

---

## Notes for Implementors

1. **This library does NOT validate signatures.** Cryptographic verification must be handled by dedicated middleware (e.g., PDND gateway, custom `IAuthorizationHandler`, or a dedicated validation library).

2. **All keys are case-insensitive** in `PdndCallerMetadata`. Lookups via `GetFirstValue(key)` and `GetValues(key)` are ordinal-case-insensitive.

3. **Extensibility**: Implement `IPdndMetadataExtractor` to add custom extraction logic while reusing the canonical key schema.

4. **Security considerations**: Raw signed blobs (tracking evidence, DPoP proofs, signatures) are suppressed by default. Enable `CaptureRaw*` options only when required for debugging and **never in production logs exposed externally**.

5. **Identity claims are unverified.** `consumerId`, `purposeId`, and the other identifiers are whatever the caller put in the token until the voucher has been validated. Do not authorize on them, and bound any use that creates state per distinct value (metrics, caches).
