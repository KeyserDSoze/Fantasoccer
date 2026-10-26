# Windows: membri, reset locale e migrazione Azure Storage → PostgreSQL

## Permessi utenti

Chi crea un gruppo con un codice ne diventa **responsabile e amministratore**. Nel menu trova **Membri e inviti** e **Configura gruppo**. Può cercare un **indirizzo email completo**, aggiungere un account esistente o predisporre l'associazione per chi non ha ancora effettuato il primo login. Non vede l'elenco globale, lo stato privato dell'account o le appartenenze ad altri gruppi.

L'associazione non spedisce una mail: comunica tu all'amico il link del frontend e fagli usare Microsoft con quell'email. Un amministratore ordinario può invitare, ma solo un responsabile può assegnare/togliere il ruolo di responsabile; l'ultimo responsabile/amministratore è protetto dalla rimozione.

Il **super admin di piattaforma** ha la vista separata `/platform`: tutti gli utenti (anche senza gruppi o disabilitati), con le rispettive appartenenze, e tutti i gruppi. Le normali schermate di gruppo restano limitate alle membership reali.

## 1. Quale connection string impostare

Apri il `.env` **privato** nella root e compila:

```dotenv
# SORGENTE Azure: lettura/export soltanto. Non usare una SAS di un singolo Blob.
FANTASOCCER_EXPORT_STORAGE='DefaultEndpointsProtocol=https;AccountName=ACCOUNT_SORGENTE;AccountKey=CHIAVE_REALE;EndpointSuffix=core.windows.net'

# Per NON importare aste storiche e vecchie liste randomizzate:
IMPORT_LEGACY_AUCTIONS=false
# Import SQL dei log storici disattivato; distinto dall'opzione -IncludeTables.
IMPORT_LEGACY_LOGS=false
# Possono restare vuoti con la scelta precedente.
# Se invece IMPORT_LEGACY_AUCTIONS=true, servono le chiavi ORIGINALI per le liste cifrate.
LEGACY_ENCRYPTION_KEY=
LEGACY_ENCRYPTION_IV=

# Nessuno scrittore/notifica esterna sulla copia, specialmente durante l'import.
BACKGROUND_JOBS_ENABLED=false
EXTERNAL_HTTP_ENABLED=false
PUSH_ENABLED=false
AI_ENABLED=false
MCP_ENABLED=false
```

Sostituisci i placeholder con la tua connection string; preferisci una **SAS account di sola lettura**, con listing/lettura Blob. Query/listing Table servono solo se scegli `-IncludeTables`. Una SAS di un singolo container/Blob non basta a elencare tutti i container dell'account. Il tool legge soltanto dalla sorgente anche se la credenziale ha permessi superiori; non supporta ancora managed identity/Entra.

**Non confondere gli ambienti:** `FANTASOCCER_EXPORT_STORAGE` è la sorgente reale, passata solo al container di export. `ConnectionStrings:Storage` dell'API e il restore puntano ad **Azurite locale**; il restore remoto è vietato. Il target SQL è solo il servizio Compose `postgres`, DB/utente `fantasoccer_local`. Le tue porte personalizzate non cambiano questi indirizzi interni.

Non mettere connection string in argomenti, screenshot, `.env.test` o Git. Usa apici singoli nel `.env` per evitare l'espansione di `$`. Non stampare `docker compose config` completo: espanderebbe i segreti.

## 2. Scaricare e analizzare, senza importare nel database

Dalla root del repo, in PowerShell, il piano senza alcuna connessione:

```powershell
.\scripts\local\sync-storage.ps1 -EnvFile .env -DumpName azure-prova1 -WhatIf
```

Per eseguire export → verifica checksum → analisi mapping:

```powershell
.\scripts\local\sync-storage.ps1 -EnvFile .env -DumpName azure-prova1
```

Il dump è in `migration-data/azure-prova1/`, ignorato da Git e dai build Docker. Contiene **dati reali e possibili token/artefatti privati**: proteggi filesystem, ACL e backup.

Per default sono esportati **solo tutti i container Blob nello stato corrente**, compresi file, immagini e contenitori vuoti: Table Storage non viene nemmeno interrogato. Il manifest dichiara `scope=blob-current-state` e `Tables` tra le esclusioni. Non è un backup completo dell'account Azure: non include Queue, File Share, vecchie versioni/snapshot Blob, soft delete, tag o policy di accesso.

