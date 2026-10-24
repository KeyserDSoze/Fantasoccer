# Fantasoccer MCP e collegamento ChatGPT

## Stato e architettura

La feature è **disabilitata per default** (`Mcp:Enabled=false`). Non richiede un nuovo progetto o deployment.

- Endpoint MCP Streamable HTTP: `/mcp`, SDK C# ufficiale `2.3.0`, stateless.
- OpenIddict `7.7.2`: Authorization Code con PKCE **solo S256**, client preregistrati, refresh e revoca.
- Persistenza OAuth nello **stesso account Azure Blob Storage** dei repository Rystem, senza database SQL/SQLite o Entity Framework.
- Login Microsoft esistente: il frontend ottiene il normale bearer e approva il collegamento con quello.
- Gli access token OAuth usano **lo stesso `BearerTokenProtector` ASP.NET Core del frontend**, non un nuovo formato JWT. Sono nuove emissioni, con identità applicativa, audience MCP e scope `fantasoccer.full_access`.
- La connessione consente letture, scritture e amministrazione secondo i permessi dell'account, non soltanto tre tool read-only.
- I bearer del frontend non vengono copiati in ChatGPT. I token MCP non sono accettati come credenziali di login/consenso né sulle URL legacy: le operazioni passano dalla facade.

```mermaid
sequenceDiagram
    participant C as ChatGPT
    participant B as Backend
    participant F as Frontend esistente
    participant M as Microsoft
    C->>B: Discovery e authorize con PKCE/resource
    B->>F: Transazione browser, senza token
    F->>M: Login esistente, se necessario
    M-->>F: Authorization code Microsoft
    F->>B: Scambio social esistente
    B-->>F: Bearer applicativo
    F->>B: Approva transazione con bearer
    F->>B: Navigazione complete, cookie first-party
    B-->>C: Authorization code OAuth monouso
    C->>B: Token request e verifier PKCE
    B-->>C: Bearer opaco MCP e refresh token
    C->>B: Tool MCP autenticato
    B->>B: Autorizzazione risorsa e pipeline endpoint reale
    B-->>C: Risultato API
```

## Configurazione minima

Configurare tramite impostazioni di hosting, variabili d'ambiente o user-secrets; non committare password, PFX o token.
Esempio **da adattare**, non una configurazione produttiva già attiva:

```json
{
  "Mcp": {
    "Enabled": true,
    "PublishAllTools": false,
    "Issuer": "https://api.example.com",
    "FrontendUrl": "https://frontend.example.com/",
    "CertificatePath": "/home/secrets/mcp.pfx",
    "DataProtectionBlobName": "keyring.xml",
    "Clients": [
      {
        "ClientId": "fantasoccer-chatgpt",
        "DisplayName": "ChatGPT",
        "RedirectUris": ["COPIARE_LA_REDIRECT_HTTPS_ESATTA_MOSTRATA_DA_CHATGPT"]
      }
    ]
  }
}
```

