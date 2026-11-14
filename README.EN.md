<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/logo-dark.svg">
    <img src="assets/logo.svg" alt="Pdnd.Metadata" width="96">
  </picture>
</p>

<h1 align="center">Pdnd.Metadata</h1>

<p align="center">
  PDND request metadata extraction for .NET services.
</p>

<p align="center">
  <a href="https://opensource.org/licenses/MIT"><img alt="MIT License" src="https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square"></a>
  <a href="https://github.com/italia/pdnd-metadata-dotnet/actions/workflows/ci.yml"><img alt="CI" src="https://img.shields.io/github/actions/workflow/status/italia/pdnd-metadata-dotnet/ci.yml?branch=main&label=CI&style=flat-square"></a>
  <a href="https://www.nuget.org/packages/Pdnd.Metadata"><img alt="NuGet" src="https://img.shields.io/nuget/v/Pdnd.Metadata?style=flat-square"></a>
  <a href="https://www.nuget.org/packages/Pdnd.Metadata"><img alt="Downloads" src="https://img.shields.io/nuget/dt/Pdnd.Metadata?style=flat-square"></a>
  <a href="https://github.com/italia/pdnd-metadata-dotnet/issues"><img alt="Issues" src="https://img.shields.io/github/issues/italia/pdnd-metadata-dotnet?style=flat-square"></a>
  <a href="https://github.com/italia/pdnd-metadata-dotnet/actions/workflows/codeql.yml"><img alt="CodeQL" src="https://img.shields.io/github/actions/workflow/status/italia/pdnd-metadata-dotnet/codeql.yml?branch=main&label=CodeQL&style=flat-square"></a>
  <a href="https://scorecard.dev/viewer/?uri=github.com/italia/pdnd-metadata-dotnet"><img alt="OpenSSF Scorecard" src="https://img.shields.io/ossf-scorecard/github.com/italia/pdnd-metadata-dotnet?label=OpenSSF%20Scorecard&style=flat-square"></a>
  <a href="README.md"><img alt="Italiano" src="https://img.shields.io/badge/lang-it-green?style=flat-square"></a>
</p>

---

A service exposing an e-service on PDND receives requests carrying a voucher, often a tracking
evidence token, a DPoP proof, a body digest, and an integrity signature. Knowing who is calling,
for which purpose, and how to correlate the call means reading and decoding all of those headers.

This library does that once, uniformly, and produces a snapshot with stable key names. The key
vocabulary is defined in a separate specification, so that two services in the same organisation
record the same fact under the same name.

## Where it sits

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/diagrams/architecture-dark.svg">
    <img src="assets/diagrams/architecture.svg" alt="Architectural placement">
  </picture>
</p>

The middleware runs inside the provider service, after the reverse proxy and authentication and
before the authorization decision.
It takes no part in the trust decision, and it never rejects a request.

## In practice

```csharp
var md = accessor.Current;

md.GetFirstValue(PdndMetadataKeys.PdndVoucherConsumerId);   // "69e2865e-..." consumer organization
md.GetFirstValue(PdndMetadataKeys.PdndVoucherPurposeId);    // "1b361d49-..." purpose
md.GetFirstValue(PdndMetadataKeys.PdndVoucherEserviceId);   // "b8c6d7ad-..." e-service
md.GetFirstValue(PdndMetadataKeys.CorrelationId);           // "b7c1..."
```

## Boundaries

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/diagrams/pipeline-dark.svg">
    <img src="assets/diagrams/pipeline.svg" alt="Extraction pipeline">
  </picture>
</p>

The library extracts and normalizes. It does not verify.

| Does | Does not |
| --- | --- |
| Decode vouchers (`Bearer` and `DPoP`), tracking evidence, and DPoP proofs | Verify JWS signatures |
| Report the voucher `cnf.jkt` and the proof `ath` | Verify the binding between voucher and DPoP proof |
| Normalize `Digest` and `Content-Digest` | Compare the digest against the actual body |
| Read `Agid-JWT-Signature` claims | Check signed header correspondence |
| Promote correlation and tracing signals | Detect replay or expiry |
| Bound what is retained | Take any authorization decision |

