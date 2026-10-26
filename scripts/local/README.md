# Applicazione locale — Docker Desktop / Windows

Questi comandi usano il `compose.yml` nella root. Non servono .NET, Node o strumenti Azure installati sull'host: build, frontend, API, database, Storage emulator e tool girano in container Linux.

**Stato della migrazione:** il runtime PostgreSQL usa servizi SQL per gioco, amministrazione, notifiche, job ed editoriale; non registra repository dati o endpoint automatici Rystem. Azurite rimane per i file. Il frontend non dipende più dal client repository.
`migrate` applica **lo schema**. `analyze-core`/`import-core` importano i [domini censiti](../../tools/Fantasoccer.DataMigration/README.md) e segnalano riconciliazioni/archivi. Il report non autorizza il cutover senza verificare dump, chiavi e login reali.

## 1. Prerequisiti e segreti

- Docker Desktop con Linux containers / backend WSL2 e Compose v2.
- Accesso Internet per il primo download delle immagini e delle dipendenze di build. Il login Microsoft resta esterno, non viene emulato.
- PowerShell (Windows PowerShell 5.1 o PowerShell 7). Aprire un terminale nella root del repository.
- Se il criterio PowerShell impedisce script locali, adottare il criterio approvato per il proprio ambiente; in alternativa usare direttamente i comandi Compose sotto.

```powershell
.\scripts\local\local.ps1 init
# Aprire .env con il proprio editor e configurare solo le credenziali necessarie.
.\scripts\local\local.ps1 up
.\scripts\local\local.ps1 smoke
```

`init` genera `.env` da `.env.test` con nuove chiavi locali per cifratura, PostgreSQL e dashboard. Non sovrascrive file esistenti.
L'applicazione usa PostgreSQL per default: fuori da Compose fornire anche `ConnectionStrings__PostgreSql` e `Authorization__GlobalAdministrators__0`; l'assenza del DSN non abilita un fallback Blob.
Il file `.env.test` è **versionato**, contiene solo valori sintetici e non deve mai ricevere segreti reali. `.env` e le altre varianti `.env.*` private sono ignorate da Git e dal contesto Docker.
Per una prova senza configurazione privata:

```powershell
.\scripts\local\local.ps1 up -EnvFile .env.test
```

Le impostazioni pubbliche `PUBLIC_API_URL`, `PUBLIC_BLOB_URL` e `MICROSOFT_CLIENT_ID` sono incorporate nel bundle frontend: non sono segreti. Nessuna password o connection string viene passata al build frontend.
I segreti reali sono letti dal `.env` solo tramite Compose e inoltrati individualmente al servizio che li usa; **non** viene montato o passato tutto il file nell'API.
`FANTASOCCER_EXPORT_STORAGE` arriva solo al container di export, mai ad API/frontend/test.

Per connection string/SAS o valori contenenti `$` usare valori **tra apici singoli** nel `.env`, secondo la sintassi Compose. Evitare `docker compose config` senza `--quiet`, che espanderebbe i segreti sul terminale.
I segreti in `.env` e nell'ambiente dei container sono leggibili dagli utenti con accesso a Docker: non sono un secret vault. Proteggere file, account Windows, ACL, volumi e backup; non condividerli.

### Login e contenuti di gioco

La home e i servizi partono senza credenziali Microsoft. Il primo login Microsoft validato crea automaticamente un account SQL di base, senza gruppi o ruoli; per accedere ai dati di gioco servono un dataset importato o un invito da un amministratore esistente. Configura `MICROSOFT_CLIENT_ID` / `MICROSOFT_CLIENT_SECRET` di una registrazione che ammetta l'origine frontend scelta (default `http://localhost:3000/`) come redirect. `GLOBAL_ADMIN_EMAIL` identifica l'amministratore globale fidato, non un nome profilo modificabile.
Non viene introdotto un bypass delle policy o un utente super-admin fittizio. Non è ancora disponibile un seed demo completo del gioco.
Il super admin predefinito è `alessandro.rapiti44@gmail.com` (modificabile con `GLOBAL_ADMIN_EMAIL`); vede **Utenti, gruppi e codici** nel menu anche senza appartenere a gruppi. Da lì genera codici monouso da consegnare agli utenti, che possono creare il proprio gruppo dalla home o da `/groups/new`. Vedere [attivazione gruppi](../../docs/GROUP-ACTIVATION.md). I pagamenti annuali sono solo annunciati: non ci sono addebiti o rinnovi automatici in questo incremento.
Se vuoi importare anche le vecchie aste, per le liste cifrate servono `LEGACY_ENCRYPTION_KEY` e `LEGACY_ENCRYPTION_IV` originali nel `.env` privato. Se non ti servono aste/liste storiche imposta `IMPORT_LEGACY_AUCTIONS=false` e lascia vuote le due chiavi legacy: squadre, rose e crediti restano importati, i documenti originali restano nel dump. Non sono le chiavi applicative `ENCRYPTION_*` generate da `init`, che restano necessarie e non vanno svuotate. Non modificare le chiavi reali sull'hosting.

