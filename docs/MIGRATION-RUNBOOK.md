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

# Solo se il dataset contiene liste d'asta cifrate: servono i valori ORIGINALI.
LEGACY_ENCRYPTION_KEY=
LEGACY_ENCRYPTION_IV=

# Nessuno scrittore/notifica esterna sulla copia, specialmente durante l'import.
BACKGROUND_JOBS_ENABLED=false
EXTERNAL_HTTP_ENABLED=false
PUSH_ENABLED=false
AI_ENABLED=false
MCP_ENABLED=false
```

Sostituisci i placeholder con la tua connection string; preferisci una **SAS account di sola lettura**, con listing/lettura Blob e query/listing Table. Una SAS di container/Blob non basta a scaricare l'intero dataset. Il tool legge soltanto dalla sorgente anche se la credenziale ha permessi superiori; non supporta ancora managed identity/Entra.

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

Sono esportati **tutti i container Blob e tutte le Table accessibili nello stato corrente**: documenti, file, immagini, log, anche contenitori vuoti. Non è un backup completo dell'account Azure: non include Queue, File Share, vecchie versioni/snapshot Blob, soft delete, tag o policy di accesso. Se mancano permessi o il dump non è completo, la pipeline si ferma.

Ripresa/riacquisizione della **stessa** sorgente:

```powershell
.\scripts\local\sync-storage.ps1 -EnvFile .env -DumpName azure-prova1 -Resume
```

`-Resume` rifa il listing, riutilizza solo Blob ancora coerenti e riscrive le Table. Non è un append cieco. Un dump completato si può verificare/analizzare senza ricontattare Azure usando `-UseExistingDump` al posto di `-Resume`.

## 3. Importare esplicitamente la copia locale

Prima leggi il report: `pendingContainers` sono dati sconosciuti/non coperti; `pendingFields` richiedono riconciliazione; `preservedArtifacts` sono file/archivi preservati senza inventare dati SQL. `fullMigrationComplete=false` non è un errore del comando, ma **non autorizza il cutover**.

Ferma anche eventuali processi `dotnet run`/debug avviati sull'host, poi:

```powershell
.\scripts\local\sync-storage.ps1 -EnvFile .env -DumpName azure-prova1 -UseExistingDump -Apply
```

Con `-Apply` lo script ferma API/frontend Compose, applica lo schema e importa SQL in transazione, quindi ripristina i Blob/Table in Azurite. SQL viene importato **prima** del restore file: un DB che rifiuta la reimportazione non causa sovrascritture dei file. Un errore successivo del restore lascia l'app ferma: correggi e ripeti sullo stesso dump. SQL/file non sono una singola transazione distribuita.

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
DUMP_NAME=azure-prova1 USE_EXISTING_DUMP=true APPLY=true bash scripts/local/sync-storage.sh .env
CONFIRM_DATABASE=fantasoccer_local WHAT_IF=true bash scripts/local/reset-postgres.sh .env
```

Per il reset Bash effettivo ometti `WHAT_IF=true`, solo dopo averne verificato il piano.

Su Linux/WSL/CI, `python3 scripts/local/test-maintenance.py` usa Docker simulato (test Bash e PowerShell, se `pwsh` è installato). `--docker` aggiunge una fixture PostgreSQL temporanea **senza porte o volumi dell'app**: non resetta mai lo stack applicativo. Nessun reset dei tuoi dati reali viene effettuato per sviluppare/verificare gli script.