- `Issuer`: origine HTTPS pubblica del backend, senza path/query/fragment. L'hosting sotto un path base non è supportato da questa prima versione.
- `FrontendUrl`: frontend web aggiornato con `McpConnectionGate`. L'origine viene aggiunta alla policy CORS.
- `Mcp__CertificatePassword`: password del PFX tramite secret manager/ambiente. Il certificato deve avere chiave privata, essere valido e idoneo a firma e cifratura.
- `Mcp__Clients__0__ClientSecret`: opzionale. Senza secret il client è pubblico con PKCE; con secret è confidenziale. Configurare coerentemente il client ChatGPT.
- Le redirect devono essere URI HTTPS **esatte**, mai wildcard. Non copiare una callback di esempio: quella mostrata da ChatGPT può dipendere dalla connessione.
- Viene riutilizzata `ConnectionStrings:Storage`, già presente per l'infrastruttura: **nessuna nuova connection string OAuth**. L'app prepara i container privati `mcp-oauth` (documenti) e `mcp-keys` (key ring); l'identità storage deve poter creare/leggere/elencare/scrivere/eliminare blob. L'avvio viene bloccato se uno di questi container è pubblico. Non renderli pubblici e non applicare policy di cancellazione indiscriminata.
- Le chiavi Data Protection persistenti sono indispensabili anche se si usa un PFX OAuth: proteggono i bearer opachi. `DataProtectionBlobName` salva il key ring nel container `mcp-keys`, cifrato con il certificato configurato. Il key ring si applica all'intera applicazione: **migrare le chiavi già usate dall'hosting prima di cambiarne lo store**, per non invalidare sessioni frontend esistenti. In alternativa è ancora supportato `DataProtectionKeyPath` per conservare un percorso persistente già in uso, ma non impostare entrambe le opzioni. Le repliche devono leggere le stesse chiavi con lo stesso discriminatore Data Protection e avere il certificato necessario a decifrarle.
- Conservare copie protette del key ring e del PFX. La rotazione del certificato richiede conservare/configurare anche i certificati di decifratura precedenti: non sostituire semplicemente il PFX cancellando quello vecchio.
- In `Development`/`Testing` sono ammesse chiavi OAuth effimere senza PFX: codici/refresh diventano invalidi al riavvio. In produzione la configurazione incompleta impedisce l'avvio, anziché esporre un endpoint non protetto.
- Non cambiano ClientId, permessi Microsoft o modello utenti del login social attuale.

### Persistenza, concorrenza e repliche

I documenti OAuth usano l'envelope `Entity<OAuthDocument,string>` di Rystem in un repository **non esponibile**. Le letture passano dal repository; le liste usano il prefisso del tipo per non leggere entità non pertinenti. Poiché l'update Blob standard di Rystem non supporta compare-and-swap, `BlobOAuthDocumentStorage` esegue solo le scritture condizionali con l'SDK Azure (`If-None-Match` per insert, versione del documento e `If-Match`/ETag per update). Un conflitto diventa `OpenIddictExceptions.ConcurrencyException`, non una sovrascrittura silenziosa.

Codici, refresh, grant e transazioni browser sono condivisi tra istanze. Il consenso attraversa stati monouso `pending → approved → completed → redeemed`, con CAS a ogni transizione; persiste solo l'email, non i bearer o i privilegi. Le cache delle entità OpenIddict sono disabilitate per rendere osservabili le revoche tra repliche. L'avvio usa ID deterministici per i client e gestisce la registrazione concorrente.

`McpRedeemTokenHandler` impone, al momento del riscatto di code e refresh, stato ancora valido e CAS riuscito. Questo copre anche richieste già autenticate mentre un'altra replica consuma il token, e rende più restrittiva la tolleranza di OpenIddict ai conflitti refresh concorrenti.

La persistenza OAuth non richiede sticky session se account storage, issuer, client, certificati e Data Protection coincidono tra le repliche. Questo **non rende automaticamente distribuita tutta l'applicazione**: hub/aste SignalR e rate limiter mantengono i propri vincoli preesistenti. I limiti di traffico qui sono locali all'istanza: per un limite globale usare anche il gateway dell'hosting.

La retention elimina in modo condizionale i documenti temporanei: consensi dopo scadenza + 1 giorno, token dopo scadenza + 7 giorni, record revocati/cancellati dopo 7 giorni. I grant validi e i client non vengono eliminati. Il job gira all'avvio e ogni 6 ore, tollera esecuzioni concorrenti e non cancella aggiornamenti più recenti. I token vengono comunque rifiutati alla scadenza: la retention non è il controllo di validità.

## Collegamento in ChatGPT

