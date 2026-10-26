# Piano di azione — Migrazione PostgreSQL

Stato: infrastruttura locale e primo nucleo applicativo relazionale implementati; migrazione completa del gioco ancora in corso.
Le fasi sono incrementali per limitare i rischi, ma il passaggio produttivo è coordinato: non si richiede mantenere due sistemi attivi in scrittura.

## Incremento 1 — Fondamenta disponibili

- [x] Progetto EF Core/Npgsql indipendente dai repository legacy, aggiunto alla solution.
- [x] Schema iniziale: utenti, gruppi, membership, stagioni, club, giocatori stagionali, basket, leghe e associazioni stagionali.
- [x] PK e FK composite su membership, dati stagionali e associazioni lega/basket; vincoli cross-group/cross-season.
- [x] Migration iniziale e snapshot, senza `EnsureCreated`.
- [x] Helper di registrazione DI e migration con timeout, verificato anche su avvii concorrenti; collegato allo startup API nel Compose root.
- [x] Laboratorio Docker PostgreSQL/Azurite, script test locale, runner Docker e workflow CI dedicato.
- [x] Test del modello e su PostgreSQL reale per vincoli, condivisione, migration ripetute e downgrade/upgrade.

Restano da caratterizzare tutti i workflow legacy e completare lo schema (inclusi regolamenti, squadre, rose e storico).
Il percorso fino a PostgreSQL richiede ancora l'importatore relazionale.
I test verificano ora l'integrità relazionale, non ancora l'autorizzazione dei nuovi endpoint.

## Incremento 2 — Dataset locale legacy

- [x] Tool separato per export in sola lettura di tutti i container Blob e delle Table dell'account, con byte originali e tipi Table preservati.
- [x] Manifest versionato, checksum, checkpoint, ripresa e marcatura di export incompleti.
- [x] Verifica offline e ripristino idempotente esclusivamente su account Azurite locali dedicati, diverso dalla sorgente.
- [x] Script export con credenziale da ambiente, permessi locali restrittivi e documentazione di protezione/consistenza.
- [x] Test sintetici di export, corruzione, ripresa, cancellazione e doppio restore su Azurite, integrati nel laboratorio e nella CI.

Ambito attuale esplicitamente limitato a `blob-table-current-state`: non inventaria/esporta Queue/File Share, versioni/snapshot/soft-delete e configurazioni di accesso.
Quindi il requisito di backup completo dell'intero account richiede ancora inventario dei servizi e copertura delle esclusioni applicabili; non considerare questo export da solo un backup disaster-recovery.
Nessun dato reale Azure è stato scaricato in questo incremento.

## Incremento 3 — Applicazione e diagnostica locali

- [x] Compose root per Docker Desktop/Linux containers: frontend web, API, PostgreSQL, Azurite e Aspire Dashboard.
- [x] File `.env.test` versionato con soli valori locali sintetici; `.env` e varianti private ignorati da Git e Docker build.
- [x] Wrapper PowerShell e Bash in `scripts/local` per avvio, test, export, verifica, restore e migration dello schema.
- [x] Selezione provider telemetria: OpenTelemetry locale / Application Insights / nessuno; log, tracce e metriche in Aspire.
- [x] Avvio API con migration EF opzionale prima dei job e health check live/ready; persistenza dominio ancora legacy.
- [x] Job, warm-up, fetch dati esterni e push disabilitati di default sulle copie locali.
- [x] Suite di migrazione isolata dai volumi DB/Storage applicativi nel Compose root.

Restano da completare l'importazione relazionale dell'intero gioco, il seed completo e un profilo HTTPS per MCP locale. Il login Microsoft non viene emulato e richiede configurazione e account applicativo legacy fino alla migrazione dell'identità.

## Incremento 4 — Nucleo strutturale end-to-end

- [x] Tabelle relazionali per squadre annuali e proprietà/comproprietà, con FK gruppo/basket/stagione e unicità del proprietario principale.
- [x] Import parziale `appidentity`/`group`, mapping stabile, checkpoint, transazione atomica, riconciliazione dei figli e rilevamento conflitti.
- [x] Blocco delle reimportazioni dopo la prima scrittura interattiva riuscita; lock condiviso da importatore e servizi.
- [x] Servizio applicativo EF diretto per account/gruppi, membership, basket, partecipazioni e anagrafiche squadre.
- [x] Authorization handler su risorse, DTO leggibili, API tipizzate e tool MCP che riusano il servizio.
- [x] Schermata frontend `/workspace/groups` con client DTO diretto, errori espliciti e nessun mapper compatto.
- [x] Comandi PowerShell/Bash `analyze-core` e `import-core`; report con elenco dei dati rimasti fuori e `fullMigrationComplete=false`.
- [x] Lock di startup esterno all'intera sequenza delle migration, verificato con più migration e avvii concorrenti.

**Non è completata la richiesta complessiva.** Il workspace strutturale è separato dal gioco legacy. Restano regolamenti, rose/movimenti, formazioni/storico, dati Serie A, calendari/risultati/classifiche, mercato/aste e gli altri servizi frontend, job e OAuth. Il test di accettazione dell'intera migrazione non va ancora considerato pronto.

## Incremento 5 — Percorso principale SQL, account e rose

