# Export e ripristino del dataset legacy

Tool .NET 10 separato dall'applicazione. Esporta lo stato corrente di **tutti i container Blob e tutte le tabelle** accessibili nell'account configurato, compresi container vuoti, immagini e log.
Non interpreta né rinomina i campi dei documenti Rystem: i byte originali servono all'importatore relazionale successivo.

**Non è ancora l'importatore PostgreSQL e non è un backup disaster-recovery completo dell'account Azure.**
Non esporta né inventaria Queue/File Share, versioni/snapshot Blob, oggetti soft-deleted, tag, policy di accesso, tier o tipo Blob.
Le esclusioni sono esplicite nel manifest. `complete=true` significa completo soltanto nell'ambito `blob-table-current-state`, non «tutto l'account».
Prima dell'uso produttivo occorre verificare l'inventario dei servizi e predisporre un backup separato per tutto ciò che non rientra in questo ambito.

## Export reale: solo su richiesta esplicita

Il tool esegue esclusivamente letture sulla sorgente. Non avviare un export reale come parte dei test.
Usare una credenziale con i soli permessi necessari di listing/lettura Blob e listing/query Table. La versione attuale accetta connection string, anche con SAS; il supporto a identità Entra è ancora da implementare.
La connection string viene letta da `FANTASOCCER_EXPORT_STORAGE`, mai dagli argomenti della CLI. Non inserirla in comandi salvati nella cronologia, file versionati, report o log.

Esempio Bash per acquisirla senza mostrarla né scriverla nella cronologia:

```bash
read -r -s -p 'Storage connection string (read-only): ' FANTASOCCER_EXPORT_STORAGE
export FANTASOCCER_EXPORT_STORAGE
bash scripts/migration/export-storage.sh --directory /percorso/protetto/fantasoccer-export --allow-remote-source
unset FANTASOCCER_EXPORT_STORAGE
```

La destinazione deve essere assoluta e inizialmente vuota. Preferire una directory esterna al repository e agli artefatti di build, in un volume protetto/cifrato.
Su Linux/macOS il tool crea directory `0700` e file `0600`. Le directory già esistenti non vengono riconfigurate: assicurarsi che anche directory padre e ACL siano protette. Su Windows configurare esplicitamente ACL equivalenti.
Non attivare shell tracing (`set -x`) o strumenti che raccolgano l'ambiente del processo.

Per un dataset consistente **fermare prima API in scrittura, job, aste e tutti gli altri scrittori**. L'export online è solo esplorativo.
`--source-quiesced` registra nel manifest la dichiarazione dell'operatore che gli scrittori sono fermi: non ferma servizi e non verifica autonomamente tale condizione.
La dichiarazione non viene ereditata automaticamente quando si riprende un export.

## Ripresa

```bash
bash scripts/migration/export-storage.sh --directory /percorso/protetto/fantasoccer-export --allow-remote-source --resume
```

- Rifiuta sorgenti differenti e directory non vuote senza `--resume`.
- Riesegue il listing per rappresentare lo stato corrente, incluse rimozioni dalla sorgente.
- Riusa i Blob soltanto se ETag, dimensione e checksum locale corrispondono; i file corrotti vengono riscaricati.
- Il download è condizionale sull'ETag: modifiche concorrenti possono far fallire l'export, non vengono ignorate.
- Checkpoint atomico ogni 50 Blob e a fine container/tabella; i Blob non ancora registrati possono essere riscaricati dopo un'interruzione.
- Le Table vengono riscritte dall'inizio, evitando di riprendere una scansione su uno stato potenzialmente cambiato.
- File e manifest vengono scritti prima in `.partial`, poi rinominati. Un lock locale impedisce due esecuzioni del tool sulla stessa directory.
- In caso di errore/cancellazione il manifest rimane incompleto e non può essere ripristinato.
- Se il processo viene terminato prima di aver creato il primo manifest valido, usare una nuova directory; nessun checkpoint precedente è disponibile.
- I payload non più referenziati dopo una ripresa possono rimanere sul disco. Non vengono ripristinati; la loro cancellazione non è automatica.

## Verifica offline

```bash
dotnet run --project tools/Fantasoccer.StorageMigration -- verify --directory /percorso/protetto/fantasoccer-export
```