## 2. Servizi disponibili

| Servizio | URL/porta predefinita | Note |
| --- | --- | --- |
| Frontend web | http://localhost:3000 | Bundle Expo statico, routing SPA tramite Nginx |
| API | http://localhost:5080 | Swagger `/swagger/index.html`, health `/health/live` e `/health/ready` |
| Log/tracce/metriche | http://localhost:18888 | Aspire Dashboard, login con `DASHBOARD_TOKEN` del file env |
| PostgreSQL | localhost:25432 | DB/utente `fantasoccer_local`, password da env |
| Azurite Blob | http://localhost:21000/migrationrestore | Dataset dell'app e file locali |
| Azurite Queue / Table | localhost:21001 / localhost:21002 | Account `migrationrestore` e chiave `AZURITE_KEY` |

Tutte le porte pubblicate sono su `127.0.0.1`, non sulla LAN. OTLP è raggiungibile soltanto nella rete Compose.
Le porte pubblicate sono tutte configurabili nel `.env`: `FRONTEND_PORT`, `API_PORT` (HTTP), `HTTPS_PORT`, `POSTGRES_PORT`, `BLOB_PORT`, `QUEUE_PORT`, `TABLE_PORT` e `DASHBOARD_PORT`. Gli URL predefiniti per API, Blob, CORS, redirect Microsoft e frontend MCP sono derivati automaticamente da queste porte. `PUBLIC_API_URL`, `PUBLIC_BLOB_URL` e `FRONTEND_ORIGIN` servono solo per hostname/reverse proxy personalizzati e, se valorizzati, prevalgono sui default. `localhost` indica il browser/host Windows, non i container.

Esempio di porte alternative, modificando i valori nel `.env`:

```dotenv
FRONTEND_PORT=3300
API_PORT=5081
HTTPS_PORT=5444
POSTGRES_PORT=25433
BLOB_PORT=21010
QUEUE_PORT=21011
TABLE_PORT=21012
DASHBOARD_PORT=18889
```

Poi ricreare i container e il bundle frontend: `.\scripts\local\local.ps1 up -EnvFile .env` oppure `up-https` se usi TLS. Le porte `8080/8443`, `5432`, `10000–10002` e `18888/18889` sono interne alla rete Docker e restano fisse; cambiano le porte host pubblicate. OTLP rimane interno e non è esposto sulla macchina.
Immagini e loghi sono configurati per Azurite, senza URL fisse allo Storage produttivo. I container file creati dall'app possono essere pubblici sul solo emulatore locale per conservare il comportamento legacy; i documenti applicativi non devono essere resi pubblici.

API Data Protection, PostgreSQL, Azurite e telemetria hanno volumi persistenti distinti. Le nuove chiavi di sessione locali sono indipendenti dalle sessioni reali: dopo restore occorre il login locale.
Cambiare la password nel file env non cambia la password di un utente PostgreSQL già presente nel volume: aggiornarla deliberatamente nel DB o usare un nuovo laboratorio, senza eliminare dati da conservare.

## 3. Log e tracciamento senza Azure

