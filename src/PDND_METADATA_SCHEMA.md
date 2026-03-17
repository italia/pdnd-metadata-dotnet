# PDND Metadata Schema – Canonical Keys Reference

> **Version**: 1.0  
> **Library**: `Pdnd.Metadata` / `Pdnd.Metadata.AspNetCore`  
> **Target**: .NET 8+ / .NET 10+ (multi-target)  
> **Approach**: Best-effort, fail-soft, no signature validation

This document defines the **canonical metadata keys** extracted by the library from PDND interoperability requests.  
It is intended as a **community reference** to standardize how erogatori (API providers) capture and normalize PDND-specific metadata for audit, observability, and compliance purposes.

---

## Design Principles

| Principle | Description |
|-----------|-------------|
| **Best-effort** | Extraction never fails the request; malformed tokens are silently skipped |
| **No validation** | JWT/JWS tokens are decoded but **never** cryptographically verified — this is the responsibility of the authentication/authorization layer |
| **Fail-soft** | Every extractor catches all exceptions internally |
| **Transport-agnostic** | Core extraction works on `PdndRequestContext`, not on `HttpContext` directly |
| **Security by default** | `Authorization`, `Cookie`, `Set-Cookie` headers are in the deny list; raw signed blobs are not captured unless explicitly enabled |

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
| `net.forwarded_for` | string | Derived | Normalized forwarded chain (from `Forwarded` / `X-Forwarded-For`) |

## 2. Security / TLS Metadata

| Canonical Key | Type | Source | Description |
|---------------|------|--------|-------------|
| `security.https` | string | Tls | `"true"` if HTTPS |
| `security.mtls.client_certificate_present` | string | Tls | `"true"` if mTLS client cert detected |
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

## 5. PDND Voucher (Authorization Bearer JWT)

