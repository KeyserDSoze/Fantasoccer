# Chiusura operativa: job, editoriale e archivi

## Decisioni

### Job SQL

Il runtime PostgreSQL può abilitare il proprio `SqlJobsWorker`; non registra il vecchio scheduler né repository dati Blob. I job richiamano gli stessi servizi applicativi di HTTP/MCP per calendario/catalogo, voti, probabilità/statistiche, formazioni future, risultati assenti, scadenza mercato, albo e foto.

- Stato e ricevute di esecuzione sono SQL. La chiave è nome job/stagione; il lease dura cinque minuti e il budget di esecuzione è quattro minuti. Il recupero marca la vecchia esecuzione `LeaseLost`.
- Il fencing token fluisce nei contesti figli. `CoreMigrationLock` lo verifica sotto il lock di scrittura: una vecchia esecuzione non può salvare dopo la sostituzione del lease.
- Annullamento/errori registrano una ricevuta e liberano il lease; i log non salvano stack trace, body provider o URL con credenziali. Le risposte di esecuzione distinguono `Succeeded`, `Partial`, `Failed`, `Canceled`, `LeaseLost`.
- Le scadenze usano Europe/Rome e sono tradotte in UTC, inclusi i cambi DST. Il refresh immagini semestrale è fissato alle 03:00 del primo marzo/settembre, sostituendo la vecchia espressione cron ambigua.
- Il ricalcolo programmato elabora soltanto risultati assenti. Non rigenera calendari e non sovrascrive risultati importati/salvati. La scadenza mercato ha un attore di sistema nullable, non un'identità utente inventata.
- Le chiamate manuali ai job globali richiedono amministrazione globale verificata. Nessuna impersonazione di un amministratore del gruppo nei task automatici.

### Albo incrementale

Un archivio storico può contenere stagioni non più disponibili. Non si sostituiscono i totali di carriera con la sola somma delle classifiche rimaste.

L'import inizializza checkpoint annuali solo se i totali disponibili corrispondono all'archivio autorevole e le identità sono univoche. Altrimenti preserva l'archivio e segnala una riconciliazione. `POST .../hall-of-fame/confirm-baseline`, autorizzato al responsabile del gruppo e protetto dalla versione attesa, sigilla i valori annuali correnti senza cambiare i totali storici, con audit.

Il refresh applica soltanto le differenze successive a quei checkpoint, aggiunge vittorie senza duplicarle e può migliorare il record partita con risultati salvati. Non cancella record standalone o record giocatore importati e non inventa un nuovo algoritmo storico per campi che il vecchio job non calcolava. Le ambiguità richiedono dati/mapping espliciti, non euristiche sui nomi.

### News e suggerimenti AI

News e suggerimenti salvati sono importati e leggibili su SQL. HTML e date originali restano dati, non vengono eseguiti. Un'etichetta lega non risolvibile viene conservata senza inventare una FK.

La generazione è opzionale, disabilitata per default e richiede un POST esplicito. Il vecchio sistema non schedulava questi due job: non si aggiungono chiamate AI a pagamento automatiche al bootstrap.

- News: responsabile della competizione, solo risultati salvati completi, facts privi delle email dei proprietari, CAS di edizione/calendario dopo il fetch.
- Suggerimenti: proprietari della squadra e destinatario personale nella PK; un co-owner può generare i propri ma non leggere quelli dell'altro account. Prompt del chiamante, proposta del motore ottimale condiviso e CAS della rosa; l'AI non salva posizioni o valori economici. Il modello preliminare senza destinatario verificabile richiede riconciliazione prima dell'upgrade; nessun downgrade rimuove questa separazione da righe popolate.
- Lock di sessione per risorsa contro generazioni concorrenti duplicate, timeout di 90 secondi, limiti di input/output e 4096 token. Nessun lock globale di scrittura mantenuto durante la rete.
- Output nuovo in testo semplice; vecchio HTML dichiarato `legacy-html` e mai inserito direttamente nel DOM. Configurazione OpenAI esclusivamente nell'env privato; `ExternalHttp:Enabled=false` disabilita anche la generazione.

### Log e file preservati

La Table `logs` è importata come archivio operativo privato: PK/RowKey, ETag e proprietà con tipo/valore originali. I timestamp conservano separatamente i tick a 100 ns perché PostgreSQL indicizza gli istanti con precisione al microsecondo. Il nuovo elenco operativo è limitato e redatto; le proprietà originali non vengono esportate alla dashboard.

Il frontend non usa più `rystem.repository.client`, né il vecchio setup o mapper `Raw`. I log SQL e le esecuzioni job sono consultabili solo da amministrazione globale verificata.

`preservedArtifacts` distingue file Blob, snapshot derivati `livegroup` e chiavi private `mcp-keys`, conservati dal dump verificato/ripristino, dai dati strutturati importati. I container sconosciuti restano `pendingContainers`: non vengono dichiarati migrati automaticamente. L'archivio originale va conservato e protetto anche dopo l'import.

## Limiti di accettazione, non bypass

`fullMigrationComplete=false` non viene trasformato in un'autorizzazione al cutover. Servono riconciliazione del dump reale, conferme dei checkpoint non ricostruibili, configurazione/rotazione delle chiavi, login Microsoft/MCP reale e collaudo delle schermate. Le prove usano credenziali sintetiche e provider controllati; nessun invio, rotazione remota o modifica Azure viene effettuato per chiudere il branch.