Solo se vuoi scaricare anche tutte le Table, aggiungi l'opzione esplicita:

```powershell
.\scripts\local\sync-storage.ps1 -EnvFile .env -DumpName azure-prova1 -Resume -IncludeTables
```

`-IncludeTables` abilita download e, con `-Apply`, restore locale delle tabelle; **non** abilita automaticamente l'import SQL dei log. Quest'ultimo ha il flag separato `IMPORT_LEGACY_LOGS=true` e richiede che il dump contenga le Table. La CLI .NET usa `--include-tables`; Bash usa `INCLUDE_TABLES=true`.

Ripresa/riacquisizione della **stessa** sorgente:

```powershell
.\scripts\local\sync-storage.ps1 -EnvFile .env -DumpName azure-prova1 -Resume
```

`-Resume` rifa il listing ma **non riscarica i Blob invariati** se ETag, dimensione e checksum locale coincidono. Riscarica file modificati, corrotti o non ancora registrati nell'ultimo checkpoint (al massimo 49 prima di un'interruzione brusca). Le Table, **solo con `-IncludeTables`**, vengono invece rilette dall'inizio: non c'è ripresa sicura di una scansione Table cambiata nel frattempo.

Un dump completato si verifica/analizza senza ricontattare Azure con `-UseExistingDump` al posto di `-Resume`. Puoi anche riprendere un vecchio export interrotto sulle Table usando `-Resume` **senza** `-IncludeTables`: riusa i Blob e completa un manifest Blob-only. Eventuali file Table già scaricati restano sul disco ma non sono referenziati dal nuovo manifest, né eliminati automaticamente.

### Se compare `invalid_email`

`Core import refused: invalid_email` è un errore di validazione nei **documenti Blob** (identità, membri, proprietari, preferenze, ecc.), non un errore di accesso a Table Storage. Escludere le tabelle non garantisce di risolverlo. Nessun dato di import viene committato parzialmente: non fare un reset solo per questo errore.

Il tool indica `Field` e `reader` (nome del file sorgente del lettore e riga), senza stampare indirizzi email, valori, Blob key o payload. Per gli errori di `appidentity` aggiunge anche `Document SHA-256`, corrispondente alla voce `sha256` nel manifest: permette di identificare localmente il documento senza pubblicarne il nome o il contenuto.

Un caso specifico è `Field: key` durante la lettura delle identità: l'email del profilo è già valida, ma la chiave interna dell'envelope non è un indirizzo. La chiave di storage **può essere un identificatore tecnico**, distinto dall'email usata per il login. Sono supportate queste forme, senza inventare indirizzi:

- chiave interna email, uguale all'email valida del profilo;
- chiave interna opaca, vuota o mancante, quando il nome del Blob è l'email valida del profilo (trim/case canonici);
- chiave tecnica non-email **non vuota e identica al nome del Blob**, confrontata esattamente con `Ordinal`: non si ignorano maiuscole o spazi per gli ID tecnici. L'email continua a provenire dal campo del profilo, non dall'ID.

Le chiavi contenenti un'altra email valida, i nomi Blob che dichiarano un'email discordante, i duplicati e gli ID tecnici non corrispondenti restano bloccati. Non si fondono automaticamente profili e un ID di storage non viene usato come subject Microsoft o credenziale.

I casi recuperati sono espliciti nel report (`reconciledIdentityDocuments`, hash SHA-256) e in `pendingFields`. La relazione documento sorgente → account viene conservata nel mapping SQL con riferimento al file di archivio e checksum, **entrambi hash**, senza duplicare in log/SQL il nome o la chiave tecnica originale. Gli originali restano nel dump. Gli ID account deterministici per email, i ruoli e i controlli di membership rimangono invariati.

Per ripetere soltanto l'analisi, senza download o import SQL:

```powershell
.\scripts\local\sync-storage.ps1 -EnvFile .env -DumpName azure-prova1 -UseExistingDump
```

Sostituisci `azure-prova1` con il **nome del dump che hai già scaricato**. Se il manifest è incompleto, usa prima `-Resume`. Non cancellare il dump o i checkpoint e non saltare automaticamente utenti: per i conflitti non recuperabili occorre riconciliare il campo indicato. Puoi condividere codice errore, `Field`, `reader` e `Document SHA-256`, non connection string, email, nomi dei Blob o l'intero dump.

### Se compare `identity_collision`

