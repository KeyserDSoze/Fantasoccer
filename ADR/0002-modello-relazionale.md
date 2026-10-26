# ADR 0002 — Modello relazionale e separazione dei dati condivisi

- Data: 2026-10-25
- Stato: principi concordati; schema fisico da validare tramite inventario e dati

## Decisione

Progettare un vero modello relazionale. Non trasferire semplicemente ogni documento Azure in una tabella con un payload JSON.
L'obiettivo è evitare duplicazioni accidentali mantenendo lo storico e le differenze reali fra competizioni.

## Ambiti dei dati

| Ambito | Contenuto indicativo | Regola |
| --- | --- | --- |
| Globale | Utenti, giocatori reali, club reali | Una identità stabile per soggetto |
| Stagione condivisa | Giocatori della stagione, appartenenza al club, ruoli, calendario Serie A, voti ufficiali/live, probabilità e statistiche di base | Non duplicare per ogni gruppo o lega |
| Gruppo | Membership e ruoli, basket, configurazione delle leghe | Fonte autorevole unica delle relazioni |
| Basket/stagione | Squadre partecipanti, proprietari e rose condivise dove previsto | Non duplicare la squadra per ogni lega che usa il basket |
| Lega/stagione | Regolamento, calendario fantasy, partecipazioni, risultati e classifiche | Dati dipendenti dal contesto e dalle regole della lega |
| Squadra/giornata | Formazione salvata, sostituzioni e relativo storico | Snapshot storico distinto dalla rosa corrente |
| Operativo/personale | Preferenze, notifiche, stato mercato/asta, documenti OAuth | Vincoli e retention espliciti |
| File | Immagini giocatori, loghi, immagini carte e altri allegati | Blob Storage; nel DB riferimenti e metadati necessari |

I voti reali di base sono condivisi; punteggi fantasy e classifiche dipendenti dai regolamenti non sono necessariamente condivisibili.
Anche eventuali giocatori randomizzati per lega devono preservare la loro semantica: riusare l'identità del giocatore e memorizzare soltanto l'assegnazione/variante specifica.

## Squadra, basket e lega: vincolo del modello esistente

Attualmente `TeamKey` contiene `Group`, `Year`, `BasketId`, `Email`; **non contiene la lega**.
`Group` contiene basket e leghe; ogni `League` possiede `BasketsId` e configurazioni annuali.
Una stessa squadra può quindi partecipare a più leghe tramite il basket.

Il modello candidato è:

```text
Group -> Basket -> BasketSeason -> FantasyTeamSeason
Group -> League -> LeagueSeason
LeagueSeason <-> BasketSeason / partecipazioni esplicite
FantasyTeamSeason -> Owners, RosterEntries, Lineups
LeagueSeason -> Fixtures, Results, Standings
Player -> PlayerSeason <- RosterEntries / voti / statistiche
```

La forma esatta delle partecipazioni va validata con i workflow e i dati: non assumere arbitrariamente che ogni squadra appartenga a una sola lega, né che una formazione possa sempre essere condivisa fra leghe con regole diverse.
Verificare separatamente competizione iniziale, rose giornaliere e factory `opening`.
Se servono formazioni specifiche per competizione, introdurre una relazione esplicita, non duplicare l'intera anagrafica della squadra.

## Integrità e identità

### Chiavi composite e relazioni

Le chiavi legacy con più componenti devono diventare colonne tipizzate e relazioni reali. Dove la combinazione rappresenta un'identità stabile, usare una chiave primaria composta e le relative FK composte, configurate esplicitamente in EF Core.
Non sostituire sistematicamente ogni relazione con un ID artificiale, né conservare stringhe concatenate con separatori o chiavi JSON come chiave del nuovo dominio.

Esempi candidati, da confermare nello schema fisico:

| Relazione | Chiave composta candidata |
| --- | --- |
| Membership | `(GroupId, UserId)` |
| Giocatore stagionale | `(PlayerId, SeasonId)` |
| Lega stagionale | `(GroupId, LeagueId, SeasonId)` se `LeagueId` è locale al gruppo |
| Basket stagionale | `(GroupId, BasketId, SeasonId)` se `BasketId` è locale al gruppo |
| Comproprietà | `(TeamSeasonId, UserId)` |
| Partecipazione | Combinazione delle chiavi della competizione e del partecipante |
| Formazione giornaliera | Squadra stagionale e giornata, più competizione soltanto se la formazione è specifica per lega |

Per entità con identità autonoma e riferimenti numerosi, come utente, giocatore o squadra, mantenere un ID stabile quando è più appropriato. Se una chiave surrogata semplifica il modello, aggiungere comunque il vincolo univoco sulla combinazione di business e le FK necessarie a garantire l'ambito.
Non promuovere email, nomi o il proprietario attuale a componenti di una PK solo perché presenti nella vecchia chiave NoSQL.
Non usare `(TeamId, PlayerId)` come unicità assoluta se impedisce più acquisti/vendite dello stesso giocatore nel tempo: distinguere identità del movimento e vincolo sulla rosa attiva.

La FK deve garantire l'intera relazione: ad esempio un riferimento a una lega locale include il gruppo, e una partecipazione non può collegare basket e competizione di gruppi o stagioni differenti.
Usare chiavi alternative/unicità e FK composite ove necessario anche quando le entità hanno ID globalmente unici. Un semplice `GroupId` aggiunto alle tabelle senza vincoli coerenti non è sufficiente.
Gli indici vanno progettati sui filtri reali di gruppo/stagione e sulle join; non duplicare inutilmente gli indici già creati dalle PK o dai vincoli univoci.