Extracted from the `Authorization: Bearer <JWT>` header.  
Reference: [Linee Guida PDND – Voucher](https://docs.pagopa.it/interoperabilita-1/manuale-operativo/guida-alladesione-tecnica)

### JOSE Header

| Canonical Key | JWT Field | Source | Description |
|---------------|-----------|--------|-------------|
| `pdnd.voucher.alg` | `alg` | Header | Signing algorithm (e.g., RS256, ES256) |
| `pdnd.voucher.kid` | `kid` | Header | Key identifier (maps to PDND key registry) |
| `pdnd.voucher.typ` | `typ` | Header | Token type (e.g., `at+jwt`) |

### Payload (Standard JWT Claims)

| Canonical Key | JWT Claim | Source | Description |
|---------------|-----------|--------|-------------|
| `pdnd.voucher.iss` | `iss` | Claims | Token issuer (PDND authorization server) |
| `pdnd.voucher.sub` | `sub` | Claims | Subject (fruitore client ID) |
| `pdnd.voucher.aud` | `aud` | Claims | Audience (erogatore; may be comma-separated if array) |
| `pdnd.voucher.jti` | `jti` | Claims | Unique token identifier |
| `pdnd.voucher.iat` | `iat` | Claims | Issued at (epoch seconds) |
| `pdnd.voucher.nbf` | `nbf` | Claims | Not before (epoch seconds) |
| `pdnd.voucher.exp` | `exp` | Claims | Expiration (epoch seconds) |

### Payload (PDND-Specific Claims)

| Canonical Key | JWT Claim | Source | Description |
|---------------|-----------|--------|-------------|
| `pdnd.voucher.purposeId` | `purposeId` | Claims | PDND purpose identifier (agreement scope) |
| `pdnd.voucher.clientId` | `clientId` | Claims | Client identifier (camelCase form) |
| `pdnd.voucher.client_id` | `client_id` | Claims | Client identifier (OAuth 2.0 underscore form) |
| `pdnd.voucher.organizationId` | `organizationId` | Claims | Fruitore organization identifier |
| `pdnd.voucher.dnonce` | `dnonce` | Claims | Anti-replay nonce (if present) |

---

## 6. PDND Tracking Evidence (`Agid-JWT-Tracking-Evidence`)

Extracted from the `Agid-JWT-Tracking-Evidence` (or `AgID-JWT-TrackingEvidence`) header.  
Reference: [Linee Guida Interoperabilità – Pattern INTEGRITY_REST_02](https://www.agid.gov.it/it/infrastrutture/sistema-pubblico-connettivita/il-nuovo-modello-interoperabilita)

### JOSE Header

| Canonical Key | JWT Field | Source | Description |
|---------------|-----------|--------|-------------|
| `pdnd.trackingEvidence.alg` | `alg` | Header | Signing algorithm |
| `pdnd.trackingEvidence.kid` | `kid` | Header | Key identifier |
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

---

## 7. DPoP Proof (`DPoP` Header)

Extracted from the `DPoP` header.  
Reference: [RFC 9449 – OAuth 2.0 Demonstrating Proof of Possession](https://www.rfc-editor.org/rfc/rfc9449)

### JOSE Header

| Canonical Key | JWT Field | Source | Description |
|---------------|-----------|--------|-------------|
| `pdnd.dpop.alg` | `alg` | Header | Signing algorithm |
| `pdnd.dpop.kid` | `kid` | Header | Key identifier |
| `pdnd.dpop.typ` | `typ` | Header | Token type (should be `dpop+jwt`) |

### Payload

| Canonical Key | JWT Claim | Source | Description | RFC Section |
|---------------|-----------|--------|-------------|-------------|
| `pdnd.dpop.htm` | `htm` | Claims | HTTP method the proof is bound to | §4.2 |
| `pdnd.dpop.htu` | `htu` | Claims | HTTP target URI | §4.2 |
| `pdnd.dpop.jti` | `jti` | Claims | Unique proof identifier | §4.2 |
| `pdnd.dpop.iat` | `iat` | Claims | Issued at (epoch seconds) | §4.2 |
| `pdnd.dpop.exp` | `exp` | Claims | Expiration (epoch seconds) | §4.2 |
| `pdnd.dpop.ath` | `ath` | Claims | Access token hash (SHA-256, base64url) | §4.2 |
| `pdnd.dpop.nonce` | `nonce` | Claims | Server-provided nonce (anti-replay) | §4.3 |

---

## 8. Digest / Content-Digest

### Legacy Digest Header

Extracted from the `Digest` header.  
Reference: [RFC 3230 – Instance Digests in HTTP](https://www.rfc-editor.org/rfc/rfc3230) *(deprecated)*

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
Used in PDND for request integrity signing.  
Reference: [Linee Guida Interoperabilità – Pattern INTEGRITY_REST_01](https://www.agid.gov.it/it/infrastrutture/sistema-pubblico-connettivita/il-nuovo-modello-interoperabilita)

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
| `pdnd.signature.signed_headers` | `signed_headers` | Claims | Digest of signed headers (integrity) |

---

## Configuration Reference (`PdndMetadataOptions`)

| Option | Default | Description |
|--------|---------|-------------|
| `CaptureAllHeaders` | `true` | Capture all request headers as `http.header.*` |
| `HeaderDenyList` | `Authorization, Cookie, Set-Cookie` | Headers that are never captured |
| `MaxHeaderValuesPerName` | `10` | Max values per multi-value header |
| `MaxValueLength` | `2048` | Max length before truncation |
| `MaxTokenLength` | `16384` | Max JWT/JWS length to attempt decoding |
| `PromoteTracingHeaders` | `true` | Promote W3C/correlation headers to canonical keys |
| `NormalizeForwardedHeaders` | `true` | Parse and normalize `Forwarded` / `X-Forwarded-For` |
| `ParsePdndVoucherFromAuthorizationBearer` | `true` | Decode Bearer token as PDND voucher |
| `ParsePdndTrackingEvidence` | `true` | Decode `Agid-JWT-Tracking-Evidence` |
| `ParseDpopHeader` | `true` | Decode `DPoP` proof header |
| `ParseDigestHeader` | `true` | Parse legacy `Digest` header |
| `ParseContentDigestHeader` | `true` | Parse `Content-Digest` header (RFC 9530) |
| `ParseAgidJwtSignature` | `true` | Decode `Agid-JWT-Signature` header |
| `CaptureRawTrackingEvidenceHeader` | `false` | Store raw tracking evidence blob |
| `CaptureRawDpopHeader` | `false` | Store raw DPoP proof blob |
| `CaptureRawDigestHeader` | `true` | Store raw Digest header |
| `CaptureRawSignatureHeader` | `false` | Store raw Agid-JWT-Signature blob |

---

## PDND Interoperability Patterns Coverage

| Pattern | Header/Token | Status |
|---------|-------------|--------|
| **ID_AUTH_REST_01** (Bearer voucher) | `Authorization: Bearer <JWT>` | ✅ Full |
| **ID_AUTH_REST_02** (Bearer + PDND voucher) | `Authorization: Bearer <JWT>` | ✅ Full |
| **INTEGRITY_REST_01** (Agid-JWT-Signature) | `Agid-JWT-Signature` | ✅ Full |
| **INTEGRITY_REST_02** (Tracking Evidence) | `Agid-JWT-Tracking-Evidence` | ✅ Full |
| **AUDIT_REST_01/02** (Tracking Evidence) | `Agid-JWT-Tracking-Evidence` | ✅ Full |
| **DPoP-bound tokens** (RFC 9449) | `DPoP` | ✅ Full |
| **Body integrity** (legacy Digest) | `Digest` | ✅ Full |
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