- [x] Login Microsoft e rinnovo claim su account PostgreSQL; inviti SQL utilizzabili senza creare una seconda identità Blob.
- [x] Profili, selezioni storiche, preferenze e regolamenti normalizzati (voti per ruolo, turni, limiti carte, premi e qualificazioni).
- [x] Import di rose stagionali/giornaliere/iniziali, prezzi, vendite e cambi formazione; giocatori/club condivisi e calendario Serie A.
- [x] Competizione iniziale riferita alla squadra della stagione precedente, distinta dall'anno della competizione e dalla lega.
- [x] API formazione con rosa server, controlli proprietario/comproprietario, scadenze, limiti live, versione e nessuna modifica di prezzi/crediti dal payload.
- [x] Frontend principale account/gruppi/preferenze/rose su DTO leggibili, senza copie Raw per account e gruppo; doppie scritture delle membership eliminate.
- [x] Loghi mantenuti su Blob, con riferimenti importati, URL DTO, upload versionati e autorizzazione SQL; nome squadra aggiornato senza riscrivere documenti duplicati.
- [x] Persistenza OAuth SQL con CAS e retention, import dei documenti protocollo e protezione dell'audience MCP.
- [x] Runtime PostgreSQL senza registrazione dei repository dati Rystem, endpoint automatici o controller Blob alternativi. Gli adattatori non ancora migrati restituiscono 404; job/push legacy non sono avviabili in questa modalità.
- [x] Profilo HTTPS locale, generatore certificato containerizzato e script dedicati; la fiducia del certificato Windows resta una scelta esplicita dell'utente.

La richiesta resta aperta: **partite e risultati/classifiche fantasy, voti/probabilità/statistiche e relative pagine, mercato, aste/SignalR, competizione iniziale completa, Hall of Fame, carte/news, notifiche/outbox e ingestione/job** devono ancora essere sostituiti e collaudati. La presenza delle API formazione non equivale ad avere già migrato il wrapper partita/SoccerField e i suoi vecchi contratti.

### Prossimo blocco operativo

1. Normalizzare e importare voti live/notification/official, probabilità e statistiche condivise.
2. Normalizzare calendario fantasy, partite e classifiche; riusare i calcolatori esistenti separando i caricamenti dai repository.
3. Sostituire `gameService`, `calendarService`, `rankService`, modelli e schermate partita, inclusi salvataggi e cambi live con DTO completi.
4. Poi mercato/aste e i relativi servizi condivisi, senza riabilitare gli adattatori Blob.

Note tecniche per il blocco partite:

- `Fantasoccer.Business/Vote/TeamCalculator.cs` contiene calcolo già caratterizzato ma carica calendario/voti/probabilità attraverso repository. Separare questi caricamenti in un reader applicativo SQL specifico, non introdurre un nuovo repository generico/adattatore EF dietro `IRepository`.
- `Calendar.Rounds` distingue stage, turno fantasy e giornata Serie A. Conservare queste dimensioni e i risultati importati, senza ricalcolare lo storico durante l'import.
- `Rank.RankedTeams` e `Rank/day` sono snapshot diversi per lega/stagione/stage/giornata; i voti reali di base sono condivisi fra gruppi.
- `gameService`, `types/game.ts`, `types/calendar.ts`, `types/rank.ts`, `types/serieA.ts` e `SoccerField` mantengono ancora vecchi contratti da sostituire. La nuova API formazione esiste, ma il wrapper partita deve trasmettere ID giocatore/squadra e versioni invece di prezzi/documenti client.
- `types/legacyAuctionGroupPayload.ts` è un reader temporaneo isolato per il vecchio SignalR, introdotto solo per non reintrodurre Raw nei DTO gruppi. Deve sparire con la migrazione aste, non è parte del contratto finale.
- Continuare a mantenere `fullMigrationComplete=false` e gli adattatori non portati indisponibili fino alla copertura completa.

## Incremento 6 — Dati calcistici e competizioni (in lavorazione)

- Import relazionale di voti live/notification/official, probabilità (inclusi i campi storici non più presenti nel CLR), statistiche/storico e classifica reale con punti frazionari.
- Import di calendario fantasy, risultati, snapshot partite e classifiche stagionali/giornaliere, preservando stage, turno fantasy e giornata Serie A. Nessun ricalcolo durante l'import.
- `TeamCalculator` e `OptimalFormationService` conservano gli algoritmi caratterizzati; reader specifici SQL/legacy separano il caricamento senza introdurre repository generici EF.
- Nuove API `/api/football` e `/api/competitions`, incluse formazione/versioni, proposta ottimale, generazione calendario, ricalcolo esplicito e live derivato. Il ricalcolo attende i voti ufficiali di tutte le squadre reali e chiude tutto in una transazione.
- Il calendario con risultati non viene sostituito automaticamente. La rigenerazione richiede conferma/versione ed è limitata a calendari senza risultati storici.
- Game/SoccerField non inviano più documenti Team/prezzi o coppie di chiavi-nome: inviano ID e posizioni, versione giornaliera e versione della rosa di origine. Risposte `isOk/message`.
- Competizione iniziale: preparazione POST esplicita, classifica e formazioni SQL; snapshot squadra N-1 e giocatori della stagione N. Import della classifica originale senza ricalcolarla.
- Frontend Serie A/voti/probabilità/statistiche/calendario/classifiche e live su API SQL. Restano mapper compatti condivisi usati da aste/Hall of Fame: rimuoverli nel relativo blocco, non dichiarare completata la pulizia frontend.

