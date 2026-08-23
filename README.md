<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/logo-dark.svg">
    <img src="assets/logo.svg" alt="Pdnd.Metadata" width="96">
  </picture>
</p>

<h1 align="center">Pdnd.Metadata</h1>

<p align="center">
  Estrazione dei metadati delle richieste PDND per servizi .NET.
</p>

<p align="center">
  <a href="https://opensource.org/licenses/MIT"><img alt="Licenza MIT" src="https://img.shields.io/badge/License-MIT-yellow.svg"></a>
  <a href="https://www.nuget.org/packages/Pdnd.Metadata"><img alt="NuGet" src="https://img.shields.io/nuget/v/Pdnd.Metadata?style=flat-square"></a>
  <a href="https://www.nuget.org/packages/Pdnd.Metadata"><img alt="Download" src="https://img.shields.io/nuget/dt/Pdnd.Metadata?style=flat-square"></a>
  <a href="https://github.com/italia/pdnd-metadata-dotnet/issues"><img alt="Issue" src="https://img.shields.io/github/issues/italia/pdnd-metadata-dotnet?style=flat-square"></a>
  <a href="README.EN.md"><img alt="English" src="https://img.shields.io/badge/lang-en-blue"></a>
</p>

---

Chi espone un e-service sulla PDND riceve richieste che trasportano un voucher, spesso un token di
tracking evidence, una prova DPoP, un digest del corpo, e una firma di integrità. Sapere chi sta
chiamando, con quale finalità, e come correlare la chiamata richiede di leggere e decodificare
tutti questi header.

Questa libreria lo fa una volta sola, in modo uniforme, e produce uno snapshot con nomi di chiave
stabili. Il vocabolario di chiavi è definito in una specifica separata, cosicché due servizi della
stessa amministrazione registrino lo stesso fatto sotto lo stesso nome.

```csharp
var md = accessor.Current;

md.GetFirstValue(PdndMetadataKeys.PdndVoucherOrganizationId);  // "org-001"
md.GetFirstValue(PdndMetadataKeys.PdndVoucherPurposeId);       // "purpose-001"
md.GetFirstValue(PdndMetadataKeys.CorrelationId);              // "b7c1..."
```

## Confini

La libreria estrae e normalizza. Non verifica.

| Fa | Non fa |
| --- | --- |
| Decodifica voucher, tracking evidence, e prove DPoP | Verifica delle firme JWS |
| Normalizza `Digest` e `Content-Digest` | Confronto del digest con il corpo effettivo |
| Legge i claim di `Agid-JWT-Signature` | Controllo di corrispondenza degli header firmati |
| Promuove correlazione e tracing | Rilevamento di replay o di scadenza |
| Applica limiti su ciò che viene trattenuto | Qualsiasi decisione di autorizzazione |

Voucher e token vanno validati nel proprio livello di autenticazione, in un gateway, o in un
middleware dedicato. Nessun controllo di sicurezza deriva dall'uso di questa libreria.

## Pacchetti

| Pacchetto | Contenuto |
| --- | --- |
| `Pdnd.Metadata` | Pipeline di estrazione, indipendente dal framework web |
| `Pdnd.Metadata.AspNetCore` | Middleware, registrazione DI, accessor, binding per Minimal API |
| `Pdnd.Metadata.Diagnostics` | Proiezione su `Activity` e metriche, compatibile con OpenTelemetry |

Tutti e tre supportano `net8.0` e `net10.0`.

```bash
dotnet add package Pdnd.Metadata.AspNetCore
```

## Uso

Registrazione dei servizi. La postura consigliata cattura solo ciò che si governa esplicitamente.

```csharp
builder.Services.AddPdndMetadata(options =>
{
    options.CaptureAllHeaders = false;

    options.HeaderAllowList.Add("traceparent");
    options.HeaderAllowList.Add("x-correlation-id");
    options.HeaderAllowList.Add("x-forwarded-for");

    options.PathExclusions.Add("/health");
    options.PathExclusions.Add("/metrics");
});
```

