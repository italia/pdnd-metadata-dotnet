# Pdnd.Metadata

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![issues - pdndmetadata](https://img.shields.io/github/issues/engineering87/pdnd-metadata-dotnet)](https://github.com/engineering87/pdnd-metadata-dotnet/issues)
[![stars - pdndmetadata](https://img.shields.io/github/stars/engineering87/pdnd-metadata-dotnet?style=social)](https://github.com/engineering87/pdnd-metadata-dotnet)
[![EN](https://img.shields.io/badge/lang-en-blue)](./README.md)
[![IT](https://img.shields.io/badge/lang-it-green)](./README.it.md)
[![Sponsor me](https://img.shields.io/badge/Sponsor-â¤-pink)](https://github.com/sponsors/engineering87)

**Pdnd.Metadata** Ã¨ una libreria .NET leggera progettata per estrarre in modo coerente i **metadati delle richieste** dalle chiamate HTTP in ingresso, in un formato agnostico rispetto al trasporto HTTP, con supporto dedicato per scenari **PDND** (voucher, tracking evidence, digest, DPoP) e per i segnali standard di correlazione/tracing.

La libreria nasce per unâ€™esigenza molto pratica: quando esponi un e-service come **provider (erogatore)**, spesso devi capire *chi sta chiamando*, *con quale contesto PDND*, e *come la chiamata puÃ² essere correlata e auditata*, senza disseminare parsing â€œad hocâ€ degli header tra controller, minimal API e middleware.

## Contenuti

| Sezione | Cosa troverai |
|---|---|
| [PerchÃ© esiste questa libreria](#perchÃ©-esiste-questa-libreria) | Il problema che risolve nei servizi provider reali |
| [Panoramica PDND](#panoramica-pdnd) | Cosâ€™Ã¨ la PDND e perchÃ© la richiesta in ingresso trasporta token |
| [Cosa fa Pdnd.Metadata](#cosa-fa-pdndmetadata) | ResponsabilitÃ , modello dati e confini |
| [Campi estratti](#campi-estratti) | Chiavi canoniche prodotte dallâ€™estrattore (generiche + specifiche PDND) |
| [Modello di sicurezza](#modello-di-sicurezza) | Cosa non viene mai memorizzato, comportamento fail-soft, default consigliati |
| [Struttura dei pacchetti](#struttura-dei-pacchetti) | Core vs integrazione ASP.NET Core |
| [Quick start (ASP.NET Core)](#quick-start-aspnet-core) | Registrazione, middleware e consumo dei metadati |
| [Configurazione consigliata per produzione](#configurazione-consigliata-per-produzione) | Postura conservativa e note di governance |
| [API di esempio](#api-di-esempio) | Endpoint per verificare localmente lâ€™estrazione |
| [Cosa questa libreria non fa](#cosa-questa-libreria-non-fa) | Non-obiettivi espliciti (validazione, enforcement, chiamate API PDND) |
| [Schema chiavi canoniche](#schema-chiavi-canoniche) | Riferimento completo di tutte le chiavi di metadati estratte |
| [Riferimenti ufficiali PDND](#riferimenti-ufficiali-pdnd) | Link alla documentazione ufficiale |

## PerchÃ© esiste questa libreria

Nei servizi provider, lâ€™estrazione dei metadati tende a crescere in modo organico:
- team diversi parsano header diversi in modo diverso,
- gli ID di correlazione vengono duplicati o sovrascritti,
- i token PDND vengono trattati come stringhe raw (con rischio di log accidentali),
- e la stessa logica viene re-implementata in controller, minimal API o filtri di gateway.

**Pdnd.Metadata** standardizza questo lavoro:
- raccoglie i metadati in uno snapshot strutturato (`PdndCallerMetadata`),
- estrae informazioni PDND in modalitÃ  best-effort e non bloccante,
- rende raggiungibili â€œsafe defaultsâ€ (niente `Authorization` raw, niente blob firmati di default),
- e mantiene separata lâ€™integrazione ASP.NET Core dalla logica core di estrazione.

## Panoramica PDND

**PDND** abilita lo scambio sicuro e tracciabile di servizi/dati tra amministrazioni. Nel pattern di interazione piÃ¹ comune, il fruitore chiama lâ€™erogatore inviando un **voucher** (JWT) in:

- `Authorization: Bearer <voucher>`

Riferimento ufficiale: utilizzo voucher. ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/riferimenti-tecnici/utilizzare-i-voucher))

Per alcuni e-service, informazioni aggiuntive vengono veicolate tramite un token di **Tracking Evidence** in un header dedicato. Riferimento ufficiale: â€œvoucher bearer â€¦ con informazioni aggiuntiveâ€. ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/tutorial/tutorial-per-il-fruitore/come-richiedere-un-voucher-bearer-per-le-api-di-un-erogatore-con-informazioni-aggiuntive))

La libreria cattura anche segnali standard di correlazione/tracing comunemente usati nei moderni servizi HTTP (es. W3C Trace Context). PDND fornisce indicazioni su pratiche di tracing/osservabilitÃ  per il monitoraggio dellâ€™interoperabilitÃ . Riferimento ufficiale: manuale tracing. ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-tracing))

Infine, la piattaforma include flussi **DPoP** (proof-of-possession). Riferimento ufficiale: approfondimento su DPoP. ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/riferimenti-tecnici/utilizzare-i-voucher/approfondimento-su-dpop))

## Cosa fa Pdnd.Metadata

A runtime, la libreria costruisce uno **snapshot di metadati** che rappresenta ciÃ² che Ã¨ possibile inferire in modo sicuro e utile dalla richiesta in ingresso:

1. **Metadati generici della richiesta**
   - method/scheme/host/path/query
   - IP e porte remote/local
   - catena forwarded normalizzata (best-effort)
   - hint di correlazione e tracing (W3C trace context + request id comuni)
   - header selezionati (regole di cattura configurabili)

2. **Estrazione PDND-aware (best-effort)**
   - voucher: parsing del JOSE header JWT (alg/kid/typ) e payload, estrazione di claim standard e PDND-specific
   - tracking evidence: parsing di header/payload token ed estrazione di campi selezionati
   - digest: parsing del valore dell'header `Digest` nellâ€™header in una coppia normalizzata (alg, value)
   - content-digest: parsing dell'header `Content-Digest` (RFC 9530) in una coppia normalizzata (alg, value)
   - DPoP: parsing di header/payload proof token ed estrazione di campi selezionati (incl. ath, nonce per RFC 9449)
   - signature: parsing dell'header `Agid-JWT-Signature` per campi di integrita della richiesta

3. **Comportamento fail-soft**
   - header mancanti ignorati
   - errori di parsing â€œswallowedâ€
   - token troppo grandi saltati (guard-rail tramite `MaxTokenLength`)

Lâ€™output Ã¨ pensato per essere stabile e facile da integrare con logging, auditing o dashboard di tracing interne senza esporre segreti.

## Campi estratti

Lo snapshot Ã¨ un `PdndCallerMetadata` contenente item indicizzati da chiavi canoniche.

### Chiavi generiche

- `http.method`, `http.scheme`, `http.host`, `http.path`, `http.query`
- `net.remote_ip`, `net.remote_port`, `net.local_ip`, `net.local_port`, `net.forwarded_for`
- `correlation.id`
- `trace.traceparent`, `trace.tracestate`, `trace.baggage`
- `http.header.<lowercase-name>` (solo se lâ€™header capture Ã¨ abilitato e lâ€™header non Ã¨ negato)

> Nota sugli header forwarded: valori come `Forwarded` / `X-Forwarded-For` sono affidabili solo se impostati da un reverse proxy / API gateway trusted. In reti aperte sono controllabili dallâ€™utente e non vanno considerati segnali di identitÃ  autorevoli.

### Chiavi PDND

#### Voucher (da `Authorization: Bearer ...`)
- `pdnd.voucher.alg`, `pdnd.voucher.kid`, `pdnd.voucher.typ` (JOSE header)
- `pdnd.voucher.iss`
- `pdnd.voucher.sub`
- `pdnd.voucher.aud` (stringa normalizzata; puÃ² originare da un array JWT)
- `pdnd.voucher.jti`
- `pdnd.voucher.iat`, `pdnd.voucher.nbf`, `pdnd.voucher.exp` (memorizzati come stringhe; tipicamente epoch seconds)
- `pdnd.voucher.purposeId` (se presente)
- `pdnd.voucher.clientId`, `pdnd.voucher.client_id` (se presente)
- `pdnd.voucher.organizationId` (organizzazione fruitore PDND, se presente)
- `pdnd.voucher.dnonce` (nonce anti-replay, se presente)

Riferimento: utilizzo e semantica voucher. ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/riferimenti-tecnici/utilizzare-i-voucher))

#### Tracking Evidence (da `Agid-JWT-Tracking-Evidence` / `AgID-JWT-TrackingEvidence`)
- `pdnd.trackingEvidence.alg`, `pdnd.trackingEvidence.kid`, `pdnd.trackingEvidence.typ`
- `pdnd.trackingEvidence.iss`, `pdnd.trackingEvidence.sub`, `pdnd.trackingEvidence.jti` (se presenti)
- `pdnd.trackingEvidence.aud` (se presente; puo essere separato da virgole)
- `pdnd.trackingEvidence.iat`, `pdnd.trackingEvidence.nbf`, `pdnd.trackingEvidence.exp` (se presenti)

**Nota di compatibilitÃ :** nella documentazione PDND il nome dellâ€™header appare in due varianti (`Agid-JWT-Tracking-Evidence` e `AgID-JWT-TrackingEvidence`). Lâ€™estrattore supporta entrambe per interoperabilitÃ .

Riferimento: flussi con informazioni aggiuntive. ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/tutorial/tutorial-per-il-fruitore/come-richiedere-un-voucher-bearer-per-le-api-di-un-erogatore-con-informazioni-aggiuntive))

#### Digest (da `Digest`)
- `pdnd.digest.alg`
- `pdnd.digest.value`

Riferimento: note digest nelle FAQ voucher. ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/PDND-Interoperability-Operating-Manual/technical-references/utilizzare-i-voucher/faqs))

#### DPoP (da `DPoP`)
- `pdnd.dpop.alg`, `pdnd.dpop.kid`, `pdnd.dpop.typ`
- `pdnd.dpop.htm`, `pdnd.dpop.htu`, `pdnd.dpop.jti`, `pdnd.dpop.iat`, `pdnd.dpop.exp` (se presenti)
- `pdnd.dpop.ath` (hash access token, RFC 9449 u00a74.2)
- `pdnd.dpop.nonce` (nonce fornito dal server, RFC 9449 u00a74.3)

Riferimento: approfondimento DPoP. ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/riferimenti-tecnici/utilizzare-i-voucher/approfondimento-su-dpop))

#### Content-Digest (da `Content-Digest`, RFC 9530)
- `pdnd.content_digest.alg`
- `pdnd.content_digest.value`

RFC 9530 sostituisce l'header legacy `Digest` con `Content-Digest` usando il formato structured field dictionary (`alg=:base64value:`). La libreria supporta entrambi.

#### Agid-JWT-Signature (da `Agid-JWT-Signature`)
- `pdnd.signature.alg`, `pdnd.signature.kid`, `pdnd.signature.typ` (JOSE header)
- `pdnd.signature.iss`, `pdnd.signature.sub`, `pdnd.signature.jti` (se presenti)
- `pdnd.signature.aud` (se presente; puo essere separato da virgole)
- `pdnd.signature.iat`, `pdnd.signature.exp` (se presenti)
- `pdnd.signature.signed_headers` (digest degli header firmati per integrita)

Usato nel pattern PDND INTEGRITY_REST_01 per la firma delle richieste.

## Modello di sicurezza

### Cosa non viene mai memorizzato (raw)
Di default, la libreria non memorizza mai:
- `Authorization` (raw)
- `Cookie`, `Set-Cookie`

Questo previene la persistenza o il logging accidentale di segreti.

### Blob firmati (raw)
Di default, la libreria **non** memorizza valori raw per:
- header Tracking Evidence
- header DPoP
- header Agid-JWT-Signature

Invece, effettua parsing best-effort e memorizza campi selezionati sotto chiavi canoniche `pdnd.*`. Questo rende lâ€™output ispezionabile riducendo il rischio di leakage.

### Comportamento fail-soft
- Qualunque errore di parsing viene ignorato; la request prosegue.
- Token piÃ¹ lunghi di `MaxTokenLength` vengono saltati.
- Header PDND mancanti non producono errori.

### Nota operativa
La cattura degli header puÃ² comunque raccogliere dati sensibili se il tuo servizio (o i gateway) iniettano header applicativi contenenti informazioni personali. In produzione Ã¨ raccomandata una allow-list stretta (vedi sotto).

## Struttura dei pacchetti

- `Pdnd.Metadata`  
  Astrazioni core e pipeline di estrazione, incluse utility di parsing PDND.

- `Pdnd.Metadata.AspNetCore`  
  Integrazione ASP.NET Core: middleware, accessor e tipi di binding per minimal API.

## Quick start (ASP.NET Core)

### 1) Registra i servizi (production-first)

Preferisci un approccio allow-list: cattura solo ciÃ² che serve (trace/correlation/forwarded + eventuali header PDND se decidi di persisterli come header raw).

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

### ModalitÃ  demo (solo local)

Se vuoi ispezionare gli header in sviluppo locale, puoi abilitare temporaneamente la cattura completa:

```csharp
builder.Services.AddPdndMetadata(options =>
{
    options.CaptureAllHeaders = true;

    options.ParsePdndVoucherFromAuthorizationBearer = true;
    options.ParsePdndTrackingEvidence = true;
    options.ParseDpopHeader = true;
    options.ParseDigestHeader = true;

    options.CaptureRawTrackingEvidenceHeader = false;
    options.CaptureRawDpopHeader = false;

    options.MaxTokenLength = 16_384;
});
```

### 2) Aggiungi il middleware

Posizionalo prima del mapping degli endpoint cosÃ¬ ogni request ottiene uno snapshot.

```csharp
app.UsePdndMetadata();
```

### 3) Consuma i metadati (Controllers)

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

### 4) Consuma i metadati (Minimal APIs)

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

## Configurazione consigliata per produzione

Per i servizi in produzione, in genere Ã¨ meglio decidere esplicitamente *quali header catturare* invece di collezionare tutto e filtrare dopo.

Approccio conservativo:
- `CaptureAllHeaders = false`
- mantieni una `HeaderAllowList` stretta (trace + correlation + forwarded + solo ciÃ² che governi esplicitamente)
- mantieni `CaptureRawTrackingEvidenceHeader = false`, `CaptureRawDpopHeader = false` e `CaptureRawSignatureHeader = false`
- valuta di disabilitare `CaptureRawDigestHeader` se non serve davvero
- nelle pipeline di logging/audit evita di persistere lâ€™intera mappa `items` se non hai governance; preferisci loggare solo chiavi canoniche whitelistate

La libreria applica giÃ  la regola piÃ¹ importante di default: `Authorization` raw non viene mai memorizzato.

## API di esempio

Il progetto di esempio serve per validare rapidamente lâ€™integrazione, senza loggare token raw.

- `GET /minimal/pdnd`  
  Restituisce sezioni voucher / trackingEvidence / dpop / digest.

- `GET /minimal/sanity`  
  Verifica che `Authorization` raw, `DPoP` raw e tracking evidence raw non vengano catturati.

Esempio request (token fake sono sufficienti per verificare lâ€™estrazione):

```bash
curl \
  -H "Authorization: Bearer <jwt>" \
  -H "Agid-JWT-Tracking-Evidence: <jws>" \
  -H "DPoP: <dpop-jws>" \
  -H "Digest: SHA-256=<base64>" \
  -H "Content-Digest: sha-256=:<base64>:" \
  -H "Agid-JWT-Signature: <jws>" \
  http://localhost:5043/minimal/pdnd
```

## Cosa questa libreria non fa

Questa Ã¨ intenzionalmente una extraction layer, non una enforcement/security layer.

- Non valida firme JWT/JWS.
- Non applica regole di autorizzazione PDND.
- Non chiama API PDND (catalogo/registry/auth) per arricchire i metadati.
- Non logga token. Se aggiungi logging, limitati a chiavi canoniche `pdnd.*` ed evita header raw.

Se ti serve validazione/enforcement, posizionala nel tuo layer auth (gateway/middleware di servizio) e usa Pdnd.Metadata solo come snapshot â€œaudit-friendlyâ€ per osservabilitÃ /diagnostica.

## Schema chiavi canoniche

Per un riferimento completo e strutturato di tutte le 55+ chiavi canoniche di metadati estratte dalla libreria, consulta il documento **[PDND Metadata Schema](./src/PDND_METADATA_SCHEMA.md)**.

Lo schema copre:
- Tutti i pattern di interoperabilita PDND (ID_AUTH, INTEGRITY, AUDIT)
- Mapping campi JWT/JWS per ogni tipo di token
- Riferimento opzioni di configurazione
- Considerazioni di sicurezza

Questo schema e pensato come **riferimento di community** per standardizzare l'estrazione dei metadati PDND nelle implementazioni .NET.

## Riferimenti ufficiali PDND

- PDND InteroperabilitÃ  â€“ Hub guide ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides))
- Voucher (utilizzo) ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/riferimenti-tecnici/utilizzare-i-voucher))
- Voucher con informazioni aggiuntive (Tracking Evidence) ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/tutorial/tutorial-per-il-fruitore/come-richiedere-un-voucher-bearer-per-le-api-di-un-erogatore-con-informazioni-aggiuntive))
- Voucher FAQ / note Digest ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/PDND-Interoperability-Operating-Manual/technical-references/utilizzare-i-voucher/faqs))
- Tracing â€“ Manuale Operativo ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-tracing))
- Approfondimento DPoP ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/riferimenti-tecnici/utilizzare-i-voucher/approfondimento-su-dpop))
- Release notes ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/release-note/2025))

## Contribuire
Grazie per aver considerato di contribuire al codice sorgente!
Se vuoi contribuire, fai un fork, applica le modifiche, esegui commit e apri una pull request cosÃ¬ che i maintainer possano revisionare e fare merge nel branch principale.

**Per iniziare con Git e GitHub**

 * [Configurare Git](https://docs.github.com/en/get-started/getting-started-with-git/set-up-git)
 * [Fare il fork del repository](https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/working-with-forks/fork-a-repo)
 * [Aprire una issue](https://github.com/engineering87/pdnd-metadata-dotnet/issues) se incontri un bug o hai suggerimenti per miglioramenti/nuove funzionalitÃ 

## Licenza
Il codice sorgente di Pdnd.Metadata Ã¨ distribuito con licenza MIT; consulta il file di licenza nel repository.

## Contatti
Per qualsiasi informazione, contatta francesco.delre[at]protonmail.com.