Il profilo Compose sceglie `Telemetry:Provider=OpenTelemetry`, disabilitando la registrazione Application Insights anche se esistono altre configurazioni Azure.
L'API invia log strutturati, span ASP.NET/HttpClient e metriche runtime via OTLP ad Aspire Dashboard. Fare una chiamata applicativa (anche una risposta 401) per vedere la traccia: gli health check non vengono tracciati.
I log **storici** della Table `logs` non sono importati in PostgreSQL per default: `IMPORT_LEGACY_LOGS=false`. Anche il download/restore Table è disattivato: serve `-IncludeTables` per acquisirle; se già presenti in un vecchio dump restano conservate lì. `IMPORT_LEGACY_LOGS=true` riattiva solo l'import SQL dell'archivio da un dump che lo contenga. Queste opzioni non spengono la telemetria corrente né le ricevute/lease dei job.
Le query string, userinfo URL e SQL vengono rimossi dagli span prima dell'export. Non vengono raccolti body o header Authorization. I messaggi di business preesistenti possono comunque contenere email o altri dati: la dashboard e il suo volume sono dati sensibili.

Il token della UI è nel `.env`; l'endpoint OTLP è non autenticato ma non pubblicato sull'host. Non esporre questo stack con tunnel/proxy pubblici.
Aspire conserva una cronologia locale limitata nel volume `local-telemetry`, non è una soluzione di retention produttiva. In modalità standalone mostra telemetria, non controlli di start/stop dei container; per questi usare Docker Desktop.

```powershell
.\scripts\local\local.ps1 logs
```

Fuori dal Compose si può scegliere `Telemetry__Provider=ApplicationInsights` con `ConnectionStrings__AppInsights`, oppure `None`. Il comportamento produttivo non viene cambiato implicitamente.
MCP applicativo non è la dashboard. Nel Compose HTTP resta disabilitato; per abilitarlo usare il profilo HTTPS descritto sotto, non indebolire il requisito HTTPS del protocollo.

### Debug API in VS Code su Windows

Questa modalità esegue PostgreSQL, Azurite e Dashboard in Docker, ma l'API come processo .NET locale, così i breakpoint di VS Code funzionano. Usa i valori sintetici di `.env.test`; per `.env` privato sostituisci password/chiavi con gli stessi valori del tuo file. Imposta prima le porte desiderate nell'env e riportale nelle variabili PowerShell sotto.

```powershell
# Nella root repo; scegli .env oppure .env.test
$envFile = '.env.test'
docker compose --env-file $envFile up -d --wait postgres azurite dashboard

# Valori da allineare al proprio env file e alle porte selezionate
$pgPort = 25432; $blobPort = 21000; $queuePort = 21001; $tablePort = 21002; $apiPort = 5080
$azuriteKey = 'bG9jYWwtdGVzdC1vbmx5'
$pgPassword = 'local-postgres-test-only'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = "http://localhost:$apiPort"
$env:PostgreSql__Enabled = 'true'
$env:Integrations__Infrastructure = 'Storage'
$env:ConnectionStrings__PostgreSql = "Host=localhost;Port=$pgPort;Database=fantasoccer_local;Username=fantasoccer_local;Password=$pgPassword;GSS Encryption Mode=Disable"
$env:ConnectionStrings__Storage = "DefaultEndpointsProtocol=http;AccountName=migrationrestore;AccountKey=$azuriteKey;BlobEndpoint=http://127.0.0.1:$blobPort/migrationrestore;QueueEndpoint=http://127.0.0.1:$queuePort/migrationrestore;TableEndpoint=http://127.0.0.1:$tablePort/migrationrestore"
$env:Authorization__GlobalAdministrators__0 = 'admin@example.test'
$env:Assets__PublicBlobBaseUrl = "http://localhost:$blobPort/migrationrestore"
$frontendPort = 3000 # allineare a FRONTEND_PORT se personalizzata
$frontendOrigin = "http://localhost:$frontendPort"
$env:Cors__Origins__0 = $frontendOrigin
$env:SocialLogin__Microsoft__RedirectDomains = $frontendOrigin
$env:Encryption__Key = 'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA='
$env:Encryption__IV = 'AAAAAAAAAAAAAAAAAAAAAA=='
$env:ExternalHttp__Enabled = 'false'; $env:BackgroundJobs__Enabled = 'false'; $env:Push__Enabled = 'false'; $env:Mcp__Enabled = 'false'
$env:Startup__WarmUpEnabled = 'false'; $env:Http__RedirectToHttps = 'false'; $env:Telemetry__Provider = 'None'
dotnet run --project src/Fantasoccer.Applications/Fantasoccer.Application.Api --no-launch-profile --urls $env:ASPNETCORE_URLS
```