Non significa necessariamente "due utenti duplicati": l'importatore usa questo blocco anche se una chiave email o il nome email del Blob contraddicono il profilo. La riga `Identity details` ora distingue:

- `blob_email_mismatch`: il nome Blob è un'email valida diversa da quella del profilo;
- `embedded_key_email_mismatch`: la chiave interna è un'email valida diversa da quella del profilo;
- `duplicate_profile_email`: due documenti hanno la stessa email canonica del profilo, anche se le chiavi tecniche sono diverse.

Per i duplicati indica checksum e riferimenti di archivio **hash** di entrambe le copie. I booleani `profilesIdentical`, `displayNameMatches`, `selectionMatches`, `groupRolesMatch` e `groupLabelsMatch` confrontano i dati senza mostrarli. I gruppi/ruoli sono confrontati indipendentemente dall'ordine; `profilesIdentical` confronta l'intero valore JSON, inclusi eventuali campi non modellati. I flag `currentBlobIsEmailLookup`/`otherBlobIsEmailLookup` indicano soltanto se il nome del Blob corrisponde all'email del profilo, non quale copia sia automaticamente autorevole.

**La diagnostica non cambia le regole dell'import:** copie identiche o discordanti rimangono bloccate fino a una riconciliazione esplicita. Non usare un reset o cancellare una copia solo per far passare il controllo. Rilancia soltanto `analyze-core` sul dump esistente e condividi `Identity details`, non i valori originali dei documenti. Dopo aver stabilito che cosa differisce si potrà scegliere una politica di riconciliazione; nessuna unione di ruoli o preferenza arbitraria viene introdotta.

## 3. Importare esplicitamente la copia locale

Prima leggi il report: `pendingContainers` sono dati sconosciuti/non coperti; `pendingFields` richiedono riconciliazione; `excludedContainers` sono esclusioni deliberate (aste/liste con `IMPORT_LEGACY_AUCTIONS=false`, `table:logs` con `IMPORT_LEGACY_LOGS=false`); `preservedArtifacts` sono file/archivi preservati senza inventare dati SQL. `fullMigrationComplete=false` non è un errore del comando, ma **non autorizza il cutover**.

L'esclusione delle aste non tocca squadre, rose, prezzi già salvati, crediti, risultati o classifiche. Non elimina i Blob sorgente: i byte restano nel dump completo verificato come archivio, ma non sono caricati nelle tabelle SQL delle aste/ordini. Il default del tool rimane `true` per evitare di perdere dati senza una scelta esplicita.

### Log storici e logging attuale sono separati

I log della Table `logs` sono **esclusi da PostgreSQL per default** (`IMPORT_LEGACY_LOGS=false`). Restano nel dump solo se erano già presenti o vengono acquisiti con `-IncludeTables`; nessuna riga sorgente viene cancellata. Solo se necessario si può riattivare l'import SQL con `IMPORT_LEGACY_LOGS=true` (CLI diretta: `FANTASOCCER_IMPORT_LEGACY_LOGS=true`) su un dump che includa le tabelle. Le altre tabelle acquisite ma sconosciute restano segnalate come pendenti, non vengono ignorate.

Questa scelta non disabilita la telemetria corrente OpenTelemetry/Aspire né elimina ricevute, lease, outbox o audit SQL dell'app. La revisione futura dovrà distinguere log diagnostici da mantenere solo nella piattaforma di logging e dati operativi necessari per coordinamento/idempotenza. Non viene cambiata oggi la retention o la semantica dei job. Anche per cambiare opzione resta valido il blocco di reimport dopo scritture applicative.

Ferma anche eventuali processi `dotnet run`/debug avviati sull'host, poi:

```powershell
.\scripts\local\sync-storage.ps1 -EnvFile .env -DumpName azure-prova1 -UseExistingDump -Apply
```

Con `-Apply` lo script ferma API/frontend Compose, applica lo schema e importa SQL in transazione, quindi ripristina i Blob in Azurite (le Table solo con `-IncludeTables`, anche per un vecchio dump che le contenga). SQL viene importato **prima** del restore file: un DB che rifiuta la reimportazione non causa sovrascritture dei file. Un errore successivo del restore lascia l'app ferma: correggi e ripeti sullo stesso dump. SQL/file non sono una singola transazione distribuita.

**Nessun reset automatico, modifica di Azure o riavvio dell'app.** Dopo aver verificato i report, avvia deliberatamente con `local.ps1 up-https -EnvFile .env` (oppure `up` senza TLS).