Il nuovo blocco richiede ancora la verifica Docker e smoke finale. Ingestione automatica, job, mercato, aste, Hall of Fame, news/carte, notifiche/outbox e le restanti integrazioni rimangono aperti. Non considerare completata l'intera migrazione.

## Incremento 7 — Ordinamento aste e mercato (in lavorazione)

- Import e randomizzazione dell'ordinamento giocatori per lega/stagione, con ordine e visibilità separati dal catalogo globale e protezione di versione.
- Import del mercato senza eseguire le proposte: snapshot giocatori/prezzi, parti, consensi, rifiuti, stato, accettazione e timestamp originali.
- Servizio mercato EF condiviso dagli adattatori HTTP/MCP: creazione solo con ID, importi pattuiti e versioni delle rose; stato, approvatori e prezzi client ignorati.
- Accettazione del venditore, rifiuto, annullamento e quorum per squadra (non per account/co-owner), con controlli correnti di proprietà e versione proposta.
- Scambio e accrediti ad entrambe le squadre in un'unica transazione. Le righe di vendite/riacquisti precedenti non vengono cancellate da un `RemoveAll` per nome.
- Eventi tipizzati durevoli committati con le rose, da collegare alla consegna notifiche nel relativo blocco. L'import non genera eventi.
- Frontend mercato su contratti leggibili; selezione squadre con ID, caricamento di tutti i basket della lega e richieste senza documenti giocatore/prezzi.

Le aste live/SignalR e la loro importazione cifrata, la consegna push e gli altri domini rimanenti sono ancora da implementare. L'ordinamento non equivale al completamento del flusso asta.

## Incremento 8 — Aste SQL e checkpoint economici (in verifica)

- Modello/import di sessioni, ordine privato, ultimi mostrati, vincite, lotto attuale, statistiche realmente mostrate e timer UTC. I partecipanti persistiti legacy sono audit, non presenze o autorizzazioni correnti.
- Le liste cifrate richiedono `LEGACY_ENCRYPTION_KEY` e `LEGACY_ENCRYPTION_IV` espliciti nel file env privato. Non viene usata per errore la nuova chiave locale generata da `init`; la mancanza delle chiavi blocca l'analisi senza scritture.
- Servizio applicativo unico per controller, MCP e SignalR: ruoli/proprietà riletti dal DB, offerte versionate, budget/limiti e sostituti server, assegnazione atomica, pausa/ripresa/chiusura/reset/finalizzazione e cambio banditore.
- Le API non pubblicano l'ordine futuro. Gli eventi SignalR contengono ID/versione/operazione, e il client rilegge uno stato autorizzato; presenze per connessione e token query limitati al solo hub.
- Frontend aste su DTO leggibili senza `Raw`/decoder/chiavi cifrate. Il reader temporaneo `legacyAuctionGroupPayload.ts` è stato eliminato.
- Gli accrediti classifica usano un checkpoint incrementale, separato dai conguagli mercato. Per i saldi legacy il checkpoint non è ricostruibile in modo affidabile: il report lo segnala e il responsabile può confermarlo esplicitamente con saldo/versione attesi, senza alterare il saldo importato.
- Sincronizzazione della disponibilità delle rose future nella stessa transazione di mercato/assegnazioni/riparazioni, preservando le posizioni scelte per i giocatori rimasti e gli slot dei sostituiti. Nessuna riscrittura di giornate iniziate o storiche.

Restano da portare Hall of Fame, carte/news/AI, ingestione e job, consegna notifiche/outbox, log/archivi e i restanti mapper condivisi. Le nuove aste richiedono ancora verifica Docker/smoke del blocco corrente e casi limite finali: nessuna dichiarazione di passaggio produttivo.

## Incremento 9 — Albo, carte e notifiche (in lavorazione)

- Albo e carte: import e servizi HTTP/MCP su DTO leggibili, con test di idempotenza/rimozione, storico standalone, scope e versioni. Eliminati i vecchi mapper compatti e il bug dell'editor che cancellava condizioni esistenti salvando il nome.
- Preferenze push, dispositivi globali e capacità privata ruotata: migrazione/applicazione e test PostgreSQL/API passati; regressione legacy 243 passati/1 saltato anche dopo rimozione VAPID incorporata.
- Client e worker passano a JSON leggibile, chiave pubblica da API e riattivazione esplicita dopo rotazione. Nessuna prova segreta in settings/worker.
- Import outbox/checkpoint/ricevute e quarantena conservativa dei pending legacy; dispatcher SQL con lease/backoff/limiti e CAS; producer mercato e giornata in verifica.
- Il percorso SQL non registra repository legacy. BackgroundJobs rimane rifiutato finché ingestione/orchestrazione non sono portate; l'abilitazione push richiede configurazione VAPID privata valida e test delle consegne.

Restano aperti refresh albo, news/AI, ingestione/orchestrazione e immagini, log/archivi e verifica end-to-end finale. `fullMigrationComplete` resta falso, senza dichiarare pronto il cutover.

## Incremento 10 — Sorgenti e automazioni SQL (in lavorazione)