Middleware, prima del mapping degli endpoint.

```csharp
app.UsePdndMetadata();
```

Lettura in un controller.

```csharp
[HttpGet("/resource")]
public IActionResult Get([FromServices] IPdndMetadataAccessor accessor)
{
    var md = accessor.Current;

    return Ok(new
    {
        organization = md?.GetFirstValue(PdndMetadataKeys.PdndVoucherOrganizationId),
        purpose = md?.GetFirstValue(PdndMetadataKeys.PdndVoucherPurposeId)
    });
}
```

Lettura in una Minimal API, tramite binding del parametro.

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

`Snapshot()` restituisce l'intera mappa. È una copia completa, quindi va chiamata una volta e
riusata, non dentro un ciclo.

## Metadati estratti

Le chiavi seguono uno schema gerarchico. Questi sono i prefissi.

| Prefisso | Origine |
| --- | --- |
| `http.*`, `net.*` | Metodo, schema, host, path, indirizzi, e porte |
| `trace.*`, `correlation.id` | W3C Trace Context e identificativi di richiesta |
| `pdnd.voucher.*` | Claim del voucher, inclusi `purposeId` e `organizationId` |
| `pdnd.trackingEvidence.*` | Claim del token di tracking evidence |
| `pdnd.dpop.*` | Prova DPoP, inclusi `ath` e `nonce` |
| `pdnd.digest.*`, `pdnd.content_digest.*` | Digest del corpo, formato legacy e RFC 9530 |
| `pdnd.signature.*` | Claim di `Agid-JWT-Signature`, inclusi gli header firmati |
| `http.header.*` | Header grezzi, quando la cattura è abilitata |
| `metadata.truncated` | Presente quando la cattura si è fermata a un limite configurato |

L'elenco completo, con il claim di origine di ogni chiave, si trova in
[`spec/PDND_METADATA_SCHEMA.md`](spec/PDND_METADATA_SCHEMA.md).

## Sicurezza

Tre comportamenti valgono come garanzie, non come impostazioni predefinite da modificare per
distrazione.

**L'header `Authorization` non viene mai memorizzato in chiaro.** Il voucher viene decodificato e
se ne estraggono i claim, ma il token non compare in nessuna chiave.

**La cattura degli header è opt-in.** `CaptureAllHeaders` vale `false`. Catturare tutto trattiene
qualunque header di credenziale che la deny list non conosce, quindi abilitarlo è una decisione
deliberata. La deny list resta come rete di sicurezza e copre `Authorization`,
`Proxy-Authorization`, `Cookie`, `X-Api-Key`, e altri nomi ricorrenti.

**I blob firmati non vengono trattenuti.** Tracking evidence, prova DPoP, e `Agid-JWT-Signature`
vengono decodificati per estrarne i claim, ma il token grezzo non viene salvato salvo richiesta
esplicita.

Il comportamento è fail-soft lungo tutto il percorso. Un header assente, malformato, o troppo
grande viene ignorato e la richiesta prosegue. Nessun errore di parsing si trasforma in una
richiesta respinta.

Gli header `Forwarded` e `X-Forwarded-For` sono attendibili solo se impostati da un reverse proxy
o da un gateway fidato. Su reti aperte sono controllabili dal chiamante e non costituiscono un
segnale di identità.

## Osservabilità

`Pdnd.Metadata.Diagnostics` proietta lo snapshot sui tag dell'attività corrente e su un contatore
di richieste. Non dipende da alcun pacchetto OpenTelemetry: usa `Activity` e `Meter` della libreria
di base, che l'SDK consuma sottoscrivendo i nomi delle sorgenti.

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(PdndInstrumentation.ActivitySourceName))
    .WithMetrics(m => m.AddMeter(PdndInstrumentation.MeterName));

