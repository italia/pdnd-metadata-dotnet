# Pdnd.Metadata.Diagnostics

Projects the `PdndCallerMetadata` snapshot onto `Activity` tags and a request counter.

No dependency on any OpenTelemetry package. The types used, `Activity` and `Meter`, are part of
the base class library, and the OpenTelemetry SDK consumes them once the source names are
subscribed:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(PdndInstrumentation.ActivitySourceName))
    .WithMetrics(m => m.AddMeter(PdndInstrumentation.MeterName));
```

Then, anywhere the snapshot is available:

```csharp
PdndInstrumentation.Record(accessor.Current);
```

Or automatically, via the ASP.NET Core integration:

```csharp
app.UsePdndMetadata();
app.UsePdndDiagnostics();
```