- Provider calcistici separati dai vecchi repository tramite reader specifico SQL/legacy; nuova API globale `POST /api/football/refresh` condivisa con MCP.
- Catalogo, calendario, voti e probabilità salvati con EF/transazioni; parse vuote/incomplete conservano i dati precedenti. Statistiche tramite motore puro condiviso, senza cancellare descrizioni.
- Test HTTP con provider sintetici per autorizzazioni, identità stabili, rifiuto di catalogo/voti parziali, conteggi e conservazione delle descrizioni.
- Servizio di preparazione delle formazioni future e percorso programmato per ricalcolare solo risultati assenti, senza sovrascrivere quelli storici. La schedulazione completa non è ancora abilitata.
- Foto giocatore/rosa su nuovo servizio SQL e sorgente Blob condivisa; frontend invia UUID e non chiavi nome. Ricalcolo voti/statistiche frontend spostato da GET legacy a POST espliciti SQL.

### Verifica consolidata degli incrementi 9–10

- Suite locale e Docker isolata: **52 test PostgreSQL + 34 test API passati**. Frontend e TypeScript passati anche nei container.
- Regressione API legacy: **243 passati, 1 saltato**, con chiavi VAPID sintetiche e i nuovi reader dei provider.
- Test mirato aggiuntivo: eventi mercato → producer → outbox, scope di lega/stagione e idempotenza passati.
- Schema EF allineato: nessuna modifica del modello priva di migration.
- Stack HTTPS ricostruito e sano su frontend `http://localhost:3300`, API `https://localhost:5443`; smoke discovery/OAuth/MCP non autenticato passato. Questo non equivale a un login Microsoft/MCP reale o a collaudo gameplay.
- Browser non controllabile da questa sessione OpenChamber: nessuna verifica manuale delle schermate dichiarata.

Prossimi blocchi necessari per il completamento dell'intera richiesta: scheduler/orchestrazione SQL (senza riabilitare i writer legacy), refresh albo con conservazione dello storico, import/generazione news e suggerimenti AI, log/archivi e dismissione dell'ultimo client repository frontend, matrice finale route/hook/policy e accettazione end-to-end con dump/configurazione reali.

Il branch resta `refactor/postgresql-multi-group`; modifiche non committate/non pubblicate. Nessuna issue o PR fornita/aperta da collegare alla sessione. `fullMigrationComplete=false` e `BackgroundJobs:Enabled=false` rimangono intenzionali.

## Incremento 11 — Chiusura del finibile e preparazione del commit

La richiesta del 26 ottobre è completare quanto verificabile senza credenziali/dump reali e committare sul branch dedicato, senza push.

- Orchestrazione SQL per sorgenti, statistiche, formazioni future, risultati assenti, scadenza mercato, albo e foto. Lease con fencing token, recupero esecuzioni scadute, cancellazione e ricevute operative verificati; nessun vecchio writer Blob riabilitato.
- Albo incrementale con checkpoint annuali, preservazione di totali/record storici, conferma autorizzata quando il dump non basta e idempotenza del refresh.
- Import news/suggerimenti/log; AI opzionale esplicita con lock, limiti, CAS e provider testabile. Suggerimenti separati per account anche fra co-owner; migration preliminari rifiutate quando il destinatario non è verificabile.
- Archivio Table tipizzato con provenienza esatta, inclusi tick a 100 ns; API log/job protette/redatte. Eliminati setup, mapper e dipendenza dell'ultimo client repository frontend.
- Artefatti Blob/derivati/chiavi classificati esplicitamente in `preservedArtifacts`; container sconosciuti ancora pendenti. Matrice route/factory/policy aggiunta e inventario sincronizzato.
- SQL diventa il default dell'applicazione; DSN e amministratori globali devono essere configurati, senza fallback Blob silenzioso o email amministrativa incorporata. La fixture legacy sceglie il vecchio backend esplicitamente e intercetta ogni provider.

Restano **accettazione e configurazione del gestore**, non un bypass da implementare: dump reale/quiescenza, mapping o conferme non ricostruibili, issuer/certificati/chiavi originali, rotazione VAPID e riattivazione dispositivi, login Microsoft/MCP reale e collaudo UI. `fullMigrationComplete=false` rimane un presidio di cutover; job, rete, push e AI sono disabilitati nei default locali.

La cartella ADR descrive anche gli stati intermedi precedenti: questo incremento e la matrice sono lo stato corrente del pacchetto da committare.

### Verifiche del pacchetto finale

- **57 test PostgreSQL + 43 test API** passati localmente e nei container isolati, comprese migration di privacy, import Table/news/suggerimenti, baseline albo, lease/fencing/cancellazioni, scadenza mercato, generazioni concorrenti e CAS.
- **243 regressioni API legacy passate / 1 saltata** e **102 test dei job legacy passati**; fixture legacy esplicite con HTTP controllato.
- Tutti i test frontend e TypeScript passati, anche nel target Docker; test statico vieta la reintroduzione del client repository/mapper compatti nel frontend attivo.
- Modello EF allineato alle migration; nessuna modifica pendente. Controllo whitespace e audit dei file candidati: nessun certificato/chiave privata, env privato, binario o literal di credenziale aggiunto al pacchetto.
- Build finale avviato con schema applicato allo startup; health e smoke HTTPS/OAuth/MCP non autenticato passati. Login reale e UI restano accettazione del gestore; nessuna verifica browser manuale viene dichiarata senza client controllabile.

## Incremento 12 — Onboarding e attivazione dei gruppi

