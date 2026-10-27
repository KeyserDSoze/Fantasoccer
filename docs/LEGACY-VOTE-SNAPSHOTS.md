# Snapshot voti legacy senza club

## Decisione

Alcuni documenti storici `live` contengono l'intero elenco di giocatori/voti senza l'oggetto `t` del club. Il caso esaminato ha 317 record, tutti con club null, nella stagione 12 e giornata 10. Un giocatore nominato con `hasVote:true`, voto `6` e `isActive:false` non è una voce vuota di catalogo: **il voto non va scartato né il flag corretto per farlo sembrare attivo**.

I voti `live`, `notification`, `official` e le baseline `pushnotificationsent.MatchEventVotes` possono quindi conservare `ClubCode = NULL`. La FK verso `(PlayerKey, SeasonIndex)` resta obbligatoria; la FK verso il club globale rimane valida quando il codice è presente. Non vengono creati club fittizi, indovinati codici, assegnate squadre dal ruolo o dalla posizione, né copiato il club corrente nel voto storico.

Un oggetto club non nullo ma invalido resta un errore, come prima. Cataloghi, rose, probabilità, statistiche e gli altri documenti restano soggetti ai propri controlli: questo non è uno skip generico di `missing_real_club`, né permette giocatori con chiave/nome irrisolvibile o ruoli non validi.

## Club annuale e club del voto sono informazioni diverse

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

MappingVersion 34; nessuna nuova opzione env. Rianalizzare l'intero dump verificato con `-UseExistingDump` e le risoluzioni utenti già approvate, senza reset o modifiche agli originali. Usare `-Apply` solo dopo analisi riuscita, revisione del report e backup del target se necessario. Restano atomicità, idempotenza, vincoli sulla sorgente e blocco di reimport dopo scritture applicative.
