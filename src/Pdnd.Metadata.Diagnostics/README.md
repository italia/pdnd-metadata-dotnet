# Pdnd.Metadata.Diagnostics

Projects the `PdndCallerMetadata` snapshot onto `Activity` tags and a request counter.

No dependency on any OpenTelemetry package. The types used, `Activity` and `Meter`, are part of
the base class library, and the OpenTelemetry SDK consumes them natively.

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation())
    .WithMetrics(m => m.AddMeter(PdndInstrumentation.MeterName));

builder.Services.AddPdndMetadata();
builder.Services.AddPdndDiagnostics();   // optional, to change the defaults below

app.UseAuthentication();
app.UsePdndMetadata();
app.UsePdndDiagnostics();
```

Or, anywhere the snapshot is available:

```csharp
PdndInstrumentation.Record(accessor.Current);
```

## What is recorded

**Activity tags.** Written to `Activity.Current`, the request activity ASP.NET Core creates, so
they appear in traces once the ASP.NET Core instrumentation is enabled. No `ActivitySource` of its
own is created. The promoted keys are a narrow set of PDND identity and integrity signals, among
them `pdnd.voucher.consumerId`, `purposeId`, `client_id`, `eserviceId`, `descriptorId`, `jti`,
and `pdnd.signature.kid`. Never the token itself.

**Counter `pdnd.requests`.** One measurement per request, dimensioned by:

| Dimension | Source |
| --- | --- |
| `pdnd.voucher.consumerId` | `consumerId`, falling back to the legacy `organizationId` |
| `pdnd.voucher.purposeId` | `purposeId` |
| `pdnd.voucher.clientId` | `clientId`, falling back to `client_id` |

An absent claim is recorded as `unknown`.

## Cardinality

The dimensions come from a voucher that has not been verified at this point, so any caller can set
them. Each dimension records at most `MaxDistinctValuesPerDimension` distinct values; values first
seen after that are recorded as `other`.

| Option | Default | Effect |
| --- | --- | --- |
| `IncludeConsumerDimensions` | `true` | When `false`, the counter carries no dimensions |
| `MaxDistinctValuesPerDimension` | `100` | Distinct values per dimension before `other` |

```csharp
builder.Services.AddPdndDiagnostics(o => o.MaxDistinctValuesPerDimension = 500);
```