Su richiesta esplicita del gestore, il super admin predefinito è ora `alessandro.rapiti44@gmail.com`, configurabile con `Authorization:GlobalAdministrators`/`GLOBAL_ADMIN_EMAIL`. La capability `isGlobalAdministrator` proviene dal profilo server; l'area piattaforma resta visibile anche senza membership e non confonde i responsabili di gruppo con gli amministratori globali.

- `/platform` offre utenti e gruppi globali paginati, ricerca, panoramica membri/leghe e gestione di codici monouso. I controlli server richiedono account attivo, ruolo autenticato e allowlist corrente; le viste globali non aggiungono membership fittizie.
- I codici contengono 192 bit casuali, sono mostrati integralmente una sola volta, salvati come hash e revocabili finché inutilizzati. Riscatto, creazione gruppo, assegnazione del responsabile e selezione del nuovo gruppo sono atomici, sotto lo stesso lock PostgreSQL delle altre scritture.
- La home pubblica e senza gruppi presenta funzionalità, leghe/coppe e regole configurabili. `/groups/new` riscatta il codice; il menu permette di creare altri gruppi anche a chi è già membro. Le impostazioni e il workspace rimandano allo stesso percorso.
- Pagamenti, prezzi e rinnovi annuali sono dichiarati **futuri**, non simulati. I codici attuali non effettuano addebiti e non sospendono gruppi importati/esistenti.

Verifiche: suite PostgreSQL e API reali su database isolati, incluso uso concorrente/riuso/revoca/cross-role e rollback forzato; test frontend di presentazione, menu e trasporto codice solo nel POST; TypeScript. Prova browser headless desktop/mobile con risposte API sintetiche per home, riscatto e console globale, non un collaudo di pagamento o di login Microsoft reale. Dettagli operativi: [attivazione gruppi](../docs/GROUP-ACTIVATION.md).

## Incremento 13 — Inviti per email e gestione del laboratorio di migrazione

- Il creatore resta responsabile/amministratore del gruppo; anche gli amministratori delegati possono accedere a **Membri e inviti**. Un pannello condiviso con il workspace effettua lookup di email complete, associa account esistenti e predispone membership per i primi login. Non espone la directory globale o gruppi altrui; non invia email automaticamente. Protezioni sull'ultimo responsabile e sull'assegnazione dei ruoli rimangono nel servizio SQL.
- `reset-postgres.ps1`/`.sh` operano solo sul Docker locale e DB/ruolo `fantasoccer_local`, richiedono conferma esplicita, supportano anteprima, fermano API/frontend e creano un backup prima del reset transazionale delle righe applicative. Schema/EF history, volumi file/certificati/key ring e dump originali non sono cancellati; nessun riavvio automatico.
- `sync-storage.ps1`/`.sh` orchestrano export Blob/Table, checksum e analisi. Solo `-Apply`/`APPLY=true` importano SQL e ripristinano Azurite; non esiste reset implicito o scrittura Azure. Sono snapshot a senso unico, non replica continua e non backup completo di tutti i servizi Azure. Rimangono in vigore il blocco dopo scritture interattive e la distinzione fra copia esplorativa e dump finale a sorgente ferma.
- Test workflow PowerShell/Bash con Docker simulato e reset SQL su fixture PostgreSQL separata senza porte/volumi dell'app; test HTTP di lookup/invito e privacy, frontend/TypeScript e prova browser desktop/mobile con risposte API sintetiche. Nessun reset dei dati dello stack applicativo o export da sorgente reale è stato eseguito durante questo lavoro.

Comandi e precauzioni: [runbook Windows](../docs/MIGRATION-RUNBOOK.md).

### Esclusione esplicita delle aste storiche

Su richiesta del gestore è disponibile `IMPORT_LEGACY_AUCTIONS=false` nel `.env` (CLI: `FANTASOCCER_IMPORT_LEGACY_AUCTIONS=false`). Esclude `auction` e `randomizedplayers` prima della decodifica JSON, lasciando opzionali le chiavi `LEGACY_ENCRYPTION_*`; non tocca gruppi, rose, prezzi, crediti o altre competizioni. L'export mantiene comunque tutti i byte originali verificati e il report dichiara `excludedContainers`/`preservedArtifacts`. Il default rimane inclusivo (`true`) per evitare perdite silenziose. Il mapping è aggiornato e i test coprono idempotenza, esclusione senza chiavi e cambio modalità prima dell'apertura alle scritture applicative.

### Log Table fuori dall'import SQL predefinito

Su richiesta successiva il default diventa `IMPORT_LEGACY_LOGS=false` (CLI: `FANTASOCCER_IMPORT_LEGACY_LOGS=false`): il dump continua a preservare la Table `logs`, ma l'archivio PostgreSQL non viene popolato. È disponibile opt-in con `true`; il report esplicita `table:logs` tra esclusioni e artefatti conservati, senza nascondere eventuali altre Table sconosciute. Il mapping è aggiornato; i test verificano default, opt-in, cambio opzione e conservazione dei dati sorgente/gruppi.

**Rinviata a una revisione dedicata:** classificare log diagnostici attuali, audit, ricevute e stato dei job; valutare quali messaggi tenere solo su OpenTelemetry/piattaforma di logging e la retention dei dati rimanenti. Questo incremento non modifica i writer correnti, non elimina schemi e non rimuove lease/checkpoint/outbox necessari al comportamento del gioco.

### Export Blob-only e ripresa dopo errori del primo import