Vouchers and tokens must be validated in your own authentication layer, in a gateway, or in
dedicated middleware. No security guarantee follows from using this library.

## Packages

| Package | Contents |
| --- | --- |
| `Pdnd.Metadata` | Extraction pipeline, independent of any web framework |
| `Pdnd.Metadata.AspNetCore` | Middleware, DI registration, accessor, Minimal API binding |
| `Pdnd.Metadata.Diagnostics` | Projection onto `Activity` and metrics, consumable by OpenTelemetry |

All three target `net8.0` and `net10.0`.

```bash
dotnet add package Pdnd.Metadata.AspNetCore
```

## Usage

Service registration. The defaults are already the recommended posture: only allow-listed headers
are captured, and the allow list covers tracing, correlation, `Forwarded`, and `X-Forwarded-For`.
Add only what you need on top.

```csharp
builder.Services.AddPdndMetadata(options =>
{
    options.HeaderAllowList.Add("X-Requesting-Office");   // an application header worth keeping

    options.PathExclusions.Add("/health");
    options.PathExclusions.Add("/metrics");
});
```

Middleware, before endpoint mapping. Order matters in two places:

- after `UseAuthentication()`, otherwise the `claim.*` keys stay empty;
- `UseForwardedHeaders()`, if present, consumes `X-Forwarded-For` and moves it to
  `X-Original-For`, so `net.forwarded_for` reflects what is left.

```csharp
app.UseAuthentication();
app.UsePdndMetadata();
app.UseAuthorization();
```

Reading in a controller.

```csharp
[HttpGet("/resource")]
public IActionResult Get([FromServices] IPdndMetadataAccessor accessor)
{
    var md = accessor.Current;

    return Ok(new
    {
        consumer = md?.GetFirstValue(PdndMetadataKeys.PdndVoucherConsumerId),
        purpose = md?.GetFirstValue(PdndMetadataKeys.PdndVoucherPurposeId)
    });
}
```

Reading in a Minimal API, through parameter binding.

```csharp
app.MapGet("/resource", (PdndCallerMetadataParameter pdnd) =>
{
    var md = pdnd.Value;

    return Results.Ok(new
    {
        purpose = md.GetFirstValue(PdndMetadataKeys.PdndVoucherPurposeId),
        dpopMethod = md.GetFirstValue(PdndMetadataKeys.PdndDpopHtm)
    });
});
```

`Snapshot()` returns the whole map. It is a full copy, so call it once and reuse the result rather
than calling it inside a loop.

## Extracted metadata

Keys follow a hierarchical scheme. These are the prefixes.

| Prefix | Source |
| --- | --- |
| `http.*`, `net.*` | Method, scheme, host, path, addresses, ports, and forwarded chain |
| `security.*` | HTTPS, mTLS (certificate thumbprint only), TLS and HTTP protocol |
| `claim.*` | Claims of the authenticated user, after `UseAuthentication()` |
| `trace.*`, `correlation.id` | W3C Trace Context and request identifiers |
| `pdnd.voucher.*` | Voucher claims: `client_id`, `purposeId`, `consumerId`, `producerId`, `eserviceId`, `descriptorId`, `cnf.jkt`, `digest` |
| `pdnd.trackingEvidence.*` | `Agid-JWT-TrackingEvidence` claims, including the audit data `userID`, `userLocation`, `LoA` |
| `pdnd.dpop.*` | DPoP proof, including `htm`, `htu`, and `ath` |
| `pdnd.digest.*`, `pdnd.content_digest.*` | Body digest, legacy and RFC 9530 formats |
| `pdnd.signature.*` | `Agid-JWT-Signature` claims, including the signed headers |
| `http.header.*` | Raw headers, when capture is enabled |
| `metadata.truncated` | Present when capture stopped at a configured bound |

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/diagrams/keyspace-dark.svg">
    <img src="assets/diagrams/keyspace.svg" alt="Canonical key namespace">
  </picture>