1. Distribuire backend e frontend aggiornati, inizialmente su un ambiente di prova.
2. Nella superficie ChatGPT che consente server MCP personalizzati, inserire `https://api.example.com/mcp` e configurare OAuth con il client preregistrato.
3. Copiare la redirect mostrata da ChatGPT in `RedirectUris`; configurare l'eventuale client secret in entrambi gli ambienti.
4. Richiedere `fantasoccer.full_access`; aggiungere `offline_access` per il rinnovo automatico, se previsto dalla configurazione del client.
5. Collegarsi: login Microsoft esistente, consenso esplicito, ritorno a ChatGPT.
6. Provare prima `get_my_account`, poi consultazione di una lega; verificare una scrittura innocua e un'operazione amministrativa con account distinti.

Il codice non installa un'app nel catalogo ChatGPT e non abilita funzioni mancanti nel piano/workspace. Il collegamento reale Microsoft/ChatGPT richiede configurazione e verifica manuale con account reali, non sostituibile dai test locali.

## Tool e copertura delle API

Per default vengono pubblicati tre tool:

1. `get_my_account`: identità, gruppi e selezioni correnti; nessun parametro utente arbitrario.
2. `list_api_operations`: catalogo ricercabile/paginato con identificatore operazione, route, verbo, annotazione di scrittura e JSON Schema esatto.
3. `execute_api_operation`: esecuzione per identificatore di catalogo, con `query`, `route`, `body` e/o `files`.

Questo evita di inviare centinaia di schemi a ogni connessione. `PublishAllTools=true` pubblica anche un tool per ogni operazione per i client che lo supportano; non è necessario per avere accesso all'intero catalogo.

Il catalogo deriva da ApiExplorer: controller applicativi e repository **effettivamente esposti**. Conserva varianti/factory di repository, route parameter, parametri query e body. Non pubblica login, emissione token, metadata, Swagger, repository `SetNotExposable` o URL scelte dal modello.