Il default senza Table vale ora anche per **download e restore**, non solo per l'import SQL dei log. `-IncludeTables` nei wrapper PowerShell (`INCLUDE_TABLES=true` Bash, `--include-tables` CLI) è l'opt-in esplicito; i manifest dichiarano `blob-current-state`/esclusione `Tables`, mentre i vecchi dump Blob+Table rimangono leggibili. È possibile riprendere un export interrotto sulle Table riutilizzando i checkpoint Blob e omettendo la fase Table; nessun file preesistente viene cancellato.

`-UseExistingDump` evita completamente nuove letture Azure; `-Resume` rifà il listing e riutilizza soltanto i Blob coerenti con ETag/dimensione/checksum. Le scansioni Table, quando richieste, ripartono invece dall'inizio. Un errore `invalid_email` è distinto dalla fase download: la diagnostica indica campo ed esatte coordinate del lettore senza stampare email, chiavi dei documenti o payload. Non vengono saltati o corretti automaticamente utenti: il dataset reale richiederà riconciliazione del campo indicato, senza reset obbligatorio per un'analisi fallita.

Test: assenza di richieste Table sul percorso predefinito, opt-in, riuso Blob fra modalità, recupero di manifest incompleto, restore Table solo su richiesta, errore email da Blob anche senza Table, wrapper PowerShell/Bash e suite PostgreSQL/API.

### Riconciliazione delle chiavi interne `appidentity`

Il dump reale ha segnalato `invalid_email` sulla chiave interna, dopo la validazione dell'email del profilo. Verificato il comportamento dell'adapter Blob Rystem installato: il point read usava l'email per il nome del Blob e ritornava `Value`, senza usare `Entity.Key`. L'importatore ammette quindi metadati `Key` opachi/vuoti/mancanti soltanto se il lookup Blob è un'email valida e coincide con quella del profilo. Chiavi con email diverse, lookup discordanti, email del profilo invalide e duplicati continuano a bloccare l'import.

Il mapping è aggiornato; report e audit del mapping indicano i documenti riconciliati senza copiare chiavi opache in SQL/log. Gli errori `appidentity` espongono solo SHA-256 del documento per individuarlo nel manifest. Test dedicati verificano fallback, idempotenza e stabilità degli ID/ruoli, dati sorgente immutati e rifiuto dei conflitti. Non è una conferma del contenuto del dump del gestore: i casi che non rispettano l'invariante richiedono ancora riconciliazione esplicita.

### Identificatori tecnici corroborati dal dump del gestore

L'ispezione locale del documento bloccante ha mostrato chiave e nome Blob coincidenti nel confronto normalizzato dello script, entrambi di 10 caratteri, non email né codifiche comuni della stessa: la chiave di storage è distinta dall'indirizzo del profilo. Il lettore ammette anche ID tecnici non-email non vuoti **quando chiave interna e nome Blob sono identici con confronto Ordinal**; non estende ai tecnici la normalizzazione di case/spazi usata per le email e non impone che ogni chiave del repository sia un indirizzo. Il profilo continua a richiedere un'email valida e univoca, con le normali verifiche di membership/cache; chiavi email discordanti e duplicati restano bloccati.

Il mapping è aggiornato e il riferimento documento → account usa solo hash del file di archivio e del contenuto, evitando di duplicare gli ID opachi nei metadati SQL/report. Test di regressione coprono ID corti e UUID, stabilità degli ID account durante cambio chiave di storage, ruoli invariati, nessuna creazione di subject Microsoft, sorgente immutata, idempotenza e rifiuto di differenze di case/spazi, email invalide e duplicati. Non viene introdotta una ricerca per nome utente o una decodifica euristica.

### Diagnostica delle collisioni senza fusione automatica

Il passo successivo sul dump reale ha segnalato `identity_collision`, senza ancora indicare quale dei tre controlli l'avesse generato. La diagnostica ora distingue nome email del Blob discordante, chiave email interna discordante ed email di profilo duplicata. In quest'ultimo caso conserva l'hash del documento precedente e i due riferimenti di archivio hash, con soli confronti booleani di profilo completo, nome, selezione, gruppi/ruoli e label; nessun indirizzo, valore di selezione o ID tecnico viene stampato.

Non è una riconciliazione automatica e non modifica il mapping o le regole di import: i duplicati continuano a essere rifiutati. La scelta della politica sul dataset reale resta subordinata ai dettagli del conflitto e alla decisione del gestore, senza reset, cancellazioni o unione di privilegi come scorciatoia.

### Risoluzione puntuale approvata dal gestore

Il confronto reale ha confermato due copie della stessa email, con ruoli e selezione differenti: una nel lookup email e una sotto ID tecnico. Il gestore ha approvato la prima **per questa coppia**. È disponibile `IDENTITY_RESOLUTIONS_JSON`, vuoto per default, con coppie di checksum `preferredDocumentSha256`/`otherDocumentSha256`; i checksum reali restano nell'env privato, non nel codice.