Controlla versione e completezza del manifest, duplicati, presenza/dimensione/SHA-256 dei payload, conteggi Table, chiavi duplicate e decodifica dei tipi.
Non richiede credenziali né connessione Azure. I checksum rilevano corruzione accidentale; non sono una firma crittografica che autentica un dump ricevuto da terzi.

Struttura:

```text
manifest.json             # provenienza non firmata, intervallo acquisizione, risorse, checksum, esclusioni
blobs/<sha256-nome>.bin    # byte originali; nome e container originali nel manifest
tables/<sha256-nome>.jsonl # PartitionKey/RowKey e valori con tipo Edm esplicito
.migration.lock           # lock locale del tool
```

I nomi dei file sono hash, non percorsi derivati direttamente dai nomi dei Blob; ciò evita traversal, collisioni fra maiuscole/minuscole e caratteri non ammessi dal filesystem.
ETag/timestamp Table sono conservati come provenienza. Int64 e Double usano una rappresentazione testuale tipizzata per evitare perdita di precisione; i binari usano Base64.
Il manifest non contiene connection string o SAS, ma può contenere nomi e metadati sensibili: proteggere l'intera directory, non solo i payload.

## Ripristino esclusivamente locale

Avviare prima `docker compose -f ops/migration/compose.yml up -d --wait postgres azurite`.
Gli account Azurite dedicati sono `migrationtest` (sorgente sintetica) e `migrationrestore` (destinazione). Le chiavi sono pubbliche e solo di test.

```bash
export FANTASOCCER_RESTORE_STORAGE='DefaultEndpointsProtocol=http;AccountName=migrationrestore;AccountKey=bG9jYWwtdGVzdC1vbmx5;BlobEndpoint=http://127.0.0.1:11000/migrationrestore;TableEndpoint=http://127.0.0.1:11002/migrationrestore'
dotnet run --project tools/Fantasoccer.StorageMigration -- restore-local --directory /percorso/protetto/fantasoccer-export
unset FANTASOCCER_RESTORE_STORAGE
```

Rifiuta Azure remoto, account non dedicati ed endpoint uguali alla sorgente registrata nel dump. Non esiste un flag che abiliti il restore remoto.
Sono ammessi loopback e il nome di servizio Docker `azurite`; usare solo l'ambiente locale controllato, non proxy/tunnel verso account reali.
Verifica l'intero dump prima della prima scrittura. Un guasto successivo può lasciare un restore parziale: rieseguire lo stesso comando.
Blob sovrascritti e Table upsert `Replace` rendono la riesecuzione idempotente. Non cancella oggetti estranei alla copia: usare un account locale dedicato per riprodurre esattamente il dataset.
I container nuovi sono privati, anche se quelli sorgente erano pubblici; non viene copiato alcun accesso pubblico o SAS.
L'opzione esplicita `--public-local-files` (usata dal wrapper `scripts/local`) abilita lettura pubblica dei soli Blob in `images`, `logos`, `cardimages` sul target Azurite per il browser locale. Non rende pubblici i documenti business o altri container.
I Blob locali sono block blob; vengono preservati byte, metadati e header contenuto, non tier/tipo/versioni Azure.
Timestamp/ETag Table vengono riassegnati da Azurite. Il mapping dell'importatore dovrà usare identità della sorgente originale, chiavi e checksum, non gli ETag del ripristino.

**Un dump reale contiene dati reali anche dopo il ripristino in Azurite.** Non attivare job, OAuth, invii push o integrazioni esterne su questa copia.
I test automatici usano soltanto fixture sintetiche e cancellano solo le risorse casuali create da ciascun test.

## Test

```bash
bash scripts/migration/test-local.sh
docker compose -f ops/migration/compose.yml --profile test run --rm tests
```

Copertura: export senza modifica delle sorgenti, container/tabelle vuoti, byte e metadati, tutti i tipi Table, doppio restore, checksum corrotti, ripresa, rimozioni sorgente, interruzioni e guardie contro sorgenti/destinazioni sbagliate.
La prova con account Azure isolato, relativi permessi SAS e dataset completo reale resta obbligatoria prima dell'uso produttivo.