### Regole di identità e storico

- ID interni stabili per utenti, squadre e giocatori; email, username e nome del giocatore non devono diventare chiavi mutabili propagate ovunque.
- Conservare le chiavi legacy e il loro mapping nell'importatore. Normalizzare email/case con una regola documentata, rilevando collisioni prima di unire account.
- Non deduplicare giocatori solo per somiglianza del nome: le attuali chiavi derivate dal nome richiedono controllo delle collisioni e, quando disponibile, un identificatore esterno affidabile.
- Membership unica per utente/gruppo. Le copie nei documenti `AppIdentity` e `Group` vanno riconciliate; i conflitti si segnalano e non si risolvono concedendo il ruolo più elevato.
- Squadra annuale e comproprietà uniche nel proprio ambito; un cambio proprietario non deve cambiare identità della squadra né riscrivere lo storico.
- Vincoli di unicità per relazioni e dati stagionali; FK che impediscano riferimenti cross-group/cross-season non validi.
- Definire quali informazioni sul giocatore siano stagionali e quali possano cambiare durante la stagione: preservare trasferimenti e valori storici necessari.
- Conservare vendite, prezzi, ricavi, premi e variazioni di credito; non importare solo i giocatori attivi perdendo i dati usati dai calcoli di bilancio.
- Snapshot giornalieri e storico sono duplicazioni intenzionali di fatti nel tempo, non dati da eliminare perché simili allo stato attuale.
- Classifiche e proiezioni derivate hanno una chiave di contesto e una strategia di ricalcolo; non cancellare risultati storici senza averne dimostrato la ricostruibilità.
- Non ricalcolare tacitamente lo storico con regole nuove durante l'importazione.
- Cancellazioni e cascata deliberate: niente perdita dello storico per cancellazione di una membership o modifica di un proprietario.
- JSONB ammesso per strutture realmente variabili, non per membership, rose o altre relazioni centrali interrogabili.

## Inventario obbligatorio

Oltre a gruppi, utenti e squadre includere tutti i repository/factory registrati:

- calendari, classifiche reali, giocatori e club reali;
- voti `live`, `notification`, `official`, probabilità, statistiche e giocatori randomizzati;
- calendari fantasy, classifiche stagionali/giornaliere, partite giocate e Hall of Fame;
- squadre stagionali, giornaliere, iniziali e iniziali giornaliere, classifiche iniziali;
- mercato, aste, carte, news, dati AI e stato live;
- impostazioni, preferenze push e deduplicazione notifiche;
- OAuth MCP, eventuali dati di sicurezza legacy e tabelle di log.

Per ogni categoria decidere: importare, ricostruire con prova di equivalenza, archiviare o scartare esplicitamente.
I log possono richiedere un archivio separato anziché tabelle applicative, ma non devono sparire senza decisione.
Chiavi di cifratura e certificati richiedono un piano di conservazione/configurazione separato; non vanno trattati come normali righe business.

## Verifiche richieste

ER diagram e migration iniziale devono essere accompagnati da casi con più gruppi, più leghe sullo stesso basket, più stagioni, comproprietà e cambi proprietario.
Dimostrare con query e vincoli che un giocatore stagionale condiviso non viene ricreato per ogni lega e che il consolidamento non altera rose, bilanci, storico o autorizzazioni.

## Multi-gruppo come requisito funzionale

- Una stessa applicazione e uno stesso database devono ospitare più gruppi indipendenti, non soltanto il gruppo storico.
- Un utente può appartenere a più gruppi con ruoli diversi. Il gruppo selezionato nel frontend è un contesto di navigazione, non una prova di autorizzazione.
- Nessun ID del gruppo storico, account amministratore di gruppo o stagione deve essere codificato nei nuovi flussi.
- Creazione/configurazione del nuovo gruppo e assegnazione del primo amministratore devono avere un flusso esplicito e autorizzato; decidere se riservarlo all'amministratore globale o consentirlo agli utenti. Non introdurre registrazione pubblica senza decisione.
- Inviti, adesione, gestione membri e configurazione delle competizioni devono funzionare senza modifiche manuali al database.
- Ogni lettura, ricerca, conteggio, esportazione o comando su dati di gruppo verifica il contesto. Un ID valido di un altro gruppo non deve consentire accesso.
- I job iterano i gruppi configurati e attivi; cache, lock e chiavi di idempotenza includono l'ambito corretto, evitando collisioni fra gruppi.
- Le sottoscrizioni/broadcast SignalR verificano la membership del gruppo/asta: conoscere il nome del canale non autorizza l'accesso.
- Upload e riferimenti ai file di gruppo non devono consentire sovrascritture cross-group. La visibilità pubblica dei file esistenti è distinta dall'autorizzazione a modificarli; eventuali allegati riservati richiedono accesso privato esplicito.
- I dati calcistici globali sono condivisi in lettura; la loro modifica/ingestione non è attribuita automaticamente a un amministratore di gruppo.
- I test devono includere lo stesso utente amministratore nel gruppo A e partecipante nel gruppo B, gruppi con nomi/ID locali coincidenti e tentativi di collegamento cross-group respinti dal DB.