La risoluzione richiede due soli candidati per email, hash univoci/presenti e corrispondenza email comune, copia preferita sotto lookup email e altra copia non-email; rifiuta scelte invertite, file cambiati/mancanti, sovrapposizioni e terze copie. Il profilo viene scelto interamente, mai con OR dei ruoli o unione delle selezioni. Tutte le identità sono validate e restano le verifiche autorevoli delle membership dei gruppi. La decisione è esplicita in `resolvedIdentityConflicts`, `preservedArtifacts` e mapping hash `identity-resolution`, senza mutare sorgente/dump. Il mapping è aggiornato e l'opzione arriva soltanto ai tool di migrazione. `local.env` viene inoltre escluso da Git/build context per proteggere il nome di file privato utilizzato dal gestore.

### Alias email con case diverso: seconda scelta approvata

Un'ulteriore collisione reale è stata confrontata localmente: due lookup della stessa email con case diverso, uno con gruppo/selezione e uno vuoto. Il gestore ha approvato integralmente la prima copia per questa seconda coppia. La configurazione ammette `allowEmailLookupAlias:true` soltanto sull'oggetto approvato; il default rimane `false` e le precedenti coppie email/ID tecnico non cambiano modalità.

Il flag richiede due lookup email con nomi diversi solo per case, non spazi o altre trasformazioni; conserva le copie originali, non fa merge e non sceglie automaticamente il profilo più ricco. Report e audit hash distinguono la modalità. Test riproducono gruppo/selezione contro alias vuoto, risoluzioni miste, idempotenza, sorgente invariata e blocco di modalità errata o membership incoerenti; nessun dato personale del gestore è inserito nel codice o nei test.

## Fase 1 — Inventario e caratterizzazione

- [ ] Censire endpoint automatici, factory, policy, autorizzazioni, hook business e repository interni nella matrice descritta nell'ADR 0001.
- [ ] Censire controller, hub, job, dipendenze frontend e operazioni MCP.
- [ ] Censire container, tabelle, formati/envelope e categorie di dati, incluse quelle non più registrate nel codice.
- [ ] Identificare fonti autorevoli, conflitti membership, chiavi basate su email/nome e dati derivati.
- [ ] Scrivere test di caratterizzazione di regole e calcoli da preservare prima di spostarli.
- [ ] Definire i nuovi contratti API e la convenzione di errori, enum, date e paginazione.

**Uscita:** nessun endpoint o dataset necessario resta implicito. Ogni dismissione è motivata.

## Fase 2 — Export riproducibile e laboratorio locale

- [ ] Implementare export in sola lettura con manifest, checksum, ripresa e report di completezza.
- [ ] Documentare autenticazione, protezione dei dump, anonimizzazione e retention.
- [ ] Preparare fixture legacy sintetiche, incluse chiavi composite e factory con lo stesso tipo.
- [ ] Aggiungere Docker Compose PostgreSQL/Azurite e tool di caricamento del dump.
- [ ] Esplicitare comandi di avvio, seed, migrazione, verifica e pulizia locale sicura.
- [ ] Bloccare nei test endpoint produttivi e integrazioni esterne.

**Uscita:** da checkout pulito si può ricreare l'ambiente ed esportare/ripristinare un account di test senza credenziali produttive.

## Fase 3 — Schema e infrastruttura

- [ ] Produrre ER diagram con ambiti globali/stagionali/gruppo/lega e relazioni squadra-basket-lega validate.
- [ ] Mappare ogni chiave legacy multicomponente a colonne, PK/unicità e FK composite dove sensato; motivare le eventuali chiavi surrogate.
- [ ] Implementare entità, mapping EF, FK, unicità, indici e regole di cancellazione.
- [ ] Implementare migration iniziale e test su PostgreSQL reale, non EF InMemory come prova del comportamento SQL.
- [ ] Implementare migration startup prima di warm-up e job, con coordinamento repliche e fallimento controllato.
- [ ] Mantenere separata l'infrastruttura Blob per file e l'eventuale persistenza delle chiavi.

**Uscita:** schema vuoto installabile e aggiornabile, relazioni condivise senza duplicazioni e avvio concorrente verificato.

## Fase 4 — Prima funzionalità end-to-end

- [ ] Implementare contesto chiamante, policy/handler e modalità di esecuzione di sistema.
- [ ] Migrare account, gruppi, membership, basket, squadre e formazioni.
- [ ] Definire e verificare creazione/configurazione di nuovi gruppi, primo amministratore, inviti/adesioni e cambio gruppo, senza interventi manuali sul DB.
- [ ] Introdurre DTO leggibili e nuove API, con servizi condivisi HTTP/MCP.
- [ ] Aggiornare il frontend della funzionalità, eliminando relativi `Raw`, mapper e client repository.
- [ ] Implementare la prima parte dell'importatore con mapping e checkpoint transazionali.
- [ ] Verificare il percorso completo fixture legacy -> Azurite -> PostgreSQL -> API/MCP -> frontend.

**Uscita:** un flusso reale completo dimostra che architettura, autorizzazioni, contratti e importazione funzionano insieme.

## Fase 5 — Copertura funzionale completa

- [ ] Migrare mercato e aste con transazioni, controllo concorrenza e riuso da SignalR/MCP.
- [ ] Migrare calendari, competizioni, risultati, classifiche e competizione iniziale.
- [ ] Migrare dati Serie A condivisi, ingestione, voti, statistiche e relativi job.
- [ ] Migrare carte, news, dati live/AI, preferenze e notifiche secondo inventario.
- [ ] Decidere e implementare persistenza OAuth e conservazione o rinnovo dei collegamenti MCP.
- [ ] Aggiornare tutte le chiamate frontend e rimuovere mapper di abbreviazione, modelli `Raw` e configurazione repository non più necessari.
- [ ] Aggiornare catalogo/guida MCP e documentazione pubblica, senza sovraingegnerizzazione.
- [ ] Eliminare endpoint automatici e hook legacy solo quando le rispettive righe della matrice sono coperte.