L'API applica le migration PostgreSQL all'avvio. Per fermarla premi `Ctrl+C`; per i breakpoint seleziona in VS Code **Run and Debug → Attach to .NET Process** e scegli `Fantasoccer.Application.Api`. Swagger è su `http://localhost:<API_PORT>/swagger` e health su `/health/ready`.

Per il frontend web con hot reload in un secondo terminale (Node/npm richiesti):

```powershell
cd src/Fantasoccer.Applications/fantasoccer
npm ci
$frontendPort = 3000 # scegliere FRONTEND_PORT
$env:EXPO_PUBLIC_API_URL = 'http://localhost:5080' # sostituire 5080 con API_PORT
npm run web -- --port $frontendPort
```

Se usi porte diverse, imposta CORS e redirect Microsoft alla stessa origine frontend e aggiorna `EXPO_PUBLIC_API_URL` come sopra. Per l'app completa in container invece usa `local.ps1 up`; non avviare contemporaneamente l'API host e il servizio `api` Compose sulla stessa porta.
Per l'app Expo dev-client, scegli la porta esplicitamente con `npm start -- --port 3000` (sostituisci con `FRONTEND_PORT`); lo script non fissa più la porta.

### HTTPS e MCP locale

```powershell
.\scripts\local\local.ps1 certificate
# Fiducia esplicita del certificato PUBBLICO, nel proprio account Windows:
Import-Certificate -FilePath .\local-certificates\localhost.cer -CertStoreLocation Cert:\CurrentUser\Root
# Impostare MCP_ENABLED=true nel proprio .env solo dopo l'import desiderato.
.\scripts\local\local.ps1 up-https
```

- Generazione effettuata in container, nessun SDK richiesto sull'host. Non viene modificato automaticamente alcun trust store.
- `LOCAL_HTTPS_CERT_PASSWORD` protegge il PFX; `init` genera una password privata. I valori `.env.test` sono esclusivamente sintetici.
- Certificati in `local-certificates/`, esclusi da Git/build; proteggere la cartella e gli ACL Windows. Il PFX cifrato viene montato individualmente, in sola lettura, nell'API non-root.
- API HTTPS e MCP: `https://localhost:5443` e `/mcp` (porta configurabile con `HTTPS_PORT`). Il frontend usa automaticamente questa API nel profilo HTTPS.
- I wrapper `up` continuano a usare `compose.https.yml` quando il certificato esiste. Conservare profilo, password e certificato anche disabilitando MCP: servono per leggere le chiavi Data Protection già cifrate.
- Il generatore rifiuta di sovrascrivere certificati esistenti. Non ruotare/eliminare PFX o key ring indiscriminatamente; potrebbe essere necessario un nuovo login/collegamento.
- `localhost.pem` è il certificato pubblico per `curl --cacert` o `NODE_EXTRA_CA_CERTS` nei client Node. Non usare `-k` o disabilitare TLS nei test abituali.
- Il frontend resta HTTP su loopback (consentito per login locale Microsoft); il consenso MCP usa cookie sicuri sull'API HTTPS.
- Client cloud come ChatGPT non possono raggiungere il `localhost` del PC. Per provarli serve un ambiente HTTPS pubblicamente raggiungibile e configurato separatamente, non esporre questo Compose di sviluppo con i suoi segreti.
- MCP/OAuth scrive nel DB: importare prima di registrare client o aprire il traffico, altrimenti la protezione anti-reimport chiude correttamente la finestra di migrazione.

## 4. Scaricare lo Storage e ripristinarlo localmente

Per il percorso guidato completo (connection string sorgente, export/analisi, import SQL/file e reset locale con backup) vedere il [runbook Windows](../../docs/MIGRATION-RUNBOOK.md). `sync-storage.ps1` analizza soltanto per default; `-Apply` importa esplicitamente e non esegue mai un reset automatico. `reset-postgres.ps1` richiede `-ConfirmDatabase fantasoccer_local` e supporta `-WhatIf`.