app.UsePdndMetadata();
app.UsePdndDiagnostics();
```

Le dimensioni del contatore sono limitate a tre claim a bassa cardinalità. Identificativi come
`jti` restano tag di traccia e non diventano dimensioni di metrica.

## Specifica e vettori di conformità

La directory [`spec/`](spec/) contiene il vocabolario di chiavi canoniche e un corpus di vettori di
conformità in formato neutro rispetto al linguaggio. Ogni vettore associa un insieme di header in
ingresso alle chiavi che un'implementazione conforme deve produrre, e a quelle che non deve
produrre.

Il corpus viene eseguito dalla suite di test di questa libreria. Un'implementazione in un altro
linguaggio può eseguire gli stessi vettori con poche decine di righe.

Il gruppo `adversarial` è la parte più utile: copre token a quattro segmenti, caratteri Base64
standard dove serve Base64Url, UTF-8 non valido che un decoder permissivo sostituisce in silenzio,
e `alg: none`.

## Configurazione

| Opzione | Predefinito | Effetto |
| --- | --- | --- |
| `CaptureAllHeaders` | `false` | Cattura di tutti gli header, soggetta alla deny list |
| `HeaderAllowList` | insieme minimo | Header catturati quando la cattura non è globale |
| `HeaderDenyList` | credenziali note | Header mai catturati in chiaro |
| `MaxHeaderCount` | `64` | Numero massimo di header distinti |
| `MaxHeaderValuesPerName` | `10` | Valori massimi per nome di header |
| `MaxValueLength` | `2048` | Lunghezza massima di un valore, oltre la quale viene troncato |
| `MaxTokenLength` | `16384` | Lunghezza massima di un token da decodificare |
| `PathExclusions` | vuoto | Prefissi di path per cui l'estrazione viene saltata |
| `CaptureRaw*` | `false` | Cattura dei blob firmati grezzi, sconsigliata in produzione |

## Compatibilità

La versione 2.0.0 introduce due modifiche non compatibili. `CaptureAllHeaders` passa a `false`, e
la proprietà `Items` è sostituita dal metodo `Snapshot()`, mantenuta come obsoleta fino alla 3.0.0.
Il dettaglio è nel [changelog](CHANGELOG.md).

## Documentazione ufficiale PDND

- [Guide PDND Interoperabilità](https://developer.pagopa.it/pdnd-interoperabilita/guides)
- [Utilizzo dei voucher](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/riferimenti-tecnici/utilizzare-i-voucher)
- [Approfondimento su DPoP](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/riferimenti-tecnici/utilizzare-i-voucher/approfondimento-su-dpop)
- [Manuale operativo tracing](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-tracing)
- [Linee Guida AgID sull'interoperabilità tecnica](https://www.agid.gov.it/it/argomenti/interoperabilita)

## Contribuire

Fork, modifica, commit, pull request. I messaggi di commit seguono
[Conventional Commits](https://www.conventionalcommits.org/) e sono verificati in CI.

I vettori di conformità valgono più della prosa. Se hai la forma di un token proveniente da
un'integrazione reale che il corpus non copre, aprine uno con i valori sostituiti da equivalenti
sintetici. I vettori che documentano un caso gestito male da questa implementazione sono
particolarmente benvenuti, e vengono accettati come test che falliscono prima della correzione.

## Autore e maintainer

| [![Francesco Del Re](https://github.com/engineering87.png?size=100)](https://github.com/engineering87) |
| ------------------------------------------------------------------------------------------------------ |
| **Francesco Del Re** |
| Autore e maintainer |

Per qualsiasi informazione: francesco.delre[at]protonmail.com.

Le vulnerabilità vanno segnalate secondo la [security policy](SECURITY.md), non tramite issue
pubbliche.

## Licenza

Distribuito con licenza MIT. Vedi [LICENSE](LICENSE).
