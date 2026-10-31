# ADR 0014 — Rose attestate, formazione effettiva unica e refresh immagini limitato

- Data: 2026-10-31
- Stato: implementato; nessun intervento sui dati produttivi.

## Storico e formazione effettiva

Una rosa stagionale corrente non dimostra chi fosse posseduto in una partita passata. La risoluzione usa, nell'ordine:

1. `RosterSnapshots` della squadra autorevole per l'esatta stagione/giornata;
2. lo snapshot `PlayedMatchSnapshots` della stessa partita e dello stesso lato, se quello giornaliero manca;
3. la rosa stagionale soltanto per una giornata **non ancora iniziata**, con scadenza verificabile dal calendario. Per i recuperi vale la scadenza originale, non quella riprogrammata.

Lo snapshot di partita viene letto in memoria, senza inventare un nuovo snapshot giornaliero e senza completarlo con giocatori o prezzi correnti. Se non c'è una fonte attestata dopo la scadenza, lettura/ricalcolo restituiscono `historical_roster_unavailable`; il job risultati salta l'ambito incompleto con quel codice, senza consolidare dati parziali. Anche l'amministratore non può creare implicitamente una rosa storica dalla rosa attuale.

Live, vista della partita, copia e scoring usano la stessa formazione effettiva. Vince la lineup della lega; in sua assenza si mantiene lo schieramento giornaliero attestato. Se è tutto in tribuna, si applica l'ultima formazione precedente ai soli giocatori di quella rosa attestata. Per le giornate future senza snapshot si può proporre la formazione precedente sulla rosa corrente, in sola lettura. `proposedFromDay` indica il fallback non ancora salvato anche nelle viste dell'avversario/Live, non concede diritti di modifica. Nessun GET salva una formazione o un risultato.

Il fingerprint Live considera anche versioni delle lineup e rose precedenti: possono essere la fonte del fallback. La sincronizzazione di mercato/asta resta limitata ai giorni futuri; un test verifica vendita e riacquisto dopo la preparazione, una sola copia attiva e prezzi storici immutati.

Queste regole impediscono nuove ricostruzioni non attestate; non certificano né correggono automaticamente eventuali snapshot già presenti o un dump incompleto.

## Foto dei giocatori

L'ID PostgreSQL viaggia nel dato runtime fino al downloader e al risultato del batch, senza cambiare il formato JSON legacy dei modelli condivisi. Deduplicazione, lock e nuovi file usano l'ID: `images/players/{playerId}.jpg`. Due persone con lo stesso nome normalizzato non vengono più fuse né riconciliate con `Single(alias)`.

Il frontend prova prima il file per ID, poi il vecchio alias del nome e infine l'immagine predefinita. Il fallback conserva la consultazione dei file legacy senza sovrascriverli con l'immagine di un altro ID. Anche le revisioni client sono separate per ID.

`PlayerImages` permette di configurare:

- `BatchTimeout`: 60 secondi complessivi, catalogo incluso (massimo configurabile due minuti);
- `ImageTimeout`: 15 secondi per download, entro il budget complessivo;
- `ParallelDownloads`: quattro per default, massimo quattro.

Alla scadenza interna il batch risponde con successi, fallimenti e ID non completati da riprovare, nell'ordine originale. La cancellazione del chiamante interrompe il lavoro. Non si aggiungono job asincroni o nuove dipendenze per questo flusso. Il refresh singolo conserva il limite di 45 secondi.

Tutte le pagine del catalogo sono validate: array giocatori, numero pagina, totale coerente e indicatore ultima pagina. Una pagina mancante/nulla o incoerente interrompe il catalogo prima di qualsiasi sovrascrittura. Sono coperti anche un errore e un payload nullo sulla terza pagina.

## Qualificazioni e manutenzione

`QualifiedTeams` è la preselezione proposta al gestore, non un vincolo automatico top-N. Il gestore può confermare una vera partecipante della lega sorgente fuori dalle prime N, o prima che esista una posizione in classifica; in quel caso il rank resta nullo. Questa era già la semantica dichiarata dall'interfaccia e rimane invariata, con un test esplicito. Restano vincolanti appartenenza alla sorgente, basket derivati e un proprietario per partecipante della lega.

Il job albo usa i cursori `SqlJobStates.CursorGroupId/CursorLeagueId` (migration `20261031001729_OperationalJobCursor`). Ogni esecuzione elabora al massimo `JobMaintenance:HallBatchSize` leghe (default/massimo 1000), avanza anche su una lega che richiede riconciliazione e riprende dopo un minuto se rimane lavoro. A fine giro azzera il cursore. Interruzioni/lease scadute conservano il progresso e le leghe vengono rivisitate nei giri successivi.

Errori dei job riportano tipo e run ID; errori prima della ricevuta e cicli push riportano un identificativo del tentativo, con fase della push. Non vengono registrati messaggi/stack delle eccezioni, SQL, payload o URL firmati.