1. Inserire `FANTASOCCER_EXPORT_STORAGE` nel `.env` privato, idealmente con SAS/credenziale **di sola lettura** per Blob (e Table solo se richieste).
2. Usare una copia esplorativa mentre la sorgente è attiva oppure concordare una finestra in cui **tutti** gli scrittori sono fermi. `-SourceQuiesced` registra la dichiarazione, non ferma Azure.
3. Eseguire l'export esplicitamente:

```powershell
.\scripts\local\local.ps1 export -DumpName prova1
# Se interrotto:
.\scripts\local\local.ps1 export -DumpName prova1 -Resume
.\scripts\local\local.ps1 verify -DumpName prova1
.\scripts\local\local.ps1 restore -DumpName prova1
.\scripts\local\local.ps1 migrate
.\scripts\local\local.ps1 up
```

Il dump rimane in `migration-data/prova1/`, escluso da Git e dai build Docker, visibile sul filesystem Windows. Non contiene solo dati di test se la sorgente è reale.
Per default export e restore operano solo sui Blob. Aggiungere `-IncludeTables` a `local.ps1 export`/`restore` o `sync-storage.ps1` solo per richiedere esplicitamente anche le Table. `-Resume` riusa i Blob invariati verificati; `-UseExistingDump` nel wrapper sync evita ogni nuovo accesso ad Azure. Un `invalid_email` dell'import riguarda un documento Blob, non la fase Table: vedere la diagnosi nel runbook.
`restore` ferma frontend/API prima di scrivere in Azurite e non li riavvia automaticamente. Ripristina Table solo con `-IncludeTables`. Abilita lettura pubblica dei soli file in `images`, `logos`, `cardimages` sull'emulatore locale (`--public-local-files`); gli altri container nuovi restano privati. Non cancella oggetti locali estranei: usare un laboratorio dedicato per ottenere una copia esatta. Non esiste restore remoto.
`migrate` è ripetibile e applica solo le migration EF. Per l'import dei dati, eseguire esplicitamente:

```powershell
.\scripts\local\local.ps1 analyze-core -DumpName prova1
.\scripts\local\local.ps1 import-core -DumpName prova1
.\scripts\local\local.ps1 up
```

Controllare `pendingContainers`, `pendingFields` e `preservedArtifacts`. Navigazione, gruppi/regolamenti, rose, partite/formazioni, dati calcistici, classifiche, mercato/aste, albo, carte, log ed editoriale usano DTO SQL leggibili. Generazione calendario, ricalcolo, conferme baseline e AI sono comandi espliciti. Le riconciliazioni dipendenti dal dump e il collaudo con login reale non sono sostituiti dai test sintetici.

### Job SQL ed editoriale opzionale

Dopo l'import desiderato, `BACKGROUND_JOBS_ENABLED=true` abilita il worker SQL con stato, lease/fencing e ricevute in PostgreSQL. Le sorgenti di rete richiedono anche `EXTERNAL_HTTP_ENABLED=true`; non si riattiva il vecchio scheduler Blob. L'amministratore globale può vedere `/api/operations/jobs` ed eseguire un singolo job tramite `POST /api/operations/jobs/run` con `{ "name": "calendar", "year": 15 }` (anno stagionale corrente, non anno solare). Nomi: `calendar`, `catalogue`, `live`, `official`, `statistics`, `chances`, `formations`, `results`, `market-expiry`, `hall`, `images`.

La vista operativa `/api/operations/logs` richiede amministrazione globale verificata e applica limiti/redazione. I log legacy compaiono solo se importati con `IMPORT_LEGACY_LOGS=true`; altrimenti restano nel dump, non in SQL. Le ricevute dei job attuali sono distinte dall'archivio storico e per ora rimangono invariate.

Per generazione AI esplicita impostare nell'env privato `AI_ENABLED=true`, `AI_ENDPOINT`, `AI_API_KEY`, `AI_MODEL`, oltre ad abilitare HTTP esterno. Le news richiedono un manager della lega e risultati salvati completi; i suggerimenti sono personali per account, anche fra co-owner. Le letture restano disponibili con AI disabilitata. Nessuna chiamata AI a pagamento viene schedulata automaticamente.