**Uscita:** ogni flusso e dataset censito ha una destinazione verificata; nessun percorso secondario aggira le nuove autorizzazioni.

## Fase 6 — Prova della migrazione

- [ ] Completare importatore, riconciliazione e report per tutte le categorie.
- [ ] Ripetere importazione sullo stesso target: nessuna duplicazione e nessuna modifica ingiustificata.
- [ ] Interrompere durante un batch e riprendere, confrontando con un'importazione pulita.
- [ ] Provare documento modificato, figli aggiunti/rimossi, chiavi/factory omonime e mapping versionato.
- [ ] Verificare guardia che impedisce riesecuzione dopo l'apertura alle scritture applicative.
- [ ] Provare casi anomali: JSON malformati, envelope mancanti, chiavi invalide, collisioni, membership discordanti e riferimenti orfani.
- [ ] Provare più leghe sullo stesso basket, più gruppi sugli stessi giocatori, storico giornaliero, vendite e cambi proprietario.
- [ ] Provare PK/FK composite e rifiuto di associazioni cross-group/cross-season anche tramite accesso diretto al DB.
- [ ] Provare lo stesso utente con ruoli diversi in più gruppi, ID locali coincidenti e isolamento di ricerche, cache, lock, job, file e broadcast SignalR.
- [ ] Provare round-trip export/ripristino di Blob e Table, inclusi tipi, metadati e checksum dove applicabili.
- [ ] Eseguire prova completa sul dump reale protetto, in ambiente isolato, con report delle discrepanze e tempi misurati.

**Uscita:** zero discrepanze bloccanti, runbook ripetibile e durata compatibile con la manutenzione.

## Fase 7 — Azure e passaggio finale

- [ ] Preparare risorse PostgreSQL, TLS/rete, segreti, backup/PITR, monitoraggio e prova di restore.
- [ ] Aggiornare pipeline di build/deploy per contratti, test, nuove configurazioni e rilascio coordinato.
- [ ] Provare il tutto su risorse Azure isolate, incluse autenticazione Storage, OAuth e key ring.
- [ ] Concordare manutenzione e punto oltre il quale il rollback non è immediato.
- [ ] Fermare tutti gli scrittori, esportare, importare e riconciliare secondo ADR 0003.
- [ ] Verificare frontend, API, MCP e SignalR prima della riapertura; poi riattivare job ed effetti esterni.
- [ ] Conservare backup/dump e dismettere componenti legacy soltanto dopo il periodo concordato.

## Matrice minima dei test

| Area | Prove richieste |
| --- | --- |
| Dominio | Regole formazioni, mercato, bilanci, punteggi, calendari e classifiche |
| PostgreSQL | FK/unicità, transazioni, rollback, query, concorrenza e aggiornamento schema |
| Sicurezza | Anonimo, membro, comproprietario, amministratore gruppo, amministratore globale, accesso cross-group e revoca membership |
| API/MCP | Stesso risultato business e stessi rifiuti sui diversi adattatori; nessun campo amministrativo modificabile via input ordinari |
| Frontend | Contratti leggibili, nessun mapper compatto, workflow utente/admin e gestione errori |
| SignalR/job | Stessi servizi, corretti scope EF, nessuna perdita di controlli o duplicazione degli effetti |
| Importatore | Idempotenza, crash/ripresa, modifiche/figli rimossi, errori, riconciliazione e protezione del target |
| Export | Completezza, checksum, paginazione, ripresa, sola lettura e segreti assenti da log/manifest |
| Docker | Comando riproducibile da zero, Azurite + PostgreSQL + test automatici, senza servizi reali |
| Azure | Rete/TLS, identità, backup/restore, startup, log sicuri e collegamento MCP reale |

## Strategia CI

- Suite rapida di dominio e contratti a ogni modifica.
- Suite d'integrazione con PostgreSQL e Azurite, fixture sintetiche e versioni dei container fissate.
- Prova automatica della migrazione completa e della seconda esecuzione, con confronto dello stato risultante.
- Build/test frontend e controllo di allineamento OpenAPI/TypeScript.
- Dump produttivi e relativi segreti mai necessari alla CI ordinaria.

## Definition of done

- Nessuna dipendenza applicativa dal repository generico o dalle sue API automatiche.
- Tutti i contratti pubblici usano nomi descrittivi; il frontend consuma direttamente i DTO.
- Dati condivisi normalizzati e storico preservato con vincoli verificati.
- Chiavi composte trasformate in colonne e vincoli relazionali dove appropriato, senza chiavi concatenate nel nuovo modello.
- Nuovi gruppi e amici possono essere accolti tramite flussi autorizzati, senza duplicare dati globali o configurare manualmente il database.
- Servizi e autorizzazioni riusati da API, MCP, SignalR e job.
- Importazione ripetibile, verificata dopo interruzione e con riconciliazione completa.
- Migrazione schema allo startup sicura e indipendente dall'importazione legacy.
- File ancora disponibili su Blob Storage.
- Script, ambiente Docker e runbook Azure eseguibili e documentati; backup e ripristino provati.