Esempio (recuperare sempre l'identificatore effettivo dal catalogo):

```json
{
  "operation": "IDENTIFICATORE_RESTITUITO_DAL_CATALOGO",
  "arguments": {
    "query": { "key": "{\"g\":\"gruppo\",\"l\":\"lega\",\"y\":2026}" }
  }
}
```

Le chiavi composite sono le stesse stringhe JSON accettate dalle API, non nuove chiavi MCP. Gli schemi descrivono i nomi JSON dei modelli esistenti.

Gli upload usano `files: { nomeParametro: { fileName, contentType, base64 } }`; la facade ricostruisce il multipart. Limite per file 2 MiB, body/risposta facade 4 MiB, richiesta MCP 8 MiB. Le API mantengono gli ulteriori limiti già presenti. Non vengono effettuati download da URL fornite dal modello.

### Come viene riutilizzata la business logic

`McpApiFacade` costruisce un `HttpContext` isolato e attraversa routing, autorizzazione e delegate degli endpoint originali nello stesso processo. Vengono eseguiti model binding, filtri MVC, policy, autorizzazione repository e business hook esistenti. Non chiama direttamente repository bypassando gli endpoint e non effettua richieste HTTP verso host selezionabili dall'utente.

Le risposte riportano `statusCode`, `contentType`, `encoding` e `body`; il body conserva il contratto originale (UTF-8 per testo/JSON, base64 per contenuti binari). Le risposte HTTP di errore sono tool error. Un eventuale fallimento business rappresentato da un payload HTTP 200 resta nel payload originale: il client deve leggerlo, non presumere successo dal solo status.

### Aste SignalR

Il nuovo adapter `McpAuction/Invoke` richiama gli stessi metodi di `AuctionHub`: offerte, gestione giocatori, timer, chiusura/riapertura, ruoli e altri comandi dichiarati nell'enum `AuctionCommand`.

Prima chiamare `McpAuction/Commands` per conoscere i parametri dei comandi. Il chiamante viene sempre dal principal autenticato; non è possibile fornire un `HubCallerContext` arbitrario. Il gruppo viene controllato prima dell'invocazione e restano i controlli auctioneer/owner nel metodo originale. Le notifiche al caller diventano risposta; i broadcast raggiungono i client SignalR reali.

Non viene aperta una connessione SignalR persistente per ChatGPT: non riceve automaticamente eventi futuri o un timer in streaming. Per osservare nuovi eventi deve effettuare una nuova lettura.

## Autorizzazione e cambiamenti intenzionali

- L'utente deriva solo dal ticket validato. Le claim applicative vengono ricaricate per ogni richiesta MCP: una demozione non attende la scadenza del token.
- Il ruolo globale `super_admin` dipende dall'email dell'account configurato nel provider esistente, **non da `Username` modificabile**.
- Le letture di gruppo richiedono membership attiva; scritture amministrative richiedono il ruolo nel gruppo autorevole.
- Le scritture generiche dei documenti `Team` (stagionali, giornalieri e competizione iniziale) richiedono un ruolo amministrativo **nel gruppo autorevole**. Proprietà/comproprietà non autorizza a cancellare giocatori, venderli, cambiarne prezzo/stato o riscrivere crediti. Le formazioni passano dai controller dedicati, che copiano solo le posizioni sulla rosa server; i trasferimenti passano dal mercato o dal flusso amministrativo/asta autorizzato. Batch/bootstrap generici delle squadre sono bloccati.
- Gli admin di gruppo possono cercare/provisionare identità come nel frontend, ma modificare solo membership di gruppi che amministrano, senza attribuirsi `SuperAdmin`.
- Un utente ordinario può aggiornare il proprio stato/profilo, non le proprie membership o i ruoli.
- Bulk/batch/bootstrap e query non circoscrivibili a un gruppo sono riservati al super-admin globale; le query delle identità restano disponibili agli amministratori verificati per mantenere il workflow admin esistente.
- I repository non esposti dal backend non diventano automaticamente accessibili via MCP.
- Le operazioni GET che modificano dati (es. creazione calendario, ricalcoli) **non** vengono marcate read-only.

I confini su `AppIdentity`, `Group`, `LeagueManager` e scritture generiche `Team` si applicano anche alle URL HTTP legacy, **anche con MCP disabilitato**: altrimenti si potrebbero modificare rosa o documenti di autorizzazione da una seconda superficie. È un hardening intenzionale: chiamate prima ammesse per la sola presenza di un'email possono ora restituire 403. Provare i workflow admin con i dati reali prima del rilascio in produzione. La modifica di nome, logo e altre preferenze resta disponibile attraverso i controller personali esistenti.

Inoltre il frontend usa ora verifier PKCE casuali per transazione, verifica `state` e invia il verifier nel body supportato da Rystem. Il provider applicativo controlla issuer `consumers` e audience sulle claim Microsoft verificate dalla libreria. Il contratto di access token/refresh del frontend non cambia.

## Durata e revoca

- Access token MCP: 24 ore; refresh OAuth: 30 giorni.
- I refresh token ruotano senza finestra di riuso: non inviare refresh concorrenti e non ripetere automaticamente uno scambio dall'esito incerto. Il rilevamento del replay può richiedere un nuovo collegamento.
- `/connect/revoke`: revoca OAuth standard, per access/refresh token del client.
- `POST /connect/disconnect` con il **bearer frontend** e `{ "clientId": "fantasoccer-chatgpt" }`: revoca tutte le concessioni di quel client per l'utente autenticato.
- La revoca della concessione invalida subito gli access token MCP già emessi e impedisce refresh; non disconnette gli altri utenti né elimina i bearer frontend.
- Il logout frontend continua ad avere il significato attuale: non revoca automaticamente i collegamenti ChatGPT.
- Nelle impostazioni del frontend, **Collegamenti ChatGPT / MCP** elenca i client autorizzati e permette la revoca con conferma. Usa `GET /connect/connections` e `POST /connect/disconnect` con il bearer frontend; non espone token o secret. Se MCP è disabilitato la sezione non viene mostrata.

Non vengono registrati gli argomenti dei tool o le credenziali nei log della facade. Gli errori inattesi restituiscono un riferimento, non eccezioni complete. Configurare anche proxy/Application Insights per non raccogliere token o query sensibili del login legacy.

Rate limiting: 120 richieste/minuto per origine IP sulle superfici MCP/OAuth, senza coda. Non fidarsi di `X-Forwarded-For` arbitrari; configurare il reverse proxy con gli indirizzi fidati del proprio hosting.

## Test

```bash
dotnet test src/Fantasoccer.Tests/Fantasoccer.Tests.csproj --filter "FullyQualifiedName~Tests.Api"
```

Nella directory frontend:

```bash
npm test
npx tsc --noEmit
```

I test API condividono un solo host (RepositoryFramework mantiene stato statico per la registrazione endpoint) e normalmente uno store OAuth in memoria con lo stesso contratto CAS, snapshot isolati e retention. Il login Microsoft è simulato con un bearer emesso dallo stesso protector, non con credenziali reali. Sono coperti consenso/browser binding, riavvio del servizio, transizioni concorrenti, PKCE e replay, riscatto concorrente del code, refresh, revoca, token frontend, isolamento, scritture, ruoli, upload e adattatore aste.

Per eseguire gli stessi test contro **Blob reale/Rystem**, avviare Azurite su una porta libera e impostare `FANTASOCCER_TEST_OAUTH_BLOB` alla sua connection string (o a un account di test dedicato), poi eseguire i test MCP. Non usare mai l'account di produzione: il test retention può eliminare documenti di test scaduti. Non occorre un emulatore SQL.

```bash
docker run --rm --name fantasoccer-mcp-azurite -p 127.0.0.1:11000:10000 \
  mcr.microsoft.com/azure-storage/azurite:3.35.0 \
  azurite-blob --blobHost 0.0.0.0 --skipApiVersionCheck
# In un altro terminale: esportare la connection string Azurite con BlobEndpoint sulla porta 11000.
dotnet test src/Fantasoccer.Tests/Fantasoccer.Tests.csproj --filter "FullyQualifiedName~Mcp"
```

### Checklist prima dell'abilitazione online

- [ ] Distribuire backend e frontend insieme su staging, mantenendo `Mcp:Enabled=false` fino alla configurazione completa.
- [ ] Verificare `ConnectionStrings:Storage`, container privati, permessi, backup e migrazione del key ring corrente.
- [ ] Configurare issuer HTTPS pubblico, URL frontend, certificato e callback ChatGPT esatta (client preregistrato; non è prevista registrazione dinamica aperta).
- [ ] Il proxy deve preservare lo schema HTTPS e non bufferizzare le risposte MCP; configurare solo proxy fidati, non fidarsi di header forwarded arbitrari.
- [ ] Abilitare la feature; verificare discovery, challenge 401 e un consenso reale Microsoft → ChatGPT.
- [ ] Provare account ordinario/admin/super-admin: lettura, modifica innocua, rifiuto cross-group e revoca da impostazioni.
- [ ] Provare refresh, riavvio API e, se usate, due repliche con lo stesso key ring. Provare separatamente i workflow aste/SignalR.
- [ ] Verificare che log/APM/proxy non raccolgano credenziali, code o payload sensibili; impostare alert su errori OAuth, 403 inattesi e retention.

Dopo il deploy è disponibile uno smoke test **senza credenziali e senza scritture**:

```bash
python3 scripts/check-mcp.py https://api.example.com
```

Controlla discovery HTTPS, endpoint OAuth pubblici e rifiuto 401 della chiamata MCP anonima; non effettua login e non sostituisce il collaudo autenticato.

Restano obbligatori il login Microsoft reale e il collegamento nel workspace ChatGPT target. I test locali ed Azurite non certificano la configurazione dei domini o i permessi del workspace. Nessun deploy o modifica a risorse di produzione viene effettuato da questi test.