## 4. Perché una nuova sincronizzazione può essere rifiutata

È una migrazione **snapshot sorgente → target**, non una replica continua/bidirezionale. Finché il target non riceve scritture applicative, un nuovo export/import riconcilia i dati della stessa sorgente, anche figli rimossi; uno snapshot invariato viene saltato.

La prima scrittura dell'app — collegamento Microsoft, gruppi/codici, OAuth MCP, job, ecc. — chiude la finestra di reimportazione. Non fondiamo automaticamente gruppi nuovi locali e dump legacy né scavalchiamo il controllo. Usa un laboratorio pulito separato o il reset esplicito, sapendo che perderai lo stato locale. Il restore Azurite non elimina file estranei al dump: per una replica esatta serve un emulatore dedicato pulito; il reset PostgreSQL non pulisce Azurite.

### Dump finale consistente

Una copia acquisita mentre la sorgente riceve scritture è **esplorativa**. Ferma prima **tutti gli scrittori sulla sorgente**, incluse API legacy, job, aste e altre istanze. Poi acquisisci un nuovo dump:

```powershell
.\scripts\local\sync-storage.ps1 -EnvFile .env -DumpName azure-finale1 -SourceQuiesced
# Dopo review del report, sul target pulito:
.\scripts\local\sync-storage.ps1 -EnvFile .env -DumpName azure-finale1 -UseExistingDump -Apply
```

`-SourceQuiesced` registra la tua dichiarazione nel manifest: **non ferma Azure e non verifica da solo la quiescenza**. Va specificato di nuovo quando riprendi un export, non è ereditato automaticamente.

## 5. Reset di TUTTI i dati PostgreSQL locali (distruttivo)

Solo per il PostgreSQL dedicato del Compose locale, **non per DB remoti o produzione**. Richiede un Docker context con socket locale/named pipe; TCP/SSH sono rifiutati. Non accetta connection string SQL arbitrarie.

Prima mostra il piano: nessuna operazione Docker/cancellazione.

```powershell
.\scripts\local\reset-postgres.ps1 -EnvFile .env -ConfirmDatabase fantasoccer_local -WhatIf
```

**Solo quando vuoi davvero perdere tutti i dati applicativi locali:**

```powershell
.\scripts\local\reset-postgres.ps1 -EnvFile .env -ConfirmDatabase fantasoccer_local
```

Lo script:

1. ferma API/frontend Compose e rifiuta tool di migrazione/ripristino attivi; ferma tu anche il debugger/API sull'host;
2. crea un backup custom `pg_dump` in `migration-data/postgres-backups/before-reset-….dump` e ne controlla l'indice; se fallisce, **non esegue il reset**;
3. svuota le tabelle applicative in transazione con advisory lock: **account, gruppi, membership, gioco, codici, OAuth, outbox e checkpoint import**;
4. conserva schema e `__EFMigrationsHistory`: niente downgrade o `down --volumes`;
5. lascia l'app ferma e conserva Azurite, dump sorgente, backup, telemetria, certificati e key ring.

Il backup contiene dati sensibili: proteggilo con ACL/cifratura. La verifica dell'indice non sostituisce un test completo di recovery. Un errore SQL annulla il reset e conserva il backup. Dopo il reset importa **prima** di riaprire login/MCP. Per ripartire senza legacy, avvia e fai nuovamente login: gli account saranno nuovi. Il reset non revoca credenziali presso Microsoft o altri provider.

## Bash / WSL e test

```bash
DUMP_NAME=azure-prova1 bash scripts/local/sync-storage.sh .env
DUMP_NAME=azure-prova1 RESUME=true INCLUDE_TABLES=true bash scripts/local/sync-storage.sh .env
DUMP_NAME=azure-prova1 USE_EXISTING_DUMP=true APPLY=true bash scripts/local/sync-storage.sh .env
CONFIRM_DATABASE=fantasoccer_local WHAT_IF=true bash scripts/local/reset-postgres.sh .env
```

Per il reset Bash effettivo ometti `WHAT_IF=true`, solo dopo averne verificato il piano.

Su Linux/WSL/CI, `python3 scripts/local/test-maintenance.py` usa Docker simulato (test Bash e PowerShell, se `pwsh` è installato). `--docker` aggiunge una fixture PostgreSQL temporanea **senza porte o volumi dell'app**: non resetta mai lo stack applicativo. Nessun reset dei tuoi dati reali viene effettuato per sviluppare/verificare gli script.