</p>

The full list, with the source claim for every key, is in
[`spec/PDND_METADATA_SCHEMA.md`](spec/PDND_METADATA_SCHEMA.md).

Claim names follow the examples in the PDND operating manual. Current vouchers identify the
consumer organization with `consumerId`; `pdnd.voucher.organizationId` remains available for tokens
that still carry it, but should not be used as the caller's identity. Likewise, the tracking
evidence header is named `Agid-JWT-TrackingEvidence`; the hyphenated `Agid-JWT-Tracking-Evidence`
is accepted for compatibility only.

## Security

Three behaviours are guarantees rather than defaults to be changed inattentively.

**The `Authorization` header is never stored in the clear.** The voucher is decoded and its claims
are extracted, but the token itself appears under no key.

**Header capture is opt-in.** `CaptureAllHeaders` is `false`. Capturing everything retains any
bespoke credential header the deny list does not know about, so enabling it is a deliberate
decision. The deny list remains as a backstop and covers `Authorization`, `Proxy-Authorization`,
`Cookie`, `X-Api-Key`, and other recurring names.

**Signed blobs are not retained.** Tracking evidence, the DPoP proof, and `Agid-JWT-Signature` are
decoded to extract their claims, but the raw token is not stored unless explicitly requested.

Behaviour is fail-soft throughout. A header that is missing, malformed, or oversized is ignored and
the request proceeds. No parsing error turns into a rejected request.

The `Forwarded` and `X-Forwarded-For` headers are trustworthy only when set by a reverse proxy or a
trusted gateway. On open networks the caller controls them, and they are not an identity signal.

## Observability

`Pdnd.Metadata.Diagnostics` projects the snapshot onto the current activity tags and onto a request
counter. It takes no dependency on any OpenTelemetry package: it uses `Activity` and `Meter` from
the base class library, which the SDK consumes once the meter is subscribed.

Tags are written to the current `Activity`, which is the request activity ASP.NET Core creates:
to see them in traces you need the OpenTelemetry ASP.NET Core instrumentation. The library does not
create an `ActivitySource` of its own, so `AddSource(PdndInstrumentation.ActivitySourceName)` is not
needed.

```bash
dotnet add package Pdnd.Metadata.Diagnostics
```

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation())
    .WithMetrics(m => m.AddMeter(PdndInstrumentation.MeterName));

