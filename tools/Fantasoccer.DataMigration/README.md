# Importatore relazionale — stato e comandi

L'importatore copre i domini censiti e le riconciliazioni note, ma **non autorizza automaticamente il passaggio produttivo** senza dump reale e accettazione. Il nome CLI `import-core` è mantenuto per il comando di import esplicito, distinto dalle migration dello schema.

## Ambito importato

Dai container legacy `appidentity`, `group`, `settings`, `team`, `dailyteams`, `openingteams`, `openingdailyteams`, `openingstandings`, `realplayerswrapper`, `randomizedplayers`, `realteamwrapper`, `realcalendar`, `realrank`, `live`, `notification`, `official`, `chancedrealplayerwrapper`, `statplayerswrapper`, `calendar`, `playedgame`, `rank`, `dailyrank`, `marketwrapper`, `auction`, `halloffame`, `cards`, `pushnotification`, `pushnotificationsent`, `news`, `teamwithai`, `mcp-oauth` e dalla Table `logs`:

- utenti con email canonica e ID stabile;
- gruppi e membership autorevoli, con ruoli distinti per gruppo;
- basket e leghe, stagioni e partecipazioni condivise;
- anagrafiche delle squadre annuali e proprietà/comproprietà;
- mapping delle identità legacy e checkpoint dei documenti.
- profili, selezioni e preferenze; riferimenti storici non risolvibili conservati in `UnresolvedAccountSelections` per revisione;
- regolamenti completi in tabelle relazionali, senza payload JSON di gioco;
- rose, vendite/riacquisti, prezzi, ricavi, fondi, posizioni e storico dei cambi, separati per snapshot;
- giocatori stagionali condivisi, club e calendario Serie A;
- voti per sorgente/giornata, probabilità (inclusi trend/ultima partita storici), statistiche e gare, classifica reale con punti frazionari;
- calendario fantasy con stage/turni/giornate distinti, risultati/snapshot partite e classifiche stagionali/giornaliere;
- classifica iniziale con punteggi e premi originali, riferita alla squadra N-1 e alla competizione N;
- ordine/visibilità dei giocatori per lega-stagione con riferimenti al catalogo condiviso;
- proposte di mercato, parti, snapshot giocatori/prezzi, stato, consensi/voti e date, senza eseguire nuovamente gli scambi già approvati;
- sessioni aste, ordine/ultimi mostrati, vincite, lotto e statistiche mostrate, timer e partecipanti legacy come audit;
- albo con totali per proprietario, vittorie per stage/anno, record giocatore anche senza anno e record partita standalone;
- librerie carte, condizioni/soglie, presenza di bounds vuoti, zero/false/ruoli duplicati e riferimento alle immagini Blob;
- preferenze e dispositivi push con audit originale; checkpoint, baseline voti, sequenze, ricevute e pending con tag/tentativi/scadenze originali (pending legacy in quarantena, mai inviate dall'import);
- documenti OAuth privati con versione/CAS e retention preservate.
- news con contenuto/date originali e suggerimenti privati legati al proprietario identificato dalla chiave sorgente, non al proprietario corrente indovinato;
- log storici, **solo con opt-in `IMPORT_LEGACY_LOGS=true`**, come archivio SQL privato con PK/RowKey, ETag, proprietà tipizzate e timestamp originali (tick esatti separati dall'istante indicizzato).

I documenti sono letti dal dump verificato, usando il **converter reale Rystem `Entity<T,TKey>`**: envelope attuale `k/v` e storico `Key/Value`.
Le abbreviazioni restano nel lettore legacy, non nei nuovi DTO applicativi.

### Identità con chiavi legacy distinte dall'email

Il repository string-key apriva il Blob con la chiave fornita e restituiva il profilo (`Value`) senza confrontare `Key` con l'email. Per `appidentity` sono ammessi chiave email coerente, lookup Blob email coerente con chiave interna opaca/vuota/mancante, oppure **ID tecnico non-email non vuoto che coincide esattamente con il nome del Blob**. In quest'ultimo caso il confronto è case-sensitive e non rimuove spazi: non si presume che un ID tecnico abbia le regole di canonicalizzazione di un'email.

L'email del profilo resta obbligatoria e valida; chiavi che dichiarano un'altra email, lookup email discordanti e documenti con email duplicata rimangono errori. Non vengono decodificati o indovinati ID, non si usa il nome visualizzato per trovare un account e non si tratta l'ID tecnico come subject di autenticazione.

Ogni recupero è segnalato in `pendingFields` e `reconciledIdentityDocuments` (SHA-256 dei documenti), con mapping durevole `identity-document` → ID account e riferimento al file di archivio/checksum sotto forma di hash, senza alterare ID deterministici, ruoli o membership e senza copiare in SQL nomi/chiavi tecniche originali. Gli originali restano nel dump. Gli errori di identità riportano il checksum del documento per trovarlo nel manifest, non indirizzi o chiavi potenzialmente private. Vedere il [runbook](../../docs/MIGRATION-RUNBOOK.md#se-compare-invalid_email).

`identity_collision` mantiene il blocco per default e fornisce `Identity details`: distingue conflitto del nome email del Blob, conflitto della chiave email interna e duplicato dell'email del profilo. Per i duplicati espone i due riferimenti hash e confronti booleani di profilo completo, nome, selezione e gruppi/ruoli (senza valori). Nessuna copia viene scelta o unita automaticamente, nemmeno se identica. Vedere [diagnosi collisioni](../../docs/MIGRATION-RUNBOOK.md#se-compare-identity_collision).

Il gestore può approvare una coppia esatta tramite `IDENTITY_RESOLUTIONS_JSON` nell'env Compose (CLI diretta: `FANTASOCCER_IDENTITY_RESOLUTIONS_JSON`), array di oggetti con `preferredDocumentSha256` e `otherDocumentSha256`. Per default sono ammessi solo checksum distinti/univoci di due documenti della stessa email, con copia preferita nel lookup email e altra copia non-email; in questa modalità non è ammesso invertire la preferenza. Chiavi email discordanti, terze copie, riferimenti mancanti/stantii e gruppi/membership incoerenti rimangono errori in entrambe le modalità. Ruoli e selezioni non vengono mai uniti; la copia non scelta resta nel dump. Report `resolvedIdentityConflicts` e mapping `identity-resolution` registrano la decisione con soli riferimenti hash. I dettagli sono nel [runbook](../../docs/MIGRATION-RUNBOOK.md#scelta-esplicita-e-vincolata-ai-checksum).

Per due lookup email diversi **solo per case**, è necessaria un'approvazione separata: quell'oggetto deve impostare `allowEmailLookupAlias:true` (default `false`). Sono rifiutati ID tecnici, differenze di spazi o altri nomi non equivalenti: non è un override generico. La copia preferita viene scelta integralmente e il report espone `emailLookupAlias`; le regole precedenti restano nell'array senza questo flag. Non si tratta di un'unione automatica dei gruppi o di una scelta basata su nome/quantità di dati. Vedere [alias email](../../docs/MIGRATION-RUNBOOK.md#due-lookup-email-diversi-solo-per-maiuscole).

### Proprietari storici senza email

`IMPORT_LEGACY_NAMED_OWNERS=true` (CLI: `FANTASOCCER_IMPORT_LEGACY_NAMED_OWNERS`) abilita esplicitamente i riferimenti non-email dei partecipanti storici. Il default è `false`. Sono creati veri record `Users`, globalmente abilitati, con email `null`, nome originale e membership di gruppo disattiva (`Roles=None`). Non sono email fittizie né identità di autenticazione. Il report espone il conteggio `historicalNamedUsers`.

L'identità sorgente è il valore esatto **nel gruppo**, con un mapping deterministico hash: nessun accorpamento per nome visualizzato, case/spazi o fra gruppi diversi. Rose, classifiche, calendari e albo mantengono i collegamenti tramite gli ID; i valori storici originali restano nei relativi snapshot e nel dump. Valori vuoti, con caratteri di controllo, oltre 200 caratteri o email malformate contenenti `@` rimangono errori. Le identità di login e i membri con ruoli attivi continuano a richiedere email valide e a superare i controlli di membership.

Nella lista membri sono visibili anche i disattivi: un amministratore del gruppo può gestirli per ID, riattivarli con ruoli espliciti e configurare un'email reale ancora mancante. L'operazione non unisce un account già esistente con quell'email, non modifica email già configurate e non riabilita account disabilitati globalmente. Il login richiede comunque la verifica Microsoft. Vedere [gestione utenti](../../docs/GROUP-ACTIVATION.md#utenti-esistenti-e-stato-nel-gruppo).

### Diagnosi di `invalid_player`

Gli errori sui calciatori riportano checksum del documento e `Player details`: container, riferimento hash al file del dump, percorso JSON del record, motivi controllati (`empty_player_key`, `role_out_of_range`, `null_player`), presenza/lunghezza del nome e ruolo numerico. Nomi, chiavi giocatore, proprietari, nomi Blob originali e payload non sono loggati. Il contesto è passato esplicitamente dal lettore, anche per snapshot annidati e per copie discordanti di un giocatore già noto; supporta envelope `k/v` e `Key/Value` e il casing storico accettato dal lettore push.

È solo diagnostica: stessi controlli sulla chiave e sui ruoli `-1..3`, nessun salto, correzione automatica, cambio di mapping/schema o nuova opzione env. Le voci `null` e i record con nome mancante restano rifiutati con un errore mirato invece di un'eccezione generica. I container esclusi non vengono decodificati. Vedere il [runbook](../../docs/MIGRATION-RUNBOOK.md#se-compare-invalid_player) e rianalizzare il dump esistente senza `-Apply`.

## Riconciliazioni e artefatti conservati

### Escludere le vecchie aste senza fornire le chiavi legacy

Nel `.env` privato imposta `IMPORT_LEGACY_AUCTIONS=false`: analisi e import omettono **sia `auction` sia `randomizedplayers`** prima di decodificare i documenti. Puoi lasciare vuoti `LEGACY_ENCRYPTION_KEY` e `LEGACY_ENCRYPTION_IV`. Con CLI .NET diretta l'opzione corrispondente è `FANTASOCCER_IMPORT_LEGACY_AUCTIONS=false`. Il default è `true`: l'assenza delle chiavi non causa omissioni silenziose.

Il report dichiara `excludedContainers` e conserva le aste originali nel dump verificato (`preservedArtifacts`), non in SQL. Non vengono cancellati Blob sorgente, rose, giocatori acquistati, prezzi o saldi già presenti negli snapshot delle squadre. L'opzione non disabilita le nuove aste dell'app. Le chiavi applicative locali `ENCRYPTION_*` sono una configurazione distinta e non vanno svuotate.

### Log storici esclusi per default

`IMPORT_LEGACY_LOGS=false` è il default del Compose, del template env e dell'importatore. Download/restore Table sono anch'essi opt-in (`-IncludeTables` nel wrapper, `--include-tables` nella CLI): la Table `logs` resta nell'archivio solo se già acquisita o richiesta esplicitamente, ma non viene salvata in SQL. Il report dichiara `table:logs` in `excludedContainers` e, se il dump contiene righe, in `preservedArtifacts`. Altre Table acquisite ma sconosciute restano pendenti. Per importare l'archivio SQL servono sia un dump con Table sia `IMPORT_LEGACY_LOGS=true` (CLI diretta: `FANTASOCCER_IMPORT_LEGACY_LOGS=true`); un opt-in SQL su dump Blob-only produce un avviso nel report.

L'opzione riguarda solo il passato: la telemetria OpenTelemetry/Aspire e lo stato/ricevute dei job SQL non cambiano. La loro futura revisione dovrà separare diagnostica da audit e coordinamento funzionale, senza eliminare controlli di lease/idempotenza insieme ai messaggi di log.

Il report elenca `pendingFields`, `pendingContainers`, `excludedContainers`, `preservedArtifacts` e mantiene `fullMigrationComplete=false` per non confondere copertura implementata e accettazione del cutover.
I job SQL, refresh incrementale albo, editoriale/AI opzionale, log e outbox sono implementati. Restano decisioni dipendenti dai dati: checkpoint crediti/albo non ricostruibili, pending push senza scope verificabile, librerie non-default e container sconosciuti. Non vengono risolte inventando identità o scartando dati.
`images`, `logos`, `cardimages` restano file Blob. Gli snapshot derivati `livegroup` e le chiavi `mcp-keys` sono conservati nel dump verificato/ripristino privato, non usati come fallback dati SQL né pubblicati: il report li distingue in `preservedArtifacts`. I container sconosciuti rimangono pendenti. Conservare il dump originale e i suoi checksum anche dopo l'import.
I loghi importati mantengono il riferimento al blob originale (inclusa la maiuscola dell'owner nel nome file), non un percorso inventato usando i nuovi UUID. Gli altri riferimenti ai file vanno riconciliati prima del passaggio definitivo.
Non scartare il dump originale e non considerare l'import strutturale equivalente alla migrazione completa.

## Comandi Docker Desktop

Il [runbook Windows](../../docs/MIGRATION-RUNBOOK.md) documenta anche `sync-storage.ps1` (export/analisi, con import solo tramite `-Apply`) e `reset-postgres.ps1` (reset locale esplicito con backup e conferma).

Dopo export e verifica del dump (vedere `scripts/local/README.md`):

```powershell
.\scripts\local\local.ps1 analyze-core -DumpName prova1
.\scripts\local\local.ps1 import-core -DumpName prova1
.\scripts\local\local.ps1 up
```

- `analyze-core`: verifica l'archivio e valida il piano senza scrivere nel DB.
- `import-core`: ferma API/frontend, applica lo schema e importa sul DB locale dedicato. Non riapre automaticamente il traffico.
- `migrate`: continua ad applicare soltanto lo schema EF.

Il mount del dump per l'import è read-only. Conservare anche `.migration.lock`: i lettori acquisiscono una lease condivisa e l'export una lease esclusiva.
La CLI rifiuta database remoti o diversi dal laboratorio `fantasoccer_local`. Nessuna credenziale o payload personale viene stampato nei messaggi d'errore.

## Idempotenza e sicurezza

- ID deterministici derivati da sorgente originale, tipo e chiave legacy, con mapping esplicito; non dipendono dall'ordine o dal nome del file locale.
- Snapshot immutato: nessuna riscrittura; il report indica i documenti saltati.
- Snapshot modificato: riconciliazione delle sole righe appartenenti a questa sorgente, inclusi figli rimossi, in **un'unica transazione**. Per ora non è un import batch di grandi volumi.
- Due importatori sono serializzati da advisory lock PostgreSQL. Errori o interruzioni prima del commit non lasciano aggregati parziali.
- Target già popolato e non riconducibile all'import rifiutato; sorgente diversa rifiutata.
- Membership discordanti fra account e gruppo, collisioni email/chiavi o riferimenti mancanti bloccano l'import: non vengono risolti attribuendo più privilegi.
- Un proprietario storico senza membership attiva conserva una relazione con ruolo `None`, non ottiene accesso al gruppo.
- La prima scrittura riuscita attraverso i servizi applicativi marca il target come aperto alle modifiche. Ogni import successivo è bloccato, anche se il dump è invariato.
- I tentativi non autorizzati non chiudono l'import: la transazione viene annullata.
- Il marker non intercetta scritture manuali SQL o future integrazioni che non usino il coordinamento: il target di migrazione va tenuto isolato e ogni nuovo writer deve rispettare la stessa regola.
- Nessun invio push, evento di business, login o modifica della sorgente durante l'import.

## Workspace frontend

Nel Compose root, aprire Impostazioni → **Gestisci i tuoi gruppi**, oppure `/workspace/groups` dopo il login.
La schermata usa DTO leggibili e non usa `Raw`, mapper di abbreviazioni o `rystem.repository.client`.
Consente lettura dei gruppi, amministrazione membri, creazione basket/partecipazioni/squadre e creazione gruppo per l'amministratore globale.
Le stesse operazioni applicative sono disponibili ai controller; i tool MCP dedicati riusano direttamente il servizio per elenco, lettura e creazione gruppo.

Il percorso principale di identità, navigazione, gruppi/regolamenti, preferenze, rose, partite/risultati, dati calcistici, classifiche, mercato/aste, albo e carte usa nuove API e DTO. Generazione e ricalcolo sono comandi espliciti. I flussi rimanenti nell'inventario non vengono serviti da un fallback Blob.
Il login social in modalità PostgreSQL usa soltanto l'account SQL. Un amico invitato può autenticarsi senza una copia Blob. I proprietari storici esistono come utenti, ma senza membership attive non hanno accesso ai gruppi; quelli senza email non possono effettuare il login. Lo stato del gruppo è distinto dalla disabilitazione globale dell'account.
Il workspace è disponibile anche fuori dal Compose; il backend richiede `PostgreSql:Enabled=true` e una configurazione SQL/file valida.

## Vincoli e riconciliazioni specifiche

- Le rose iniziali dell'anno N si riferiscono alla squadra annuale N-1 e alla lega dell'anno N. Non collegarle alla squadra N solo per rendere valida una FK.
- L'import conserva i risultati e i punteggi salvati, inclusi null, frazioni, cancellazioni, ordine delle righe e stage vuoti. Non invoca calcolatori né invia premi/notifiche.
- Per le liste aste cifrate impostare `LEGACY_ENCRYPTION_KEY` e `LEGACY_ENCRYPTION_IV` originali nel proprio env privato; non sono la nuova chiave/IV locale generata per l'app. Il tool li legge soltanto per decifrare riferimenti, non li stampa né li salva nel DB.
- I saldi legacy restano invariati e i checkpoint dei premi classifica non documentati restano `null`. Il report segnala la conferma richiesta prima di nuovi accrediti automatici; vedere [ADR 0008](../../ADR/0008-aste-sql-e-checkpoint-crediti.md).
- Le nuove API marcano le anteprime live come tali. I risultati salvati restano autoritativi; il ricalcolo è un comando separato, autorizzato e transazionale.
- I pending push importati richiedono conferma globale esplicita di scope prima dell'invio; confermare non riapre scadenze né azzera ricevute. La prova privata dei dispositivi non esiste nel legacy e non è inventata durante l'import.
- Il refresh albo applica delta annuali solo con baseline coerente/confermata, senza rimpiazzare totali storici con stagioni parziali. La conferma autorizzata non cambia i totali importati.
- I suggerimenti AI sono per account, anche fra co-owner della stessa squadra. Un upgrade del modello preliminare con destinatario non verificabile si ferma per riconciliazione; non li attribuisce automaticamente al nuovo proprietario.
- Se un owner storico non è risolvibile in una squadra annuale univoca, l'import si ferma con `unresolved_historical_team_identity`; non usa il nome come euristica per fondere squadre.
- I timestamp di calendario legacy erano confrontati con `ItalyNow`, che poteva conservare un `Kind=Utc` su un'ora locale. Si preserva l'ora originale e si normalizza esplicitamente con `Europe/Rome`; ore DST ambigue/non esistenti bloccano l'import anziché essere indovinate.
- I documenti OAuth sono metadati protocollo e usano JSONB privato; questa eccezione non viene applicata a rose, gruppi o regolamenti. Le sessioni restano utilizzabili solo se anche issuer, certificati e key ring/discriminatore sono preservati; in locale può essere necessario ricollegarsi.
- Scritture OAuth e retention effettiva chiudono anch'esse la finestra d'importazione. Importare prima di abilitare i client/traffico MCP.
- Il vecchio prototipo SQL collegava gli snapshot iniziali all'anno errato. L'upgrade/downgrade con quelle righe si ferma e richiede riconciliazione esplicita; non distrugge o rimappa automaticamente dati esistenti.

## Verifiche automatiche

La suite Docker include fixture con envelope Rystem reali, più gruppi con gli stessi ID locali, basket condivisi, ruoli differenti, doppio import concorrente, rimozione figli, conflitti e blocco dopo le scritture applicative.
Verifica inoltre autorizzazioni correnti, rifiuto cross-group, protezione dell'ultimo amministratore e impossibilità di cambiare il profilo globale tramite membership.
Il frontend ha test del client HTTP (contratti, errori, refresh e cancellazione), TypeScript e build web.