### Push private e riconciliazione legacy

`PUSH_ENABLED=false` resta il valore sicuro locale. Per prove di consegna reali configurare nel proprio `.env` privato `PUSH_PUBLIC_KEY`, `PUSH_PRIVATE_KEY`, `PUSH_SUBJECT` (`mailto:...` oppure HTTPS) e poi abilitare esplicitamente l'invio. Il server verifica che le chiavi corrispondano; non ci sono chiavi di fallback incorporate nel codice. Non mettere una vera chiave privata in `.env.test`.

Le pending importate sono in quarantena: `/api/push-notifications/quarantine` e `POST .../quarantine/{id}/confirm-scope` richiedono amministrazione globale verificata e conferma di gruppo/lega/link locale. Scadenze e ricevute non vengono azzerate. Cambiare chiave VAPID richiede riattivare esplicitamente il browser; logout, opt-out e dispositivi rimossi non vengono riabilitati dal rinnovo automatico.

La vecchia chiave privata rimossa dal sorgente va ruotata operativamente dal gestore, non è sufficiente cancellarla dal file. Nessun comando di sviluppo effettua rotazioni sul provider o invii durante l'import.
**Non modificare il workspace o abilitare writer prima di aver completato l'import desiderato**: la prima scrittura dei servizi (incluse lease/esecuzioni job e OAuth) blocca le successive reimportazioni sullo stesso target.
Nessuno di questi comandi distribuisce o modifica configurazioni Azure; `export` legge la sorgente. Credenziali/source non vengono stampate.
Ambito, protezione, limiti e copertura dell'export: [tool StorageMigration](../../tools/Fantasoccer.StorageMigration/README.md). Blob/Table correnti non equivalgono al backup completo dell'account Azure.

### Protezione dagli effetti esterni

Default locali: `BACKGROUND_JOBS_ENABLED=false`, `EXTERNAL_HTTP_ENABLED=false`, `PUSH_ENABLED=false`, `AI_ENABLED=false`, warm-up disabilitato.
I client dati esterni sono bloccati e il sender push non consegna né marca come consegnate le notifiche. Il login Microsoft rimane una chiamata esterna intenzionale.
Non abilitare job o invii su copie reali: possono aggiornare stato o contattare persone vere. Queste guardie coprono i client noti, non sono un firewall generale.
OAuth MCP copiato non implica collegamenti validi localmente: issuer, certificati e chiavi differiscono.

## 5. Test automatici isolati

```powershell
.\scripts\local\local.ps1 test -EnvFile .env.test
```

I test hanno un proprio server PostgreSQL e un proprio Azurite, senza volumi del dataset applicativo e senza porte pubblicate. Non possono eliminare database dell'app o riversare dati reali nelle fixture sintetiche.
Eseguono schema, PK/FK composite, export, checksum, ripresa e doppio restore. I test del backend esistente restano nella suite .NET separata; i test browser/login reale non sono sostituiti da questa suite.

## 6. Comandi Compose diretti

Usabili anche dal terminale integrato di Docker Desktop:

```powershell
docker compose --env-file .env.test up -d --build --wait
docker compose --env-file .env.test --profile test run --rm --build tests
docker compose --env-file .env --profile tools run --rm --build storage-export
docker compose --env-file .env --profile tools run --rm --build storage-verify
docker compose --env-file .env --profile tools run --rm --build migrate
docker compose --env-file .env down
```

`down` conserva volumi e dump. Non usare `down --volumes` senza voler eliminare **tutti i dati locali dello stack**, incluse chiavi sessione e telemetria. Nessuno script di pulizia automatica li elimina.

Linux/macOS/WSL hanno lo stesso wrapper:

```bash
bash scripts/local/local.sh init
bash scripts/local/local.sh up
bash scripts/local/local.sh test .env.test
DUMP_NAME=prova1 RESUME=true bash scripts/local/local.sh export
```

Il solo comando `init` Bash usa Python 3 per generare chiavi; su Windows PowerShell usa le API crittografiche .NET già disponibili.