app.UsePdndMetadata();
app.UsePdndDiagnostics();
```

The dimensions of the `pdnd.requests` counter are limited to three low-cardinality claims:
`consumerId` (falling back to `organizationId` for legacy tokens), `purposeId`, and `client_id`.
Identifiers such as `jti` remain trace tags and do not become metric dimensions.

Those claims come from a voucher that has not been verified yet, so any caller can set them.
Each dimension records at most `MaxDistinctValuesPerDimension` distinct values (default `100`);
later values are recorded as `other`. Dimensions can also be switched off.

```csharp
builder.Services.AddPdndDiagnostics(o =>
{
    o.MaxDistinctValuesPerDimension = 500;
    // o.IncludeConsumerDimensions = false;
});
```

## Specification and conformance vectors

The [`spec/`](spec/) directory holds the canonical key vocabulary and a language-neutral corpus of
conformance vectors. Each vector pairs a set of inbound headers with the keys a conforming
implementation must produce, and with the keys it must not produce.

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/diagrams/conformance-dark.svg">
    <img src="assets/diagrams/conformance.svg" alt="Conformance model">
  </picture>
</p>

The corpus is executed by this library's test suite and by a second reference implementation in
Python, written from the specification rather than ported from the C#. That implementation is not
published and has no users: it exists to prove the specification is complete, and to find where it
is not.

The `adversarial` group is the most useful part. It covers four-segment tokens, standard Base64
characters where Base64Url is required, invalid UTF-8 that a lenient decoder silently replaces, and
`alg: none`.

## Configuration

| Option | Default | Effect |
| --- | --- | --- |
| `CaptureAllHeaders` | `false` | Capture every header, subject to the deny list |
| `HeaderAllowList` | minimal set | Headers captured when capture is not global |
| `HeaderDenyList` | known credentials | Headers never captured in the clear |
| `MaxHeaderCount` | `64` | Maximum number of distinct headers |
| `MaxHeaderValuesPerName` | `10` | Maximum values per header name |
| `MaxValueLength` | `2048` | Maximum length of a value before truncation |
| `MaxTokenLength` | `16384` | Maximum length of a token to decode |
| `PathExclusions` | empty | Paths excluded from extraction, matched by segment: `/health` does not exclude `/healthcare` |
| `CaptureRawTrackingEvidenceHeader`, `CaptureRawDpopHeader`, `CaptureRawSignatureHeader` | `false` | Capture raw signed blobs, discouraged in production |
| `CaptureRawDigestHeader` | `true` | Raw capture of `Digest` and `Content-Digest`, which are not credentials |
| `Parse*`, `PromoteTracingHeaders`, `NormalizeForwardedHeaders` | `true` | Each decoding step can be switched off on its own; the others are unaffected |

`Pdnd.Metadata.Diagnostics` options are registered with `AddPdndDiagnostics()`.

| Option | Default | Effect |
| --- | --- | --- |
| `IncludeConsumerDimensions` | `true` | Dimension the counter by consumer, purpose, and client |
| `MaxDistinctValuesPerDimension` | `100` | Distinct values per dimension; later ones become `other` |

## Compatibility

Version 2.0.0 introduces two breaking changes. `CaptureAllHeaders` now defaults to `false`, and the
`Items` property is replaced by the `Snapshot()` method, kept as obsolete until 3.0.0.

In version 2.1.0, the `pdnd.requests` counter dimension that identifies the consumer
organization is named `pdnd.voucher.consumerId` instead of `pdnd.voucher.organizationId`: current
PDND vouchers do not carry `organizationId`, so the old dimension always read `unknown`. Dashboards
that use it must be updated. Details are in the [changelog](CHANGELOG.md).

## Official PDND documentation

- [PDND Interoperability guides](https://developer.pagopa.it/pdnd-interoperabilita/guides)
- [Using vouchers](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/riferimenti-tecnici/utilizzare-i-voucher)
- [DPoP reference](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/riferimenti-tecnici/utilizzare-i-voucher/approfondimento-su-dpop)
- [Verifying a DPoP voucher (provider)](https://developer.pagopa.it/it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/v1.0/tutorial/tutorial-per-lerogatore/come-verificare-la-validita-di-un-voucher-dpop)
- [Voucher with additional information (AUDIT_REST_02)](https://developer.pagopa.it/it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/v1.0/tutorial/tutorial-per-il-fruitore/come-richiedere-un-voucher-bearer-per-le-api-di-un-erogatore-con-informazioni-aggiuntive)
- [Tracing operating manual](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-tracing)
- [AgID technical interoperability guidelines](https://www.agid.gov.it/it/argomenti/interoperabilita)

## Contributing

Fork, change, commit, open a pull request. Commit messages follow
[Conventional Commits](https://www.conventionalcommits.org/) and are checked in CI.

```bash
dotnet test --solution Pdnd.Metadata.slnx
```

Conformance vectors are worth more than prose. If you have a token shape from a real integration
that the corpus does not cover, open one with the values replaced by synthetic equivalents. Vectors
documenting a case this implementation handles badly are especially welcome, and are merged as
failing tests before the fix.

## Author and maintainer

| [![Francesco Del Re](https://github.com/engineering87.png?size=100)](https://github.com/engineering87) |
| ------------------------------------------------------------------------------------------------------ |
| **Francesco Del Re** |
| Author & Maintainer |

For any information: francesco.delre[at]protonmail.com.

Report vulnerabilities through the [security policy](SECURITY.md) rather than public issues.

## License

Distributed under the MIT license. See [LICENSE](LICENSE).
