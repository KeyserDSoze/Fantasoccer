# ADR 0012 — Cache distribuita su Blob, competizioni multi-squadra e retention push

- Data: 2026-10-28
- Stato: implementato sul branch `refactor/postgresql-multi-group` (migration `20261028230852_MultiTeamCompetitionsAndOperationalCache`, MappingVersion importer 45); da validare sul dump reale prima del cutover.

## Contesto

Prima della migrazione definitiva servono, una sola volta e sul nuovo stack: una cache condivisa per i dati operativi che non hanno valore storico, uno snapshot Live per gruppo, basket e leghe derivati, formazioni distinte per competizione e il vincolo "un utente controlla al massimo una squadra per lega e stagione". PostgreSQL resta l'unica fonte autorevole dei dati di gioco persistenti.

## Decisioni

### 1. `IDistributedCache` su Azure Blob Storage

- Provider `AzureBlobDistributedCache` (`src/Fantasoccer.Infrastructure.Storage/Caching`), registrato con `AddAzureBlobDistributedCache`. Implementa `Get/Set/Refresh/Remove` sincroni e asincroni e le tre scadenze di `DistributedCacheEntryOptions`.
- Un Blob per voce, nel container **privato** `Cache:ContainerName` (default `fantazone-cache`, verificato all'avvio: un container pubblico è rifiutato). Il contenuto è il JSON UTF-8 dell'applicazione; la scadenza logica sta nei **metadati del Blob** (`fzcache`, `createdat`, `absoluteexpiration`, `slidingexpiration`, `lastaccess`, `expiresat`), così la pulizia non scarica i contenuti. Una voce scaduta ma ancora presente fisicamente è una cache miss; metadati illeggibili o assenti sono una cache miss.
- Sliding expiration: la lettura riscrive solo i metadati (con `If-Match`) e soltanto dopo `Cache:SlidingRefreshRatio` (10%) della finestra; nessuna scrittura a ogni lettura. Le cache ad alta frequenza usano comunque scadenze assolute. Il tempo passa da `TimeProvider`.
- Retry: backoff esponenziale dell'SDK per gli errori transitori; i conflitti condizionali sono restituiti al chiamante che ha un proprio ciclo limitato.
- `IDistributedCache` non è un lock né un CAS. Per questo il provider implementa anche `Fantasoccer.Caching.IDistributedCacheCoordinator` (Domain): `GetEntryAsync` (valore + ETag), `TrySetAsync` condizionale (`If-Match`/`If-None-Match`, una voce scaduta vale come assente), `TryAcquireLeaseAsync` (lease Blob 15–60 s, rinnovato in background, mai permanente: alla perdita del rinnovo il token `LeaseLost` annulla la pubblicazione) e `PurgeExpiredAsync`.
- Doppio livello in `SharedSnapshotCache` (Application.Core): L1 `IMemoryCache` per istanza con TTL breve (`Cache:Shared:MemoryTimeToLive`, 5 s) e L2 distribuito. L1 è solo un'ottimizzazione.
- Envelope versionato `CacheEnvelope<T>`: `schemaVersion`, `kind`, `generatedAtUtc`, `freshUntilUtc`, `sourceRevision`, `sourceFingerprint`, `payload`. Nessun token, PAT, connection string o segreto nei documenti.
- Pubblicazione **monotona**: CAS sull'ETag con al massimo `PublishAttempts` tentativi e backoff con jitter; una revisione più vecchia non sostituisce mai una più nuova (`SkippedNewerExists`), una identica è idempotente (`Unchanged`).
- Stale-while-revalidate controllato: una sola istanza rigenera sotto lease; le altre servono la proiezione precedente **marcata `isStale`** solo entro `maxStale` (2 min per il Live) oppure attendono al massimo `RegenerationWait` e poi calcolano localmente senza pubblicare. Nessuna lettura resta bloccata indefinitamente.
- In host `InMemory`/test si usa `InMemoryCoordinatedDistributedCache`, con la stessa semantica (testata con gli stessi casi del provider Blob su Azurite).
- Ambienti: `Cache:Namespace` (default: nome dell'ambiente in minuscolo) è il primo segmento di ogni percorso; ambienti che condividono lo storage account non collidono.

#### Chiavi logiche e percorsi Blob

| Dato | Chiave logica | Blob | TTL fisico | Freschezza |
| --- | --- | --- | --- | --- |
| Probabili formazioni/indisponibili | `football:v1:{season}:day-DD:chances` | `{ns}/football/v1/{season}/day-DD/chances.json` | 21 giorni | 6 h |
| Voti Live provvisori | `football:v1:{season}:day-DD:live-votes` | `{ns}/football/v1/{season}/day-DD/live-votes.json` | 21 giorni | 2 min |
| Live del gruppo | `live:v1:{groupId:N}:{season}:day-DD` | `{ns}/live/v1/{groupId}/{season}/day-DD.json` | 3 giorni | fingerprint |
| Lock di rigenerazione | `{chiave}:regeneration` | `{ns}/_locks/{sha256}.lock` | lease 15–60 s | — |

Le chiavi calcistiche non contengono il gruppo (dato globale). Una chiave con caratteri fuori da `[a-z0-9._-]` viene trasformata in `{ns}/_hashed/{sha256}.json` e non può uscire dal proprio namespace.

#### Pulizia e lifecycle

- Job SQL `cache-cleanup` (giornaliero 04:20 Europe/Rome): `PurgeExpiredAsync` con 1 giorno di grazia, cancellazione condizionata all'ETag osservato; lock Blob non in lease rimossi dopo `LockBlobRetention` (1 giorno).
- Protezione aggiuntiva: lifecycle policy Azure in [`ops/azure/cache-lifecycle-policy.json`](../ops/azure/cache-lifecycle-policy.json). Se lo storage account ha **versioning** o **soft delete** attivi, ogni sovrascrittura dei voti Live (una al minuto) produce versioni: la policy elimina versioni e snapshot dopo 1 giorno. Preferire un account/container senza versioning per la cache; il soft delete trattiene comunque i Blob cancellati per il periodo configurato a livello di account.

### 2. Probabili formazioni e voti Live

- Un solo snapshot globale per stagione e giornata (`IFootballLiveDataStore`), scritto dai job `chances` e `live` con la **validazione invariata**: una sorgente vuota, ambigua o con voti non finiti produce 409 e lo snapshot precedente resta. Priorità invariata: flag in OR fra Fantagazzetta, Gazzetta e Injury; stato e descrizione dall'ultima sorgente che riporta il giocatore (Injury ultima).
- La revisione è l'istante del fetch: un job lento che pubblica dopo uno più recente viene scartato. Il recupero di un rinvio pubblica in **merge** soltanto i club interessati, nella chiave della giornata originale.
- Consumer unici: `SqlFootballMatchdayReader` (punteggi, anteprime, AIgoritmo), API `votes/live` e `chances` (contratto invariato), `MatchdayPushProducer`. Checkpoint e baseline delle notifiche restano su PostgreSQL. I voti ufficiali restano su PostgreSQL; nessun voto Live completa risultati storici.
- Le tabelle `FootballChances` e le righe `FootballVotes` con `Source = Live` restano nello schema ma non sono più scritte né lette: la loro eliminazione sarà una migration esplicita successiva alla validazione.

### 3. Live delle competizioni per gruppo

`LiveCompetitionApplicationService` costruisce un `LiveGroupSnapshot` (leghe, giornate, partite in vista `GameDto` senza `CanEdit`, voti, punteggi e risultati provvisori, classifica salvata) condiviso dagli utenti autorizzati del gruppo. A ogni richiesta si calcola un fingerprint SQL leggero delle sorgenti (revisione del gruppo, versioni dei calendari, partecipazioni, lineup e rose del giorno, partite reali, voti ufficiali, revisioni degli snapshot calcistici, fase temporale); se coincide si serve la proiezione, altrimenti una sola istanza la ricostruisce. Il fingerprint non è memoizzato: una memoizzazione di 2 s produceva letture stantie dopo una scrittura.
La vista di sola lettura di una partita della giornata Live (`GET .../games/{id}` con `editMode=view`) usa la stessa proiezione; le modalità di modifica sono sempre calcolate per il chiamante. La proiezione non scrive classifiche; la perdita della cache comporta solo una ricostruzione.

### 4. Basket e leghe autonomi/derivati

- `Baskets.Kind` (0 autonomo, 1 derivato) con chiave alternativa `(GroupId, BasketId, Kind)`. `BasketDerivations` collega un basket derivato a **un** basket di origine: le colonne costanti `DerivedKind = 1` e `OriginKind = 0` partecipano alle FK composite, quindi il database rifiuta un'origine derivata (niente catene, quindi niente cicli) e il cambio di tipo di un basket collegato.
- `TeamDerivations`: squadra derivata → una squadra di origine dello stesso gruppo e stagione, nel basket di origine dichiarato (FK composite). Unica per origine e basket. Immutabile (trigger).
- Copia carbone = condivisione permanente: la squadra derivata non ha rosa, proprietari, crediti, asta o mercato propri (trigger `fz:derived_team_roster`, `fz:derived_team_ownership`); legge sempre la rosa corrente dell'origine. Acquisti e vendite sono immediatamente visibili. Nessun job di copia.
- `Leagues.Mode` (autonoma/derivata) è distinto dal legacy `IsMain` (autorità economica/regolamento fra leghe che condividono un basket, invariato). Una lega autonoma usa basket autonomi, una derivata basket derivati (trigger). La modalità non cambia dopo i collegamenti.
- `LeagueDerivations`: una lega derivata può dipendere da più leghe di origine dello stesso gruppo e stagione (FK composita), con numero di qualificate ed eventuale stage; un trigger serializzato da advisory lock rifiuta i cicli.
- `LeagueParticipations`: squadre effettivamente ammesse, con provenienza (`BasketEnrollment`, `Manual`, `Qualification`, `Legacy`), lega di origine e posizione. Le leghe autonome iscrivono esplicitamente le squadre dei basket collegati quando si crea il collegamento o la squadra; le derivate solo per qualificazione confermata o selezione manuale. Una partecipazione confermata non segue i cambi di classifica; non si ritira se esistono partite o formazioni. `FantasyMatches` referenzia le partecipazioni.

### 5. Un utente, una squadra per lega e stagione

`LeagueParticipantOwners` ha PK `(GroupId, LeagueId, SeasonIndex, UserId)` ed è mantenuta da trigger nella stessa transazione di ogni modifica a proprietà, comproprietà, derivazioni e partecipazioni (per una derivata valgono i proprietari dell'origine). Copre proprietario+proprietario, proprietario+comproprietario e comproprietario+comproprietario, anche per SQL diretto e scritture concorrenti (la PK risolve la corsa). La violazione diventa `409 league_owner_conflict`. Lo stesso utente può possedere squadre in basket diversi del gruppo.

### 6. Formazioni per competizione e copia

- `LeagueLineups` (+ giocatori e cronologia cambi) per `(GroupId, LeagueId, SeasonIndex, TeamId, Day)`: solo posizioni, sovrapposte alla **rosa del giorno** della squadra autorevole (`RosterSnapshots` Matchday, condivisa e mai duplicata per lega). Senza lineup vale la posizione legacy della rosa del giorno (dati importati).
- Salvataggio, Live, cambi live (contatore per competizione), scadenze, regole della lega, ruoli/panchina (`TeamChecker`) e versioni concorrenti sono validati in un solo percorso. `PUT /api/teams/formation` con kind giornaliero è instradato qui; la rosa del giorno condivisa non è più una formazione modificabile.
- Copia formazione (`POST .../games/{id}/formation/copy`, `POST .../lineups/copy`, `GET .../lineups/copy-sources`): solo fra competizioni con la stessa squadra autorevole; trasferimento una tantum che passa per tutte le validazioni del bersaglio.
- Ultima formazione mostrata: aprendo (in modifica, prima del calcio d'inizio, da proprietario) una giornata senza formazione propria e con rosa tutta in tribuna, il server mostra direttamente l'ultima formazione della stessa squadra nella stessa competizione (`proposedFromDay`, senza riquadro di conferma). In lettura non scrive nulla: diventa la formazione della giornata al salvataggio o quando il job di preparazione la crea; il calcolo dei punti non cambia (penalità per formazione mai inviata).
- Copia sempre (`GET/PUT .../lineups/auto-copy`, tabella `LeagueLineupAutoCopies`): lo stato di una partecipante come formazione principale. Ogni salvataggio pre-partita (mai i cambi live) viene copiato nelle altre competizioni della stessa squadra autorevole, una per una, con tutte le validazioni del bersaglio; un rifiuto è riportato nel messaggio e non annulla il salvataggio principale. La copia non è reciproca: salvare altrove non ritorna sulla principale. Attivabile solo dal proprietario di quella squadra.
- Giocatori venduti dopo il salvataggio: segnalati in `unavailablePlayerIds`, mai corretti silenziosamente; un giocatore acquistato entra in tribuna. Le partite giocate restano negli snapshot storici e le giornate iniziate non sono modificabili.
- Risultati, automazione della giornata successiva (una lineup per competizione con partita, AIgoritmo con le regole di quella lega), notifiche dei cambi live (ricevute per lega) e AIgoritmo con `teamId` (anche in modalità SuperAdmin) usano la lineup della competizione.

### 7. Push: retention senza archivio

Outbox, ricevute, lease, retry e deduplicazione restano su PostgreSQL. `PushRetentionService` chiude una giornata conclusa (tutte le partite finite o oltre 135′ dal calcio d'inizio, nessun rinvio o risultato fantasy pendente) dopo `PushRetention:Retention` (48 h): imposta `PushDayCheckpoints.ClosedAtUtc` (il checkpoint resta e i produttori non emettono più eventi per quella giornata), poi elimina baseline voti, ricevute dei cambi formazione, voti legacy `Notification` del giorno, delivery terminate o scadute non in lease con ricevute e conferme di ambito. Le delivery di mercato (giorno 0) restano deduplicate da `MarketTradeEvents`. Preferenze, dispositivi, dati di gioco e righe in elaborazione non sono toccati. Gira alla preparazione della nuova giornata (8 h prima) indipendentemente dall'invio di promemoria e nel job giornaliero `push-retention`. L'esito di trasporto `Delivered` è rinominato `ProviderAccepted` (accettazione del servizio Web Push, non lettura); le ricevute storiche `Delivered` hanno lo stesso significato (`PushOutcomes.IsProviderAccepted`).

### 8. Migration e importer

- Migration EF Core senza `EnsureCreated`, ID preservati. Il backfill rende esplicita la regola legacy (ogni squadra dei basket collegati partecipava) con `Source = Legacy`; fallisce con `league_owner_conflict` e le coordinate da riconciliare se i dati esistenti violano il vincolo.
- Importer (MappingVersion 45): partecipazioni legacy esplicite, rifiuto `league_owner_conflict` alla fine dell'analisi, nessuna derivazione inventata, nessuna proprietà attribuita. Voti Live e probabili formazioni restano verificati nel dump e riportati in `operationalSnapshotsNotImported`; solo la giornata del cutover può essere pubblicata in cache con `seed-operational-cache` (revisione = inizio export, quindi mai sopra dati più recenti del provider).

## Diagramma ER (nuove relazioni)

```mermaid
erDiagram
    Baskets ||--o{ BasketSeasons : "per stagione"
    Baskets ||--o| BasketDerivations : "derivato (Kind=1)"
    Baskets ||--o{ BasketDerivations : "origine (Kind=0)"
    BasketSeasons ||--o{ FantasyTeamSeasons : contiene
    FantasyTeamSeasons ||--o| TeamDerivations : "squadra derivata"
    FantasyTeamSeasons ||--o{ TeamDerivations : "squadra di origine"
    BasketDerivations ||--o{ TeamDerivations : "coerenza basket"
    FantasyTeamSeasons ||--o{ TeamOwnerships : "solo autonome"
    FantasyTeamSeasons ||--o{ RosterSnapshots : "solo autonome"
    Leagues ||--o{ LeagueSeasons : "per stagione (Mode)"
    LeagueSeasons ||--o{ LeagueBasketSeasons : "basket collegati"
    LeagueSeasons ||--o{ LeagueDerivations : "lega derivata"
    LeagueSeasons ||--o{ LeagueDerivations : "lega di origine"
    LeagueBasketSeasons ||--o{ LeagueParticipations : "basket della squadra"
    FantasyTeamSeasons ||--o{ LeagueParticipations : ammessa
    LeagueDerivations ||--o{ LeagueParticipations : "qualificata da"
    LeagueParticipations ||--o{ LeagueParticipantOwners : "proprietari effettivi (trigger)"
    GroupMemberships ||--o{ LeagueParticipantOwners : "PK unica per lega/stagione"
    LeagueParticipations ||--o{ LeagueLineups : "formazione per giornata"
    LeagueLineups ||--o{ LeagueLineupPlayers : posizioni
    LeagueLineups ||--o{ LeagueLineupChanges : "cambi live"
    LeagueParticipations ||--o{ FantasyMatches : "casa/trasferta"
    PushDayCheckpoints ||--o{ PushVoteBaselines : "fino alla chiusura"
```

## Conseguenze e rischi

- Il vincolo per lega può bloccare la migration o l'import su dati reali: va riconciliato esplicitamente (nessuna scelta automatica).
- La pubblicazione monotona dipende dagli orologi delle istanze (revisione = istante del fetch); lo scarto atteso è trascurabile rispetto alla cadenza dei job.
- Lease Blob: durata minima 15 s; la scadenza reale dei lease non è riprodotta nei test (solo rilascio ed esclusività).
- Il fingerprint Live costa alcune query aggregate per richiesta; la ricostruzione completa avviene solo al cambio delle sorgenti.
- Le tabelle operative legacy (`FootballChances`, voti `Live`) restano fino a una migration di rimozione esplicita.
