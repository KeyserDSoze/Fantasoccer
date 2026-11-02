# Serie A, trasferimenti dei giocatori e partite reali

Design concordato il 2 novembre 2026. Tre fasi, una PR ciascuna.

## Fase 1 — Trasferimenti (4.0.7)

Una sola mossa per giocatore e stagione (scelta del 02/11: niente tabella di storico). Sul giocatore stagionale (`PlayerSeasons`):

| Campo | Significato |
| --- | --- |
| `ClubId` (squadra attuale) | Vuoto quando il giocatore ha lasciato la Serie A |
| `PreviousClubCode` (squadra precedente) | Squadra prima dell'ultimo movimento; vuoto per chi arriva da fuori Serie A |
| `TransferredAtUtc` (data di trasferimento) | Run notturna che ha scoperto il movimento, non la data ufficiale del trasferimento |
| `MissingSinceUtc` | Prima run in cui il giocatore non era nel listone (serve per la conferma) |

Job `catalogue`, **ogni notte alle 02:30** (Europe/Rome), avviabile anche a mano:

| Caso | Squadra attuale | Squadra precedente | Data |
| --- | --- | --- | --- |
| Prima run della stagione | squadra trovata | — | — (fotografia di partenza) |
| Nuovo giocatore | squadra trovata | — | run |
| Squadra diversa | nuova | vecchia | run |
| Assente una notte | invariata | invariata | `MissingSinceUtc` = run |
| Assente anche la notte dopo (≥ 20 ore) | vuota, `IsActive = false` | ultima squadra | prima assenza |
| Rientro in Serie A | squadra trovata | ultima squadra di Serie A (vuota se torna nella stessa) | run |

- Una run ripetuta la stessa sera non conferma l'uscita; un giocatore che ricompare la notte dopo non esce mai.
- Protezioni: listone sotto l'80% dei giocatori attivi, oppure una squadra con almeno 5 giocatori attivi del tutto assente → la run si ferma senza modificare nulla.
- I giocatori già disattivati prima del tracciamento diventano "fuori Serie A" con data sconosciuta.
- Un giocatore visto per la prima volta nei voti (dopo la fotografia iniziale) conta come arrivo.
- **Chi lascia la Serie A** non è più proponibile in aste e ordini dei giocatori (servono una squadra attuale), ma resta nelle rose fanta con l'ultima squadra e l'icona "Ha lasciato la Serie A", fino a svincolo o asta di riparazione.
- **Chi cambia squadra** resta in Serie A: la rosa fanta stagionale segue la squadra nuova; le formazioni usano sempre la squadra attuale.
- Lo storico non viene riscritto: i voti salvano la squadra del giorno (`FootballVote.ClubId`) e le rose giornaliere la loro.
- Lista giocatori: colonne "Squadra attuale" ("Fuori Serie A"), "Squadra precedente", "Data di trasferimento", anche nel CSV. Carta e dettaglio del giocatore mostrano il movimento.

## Fase 2 — Sezione Serie A e partite reali

- Nuova sezione "Serie A": calendario completo per giornata e stagione (anche passate) e classifica.
- Ogni partita è cliccabile dalla sezione e dal widget della home; il risultato pulsa con "LIVE" solo mentre si gioca.
- Pagina partita in sola lettura con la stessa grafica del nostro campo:
  - **prima del calcio d'inizio**: probabili formazioni;
  - **titolari**: giocatori con voto o s.v. non entrati dalla panchina; modulo ricavato dai ruoli fanta (approssimato);
  - **panchina**: prima i subentrati con voto ed eventi, poi i giocatori senza voto più utilizzati nelle giornate precedenti;
  - **tribuna**: il resto della rosa a quella data (squadra del voto, altrimenti squadra attuale/precedente secondo la data di trasferimento);
  - indicazione "Formazione ricostruita dai voti: panchina e tribuna stimate";
  - stagioni senza voti: "Formazione non disponibile".
- Sulle carte dei giocatori: la squadra fanta della **lega selezionata** che li possiede, con selettore di lega dentro la partita.
- Dati globali (non per gruppo) in cache condivisa aggiornata dai job, senza ricalcoli nelle richieste.

## Fase 3 — Eventi live

Una riga per giocatore, compatta; voto colorato per fascia; icone con conteggi ed etichette testuali; contrasto adeguato in dark mode; ordine per importanza degli eventi, senza minuti o cronologie inventate.
