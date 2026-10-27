# Snapshot voti legacy senza club

## Decisione

Alcuni documenti storici `live` contengono l'intero elenco di giocatori/voti senza l'oggetto `t` del club. Il caso esaminato ha 317 record, tutti con club null, nella stagione 12 e giornata 10. Un giocatore nominato con `hasVote:true`, voto `6` e `isActive:false` non è una voce vuota di catalogo: **il voto non va scartato né il flag corretto per farlo sembrare attivo**.

I voti `live`, `notification`, `official` e le baseline `pushnotificationsent.MatchEventVotes` possono quindi conservare `ClubCode = NULL`. La FK verso `(PlayerKey, SeasonIndex)` resta obbligatoria; la FK verso il club globale rimane valida quando il codice è presente. Non vengono creati club fittizi, indovinati codici, assegnate squadre dal ruolo o dalla posizione, né copiato il club corrente nel voto storico.

Per i record nominati, un oggetto club non nullo ma invalido resta un errore, come prima. Cataloghi, rose, probabilità, statistiche e gli altri documenti restano soggetti ai propri controlli: questo non è uno skip generico di `missing_real_club`, né permette giocatori con chiave irrisolvibile o ruoli non validi. L'esclusione approvata dei voti senza nome è distinta e descritta sotto.

## Voti senza nome: esclusione approvata, non archivio SQL

Il documento `official` stagione 10/giornata 1 esaminato contiene 331 voci, di cui 35 senza nome. La voce `$.Value.p[12]` ha nome vuoto, ruolo -1, club `None` e voto finale 6 con `hasVote:true` e `isIn:true`: **non è un segnaposto senza dati**. Il gestore ha esplicitamente scelto di escludere dall'import i voti senza nome, anziché creare un archivio SQL dedicato o inventare un'identità.

La regola vale per tutti gli anni in `live`, `notification`, `official` e nelle baseline `pushnotificationsent.MatchEventVotes`: gli oggetti con nome mancante, null, vuoto o composto soltanto da spazi vengono omessi da giocatori annuali, voti e baseline SQL, anche con `hasVote:true`, valori o bonus. Non si tenta di ricavarne il giocatore da club, ruolo, posizione o rose e non si crea un club `None`. Gli originali rimangono **soltanto nel dump verificato**, con tutti i byte, campi sconosciuti e duplicati invariati. Nessun record viene salvato in `UnresolvedPlayerCatalogEntries`, che resta specifico dei cataloghi.

`skippedUnnamedVotes` raggruppa gli scarti per documento: checksum, container, riferimento hash, anno, giornata, `totalEntries`, `records` esclusi, `withHasVote` e prima posizione JSON originale. Un voto scartato non viene contato anche come voto importato senza club. Se un documento contiene soltanto voci escluse, rimangono il marker del dataset o la baseline push presente ma vuota, con i suoi flag e sequenza; non vengono inventati giocatori. L'ordine relativo e i duplicati delle baseline nominate rimangono invariati.

Questo non allenta le validazioni dei record nominati: nomi non vuoti come `!!!` che non producono una PlayerKey, ruoli fuori intervallo, club invalidi, voti mancanti e duplicati dei voti rimangono errori. Una voce array interamente `null` resta `null_player`, non è un oggetto con nome mancante. Cataloghi senza nome mantengono l'archiviazione già approvata; rose e riferimenti finanziari irrisolti restano bloccanti. L'ingestione ordinaria del provider non viene modificata.

## Club annuale e club del voto sono informazioni diverse

I voti nominati duplicati nello stesso anno/giornata/fonte restano bloccanti, anche se identici. `duplicate_or_invalid_vote` espone ora `Vote details` per distinguere una collisione della chiave da un valore non finito, identificare entrambe le posizioni originali e confrontare nomi/payload tramite soli booleani, senza stamparli. Non si scelgono valori dalla squadra, dall'ordine o dal maggiore/minore voto. Vedere [diagnosi e limiti](MIGRATION-RUNBOOK.md#se-compare-duplicate_or_invalid_vote). Nessuna nuova politica di deduplica è stata applicata.

- Se il giocatore ha già la riga annuale con club verificato, un voto minimale non la modifica o disattiva.
- Se manca la riga annuale, un giocatore nominato e validato può avere una riga annuale con club null, preservando i metadati del riferimento. Non si crea un giocatore da una voce di catalogo senza nome.
- Una **rosa stagionale `team` dello stesso anno** può completare il solo club annuale mancante, se tutti i riferimenti validi concordano su un unico codice. Il report `recoveredRosterPlayers` indica `filledAnnualClub:true` e la fonte hash/posizione.
- In presenza di più club fra le rose stagionali non si sceglie il primo, quello più recente per supposizione o quello del gruppo preferito: il club annuale resta null e aumenta `ambiguousRosterClubRecoveries`.
- Rose giornaliere e competizione iniziale non vengono usate per questo completamento del club corrente annuale. Una presenza in rosa non crea un'appartenenza `ClubSeasons`.

Anche quando la rosa completa l'anagrafica annuale, **il club originariamente assente dal voto rimane null**. Si evita così di inventare il club della giornata a partire da dati di un altro momento della stagione. Prezzi, ricavi, stati e posizioni delle rose rimangono invariati.

## Report e letture

`missingVoteClubSnapshots` raggruppa i dati mancanti per documento: checksum, container, archivio hash, stagione, giornata, numero di record e prima posizione JSON. Non stampa nomi giocatore, club, proprietari o payload. La raccolta è aggregata durante la lettura, senza aggiungere una voce di report per ogni singolo voto.

`unresolvedAnnualPlayerClubs` conta le righe annuali che restano prive di club; non va confuso con i club assenti dagli snapshot. Un documento può avere tutti i voti con club null e zero club annuali irrisolti. `fullMigrationComplete` resta falso e il report richiede revisione.

L'API dei voti restituisce `realTeam:null` quando manca il club storico, anche se il catalogo annuale conosce il club corrente. Non c'è un fallback di presentazione che trasformi quel dato corrente in uno storico. I lettori SQL e il calcolo dei voti/statistiche gestiscono l'assenza: il voto viene associato al giocatore tramite la chiave, non attraverso il club. Il frontend conserva i voti con `hasVote:true` anche se `isActive:false` e non attribuisce a una partita un evento il cui club è sconosciuto.

L'ingestione ordinaria del provider conserva le validazioni già esistenti: accettare il formato storico minimale non disabilita i controlli sui nuovi feed o i confini autorizzativi. L'import delle baseline non invia notifiche e non modifica preferenze, destinatari, tentativi o ricevute.

## Schema e operatori

La migration aggiuntiva `OptionalHistoricalVoteClubs` rende nullable soltanto `FootballVotes.ClubCode` e `PushVoteBaselines.ClubCode`. Non cambia chiavi, valori dei voti o codici già presenti. Un downgrade con snapshot a club ignoto viene rifiutato, senza introdurre stringhe vuote o altre identità per soddisfare il vecchio NOT NULL.

MappingVersion 36; nessuna nuova opzione env o migration di schema specifica per escludere i voti senza nome. La migration separata delle [classifiche correnti](FANTASY-STANDINGS.md) non cambia i voti. Rianalizzare l'intero dump verificato con `-UseExistingDump` e le risoluzioni utenti già approvate, senza reset o modifiche agli originali. Usare `-Apply` solo dopo analisi riuscita, revisione del report e backup del target se necessario. Restano atomicità, idempotenza, vincoli sulla sorgente e blocco di reimport dopo scritture applicative.
