# Pdnd.Metadata

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![issues - pdndmetadata](https://img.shields.io/github/issues/italia/pdnd-metadata-dotnet)](https://github.com/italia/pdnd-metadata-dotnet/issues)
[![Nuget](https://img.shields.io/nuget/v/Pdnd.Metadata?style=plastic)](https://www.nuget.org/packages/Pdnd.Metadata)
![NuGet Downloads](https://img.shields.io/nuget/dt/Pdnd.Metadata)
[![stars - pdndmetadata](https://img.shields.io/github/stars/italia/pdnd-metadata-dotnet?style=social)](https://github.com/italia/pdnd-metadata-dotnet)
[![EN](https://img.shields.io/badge/lang-en-blue)](./README.EN.md)
[![IT](https://img.shields.io/badge/lang-it-green)](./README.md)

**Pdnd.Metadata** è una libreria .NET leggera e multi-target (`net8.0` / `net10.0`) che consente di estrarre in modo strutturato i **metadati delle richieste HTTP in ingresso**. È indipendente dal framework web utilizzato e offre un supporto specifico per scenari **PDND** (voucher, tracking evidence, digest, DPoP) e per i principali segnali di correlazione e tracing.

La libreria nasce da esigenze concrete di chi espone e-service come **provider (erogatore)**: spesso è necessario sapere *chi sta chiamando*, *con quale contesto PDND* e *come correlare o tracciare la chiamata*, senza dover scrivere ogni volta codice personalizzato per il parsing degli header nei controller, nelle minimal API o nei middleware.

## Contenuti

| Sezione | Cosa troverai |
|---|---|
| [Perché questa libreria](#perche-questa-libreria) | Il problema che risolve nei servizi provider reali |
| [Cos’è la PDND](#cose-la-pdnd) | Cos’è la PDND e perché la richiesta in ingresso trasporta token |
| [Funzionalità principali](#funzionalita-principali) | Responsabilità, modello dati e confini |
| [Quali metadati vengono estratti](#quali-metadati-vengono-estratti) | Chiavi canoniche prodotte dall’estrattore (generiche + specifiche PDND) |
| [Sicurezza e privacy](#sicurezza-e-privacy) | Cosa non viene mai memorizzato, comportamento fail-soft, default consigliati |
| [Com’è organizzata la libreria](#come-organizzata-la-libreria) | Core vs integrazione ASP.NET Core |
| [Guida rapida all’uso (ASP.NET Core)](#guida-rapida-uso-aspnet-core) | Registrazione, middleware e consumo dei metadati |
| [Configurazione consigliata in produzione](#configurazione-consigliata-in-produzione) | Postura conservativa e note di governance |
| [Esempi di API](#esempi-di-api) | Endpoint per verificare localmente l’estrazione |
| [Cosa non fa la libreria](#cosa-non-fa-la-libreria) | Non-obiettivi espliciti (validazione, enforcement, chiamate API PDND) |
| [Elenco delle chiavi estratte](#elenco-delle-chiavi-estratte) | Riferimento completo di tutte le chiavi di metadati estratte |
| [Documentazione ufficiale PDND](#documentazione-ufficiale-pdnd) | Link alla documentazione ufficiale |
| [Autore e maintainer](#autore-e-manutenzione) | Proprietà e manutenzione del progetto |
| [Come contribuire](#come-contribuire) | Come contribuire al progetto |
| [Licenza](#licenza) | Informazioni sulla licenza |
| [Contatti](#contatti) | Informazioni di contatto |

## Perché questa libreria

Nei servizi provider, la gestione dei metadati tende a diventare disomogenea:
- ogni team interpreta gli header a modo proprio;
- gli identificativi di correlazione rischiano di essere duplicati o sovrascritti;
- i token PDND vengono spesso trattati come semplici stringhe (con il rischio di finire accidentalmente nei log);
- la stessa logica viene ripetuta in controller, minimal API o filtri di gateway.

**Pdnd.Metadata** risolve questi problemi:
- raccoglie i metadati in uno snapshot strutturato (`PdndCallerMetadata`);
- estrae le informazioni PDND in modo best-effort, senza bloccare la richiesta;
- applica impostazioni sicure di default (mai `Authorization` raw, mai blob firmati salvati di default);
- separa l’integrazione con ASP.NET Core dalla logica di estrazione vera e propria.

## Cos’è la PDND

**PDND** abilita lo scambio sicuro e tracciabile di servizi e dati tra amministrazioni. Nel pattern di interazione più comune, il fruitore chiama l’erogatore inviando un **voucher** (JWT) in:

- `Authorization: Bearer <voucher>`

Riferimento ufficiale: utilizzo voucher. ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/riferimenti-tecnici/utilizzare-i-voucher))

Per alcuni e-service, informazioni aggiuntive vengono veicolate tramite un token di **Tracking Evidence** in un header dedicato. Riferimento ufficiale: “voucher bearer … con informazioni aggiuntive”. ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/tutorial/tutorial-per-il-fruitore/come-richiedere-un-voucher-bearer-per-le-api-di-un-erogatore-con-informazioni-aggiuntive))

La libreria cattura anche segnali standard di correlazione e tracing comunemente usati nei moderni servizi HTTP (es. W3C Trace Context). PDND fornisce indicazioni su pratiche di tracing e osservabilità per il monitoraggio dell’interoperabilità. Riferimento ufficiale: manuale tracing. ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-tracing))

Infine, la piattaforma include flussi **DPoP** (proof-of-possession). Riferimento ufficiale: approfondimento su DPoP. ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/riferimenti-tecnici/utilizzare-i-voucher/approfondimento-su-dpop))

## Funzionalità principali

A runtime, la libreria costruisce uno **snapshot dei metadati** che rappresenta tutto ciò che è possibile inferire in modo sicuro e utile dalla richiesta HTTP in ingresso:

1. **Metadati generali della richiesta**
   - `method`, `scheme`, `host`, `path`, `query`
   - IP e porte remote/local
   - catena `forwarded` normalizzata (best-effort)
   - indizi di correlazione e tracing (`traceparent` W3C, request id comuni)
   - header selezionati (regole di cattura configurabili)

2. **Estrazione dei metadati PDND (best-effort)**
   - voucher: parsing del JOSE header JWT (`alg`, `kid`, `typ`) e payload, estrazione di claim standard e PDND-specific
   - tracking evidence: parsing di header/payload token ed estrazione di metadati selezionati
   - digest: parsing del valore dell'header `Digest` in una coppia normalizzata (`alg`, `value`)
   - content-digest: parsing dell'header `Content-Digest` (RFC 9530) in una coppia normalizzata (`alg`, `value`)
   - DPoP: parsing di header/payload proof token ed estrazione di metadati selezionati (inclusi `ath`, `nonce` per RFC 9449)
   - signature: parsing dell'header `Agid-JWT-Signature` per metadati di integrità della richiesta

3. **Comportamento fail-soft**
   - header mancanti ignorati;
   - errori di parsing ignorati;
   - token troppo grandi saltati (guard-rail tramite `MaxTokenLength`).

L’output è progettato per essere stabile e facile da integrare con sistemi di logging, audit o dashboard di tracing interne, senza esporre segreti.

## Quali metadati vengono estratti

Lo snapshot è un oggetto `PdndCallerMetadata` che contiene tutti i **metadati** estratti dalla richiesta, organizzati tramite **chiavi canoniche**.

### Chiavi canoniche generali

- `http.method`, `http.scheme`, `http.host`, `http.path`, `http.query`
- `net.remote_ip`, `net.remote_port`, `net.local_ip`, `net.local_port`, `net.forwarded_for`
- `correlation.id`
- `trace.traceparent`, `trace.tracestate`, `trace.baggage`
- `http.header.<nome-minuscolo>` (solo se la cattura header è abilitata e l’header non è negato)

> **Nota:** valori come `Forwarded` / `X-Forwarded-For` sono affidabili solo se impostati da un reverse proxy o API gateway trusted. In reti aperte sono controllabili dall’utente e non vanno considerati segnali di identità autorevoli.

### Chiavi canoniche PDND

#### Voucher (da `Authorization: Bearer ...`)
- `pdnd.voucher.alg`, `pdnd.voucher.kid`, `pdnd.voucher.typ` (JOSE header)
- `pdnd.voucher.iss`
- `pdnd.voucher.sub`
- `pdnd.voucher.aud` (stringa normalizzata; può originare da un array JWT)
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
- `pdnd.trackingEvidence.aud` (se presente; può essere separato da virgole)
- `pdnd.trackingEvidence.iat`, `pdnd.trackingEvidence.nbf`, `pdnd.trackingEvidence.exp` (se presenti)

**Nota di compatibilità:** nella documentazione PDND il nome dell’header appare in due varianti (`Agid-JWT-Tracking-Evidence` e `AgID-JWT-TrackingEvidence`). L’estrattore supporta entrambe per interoperabilità.

Riferimento: flussi con informazioni aggiuntive. ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/tutorial/tutorial-per-il-fruitore/come-richiedere-un-voucher-bearer-per-le-api-di-un-erogatore-con-informazioni-aggiuntive))

#### Digest (da `Digest`)
- `pdnd.digest.alg`
- `pdnd.digest.value`

Riferimento: note digest nelle FAQ voucher. ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/PDND-Interoperability-Operating-Manual/technical-references/utilizzare-i-voucher/faqs))

#### DPoP (da `DPoP`)
- `pdnd.dpop.alg`, `pdnd.dpop.kid`, `pdnd.dpop.typ`
- `pdnd.dpop.htm`, `pdnd.dpop.htu`, `pdnd.dpop.jti`, `pdnd.dpop.iat`, `pdnd.dpop.exp` (se presenti)
- `pdnd.dpop.ath` (hash access token, RFC 9449 §4.2)
- `pdnd.dpop.nonce` (nonce fornito dal server, RFC 9449 §4.3)

Riferimento: approfondimento DPoP. ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/riferimenti-tecnici/utilizzare-i-voucher/approfondimento-su-dpop))

#### Content-Digest (da `Content-Digest`, RFC 9530)
- `pdnd.content_digest.alg`
- `pdnd.content_digest.value`

RFC 9530 sostituisce l'header legacy `Digest` con `Content-Digest` usando il formato structured field dictionary (`alg=:base64value:`). La libreria supporta entrambi.

#### Agid-JWT-Signature (da `Agid-JWT-Signature`)
- `pdnd.signature.alg`, `pdnd.signature.kid`, `pdnd.signature.typ` (JOSE header)
- `pdnd.signature.iss`, `pdnd.signature.sub`, `pdnd.signature.jti` (se presenti)
- `pdnd.signature.aud` (se presente; può essere separato da virgole)
- `pdnd.signature.iat`, `pdnd.signature.exp` (se presenti)
- `pdnd.signature.signed_headers` (digest degli header firmati per integrita)

Usato nel pattern PDND INTEGRITY_REST_01 per la firma delle richieste.

## Sicurezza e privacy

### Cosa non viene mai memorizzato (raw)
Di default, la libreria non memorizza mai:
- `Authorization` (raw)
- `Cookie`, `Set-Cookie`

Questo previene la persistenza o il logging accidentale di segreti.

### Blob firmati (raw)
Di default, la libreria **non** memorizza valori raw per:
- header Tracking Evidence;
- header DPoP;
- header Agid-JWT-Signature.

Invece, effettua parsing best-effort e memorizza campi selezionati sotto chiavi canoniche `pdnd.*`. Questo rende l’output ispezionabile riducendo il rischio di leakage.

### Comportamento fail-soft
- Qualunque errore di parsing viene ignorato; la richiesta prosegue.
- Token più lunghi di `MaxTokenLength` vengono saltati.
- Header PDND mancanti non producono errori.

### Nota operativa
La cattura degli header può comunque raccogliere dati sensibili se il tuo servizio (o i gateway) iniettano header applicativi contenenti informazioni personali. In produzione è raccomandata una allow-list stretta (vedi sotto).

## Com’è organizzata la libreria

Entrambi i pacchetti supportano **`net8.0`** (LTS) e **`net10.0`**, garantendo compatibilità con le versioni .NET più diffuse negli ambienti di produzione della PA.

- `Pdnd.Metadata`  
  Astrazioni core e pipeline di estrazione, incluse utility di parsing PDND.

- `Pdnd.Metadata.AspNetCore`  
  Integrazione ASP.NET Core: middleware, accessor e tipi di binding per minimal API.

## Guida rapida all’uso (ASP.NET Core)

### 1) Registra i servizi (production-first)

Preferisci un approccio allow-list: cattura solo ciò che serve (trace/correlation/forwarded + eventuali header PDND se decidi di persisterli come header raw).

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

### Modalità demo (solo local)

Se vuoi ispezionare gli header in sviluppo locale, puoi abilitare temporaneamente la cattura completa:

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

### 2) Aggiungi il middleware

Posizionalo prima del mapping degli endpoint così ogni request ottiene uno snapshot.

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

## Configurazione consigliata in produzione

Per i servizi in produzione, in genere è meglio decidere esplicitamente *quali header catturare* invece di collezionare tutto e filtrare dopo.

Approccio conservativo:
- `CaptureAllHeaders = false`
- mantieni una `HeaderAllowList` stretta (trace + correlation + forwarded + solo ciò che governi esplicitamente)
- mantieni `CaptureRawTrackingEvidenceHeader = false`, `CaptureRawDpopHeader = false` e `CaptureRawSignatureHeader = false`
- valuta di disabilitare `CaptureRawDigestHeader` se non serve davvero
- nelle pipeline di logging/audit evita di persistere l’intera mappa `items` se non hai governance; preferisci loggare solo chiavi canoniche whitelistate

La libreria applica già la regola più importante di default: `Authorization` raw non viene mai memorizzato.

## Esempi di API

Il progetto di esempio serve per validare rapidamente l’integrazione, senza loggare token raw.

- `GET /minimal/pdnd`  
  Restituisce sezioni voucher / trackingEvidence / dpop / digest / contentDigest / signature.

- `GET /minimal/sanity`  
  Verifica che `Authorization` raw, `DPoP` raw, tracking evidence raw e `Agid-JWT-Signature` raw non vengano catturati.

Esempio request (token fake sono sufficienti per verificare l’estrazione):

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

## Cosa non fa la libreria

Questa è intenzionalmente una extraction layer, non una enforcement/security layer.

- Non valida firme JWT/JWS.
- Non applica regole di autorizzazione PDND.
- Non chiama API PDND (catalogo/registry/auth) per arricchire i metadati.
- Non logga token. Se aggiungi logging, limitati a chiavi canoniche `pdnd.*` ed evita header raw.

Se ti serve validazione/enforcement, posizionala nel tuo layer auth (gateway/middleware di servizio) e usa Pdnd.Metadata solo come snapshot “audit-friendly” per osservabilità/diagnostica.

## Elenco delle chiavi estratte

Per un riferimento completo e strutturato di tutte le 55+ chiavi canoniche di metadati estratte dalla libreria, consulta il documento **[PDND Metadata Schema](./src/PDND_METADATA_SCHEMA.md)**.

Lo schema copre:
- Tutti i pattern di interoperabilità PDND (ID_AUTH, INTEGRITY, AUDIT)
- Mapping campi JWT/JWS per ogni tipo di token
- Riferimento opzioni di configurazione
- Considerazioni di sicurezza

Questo schema è pensato come **riferimento di community** per standardizzare l'estrazione dei metadati PDND nelle implementazioni .NET.

## Documentazione ufficiale PDND

- PDND Interoperabilità – Hub guide ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides))
- Voucher (utilizzo) ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/riferimenti-tecnici/utilizzare-i-voucher))
- Voucher con informazioni aggiuntive (Tracking Evidence) ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/tutorial/tutorial-per-il-fruitore/come-richiedere-un-voucher-bearer-per-le-api-di-un-erogatore-con-informazioni-aggiuntive))
- Voucher FAQ / note Digest ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/PDND-Interoperability-Operating-Manual/technical-references/utilizzare-i-voucher/faqs))
- Tracing – Manuale Operativo ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-tracing))
- Approfondimento DPoP ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/guides/manuale-operativo-pdnd-interoperabilita/riferimenti-tecnici/utilizzare-i-voucher/approfondimento-su-dpop))
- Release notes ([developer.pagopa.it](https://developer.pagopa.it/pdnd-interoperabilita/release-note/2025))

## Autore e maintainer
| [![Francesco Del Re](https://github.com/engineering87.png?size=100)](https://github.com/engineering87) |
| ------------------------------------------------------------------------------------------------------ |
| **Francesco Del Re** |
| Autore e maintainer |

## Come contribuire
Grazie per aver considerato di contribuire al codice sorgente!
Se vuoi contribuire, fai un fork, applica le modifiche, esegui commit e apri una pull request così che i maintainer possano revisionare e fare merge nel branch principale.

**Per iniziare con Git e GitHub**

 * [Configurare Git](https://docs.github.com/en/get-started/getting-started-with-git/set-up-git)
 * [Fare il fork del repository](https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/working-with-forks/fork-a-repo)
 * [Aprire una issue](https://github.com/italia/pdnd-metadata-dotnet/issues) se incontri un bug o hai suggerimenti per miglioramenti/nuove funzionalità

## Licenza
Il codice sorgente di Pdnd.Metadata è distribuito con licenza MIT; consulta il file di licenza nel repository.

## Contatti
Per qualsiasi informazione, contatta francesco.delre[at]protonmail.com.
